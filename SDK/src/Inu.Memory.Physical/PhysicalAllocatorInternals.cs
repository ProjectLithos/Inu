using Inu.Memory;
using Inu.Primitives;

namespace Inu.Memory.Physical;

internal static unsafe class PhysicalAllocatorInternals
{
    internal const ulong PageSize = 4096UL;

    internal static bool TryGetManagedFrameBounds(NormalisedMemoryMap map, out ulong minimumFrame, out ulong maximumFrameExclusive, out ulong managedPages, out int usableDescriptorCount)
    {
        minimumFrame = ulong.MaxValue;
        maximumFrameExclusive = 0;
        managedPages = 0;
        usableDescriptorCount = 0;
        if (map is null || map.Count < 1) return false;

        for (int index = 0; index < map.Count; index++)
        {
            if (!map.TryGetDescriptor(index, out MemoryDescriptor descriptor)) return false;
            if (descriptor.Availability != MemoryAvailability.AvailableAfterExitBootServices) continue;
            ulong startFrame = descriptor.PhysicalStart.Value / PageSize;
            if (descriptor.PageCount > ulong.MaxValue - startFrame) return false;
            ulong endFrame = startFrame + descriptor.PageCount;
            if (startFrame < minimumFrame) minimumFrame = startFrame;
            if (endFrame > maximumFrameExclusive) maximumFrameExclusive = endFrame;
            if (managedPages > ulong.MaxValue - descriptor.PageCount) return false;
            managedPages += descriptor.PageCount;
            usableDescriptorCount++;
        }

        return usableDescriptorCount > 0 && minimumFrame < maximumFrameExclusive;
    }

    internal static bool TryGetRequestFrameWindow(PhysicalAllocationRequest request, out ulong minimumFrame, out ulong maximumFrameExclusive)
    {
        minimumFrame = request.MinimumAddress.Value / PageSize;
        maximumFrameExclusive = request.MaximumAddressExclusive.Value == 0
            ? (ulong.MaxValue / PageSize) + 1UL
            : request.MaximumAddressExclusive.Value / PageSize;
        if (request.PageCount == 0 || request.AlignmentPages == 0) return false;
        if ((request.AlignmentPages & (request.AlignmentPages - 1)) != 0) return false;
        return maximumFrameExclusive > minimumFrame;
    }

    internal static bool TryAlignFrame(ulong frame, ulong alignmentPages, out ulong alignedFrame)
    {
        alignedFrame = 0;
        if (alignmentPages == 0 || (alignmentPages & (alignmentPages - 1)) != 0) return false;
        ulong mask = alignmentPages - 1;
        if (frame > ulong.MaxValue - mask) return false;
        alignedFrame = (frame + mask) & ~mask;
        return true;
    }

    internal static bool FitsWindow(ulong startFrame, ulong pageCount, ulong minimumFrame, ulong maximumFrameExclusive)
    {
        if (pageCount == 0 || startFrame < minimumFrame) return false;
        if (startFrame > ulong.MaxValue - pageCount) return false;
        ulong endFrame = startFrame + pageCount;
        return endFrame <= maximumFrameExclusive;
    }

    internal static ulong NextToken(ref ulong nextToken)
    {
        nextToken++;
        if (nextToken == 0) nextToken++;
        return nextToken;
    }

    internal static bool TryCreateRange(ulong startFrame, ulong pageCount, out PhysicalFrameRange range)
    {
        range = default;
        if (startFrame > ulong.MaxValue / PageSize) return false;
        return PhysicalFrameRange.TryCreate(new PhysicalAddress(startFrame * PageSize), pageCount, out range);
    }

    internal static int CeilLog2(ulong value)
    {
        if (value <= 1) return 0;
        int order = 0;
        ulong current = 1;
        while (current < value && order < 63)
        {
            current <<= 1;
            order++;
        }
        return order;
    }

    internal static int FloorLog2(ulong value)
    {
        if (value == 0) return -1;
        int order = 0;
        while (value > 1)
        {
            value >>= 1;
            order++;
        }
        return order;
    }

    internal static ulong BlockPages(int order) => 1UL << order;

    internal static bool TryAlignWorkspaceBytes(ulong value, out ulong aligned)
    {
        aligned = 0;
        if (value > ulong.MaxValue - 7UL) return false;
        aligned = (value + 7UL) & ~7UL;
        return true;
    }

