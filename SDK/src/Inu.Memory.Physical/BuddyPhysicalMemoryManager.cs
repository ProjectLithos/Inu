using Inu.Core;
using Inu.Memory;

namespace Inu.Memory.Physical;

/// <summary>Allocates physical memory as aligned power-of-two buddy blocks with recursive split and coalescing.</summary>
/// <nova.when>Choose when fast coalescing and naturally aligned power-of-two blocks are preferred and bounded internal fragmentation is acceptable.</nova.when>
/// <nova.depends>Inu.Memory.Physical.Contracts and NormalisedMemoryMap</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public unsafe struct BuddyPhysicalMemoryManager : IPhysicalMemoryManager
{
    private const sbyte ReservedPageState = -127;
    private sbyte* _states;
    private AllocationRecord* _allocations;
    private ReservationRecord* _reservations;
    private ulong _minimumFrame;
    private ulong _frameSpan;
    private ulong _totalManagedPages;
    private int _allocationCapacity;
    private int _reservationCapacity;
    private ulong _nextToken;
    private bool _initialized;

    /// <summary>Gets the buddy allocator methodology.</summary>
    /// <nova.when>Use for allocator-selection diagnostics.</nova.when>
    public readonly PhysicalAllocatorMethod Method => PhysicalAllocatorMethod.Buddy;
    /// <summary>Gets whether the manager has been initialised.</summary>
    /// <nova.when>Check before indirect allocation through a stored manager instance.</nova.when>
    public readonly bool IsInitialized => _initialized;
    /// <summary>Gets the fixed Inu physical frame size.</summary>
    /// <nova.when>Use for frame-to-byte conversions.</nova.when>
    public readonly ulong PageSize => PhysicalAllocatorInternals.PageSize;

    /// <summary>Calculates one-byte-per-frame buddy metadata plus bounded ownership record tables.</summary>
    /// <nova.when>Call before reserving caller-owned buddy metadata storage.</nova.when>
    /// <nova.depends>Highest and lowest immediately allocatable physical frame</nova.depends>
    /// <returns><see langword="true"/> when the metadata size can be represented.</returns>
    /// <example><code>bool sized = BuddyPhysicalMemoryManager.TryGetRequiredWorkspaceBytes(map, 128, 32, out ulong bytes);</code></example>
    public static bool TryGetRequiredWorkspaceBytes(NormalisedMemoryMap map, int allocationCapacity, int reservationCapacity, out ulong bytes)
    {
        bytes = 0;
        if (!PhysicalAllocatorInternals.TryGetManagedFrameBounds(map, out ulong minimumFrame, out ulong maximumFrameExclusive, out _, out _)) return false;
        ulong span = maximumFrameExclusive - minimumFrame;
        if (!PhysicalAllocatorInternals.TryAlignWorkspaceBytes(span, out ulong primaryBytes)) return false;
        return PhysicalAllocatorInternals.TryWorkspaceBytes(primaryBytes, allocationCapacity, reservationCapacity, out bytes);
    }

    /// <summary>Initialises free buddy blocks from immediately allocatable normalised ranges without allocating metadata internally.</summary>
    /// <nova.when>Call once after memory-map normalisation and before the VMM or kernel heap.</nova.when>
    /// <nova.depends>Caller-owned PhysicalAllocatorWorkspace</nova.depends>
    /// <returns><see langword="true"/> when the frame-state table and ownership records fit the supplied workspace.</returns>
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
        if (!PhysicalAllocatorInternals.TryAlignWorkspaceBytes(span, out ulong primaryBytes)) return false;
        if (workspace.ByteLength < requiredBytes || span > long.MaxValue || !PhysicalAllocatorInternals.TryPartition(workspace, primaryBytes, allocationCapacity, reservationCapacity, out byte* primary, out AllocationRecord* allocations, out ReservationRecord* reservations))
        {
            status = PhysicalMemoryStatus.WorkspaceTooSmall;
            return false;
        }

        _states = (sbyte*)primary;
        _allocations = allocations;
        _reservations = reservations;
        _minimumFrame = minimumFrame;
        _frameSpan = span;
        _totalManagedPages = managedPages;
        _allocationCapacity = allocationCapacity;
        _reservationCapacity = reservationCapacity;
        _nextToken = 0;
        for (ulong index = 0; index < _frameSpan; index++) _states[index] = 0;
        ClearRecords();

        for (int index = 0; index < map.Count; index++)
        {
            if (!map.TryGetDescriptor(index, out MemoryDescriptor descriptor)) return false;
            if (descriptor.Availability != MemoryAvailability.AvailableAfterExitBootServices) continue;
            SeedRange(descriptor.PhysicalStart.Value / PageSize, descriptor.PageCount);
        }
        CoalesceSeededBlocks();
        _initialized = true;
        status = PhysicalMemoryStatus.Success;
        return true;
    }

    /// <summary>Allocates one power-of-two buddy block large enough for the requested page count and alignment.</summary>
    /// <nova.when>Use when natural alignment and deterministic buddy coalescing are useful; inspect RequestedPageCount versus Range.PageCount for rounding.</nova.when>
    /// <nova.depends>Successful TryInitialize</nova.depends>
    /// <returns><see langword="true"/> when a suitable free block can be split and recorded.</returns>
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

        int requestedOrder = PhysicalAllocatorInternals.CeilLog2(request.PageCount);
        int alignmentOrder = PhysicalAllocatorInternals.CeilLog2(request.AlignmentPages);
        int targetOrder = requestedOrder > alignmentOrder ? requestedOrder : alignmentOrder;
        if (targetOrder < 0 || targetOrder > 62)
        {
            status = PhysicalMemoryStatus.InvalidParameter;
            return false;
        }
        ulong targetPages = PhysicalAllocatorInternals.BlockPages(targetOrder);

        for (ulong offset = 0; offset < _frameSpan; offset++)
        {
            sbyte state = _states[offset];
            if (state <= 0) continue;
            int blockOrder = state - 1;
            if (blockOrder < targetOrder) continue;
            ulong blockStart = _minimumFrame + offset;
            ulong blockPages = PhysicalAllocatorInternals.BlockPages(blockOrder);
            ulong blockEnd = blockStart + blockPages;
            ulong lower = blockStart > minimumFrame ? blockStart : minimumFrame;
            ulong upper = blockEnd < maximumFrameExclusive ? blockEnd : maximumFrameExclusive;
            if (lower >= upper || !PhysicalAllocatorInternals.TryAlignFrame(lower, targetPages, out ulong targetStart)) continue;
            if (!PhysicalAllocatorInternals.FitsWindow(targetStart, targetPages, lower, upper)) continue;
            if (!SplitToTarget(blockStart, blockOrder, targetStart, targetOrder)) continue;
            ulong targetOffset = targetStart - _minimumFrame;
            _states[targetOffset] = (sbyte)-(targetOrder + 1);

            ref AllocationRecord record = ref _allocations[recordIndex];
            record.Token = PhysicalAllocatorInternals.NextToken(ref _nextToken);
            record.StartFrame = targetStart;
            record.RequestedPages = request.PageCount;
            record.ActualPages = targetPages;
            record.Purpose = request.Purpose;
            record.Active = 1;
            allocation = PhysicalAllocatorInternals.CreateAllocation(ref record);
            status = PhysicalMemoryStatus.Success;
            return true;
        }

        status = HasFreeBlockAtLeast(targetOrder) ? PhysicalMemoryStatus.AddressConstraintUnsatisfied : PhysicalMemoryStatus.OutOfMemory;
        return false;
    }

    /// <summary>Releases a live buddy allocation and recursively coalesces matching free buddies.</summary>
    /// <nova.when>Call when the allocation owner no longer uses the whole returned buddy block.</nova.when>
    /// <nova.depends>Allocation token returned by this manager</nova.depends>
    /// <returns><see langword="true"/> when the token is live and the block is released exactly once.</returns>
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
        int order = PhysicalAllocatorInternals.CeilLog2(record.ActualPages);
        ulong offset = record.StartFrame - _minimumFrame;
        _states[offset] = (sbyte)(order + 1);
        Coalesce(record.StartFrame, order);
        _allocations[recordIndex].Active = 0;
        status = PhysicalMemoryStatus.Success;
        return true;
    }

    /// <summary>Reserves an exact arbitrary free range by splitting containing buddy blocks down to individual pages.</summary>
    /// <nova.when>Use for late fixed-range exclusions without requiring the reserved range itself to be a power of two.</nova.when>
    /// <nova.depends>PhysicalFrameRange</nova.depends>
    /// <returns><see langword="true"/> when every requested frame was free and is reserved exactly.</returns>
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
        if (startFrame < _minimumFrame || startFrame - _minimumFrame > _frameSpan || range.PageCount > _frameSpan - (startFrame - _minimumFrame))
        {
            status = PhysicalMemoryStatus.RangeNotFree;
            return false;
        }

        ulong reservedCount = 0;
        for (; reservedCount < range.PageCount; reservedCount++)
        {
            ulong frame = startFrame + reservedCount;
            if (!ReserveSinglePage(frame))
            {
                for (ulong rollback = 0; rollback < reservedCount; rollback++) ReleaseReservedPage(startFrame + rollback);
                status = PhysicalMemoryStatus.RangeNotFree;
                return false;
            }
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

    /// <summary>Releases every page of a live exact reservation and coalesces newly matching buddies.</summary>
    /// <nova.when>Call only when the reserving subsystem has made the complete range reusable.</nova.when>
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
        for (ulong index = 0; index < record.PageCount; index++) ReleaseReservedPage(record.StartFrame + index);
        _reservations[recordIndex].Active = 0;
        status = PhysicalMemoryStatus.Success;
        return true;
    }

    /// <summary>Returns current buddy block, allocation, reservation, and internal-fragmentation accounting.</summary>
    /// <nova.when>Use for diagnostics and allocator comparison.</nova.when>
    /// <nova.depends>Current buddy state and record tables</nova.depends>
    /// <returns>A current physical-memory statistics snapshot.</returns>
    /// <example><code>PhysicalMemoryStatistics statistics = allocator.GetStatistics();</code></example>
    public readonly PhysicalMemoryStatistics GetStatistics()
    {
        if (!_initialized) return default;
        ulong freePages = 0;
        ulong largest = 0;
        for (ulong offset = 0; offset < _frameSpan; offset++)
        {
            sbyte state = _states[offset];
            if (state <= 0) continue;
            ulong blockPages = PhysicalAllocatorInternals.BlockPages(state - 1);
            freePages += blockPages;
            if (blockPages > largest) largest = blockPages;
        }
        CountRecords(out ulong allocatedPages, out ulong reservedPages, out int allocations, out int reservations);
        return PhysicalMemoryStatistics.Create(_totalManagedPages, freePages, allocatedPages, reservedPages, largest, allocations, reservations);
    }

    private void SeedRange(ulong startFrame, ulong pageCount)
    {
        ulong current = startFrame;
        ulong remaining = pageCount;
        while (remaining > 0)
        {
            int order = PhysicalAllocatorInternals.FloorLog2(remaining);
            while (order > 0)
            {
                ulong pages = PhysicalAllocatorInternals.BlockPages(order);
                if ((current & (pages - 1UL)) == 0) break;
                order--;
            }
            ulong blockPages = PhysicalAllocatorInternals.BlockPages(order);
            _states[current - _minimumFrame] = (sbyte)(order + 1);
            current += blockPages;
            remaining -= blockPages;
        }
    }

    private void CoalesceSeededBlocks()
    {
        for (int order = 0; order < 62; order++)
        {
            bool merged;
            do
            {
                merged = false;
                ulong pages = PhysicalAllocatorInternals.BlockPages(order);
                for (ulong offset = 0; offset < _frameSpan; offset++)
                {
                    if (_states[offset] != order + 1) continue;
                    ulong frame = _minimumFrame + offset;
                    ulong buddyFrame = frame ^ pages;
                    if (buddyFrame < _minimumFrame || buddyFrame - _minimumFrame >= _frameSpan) continue;
                    ulong buddyOffset = buddyFrame - _minimumFrame;
                    if (_states[buddyOffset] != order + 1) continue;
                    ulong lowerFrame = frame < buddyFrame ? frame : buddyFrame;
                    ulong lowerOffset = lowerFrame - _minimumFrame;
                    ulong upperOffset = lowerOffset + pages;
                    _states[lowerOffset] = (sbyte)(order + 2);
                    _states[upperOffset] = 0;
                    merged = true;
                }
            } while (merged);
        }
    }

    private bool SplitToTarget(ulong blockStart, int blockOrder, ulong targetStart, int targetOrder)
    {
        ulong currentStart = blockStart;
        int currentOrder = blockOrder;
        while (currentOrder > targetOrder)
        {
            ulong currentOffset = currentStart - _minimumFrame;
            if (_states[currentOffset] != currentOrder + 1) return false;
            _states[currentOffset] = 0;
            currentOrder--;
            ulong childPages = PhysicalAllocatorInternals.BlockPages(currentOrder);
            ulong rightStart = currentStart + childPages;
            ulong leftOffset = currentStart - _minimumFrame;
            ulong rightOffset = rightStart - _minimumFrame;
            _states[leftOffset] = (sbyte)(currentOrder + 1);
            _states[rightOffset] = (sbyte)(currentOrder + 1);
            if (targetStart >= rightStart) currentStart = rightStart;
        }
        return currentStart == targetStart && _states[targetStart - _minimumFrame] == targetOrder + 1;
    }

    private readonly bool HasFreeBlockAtLeast(int targetOrder)
    {
        for (ulong offset = 0; offset < _frameSpan; offset++)
        {
            if (_states[offset] > 0 && _states[offset] - 1 >= targetOrder) return true;
        }
        return false;
    }

    private bool ReserveSinglePage(ulong frame)
    {
        if (frame < _minimumFrame || frame - _minimumFrame >= _frameSpan) return false;
        for (int order = 0; order <= 62; order++)
        {
            ulong pages = PhysicalAllocatorInternals.BlockPages(order);
            ulong candidate = frame & ~(pages - 1UL);
            if (candidate < _minimumFrame || candidate - _minimumFrame >= _frameSpan) continue;
            ulong offset = candidate - _minimumFrame;
            if (_states[offset] != order + 1) continue;
            if (!SplitToTarget(candidate, order, frame, 0)) return false;
            _states[frame - _minimumFrame] = ReservedPageState;
            return true;
        }
        return false;
    }

    private void ReleaseReservedPage(ulong frame)
    {
        ulong offset = frame - _minimumFrame;
        if (_states[offset] != ReservedPageState) return;
        _states[offset] = 1;
        Coalesce(frame, 0);
    }

    private void Coalesce(ulong frame, int order)
    {
        ulong currentFrame = frame;
        int currentOrder = order;
        while (currentOrder < 62)
        {
            ulong pages = PhysicalAllocatorInternals.BlockPages(currentOrder);
            ulong buddyFrame = currentFrame ^ pages;
            if (buddyFrame < _minimumFrame || buddyFrame - _minimumFrame >= _frameSpan) break;
            ulong currentOffset = currentFrame - _minimumFrame;
            ulong buddyOffset = buddyFrame - _minimumFrame;
            if (_states[buddyOffset] != currentOrder + 1) break;
            _states[currentOffset] = 0;
            _states[buddyOffset] = 0;
            currentFrame = currentFrame < buddyFrame ? currentFrame : buddyFrame;
            currentOrder++;
            _states[currentFrame - _minimumFrame] = (sbyte)(currentOrder + 1);
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
