using Inu.Core;
using Inu.Memory;

namespace Inu.Memory.Physical;

/// <summary>Allocates contiguous physical frames from a one-bit-per-frame ownership bitmap.</summary>
/// <nova.when>Choose for predictable metadata and simple frame-state checks when the physical address space is not excessively sparse.</nova.when>
/// <nova.depends>Inu.Memory.Physical.Contracts and NormalisedMemoryMap</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public unsafe struct BitmapPhysicalMemoryManager : IPhysicalMemoryManager
{
    private byte* _bitmap;
    private AllocationRecord* _allocations;
    private ReservationRecord* _reservations;
    private ulong _minimumFrame;
    private ulong _frameSpan;
    private ulong _totalManagedPages;
    private int _allocationCapacity;
    private int _reservationCapacity;
    private ulong _nextToken;
    private bool _initialized;

    /// <summary>Gets the bitmap allocator methodology.</summary>
    /// <nova.when>Use for allocator-selection diagnostics.</nova.when>
    public readonly PhysicalAllocatorMethod Method => PhysicalAllocatorMethod.Bitmap;
    /// <summary>Gets whether the manager has been initialised.</summary>
    /// <nova.when>Check before indirect allocation through a stored manager instance.</nova.when>
    public readonly bool IsInitialized => _initialized;
    /// <summary>Gets the fixed Inu physical frame size.</summary>
    /// <nova.when>Use for frame-to-byte conversions.</nova.when>
    public readonly ulong PageSize => PhysicalAllocatorInternals.PageSize;

    /// <summary>Calculates bitmap and record-table metadata required for a normalised map.</summary>
    /// <nova.when>Call before reserving caller-owned bitmap metadata memory.</nova.when>
    /// <nova.depends>Highest and lowest immediately allocatable physical frame</nova.depends>
    /// <returns><see langword="true"/> when the sparse frame span and record tables can be represented.</returns>
    /// <example><code>bool sized = BitmapPhysicalMemoryManager.TryGetRequiredWorkspaceBytes(map, 128, 32, out ulong bytes);</code></example>
    public static bool TryGetRequiredWorkspaceBytes(NormalisedMemoryMap map, int allocationCapacity, int reservationCapacity, out ulong bytes)
    {
        bytes = 0;
        if (!PhysicalAllocatorInternals.TryGetManagedFrameBounds(map, out ulong minimumFrame, out ulong maximumFrameExclusive, out _, out _)) return false;
        ulong span = maximumFrameExclusive - minimumFrame;
        if (span > ulong.MaxValue - 7UL) return false;
        ulong bitmapBytes = (span + 7UL) / 8UL;
        if (!PhysicalAllocatorInternals.TryAlignWorkspaceBytes(bitmapBytes, out ulong primaryBytes)) return false;
        return PhysicalAllocatorInternals.TryWorkspaceBytes(primaryBytes, allocationCapacity, reservationCapacity, out bytes);
    }

    /// <summary>Initialises the bitmap to unavailable, then marks only normalised immediately-allocatable frames free.</summary>
    /// <nova.when>Call once after memory-map normalisation and before virtual-memory construction.</nova.when>
    /// <nova.depends>Caller-owned PhysicalAllocatorWorkspace</nova.depends>
    /// <returns><see langword="true"/> when the bitmap and bounded record tables fit the workspace.</returns>
    /// <example><code>bool ready = allocator.TryInitialize(map, workspace, 128, 32, out PhysicalMemoryStatus status);</code></example>
    public bool TryInitialize(NormalisedMemoryMap map, PhysicalAllocatorWorkspace workspace, int allocationCapacity, int reservationCapacity, out PhysicalMemoryStatus status)
    {
        status = PhysicalMemoryStatus.InvalidParameter;
        if (_initialized)
        {
            status = PhysicalMemoryStatus.AlreadyInitialized;
            return false;
        }
        if (!PhysicalAllocatorInternals.TryGetManagedFrameBounds(map, out ulong minimumFrame, out ulong maximumFrameExclusive, out ulong managedPages, out _)) return false;
        if (!TryGetRequiredWorkspaceBytes(map, allocationCapacity, reservationCapacity, out ulong requiredBytes)) return false;
        ulong span = maximumFrameExclusive - minimumFrame;
        ulong bitmapBytes = (span + 7UL) / 8UL;
        if (!PhysicalAllocatorInternals.TryAlignWorkspaceBytes(bitmapBytes, out ulong primaryBytes)) return false;
        if (workspace.ByteLength < requiredBytes || bitmapBytes > long.MaxValue || !PhysicalAllocatorInternals.TryPartition(workspace, primaryBytes, allocationCapacity, reservationCapacity, out byte* primary, out AllocationRecord* allocations, out ReservationRecord* reservations))
        {
            status = PhysicalMemoryStatus.WorkspaceTooSmall;
            return false;
        }

        _bitmap = primary;
        _allocations = allocations;
        _reservations = reservations;
        _minimumFrame = minimumFrame;
        _frameSpan = span;
        _totalManagedPages = managedPages;
        _allocationCapacity = allocationCapacity;
        _reservationCapacity = reservationCapacity;
        _nextToken = 0;
        for (ulong index = 0; index < bitmapBytes; index++) _bitmap[index] = 0xFF;
        ClearRecords();

        for (int index = 0; index < map.Count; index++)
        {
            if (!map.TryGetDescriptor(index, out MemoryDescriptor descriptor)) return false;
            if (descriptor.Availability != MemoryAvailability.AvailableAfterExitBootServices) continue;
            ulong startFrame = descriptor.PhysicalStart.Value / PageSize;
            SetRange(startFrame, descriptor.PageCount, false);
        }
        _initialized = true;
        status = PhysicalMemoryStatus.Success;
        return true;
    }

    /// <summary>Allocates an exact contiguous run of free bitmap frames.</summary>
    /// <nova.when>Use for arbitrary page counts with page or power-of-two alignment constraints.</nova.when>
    /// <nova.depends>Successful TryInitialize</nova.depends>
    /// <returns><see langword="true"/> when a matching free run is marked owned and recorded.</returns>
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

        ulong spanEnd = _minimumFrame + _frameSpan;
        ulong scanStart = minimumFrame > _minimumFrame ? minimumFrame : _minimumFrame;
        ulong scanEnd = maximumFrameExclusive < spanEnd ? maximumFrameExclusive : spanEnd;
        if (scanStart < scanEnd && PhysicalAllocatorInternals.TryAlignFrame(scanStart, request.AlignmentPages, out ulong candidate))
        {
            while (candidate < scanEnd)
            {
                if (PhysicalAllocatorInternals.FitsWindow(candidate, request.PageCount, scanStart, scanEnd) && IsRangeFree(candidate, request.PageCount))
                {
                    SetRange(candidate, request.PageCount, true);
                    ref AllocationRecord record = ref _allocations[recordIndex];
                    record.Token = PhysicalAllocatorInternals.NextToken(ref _nextToken);
                    record.StartFrame = candidate;
                    record.RequestedPages = request.PageCount;
                    record.ActualPages = request.PageCount;
                    record.Purpose = request.Purpose;
                    record.Active = 1;
                    allocation = PhysicalAllocatorInternals.CreateAllocation(ref record);
                    status = PhysicalMemoryStatus.Success;
                    return true;
                }
                if (candidate > ulong.MaxValue - request.AlignmentPages) break;
                candidate += request.AlignmentPages;
            }
        }
        status = HasAnyFreeRun(request.PageCount) ? PhysicalMemoryStatus.AddressConstraintUnsatisfied : PhysicalMemoryStatus.OutOfMemory;
        return false;
    }

    /// <summary>Releases a live bitmap allocation after validating its opaque token and exact range.</summary>
    /// <nova.when>Call when the allocation owner has stopped using the pages.</nova.when>
    /// <nova.depends>Allocation token returned by this manager</nova.depends>
    /// <returns><see langword="true"/> when the live allocation is cleared exactly once.</returns>
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
        SetRange(record.StartFrame, record.ActualPages, false);
        _allocations[recordIndex].Active = 0;
        status = PhysicalMemoryStatus.Success;
        return true;
    }

    /// <summary>Marks an exact currently-free bitmap range reserved.</summary>
    /// <nova.when>Use for late fixed-range exclusions such as hardware or metadata discoveries.</nova.when>
    /// <nova.depends>PhysicalFrameRange</nova.depends>
    /// <returns><see langword="true"/> when every frame is managed and currently free.</returns>
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
        if (!IsRangeFree(startFrame, range.PageCount))
        {
            status = PhysicalMemoryStatus.RangeNotFree;
            return false;
        }
        SetRange(startFrame, range.PageCount, true);
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

    /// <summary>Releases a live fixed reservation back to the bitmap.</summary>
    /// <nova.when>Call only after the reserving subsystem has made the pages reusable.</nova.when>
    /// <nova.depends>Reservation token returned by this manager</nova.depends>
    /// <returns><see langword="true"/> when the reservation token is live and released exactly once.</returns>
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
        SetRange(record.StartFrame, record.PageCount, false);
        _reservations[recordIndex].Active = 0;
        status = PhysicalMemoryStatus.Success;
        return true;
    }

    /// <summary>Returns current bitmap allocator page accounting and largest free run.</summary>
    /// <nova.when>Use for diagnostics and physical-memory pressure reporting.</nova.when>
    /// <nova.depends>Current bitmap and record tables</nova.depends>
    /// <returns>A current physical-memory statistics snapshot.</returns>
    /// <example><code>PhysicalMemoryStatistics statistics = allocator.GetStatistics();</code></example>
    public readonly PhysicalMemoryStatistics GetStatistics()
    {
        if (!_initialized) return default;
        ulong freePages = 0;
        ulong largest = 0;
        ulong run = 0;
        for (ulong offset = 0; offset < _frameSpan; offset++)
        {
            if (!IsUsedOffset(offset))
            {
                freePages++;
                run++;
                if (run > largest) largest = run;
            }
            else run = 0;
        }
        CountRecords(out ulong allocatedPages, out ulong reservedPages, out int allocations, out int reservations);
        return PhysicalMemoryStatistics.Create(_totalManagedPages, freePages, allocatedPages, reservedPages, largest, allocations, reservations);
    }

    private readonly bool IsRangeFree(ulong startFrame, ulong pageCount)
    {
        if (pageCount == 0 || startFrame < _minimumFrame) return false;
        ulong offset = startFrame - _minimumFrame;
        if (offset > _frameSpan || pageCount > _frameSpan - offset) return false;
        for (ulong index = 0; index < pageCount; index++)
        {
            if (IsUsedOffset(offset + index)) return false;
        }
        return true;
    }

    private readonly bool HasAnyFreeRun(ulong pageCount)
    {
        ulong run = 0;
        for (ulong offset = 0; offset < _frameSpan; offset++)
        {
            if (!IsUsedOffset(offset))
            {
                run++;
                if (run >= pageCount) return true;
            }
            else run = 0;
        }
        return false;
    }

    private readonly bool IsUsedOffset(ulong offset)
    {
        ulong byteIndex = offset >> 3;
        int bit = (int)(offset & 7UL);
        return (_bitmap[byteIndex] & (1 << bit)) != 0;
    }

    private void SetRange(ulong startFrame, ulong pageCount, bool used)
    {
        ulong offset = startFrame - _minimumFrame;
        for (ulong index = 0; index < pageCount; index++)
        {
            ulong current = offset + index;
            ulong byteIndex = current >> 3;
            byte mask = (byte)(1 << (int)(current & 7UL));
            if (used) _bitmap[byteIndex] |= mask;
            else _bitmap[byteIndex] &= (byte)~mask;
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