    internal static bool TryWorkspaceBytes(ulong primaryBytes, int allocationCapacity, int reservationCapacity, out ulong requiredBytes)
    {
        requiredBytes = 0;
        if (allocationCapacity < 1 || reservationCapacity < 0) return false;
        ulong allocationBytes = (ulong)allocationCapacity * (ulong)sizeof(AllocationRecord);
        ulong reservationBytes = (ulong)reservationCapacity * (ulong)sizeof(ReservationRecord);
        if (primaryBytes > ulong.MaxValue - allocationBytes) return false;
        ulong total = primaryBytes + allocationBytes;
        if (total > ulong.MaxValue - reservationBytes) return false;
        requiredBytes = total + reservationBytes;
        return true;
    }

    internal static bool TryPartition(PhysicalAllocatorWorkspace workspace, ulong primaryBytes, int allocationCapacity, int reservationCapacity, out byte* primary, out AllocationRecord* allocations, out ReservationRecord* reservations)
    {
        primary = null;
        allocations = null;
        reservations = null;
        if (!TryWorkspaceBytes(primaryBytes, allocationCapacity, reservationCapacity, out ulong requiredBytes)) return false;
        if (workspace.Address == 0 || workspace.ByteLength < requiredBytes || requiredBytes > long.MaxValue) return false;
        byte* baseAddress = (byte*)workspace.Address;
        primary = baseAddress;
        allocations = (AllocationRecord*)(baseAddress + (long)primaryBytes);
        ulong allocationBytes = (ulong)allocationCapacity * (ulong)sizeof(AllocationRecord);
        reservations = (ReservationRecord*)(baseAddress + (long)(primaryBytes + allocationBytes));
        return true;
    }

    internal static bool TryFindFreeAllocationRecord(AllocationRecord* records, int capacity, out int index)
    {
        index = -1;
        for (int current = 0; current < capacity; current++)
        {
            if (records[current].Active == 0)
            {
                index = current;
                return true;
            }
        }
        return false;
    }

    internal static bool TryFindAllocationRecord(AllocationRecord* records, int capacity, PhysicalAllocation allocation, out int index)
    {
        index = -1;
        for (int current = 0; current < capacity; current++)
        {
            if (records[current].Active != 0 &&
                records[current].Token == allocation.Token &&
                records[current].StartFrame * PageSize == allocation.Range.Start.Value &&
                records[current].ActualPages == allocation.Range.PageCount)
            {
                index = current;
                return true;
            }
        }
        return false;
    }

    internal static bool TryFindFreeReservationRecord(ReservationRecord* records, int capacity, out int index)
    {
        index = -1;
        for (int current = 0; current < capacity; current++)
        {
            if (records[current].Active == 0)
            {
                index = current;
                return true;
            }
        }
        return false;
    }

    internal static bool TryFindReservationRecord(ReservationRecord* records, int capacity, PhysicalReservation reservation, out int index)
    {
        index = -1;
        for (int current = 0; current < capacity; current++)
        {
            if (records[current].Active != 0 &&
                records[current].Token == reservation.Token &&
                records[current].StartFrame * PageSize == reservation.Range.Start.Value &&
                records[current].PageCount == reservation.Range.PageCount)
            {
                index = current;
                return true;
            }
        }
        return false;
    }

    internal static PhysicalAllocation CreateAllocation(ref AllocationRecord record)
    {
        if (!TryCreateRange(record.StartFrame, record.ActualPages, out PhysicalFrameRange range)) return default;
        return new PhysicalAllocation(range, record.RequestedPages, record.Token, record.Purpose);
    }

    internal static PhysicalReservation CreateReservation(ref ReservationRecord record)
    {
        if (!TryCreateRange(record.StartFrame, record.PageCount, out PhysicalFrameRange range)) return default;
        return new PhysicalReservation(range, record.Token, record.Purpose);
    }
}

internal struct AllocationRecord
{
    internal ulong Token;
    internal ulong StartFrame;
    internal ulong RequestedPages;
    internal ulong ActualPages;
    internal PhysicalMemoryPurpose Purpose;
    internal byte Active;
}

internal struct ReservationRecord
{
    internal ulong Token;
    internal ulong StartFrame;
    internal ulong PageCount;
    internal PhysicalMemoryPurpose Purpose;
    internal byte Active;
}
