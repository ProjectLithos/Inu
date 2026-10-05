using Inu.Core;
using Inu.Memory;

namespace Inu.Memory.Physical;

/// <summary>Allocates exact contiguous physical ranges from a sorted list of free extents.</summary>
/// <nova.when>Choose when sparse maps, exact-size allocations, and arbitrary fixed reservations are more important than constant-time bitmap updates.</nova.when>
/// <nova.depends>Inu.Memory.Physical.Contracts and NormalisedMemoryMap</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public unsafe struct ExtentPhysicalMemoryManager : IPhysicalMemoryManager
{
    private ExtentRecord* _extents;
    private AllocationRecord* _allocations;
    private ReservationRecord* _reservations;
    private int _extentCount;
    private int _extentCapacity;
    private int _allocationCapacity;
    private int _reservationCapacity;
    private ulong _totalManagedPages;
    private ulong _nextToken;
    private bool _initialized;

    /// <summary>Gets the extent allocator methodology.</summary>
    /// <nova.when>Use for allocator-selection diagnostics.</nova.when>
    public readonly PhysicalAllocatorMethod Method => PhysicalAllocatorMethod.Extent;
    /// <summary>Gets whether the manager has been initialised.</summary>
    /// <nova.when>Check before indirect allocation through a stored manager instance.</nova.when>
    public readonly bool IsInitialized => _initialized;
    /// <summary>Gets the fixed Inu physical frame size.</summary>
    /// <nova.when>Use for frame-to-byte conversions.</nova.when>
    public readonly ulong PageSize => PhysicalAllocatorInternals.PageSize;

    /// <summary>Calculates the metadata bytes required for the supplied map and record capacities.</summary>
    /// <nova.when>Call before reserving the caller-owned metadata workspace.</nova.when>
    /// <nova.depends>NormalisedMemoryMap descriptor count</nova.depends>
    /// <returns><see langword="true"/> when the required byte count can be represented.</returns>
    /// <example><code>bool sized = ExtentPhysicalMemoryManager.TryGetRequiredWorkspaceBytes(map, 128, 32, out ulong bytes);</code></example>
    public static bool TryGetRequiredWorkspaceBytes(NormalisedMemoryMap map, int allocationCapacity, int reservationCapacity, out ulong bytes)
    {
        bytes = 0;
        if (!PhysicalAllocatorInternals.TryGetManagedFrameBounds(map, out _, out _, out _, out int usableDescriptors)) return false;
        if (allocationCapacity < 1 || reservationCapacity < 0) return false;
        ulong extentCapacity = (ulong)usableDescriptors + (ulong)allocationCapacity + (ulong)reservationCapacity + 2UL;
        if (extentCapacity > int.MaxValue) return false;
        ulong primaryBytes = extentCapacity * (ulong)sizeof(ExtentRecord);
        return PhysicalAllocatorInternals.TryWorkspaceBytes(primaryBytes, allocationCapacity, reservationCapacity, out bytes);
    }

    /// <summary>Initialises the extent manager from immediately allocatable normalised ranges.</summary>
    /// <nova.when>Call once after memory-map normalisation and before the VMM or kernel heap exists.</nova.when>
    /// <nova.depends>Caller-owned PhysicalAllocatorWorkspace</nova.depends>
    /// <returns><see langword="true"/> when all free extents and record tables fit the supplied workspace.</returns>
    /// <example><code>bool ready = allocator.TryInitialize(map, workspace, 128, 32, out PhysicalMemoryStatus status);</code></example>
    public bool TryInitialize(NormalisedMemoryMap map, PhysicalAllocatorWorkspace workspace, int allocationCapacity, int reservationCapacity, out PhysicalMemoryStatus status)
    {
        status = PhysicalMemoryStatus.InvalidParameter;
        if (_initialized)
        {
            status = PhysicalMemoryStatus.AlreadyInitialized;
            return false;
        }
        if (!PhysicalAllocatorInternals.TryGetManagedFrameBounds(map, out _, out _, out ulong managedPages, out int usableDescriptors)) return false;
        if (!TryGetRequiredWorkspaceBytes(map, allocationCapacity, reservationCapacity, out ulong requiredBytes)) return false;
        ulong extentCapacityValue = (ulong)usableDescriptors + (ulong)allocationCapacity + (ulong)reservationCapacity + 2UL;
        ulong primaryBytes = extentCapacityValue * (ulong)sizeof(ExtentRecord);
        if (workspace.ByteLength < requiredBytes || !PhysicalAllocatorInternals.TryPartition(workspace, primaryBytes, allocationCapacity, reservationCapacity, out byte* primary, out AllocationRecord* allocations, out ReservationRecord* reservations))
        {
            status = PhysicalMemoryStatus.WorkspaceTooSmall;
            return false;
        }

        _extents = (ExtentRecord*)primary;
        _allocations = allocations;
        _reservations = reservations;
        _extentCount = 0;
        _extentCapacity = (int)extentCapacityValue;
        _allocationCapacity = allocationCapacity;
        _reservationCapacity = reservationCapacity;
        _totalManagedPages = managedPages;
        _nextToken = 0;
        ClearRecords();

        for (int index = 0; index < map.Count; index++)
        {
            if (!map.TryGetDescriptor(index, out MemoryDescriptor descriptor)) return false;
            if (descriptor.Availability != MemoryAvailability.AvailableAfterExitBootServices) continue;
            if (!InsertFreeRange(descriptor.PhysicalStart.Value / PageSize, descriptor.PageCount))
            {
                status = PhysicalMemoryStatus.WorkspaceTooSmall;
                return false;
            }
        }
        _initialized = true;
        status = PhysicalMemoryStatus.Success;
        return true;
    }

    /// <summary>Allocates an exact-size contiguous range satisfying the supplied alignment and address window.</summary>
    /// <nova.when>Use when an exact physical page count is preferred.</nova.when>
    /// <nova.depends>Successful TryInitialize</nova.depends>
    /// <returns><see langword="true"/> when a matching extent was split and recorded.</returns>
    /// <example><code>bool ok = allocator.TryAllocate(request, out PhysicalAllocation allocation, out PhysicalMemoryStatus status);</code></example>
    public bool TryAllocate(PhysicalAllocationRequest request, out PhysicalAllocation allocation, out PhysicalMemoryStatus status)
    {
        allocation = default;
        status = PhysicalMemoryStatus.NotInitialized;
        if (!_initialized) return false;
        if (!PhysicalAllocatorInternals.TryGetRequestFrameWindow(request, out ulong minimumFrame, out ulong maximumFrameExclusive))
        {
            status = PhysicalMemoryStatus.InvalidParameter;
            return false;
        }
        if (!PhysicalAllocatorInternals.TryFindFreeAllocationRecord(_allocations, _allocationCapacity, out int recordIndex))
        {
            status = PhysicalMemoryStatus.RecordCapacityExhausted;
            return false;
        }

        bool sizeCandidateExists = false;
        for (int index = 0; index < _extentCount; index++)
        {
            ExtentRecord extent = _extents[index];
            if (extent.PageCount < request.PageCount) continue;
            sizeCandidateExists = true;
            ulong firstFrame = extent.StartFrame > minimumFrame ? extent.StartFrame : minimumFrame;
            if (!PhysicalAllocatorInternals.TryAlignFrame(firstFrame, request.AlignmentPages, out ulong candidateFrame)) continue;
            if (!PhysicalAllocatorInternals.FitsWindow(candidateFrame, request.PageCount, minimumFrame, maximumFrameExclusive)) continue;
            if (candidateFrame < extent.StartFrame || candidateFrame - extent.StartFrame > extent.PageCount) continue;
            ulong offset = candidateFrame - extent.StartFrame;
            if (request.PageCount > extent.PageCount - offset) continue;
            if (!TakeRange(index, candidateFrame, request.PageCount))
            {
                status = PhysicalMemoryStatus.WorkspaceTooSmall;
                return false;
            }

            ref AllocationRecord record = ref _allocations[recordIndex];
            record.Token = PhysicalAllocatorInternals.NextToken(ref _nextToken);
            record.StartFrame = candidateFrame;
            record.RequestedPages = request.PageCount;
            record.ActualPages = request.PageCount;
            record.Purpose = request.Purpose;
            record.Active = 1;
            allocation = PhysicalAllocatorInternals.CreateAllocation(ref record);
            status = PhysicalMemoryStatus.Success;
            return true;
        }

        status = sizeCandidateExists ? PhysicalMemoryStatus.AddressConstraintUnsatisfied : PhysicalMemoryStatus.OutOfMemory;
        return false;
    }

    /// <summary>Releases an exact live extent allocation and merges adjacent free ranges.</summary>
    /// <nova.when>Call when the owning subsystem no longer uses the physical pages.</nova.when>
    /// <nova.depends>Allocation token returned by this manager</nova.depends>
    /// <returns><see langword="true"/> when the token was live and the pages were returned once.</returns>
    /// <example><code>bool ok = allocator.TryRelease(allocation, out PhysicalMemoryStatus status);</code></example>
    public bool TryRelease(PhysicalAllocation allocation, out PhysicalMemoryStatus status)
    {
        status = PhysicalMemoryStatus.NotInitialized;
        if (!_initialized) return false;
        if (!PhysicalAllocatorInternals.TryFindAllocationRecord(_allocations, _allocationCapacity, allocation, out int recordIndex))
        {
            status = PhysicalMemoryStatus.AllocationNotFound;
            return false;
        }
        AllocationRecord record = _allocations[recordIndex];
        if (!InsertFreeRange(record.StartFrame, record.ActualPages))
        {
            status = PhysicalMemoryStatus.WorkspaceTooSmall;
            return false;
        }
        _allocations[recordIndex].Active = 0;
        status = PhysicalMemoryStatus.Success;
        return true;
    }

    /// <summary>Reserves an exact currently-free physical range and splits the containing extent.</summary>
    /// <nova.when>Use for late-discovered hardware or allocator metadata reservations.</nova.when>
    /// <nova.depends>PhysicalFrameRange</nova.depends>
    /// <returns><see langword="true"/> when the whole range was free and a reservation token was recorded.</returns>
    /// <example><code>bool ok = allocator.TryReserve(range, PhysicalMemoryPurpose.Metadata, out PhysicalReservation reservation, out PhysicalMemoryStatus status);</code></example>
    public bool TryReserve(PhysicalFrameRange range, PhysicalMemoryPurpose purpose, out PhysicalReservation reservation, out PhysicalMemoryStatus status)
    {
        reservation = default;
        status = PhysicalMemoryStatus.NotInitialized;
        if (!_initialized) return false;
        if (range.PageCount == 0 || (range.Start.Value & 0xFFFUL) != 0)
        {
            status = PhysicalMemoryStatus.InvalidParameter;
            return false;
        }
        if (!PhysicalAllocatorInternals.TryFindFreeReservationRecord(_reservations, _reservationCapacity, out int recordIndex))
        {
            status = PhysicalMemoryStatus.RecordCapacityExhausted;
            return false;
        }
        ulong startFrame = range.Start.Value / PageSize;
        for (int index = 0; index < _extentCount; index++)
        {
            ExtentRecord extent = _extents[index];
            if (startFrame < extent.StartFrame) break;
            ulong offset = startFrame - extent.StartFrame;
            if (offset > extent.PageCount || range.PageCount > extent.PageCount - offset) continue;
            if (!TakeRange(index, startFrame, range.PageCount))
            {
                status = PhysicalMemoryStatus.WorkspaceTooSmall;
                return false;
            }
            ref ReservationRecord record = ref _reservations[recordIndex];
            record.Token = PhysicalAllocatorInternals.NextToken(ref _nextToken);
            record.StartFrame = startFrame;
            record.PageCount = range.PageCount;
            record.Purpose = purpose;
            record.Active = 1;
            reservation = PhysicalAllocatorInternals.CreateReservation(ref record);
            status = PhysicalMemoryStatus.Success;
            return true;
        }
        status = PhysicalMemoryStatus.RangeNotFree;
        return false;
    }

    /// <summary>Releases a live fixed reservation and merges the returned range.</summary>
    /// <nova.when>Call only after the reserving subsystem has made the pages safe for reuse.</nova.when>
    /// <nova.depends>Reservation token returned by this manager</nova.depends>
    /// <returns><see langword="true"/> when the token was live and the exact range was returned once.</returns>
    /// <example><code>bool ok = allocator.TryReleaseReservation(reservation, out PhysicalMemoryStatus status);</code></example>
    public bool TryReleaseReservation(PhysicalReservation reservation, out PhysicalMemoryStatus status)
    {
        status = PhysicalMemoryStatus.NotInitialized;
        if (!_initialized) return false;
        if (!PhysicalAllocatorInternals.TryFindReservationRecord(_reservations, _reservationCapacity, reservation, out int recordIndex))
        {
            status = PhysicalMemoryStatus.ReservationNotFound;
            return false;
        }
        ReservationRecord record = _reservations[recordIndex];
        if (!InsertFreeRange(record.StartFrame, record.PageCount))
        {
            status = PhysicalMemoryStatus.WorkspaceTooSmall;
            return false;
        }
        _reservations[recordIndex].Active = 0;
        status = PhysicalMemoryStatus.Success;
        return true;
    }

    /// <summary>Returns current exact extent allocator accounting.</summary>
    /// <nova.when>Use for diagnostics and allocator comparison.</nova.when>
    /// <nova.depends>Current extent and record tables</nova.depends>
    /// <returns>A current physical-memory statistics snapshot.</returns>
    /// <example><code>PhysicalMemoryStatistics statistics = allocator.GetStatistics();</code></example>
    public readonly PhysicalMemoryStatistics GetStatistics()
    {
        if (!_initialized) return default;
        ulong freePages = 0;
        ulong largest = 0;
        for (int index = 0; index < _extentCount; index++)
        {
            freePages += _extents[index].PageCount;
            if (_extents[index].PageCount > largest) largest = _extents[index].PageCount;
        }
        CountRecords(out ulong allocatedPages, out ulong reservedPages, out int allocations, out int reservations);
        return PhysicalMemoryStatistics.Create(_totalManagedPages, freePages, allocatedPages, reservedPages, largest, allocations, reservations);
    }

    private bool TakeRange(int index, ulong startFrame, ulong pageCount)
    {
        ExtentRecord extent = _extents[index];
        ulong before = startFrame - extent.StartFrame;
        ulong afterStart = startFrame + pageCount;
        ulong extentEnd = extent.StartFrame + extent.PageCount;
        ulong after = extentEnd - afterStart;
        if (before > 0 && after > 0)
        {
            if (_extentCount >= _extentCapacity) return false;
            for (int move = _extentCount; move > index + 1; move--) _extents[move] = _extents[move - 1];
            _extents[index] = new ExtentRecord(extent.StartFrame, before);
            _extents[index + 1] = new ExtentRecord(afterStart, after);
            _extentCount++;
        }
        else if (before > 0)
        {
            _extents[index] = new ExtentRecord(extent.StartFrame, before);
        }
        else if (after > 0)
        {
            _extents[index] = new ExtentRecord(afterStart, after);
        }
        else
        {
            for (int move = index; move < _extentCount - 1; move++) _extents[move] = _extents[move + 1];
            _extentCount--;
        }
        return true;
    }

    private bool InsertFreeRange(ulong startFrame, ulong pageCount)
    {
        if (pageCount == 0 || _extentCount >= _extentCapacity) return false;
        int insert = 0;
        while (insert < _extentCount && _extents[insert].StartFrame < startFrame) insert++;
        for (int move = _extentCount; move > insert; move--) _extents[move] = _extents[move - 1];
        _extents[insert] = new ExtentRecord(startFrame, pageCount);
        _extentCount++;
        MergeAround(insert);
        return true;
    }

    private void MergeAround(int index)
    {
        if (index > 0 && _extents[index - 1].StartFrame + _extents[index - 1].PageCount == _extents[index].StartFrame)
        {
            _extents[index - 1].PageCount += _extents[index].PageCount;
            for (int move = index; move < _extentCount - 1; move++) _extents[move] = _extents[move + 1];
            _extentCount--;
            index--;
        }
        if (index + 1 < _extentCount && _extents[index].StartFrame + _extents[index].PageCount == _extents[index + 1].StartFrame)
        {
            _extents[index].PageCount += _extents[index + 1].PageCount;
            for (int move = index + 1; move < _extentCount - 1; move++) _extents[move] = _extents[move + 1];
            _extentCount--;
        }
    }

    private void ClearRecords()
    {
        for (int index = 0; index < _allocationCapacity; index++) _allocations[index].Active = 0;
        for (int index = 0; index < _reservationCapacity; index++) _reservations[index].Active = 0;
    }

    private readonly void CountRecords(out ulong allocatedPages, out ulong reservedPages, out int allocations, out int reservations)
    {
        allocatedPages = 0;
        reservedPages = 0;
        allocations = 0;
        reservations = 0;
        for (int index = 0; index < _allocationCapacity; index++)
        {
            if (_allocations[index].Active == 0) continue;
            allocatedPages += _allocations[index].ActualPages;
            allocations++;
        }
        for (int index = 0; index < _reservationCapacity; index++)
        {
            if (_reservations[index].Active == 0) continue;
            reservedPages += _reservations[index].PageCount;
            reservations++;
        }
    }
}

internal struct ExtentRecord
{
    internal ExtentRecord(ulong startFrame, ulong pageCount)
    {
        StartFrame = startFrame;
        PageCount = pageCount;
    }

    internal ulong StartFrame;
    internal ulong PageCount;
}
