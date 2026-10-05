using Inu.Memory;
using Inu.Primitives;

namespace Inu.Boot.Memory;

/// <summary>Creates one of Inu's supported memory-map normalisation implementations.</summary>
/// <nova.when>Use when selecting strict, safety-priority, or conservative firmware-overlap handling.</nova.when>
/// <nova.depends>IMemoryMapNormaliser</nova.depends>
public static class MemoryMapNormaliserFactory
{
    /// <summary>Creates the requested stateless normaliser.</summary>
    /// <nova.when>Use during boot-policy selection or SDK configuration.</nova.when>
    /// <nova.depends>MemoryMapNormalisationMethod</nova.depends>
    /// <returns>A normaliser implementing the selected overlap policy.</returns>
    /// <example><code>IMemoryMapNormaliser normaliser = MemoryMapNormaliserFactory.Create(MemoryMapNormalisationMethod.SafetyPriority);</code></example>
    public static IMemoryMapNormaliser Create(MemoryMapNormalisationMethod method)
    {
        return method switch
        {
            MemoryMapNormalisationMethod.Strict => new StrictMemoryMapNormaliser(),
            MemoryMapNormalisationMethod.SafetyPriority => new SafetyPriorityMemoryMapNormaliser(),
            MemoryMapNormalisationMethod.Conservative => new ConservativeMemoryMapNormaliser(),
            _ => new StrictMemoryMapNormaliser()
        };
    }
}

/// <summary>Rejects incompatible overlaps in the firmware map while applying explicit reservations.</summary>
/// <nova.when>Use on trusted firmware or when malformed maps must stop boot deterministically.</nova.when>
/// <nova.depends>MemoryMapNormaliserCore</nova.depends>
public sealed class StrictMemoryMapNormaliser : IMemoryMapNormaliser
{
    /// <summary>Gets the strict implementation identifier.</summary>
    /// <nova.when>Use to confirm that overlap conflicts will be rejected.</nova.when>
    public MemoryMapNormalisationMethod Method => MemoryMapNormalisationMethod.Strict;

    /// <summary>Normalises using strict firmware-overlap validation.</summary>
    /// <nova.when>Use when incompatible source overlaps are considered fatal.</nova.when>
    /// <nova.depends>Final immutable map source</nova.depends>
    /// <returns>A detailed normalisation result.</returns>
    /// <example><code>MemoryMapNormalisationResult result = normaliser.Normalise(source, reservations, workspace, out NormalisedMemoryMap? map);</code></example>
    public MemoryMapNormalisationResult Normalise(IMemoryMapSource source, MemoryReservation[] reservations, MemoryMapNormalisationWorkspace workspace, out NormalisedMemoryMap? map)
        => MemoryMapNormaliserCore.Normalise(source, reservations, workspace, Method, out map);
}

/// <summary>Resolves incompatible firmware overlaps by selecting the safest ownership type.</summary>
/// <nova.when>Use on real hardware where defensive progress is preferred over rejecting the map.</nova.when>
/// <nova.depends>MemoryMapNormaliserCore safety priorities</nova.depends>
public sealed class SafetyPriorityMemoryMapNormaliser : IMemoryMapNormaliser
{
    /// <summary>Gets the safety-priority implementation identifier.</summary>
    /// <nova.when>Use to confirm that overlaps will choose the safest recognised owner.</nova.when>
    public MemoryMapNormalisationMethod Method => MemoryMapNormalisationMethod.SafetyPriority;

    /// <summary>Normalises using deterministic safety-priority overlap resolution.</summary>
    /// <nova.when>Use to retain the most restrictive known owner for every overlap.</nova.when>
    /// <nova.depends>Final immutable map source</nova.depends>
    /// <returns>A detailed normalisation result.</returns>
    /// <example><code>MemoryMapNormalisationResult result = normaliser.Normalise(source, reservations, workspace, out NormalisedMemoryMap? map);</code></example>
    public MemoryMapNormalisationResult Normalise(IMemoryMapSource source, MemoryReservation[] reservations, MemoryMapNormalisationWorkspace workspace, out NormalisedMemoryMap? map)
        => MemoryMapNormaliserCore.Normalise(source, reservations, workspace, Method, out map);
}

/// <summary>Converts incompatible firmware overlaps into reserved memory while preserving runtime ownership.</summary>
/// <nova.when>Use for maximum safety when firmware ownership conflicts cannot be trusted.</nova.when>
/// <nova.depends>MemoryMapNormaliserCore conservative synthesis</nova.depends>
public sealed class ConservativeMemoryMapNormaliser : IMemoryMapNormaliser
{
    /// <summary>Gets the conservative implementation identifier.</summary>
    /// <nova.when>Use to confirm that uncertain overlaps will remain reserved.</nova.when>
    public MemoryMapNormalisationMethod Method => MemoryMapNormalisationMethod.Conservative;

    /// <summary>Normalises by reserving every incompatible firmware overlap.</summary>
    /// <nova.when>Use when preserving availability is less important than avoiding unsafe allocation.</nova.when>
    /// <nova.depends>Final immutable map source</nova.depends>
    /// <returns>A detailed normalisation result.</returns>
    /// <example><code>MemoryMapNormalisationResult result = normaliser.Normalise(source, reservations, workspace, out NormalisedMemoryMap? map);</code></example>
    public MemoryMapNormalisationResult Normalise(IMemoryMapSource source, MemoryReservation[] reservations, MemoryMapNormalisationWorkspace workspace, out NormalisedMemoryMap? map)
        => MemoryMapNormaliserCore.Normalise(source, reservations, workspace, Method, out map);
}

internal static class MemoryMapNormaliserCore
{
    internal static MemoryMapNormalisationResult Normalise(
        IMemoryMapSource source,
        MemoryReservation[] reservations,
        MemoryMapNormalisationWorkspace workspace,
        MemoryMapNormalisationMethod method,
        out NormalisedMemoryMap? map)
    {
        map = null;
        if (source is null || reservations is null || workspace is null || source.Count < 1)
            return Result(MemoryMapNormalisationStatus.InvalidInput, source?.Count ?? 0, reservations?.Length ?? 0);
        if (!source.IsFinal)
            return Result(MemoryMapNormalisationStatus.FinalMapRequired, source.Count, reservations.Length);

        int sourceCount = source.Count;
        if (sourceCount > workspace.Inputs.Length || reservations.Length > workspace.Inputs.Length - sourceCount)
            return Result(MemoryMapNormalisationStatus.InsufficientCapacity, sourceCount, reservations.Length);
        int totalCount = sourceCount + reservations.Length;
        if (totalCount > workspace.Boundaries.Length / 2)
            return Result(MemoryMapNormalisationStatus.InsufficientCapacity, sourceCount, reservations.Length);

        for (int index = 0; index < sourceCount; index++)
        {
            if (!source.TryGetDescriptor(index, out MemoryDescriptor descriptor))
                return Result(MemoryMapNormalisationStatus.InvalidDescriptor, sourceCount, reservations.Length);
            workspace.Inputs[index] = descriptor;
        }
        for (int index = 0; index < reservations.Length; index++)
            workspace.Inputs[sourceCount + index] = reservations[index].Descriptor;

        int boundaryCount = 0;
        for (int index = 0; index < totalCount; index++)
        {
            MemoryDescriptor descriptor = workspace.Inputs[index];
            if (descriptor.Length == 0 || descriptor.EndExclusive <= descriptor.PhysicalStart.Value)
                return Result(MemoryMapNormalisationStatus.InvalidDescriptor, sourceCount, reservations.Length);
            workspace.Boundaries[boundaryCount++] = descriptor.PhysicalStart.Value;
            workspace.Boundaries[boundaryCount++] = descriptor.EndExclusive;
        }
        SortAndDeduplicate(workspace.Boundaries, ref boundaryCount);

        int outputCount = 0;
        int splitCount = 0;
        int mergeCount = 0;
        int resolvedOverlapCount = 0;
        for (int boundaryIndex = 0; boundaryIndex + 1 < boundaryCount; boundaryIndex++)
        {
            ulong start = workspace.Boundaries[boundaryIndex];
            ulong end = workspace.Boundaries[boundaryIndex + 1];
            if (start == end) continue;

            int sourceSelection = -1;
            bool sourceConflict = false;
            int sourceCoverage = 0;
            for (int index = 0; index < sourceCount; index++)
            {
                MemoryDescriptor candidate = workspace.Inputs[index];
                if (!Covers(candidate, start, end)) continue;
                sourceCoverage++;
                if (sourceSelection < 0)
                {
                    sourceSelection = index;
                    continue;
                }
                MemoryDescriptor selected = workspace.Inputs[sourceSelection];
                if (!SameMetadata(selected, candidate)) sourceConflict = true;
                if (IsSafer(candidate, selected)) sourceSelection = index;
            }

            int reservationSelection = -1;
            for (int index = sourceCount; index < totalCount; index++)
            {
                MemoryDescriptor reservation = workspace.Inputs[index];
                if (!Covers(reservation, start, end)) continue;
                if (reservationSelection < 0 || IsSafer(reservation, workspace.Inputs[reservationSelection]))
                    reservationSelection = index;
            }

            if (sourceSelection < 0 && reservationSelection < 0) continue;
            if (sourceCoverage > 1) resolvedOverlapCount++;
            if (sourceConflict && method == MemoryMapNormalisationMethod.Strict)
                return new MemoryMapNormalisationResult(MemoryMapNormalisationStatus.OverlapConflict, sourceCount, reservations.Length, outputCount, splitCount, mergeCount, resolvedOverlapCount);

            MemoryDescriptor selectedDescriptor;
            if (sourceSelection < 0)
            {
                if (!workspace.Inputs[reservationSelection].TrySlice(start, end, out selectedDescriptor))
                    return Result(MemoryMapNormalisationStatus.InvalidDescriptor, sourceCount, reservations.Length);
            }
            else if (sourceConflict && method == MemoryMapNormalisationMethod.Conservative)
            {
                if (!TryCreateConservativeInterval(workspace.Inputs, sourceCount, start, end, out selectedDescriptor))
                    return Result(MemoryMapNormalisationStatus.InvalidDescriptor, sourceCount, reservations.Length);
            }
            else if (sourceConflict && method == MemoryMapNormalisationMethod.SafetyPriority)
            {
                if (!TryCreateSafetyInterval(workspace.Inputs, sourceCount, sourceSelection, start, end, out selectedDescriptor))
                    return Result(MemoryMapNormalisationStatus.InvalidDescriptor, sourceCount, reservations.Length);
            }
            else if (!workspace.Inputs[sourceSelection].TrySlice(start, end, out selectedDescriptor))
            {
                return Result(MemoryMapNormalisationStatus.InvalidDescriptor, sourceCount, reservations.Length);
            }

            if (reservationSelection >= 0 && !TryCreateReservationInterval(workspace.Inputs, sourceCount, reservationSelection, start, end, out selectedDescriptor))
                return Result(MemoryMapNormalisationStatus.InvalidDescriptor, sourceCount, reservations.Length);

            splitCount++;
            if (outputCount > 0 && workspace.Outputs[outputCount - 1].IsMergeCompatible(selectedDescriptor))
            {
                MemoryDescriptor previous = workspace.Outputs[outputCount - 1];
                if (previous.PageCount > ulong.MaxValue - selectedDescriptor.PageCount)
                    return Result(MemoryMapNormalisationStatus.InvalidDescriptor, sourceCount, reservations.Length);
                ulong pages = previous.PageCount + selectedDescriptor.PageCount;
                if (!MemoryDescriptor.TryCreate(previous.PhysicalStart, pages, previous.MemoryType, previous.CacheAttributes, previous.RuntimeStatus, previous.Availability, previous.HasNumaNode, previous.NumaNode, out MemoryDescriptor merged))
                    return Result(MemoryMapNormalisationStatus.InvalidDescriptor, sourceCount, reservations.Length);
                workspace.Outputs[outputCount - 1] = merged;
                mergeCount++;
                continue;
            }
            if (outputCount >= workspace.Outputs.Length)
                return new MemoryMapNormalisationResult(MemoryMapNormalisationStatus.InsufficientCapacity, sourceCount, reservations.Length, outputCount, splitCount, mergeCount, resolvedOverlapCount);
            workspace.Outputs[outputCount++] = selectedDescriptor;
        }

        if (outputCount == 0)
            return Result(MemoryMapNormalisationStatus.InvalidDescriptor, sourceCount, reservations.Length);
        map = new NormalisedMemoryMap(workspace.Outputs, outputCount, method);
        return new MemoryMapNormalisationResult(MemoryMapNormalisationStatus.Success, sourceCount, reservations.Length, outputCount, splitCount, mergeCount, resolvedOverlapCount);
    }

    private static bool TryCreateReservationInterval(
        MemoryDescriptor[] descriptors,
        int sourceCount,
        int reservationIndex,
        ulong start,
        ulong end,
        out MemoryDescriptor descriptor)
    {
        MemoryDescriptor reservation = descriptors[reservationIndex];
        MemoryRuntimeStatus runtimeStatus = CombineRuntimeStatus(descriptors, sourceCount, start, end);
        MemoryCacheAttributes attributes = CombineReservationAttributes(
            CombineAttributes(descriptors, sourceCount, start, end),
            reservation.CacheAttributes);
        bool hasNumaNode = TryGetConsistentNumaNode(descriptors, sourceCount, start, end, out uint numaNode);
        return MemoryDescriptor.TryCreate(
            new PhysicalAddress(start),
            (end - start) / 4096UL,
            reservation.MemoryType,
            attributes,
            runtimeStatus,
            runtimeStatus == MemoryRuntimeStatus.NotRuntime ? reservation.Availability : MemoryAvailability.RuntimeOwned,
            hasNumaNode,
            numaNode,
            out descriptor);
    }

    private static bool TryCreateSafetyInterval(
        MemoryDescriptor[] descriptors,
        int sourceCount,
        int selectedIndex,
        ulong start,
        ulong end,
        out MemoryDescriptor descriptor)
    {
        MemoryDescriptor selected = descriptors[selectedIndex];
        MemoryCacheAttributes combinedAttributes = CombineAttributes(descriptors, sourceCount, start, end);
        MemoryRuntimeStatus runtimeStatus = CombineRuntimeStatus(descriptors, sourceCount, start, end);
        bool hasNumaNode = TryGetConsistentNumaNode(descriptors, sourceCount, start, end, out uint numaNode);
        return MemoryDescriptor.TryCreate(
            new PhysicalAddress(start),
            (end - start) / 4096UL,
            selected.MemoryType,
            combinedAttributes,
            runtimeStatus,
            runtimeStatus == MemoryRuntimeStatus.NotRuntime ? selected.Availability : MemoryAvailability.RuntimeOwned,
            hasNumaNode,
            numaNode,
            out descriptor);
    }

    private static bool TryCreateConservativeInterval(
        MemoryDescriptor[] descriptors,
        int sourceCount,
        ulong start,
        ulong end,
        out MemoryDescriptor descriptor)
    {
        MemoryCacheAttributes combinedAttributes = CombineAttributes(descriptors, sourceCount, start, end);
        MemoryRuntimeStatus runtimeStatus = CombineRuntimeStatus(descriptors, sourceCount, start, end);
        MemoryAvailability availability = runtimeStatus == MemoryRuntimeStatus.NotRuntime
            ? MemoryAvailability.PermanentlyReserved
            : MemoryAvailability.RuntimeOwned;
        return MemoryDescriptor.TryCreate(
            new PhysicalAddress(start),
            (end - start) / 4096UL,
            MemoryType.FirmwareReserved,
            combinedAttributes,
            runtimeStatus,
            availability,
            false,
            0,
            out descriptor);
    }


    private static MemoryCacheAttributes CombineAttributes(
        MemoryDescriptor[] descriptors,
        int sourceCount,
        ulong start,
        ulong end)
    {
        const MemoryCacheAttributes cacheMask =
            MemoryCacheAttributes.Uncacheable |
            MemoryCacheAttributes.WriteCombining |
            MemoryCacheAttributes.WriteThrough |
            MemoryCacheAttributes.WriteBack |
            MemoryCacheAttributes.UncachedExported;
        MemoryCacheAttributes nonCacheAttributes = MemoryCacheAttributes.None;
        bool uncacheable = false;
        bool uncachedExported = false;
        bool writeThrough = false;
        bool writeCombining = false;
        bool writeBack = false;
        for (int index = 0; index < sourceCount; index++)
        {
            MemoryDescriptor candidate = descriptors[index];
            if (!Covers(candidate, start, end)) continue;
            MemoryCacheAttributes attributes = candidate.CacheAttributes;
            nonCacheAttributes |= attributes & ~cacheMask;
            uncacheable |= (attributes & MemoryCacheAttributes.Uncacheable) != 0;
            uncachedExported |= (attributes & MemoryCacheAttributes.UncachedExported) != 0;
            writeThrough |= (attributes & MemoryCacheAttributes.WriteThrough) != 0;
            writeCombining |= (attributes & MemoryCacheAttributes.WriteCombining) != 0;
            writeBack |= (attributes & MemoryCacheAttributes.WriteBack) != 0;
        }
        MemoryCacheAttributes cacheAttribute = uncacheable ? MemoryCacheAttributes.Uncacheable
            : uncachedExported ? MemoryCacheAttributes.UncachedExported
            : writeThrough ? MemoryCacheAttributes.WriteThrough
            : writeCombining ? MemoryCacheAttributes.WriteCombining
            : writeBack ? MemoryCacheAttributes.WriteBack
            : MemoryCacheAttributes.None;
        return nonCacheAttributes | cacheAttribute;
    }

    private static MemoryCacheAttributes CombineReservationAttributes(
        MemoryCacheAttributes sourceAttributes,
        MemoryCacheAttributes reservationAttributes)
    {
        const MemoryCacheAttributes cacheMask =
            MemoryCacheAttributes.Uncacheable |
            MemoryCacheAttributes.WriteCombining |
            MemoryCacheAttributes.WriteThrough |
            MemoryCacheAttributes.WriteBack |
            MemoryCacheAttributes.UncachedExported;
        MemoryCacheAttributes combined = sourceAttributes | reservationAttributes;
        MemoryCacheAttributes nonCacheAttributes = combined & ~cacheMask;
        MemoryCacheAttributes cacheAttribute = (combined & MemoryCacheAttributes.Uncacheable) != 0 ? MemoryCacheAttributes.Uncacheable
            : (combined & MemoryCacheAttributes.UncachedExported) != 0 ? MemoryCacheAttributes.UncachedExported
            : (combined & MemoryCacheAttributes.WriteThrough) != 0 ? MemoryCacheAttributes.WriteThrough
            : (combined & MemoryCacheAttributes.WriteCombining) != 0 ? MemoryCacheAttributes.WriteCombining
            : (combined & MemoryCacheAttributes.WriteBack) != 0 ? MemoryCacheAttributes.WriteBack
            : MemoryCacheAttributes.None;
        return nonCacheAttributes | cacheAttribute;
    }

    private static bool TryGetConsistentNumaNode(
        MemoryDescriptor[] descriptors,
        int sourceCount,
        ulong start,
        ulong end,
        out uint numaNode)
    {
        numaNode = 0;
        bool found = false;
        for (int index = 0; index < sourceCount; index++)
        {
            MemoryDescriptor candidate = descriptors[index];
            if (!Covers(candidate, start, end)) continue;
            if (!candidate.HasNumaNode) return false;
            if (!found)
            {
                numaNode = candidate.NumaNode;
                found = true;
            }
            else if (candidate.NumaNode != numaNode)
            {
                numaNode = 0;
                return false;
            }
        }
        return found;
    }

    private static MemoryRuntimeStatus CombineRuntimeStatus(
        MemoryDescriptor[] descriptors,
        int sourceCount,
        ulong start,
        ulong end)
    {
        bool hasCode = false;
        bool hasData = false;
        for (int index = 0; index < sourceCount; index++)
        {
            MemoryDescriptor candidate = descriptors[index];
            if (!Covers(candidate, start, end)) continue;
            hasCode |= candidate.RuntimeStatus is MemoryRuntimeStatus.RuntimeCode or MemoryRuntimeStatus.RuntimeCodeAndData;
            hasData |= candidate.RuntimeStatus is MemoryRuntimeStatus.RuntimeData or MemoryRuntimeStatus.RuntimeCodeAndData;
        }
        if (hasCode && hasData) return MemoryRuntimeStatus.RuntimeCodeAndData;
        if (hasCode) return MemoryRuntimeStatus.RuntimeCode;
        if (hasData) return MemoryRuntimeStatus.RuntimeData;
        return MemoryRuntimeStatus.NotRuntime;
    }

    private static bool Covers(MemoryDescriptor descriptor, ulong start, ulong end)
        => descriptor.PhysicalStart.Value <= start && descriptor.EndExclusive >= end;

    private static bool SameMetadata(MemoryDescriptor left, MemoryDescriptor right)
        => left.MemoryType == right.MemoryType && left.CacheAttributes == right.CacheAttributes &&
           left.RuntimeStatus == right.RuntimeStatus && left.Availability == right.Availability &&
           left.HasNumaNode == right.HasNumaNode && (!left.HasNumaNode || left.NumaNode == right.NumaNode);

    private static bool IsSafer(MemoryDescriptor candidate, MemoryDescriptor selected)
    {
        int candidateType = GetPriority(candidate.MemoryType);
        int selectedType = GetPriority(selected.MemoryType);
        if (candidateType != selectedType) return candidateType > selectedType;
        int candidateAvailability = GetAvailabilityPriority(candidate.Availability);
        int selectedAvailability = GetAvailabilityPriority(selected.Availability);
        if (candidateAvailability != selectedAvailability) return candidateAvailability > selectedAvailability;
        if (candidate.RuntimeStatus != selected.RuntimeStatus) return (int)candidate.RuntimeStatus > (int)selected.RuntimeStatus;
        if (candidate.CacheAttributes != selected.CacheAttributes) return (ulong)candidate.CacheAttributes > (ulong)selected.CacheAttributes;
        if (candidate.HasNumaNode != selected.HasNumaNode) return !candidate.HasNumaNode;
        if (candidate.HasNumaNode && candidate.NumaNode != selected.NumaNode) return candidate.NumaNode < selected.NumaNode;
        return false;
    }

    private static int GetPriority(MemoryType type)
    {
        return type switch
        {
            MemoryType.BadMemory => 140,
            MemoryType.MemoryMappedIo => 130,
            MemoryType.Framebuffer => 125,
            MemoryType.RuntimeServices => 120,
            MemoryType.FirmwareReserved => 110,
            MemoryType.AcpiNvs => 105,
            MemoryType.PageTables => 100,
            MemoryType.BootStructures => 95,
            MemoryType.LoaderKernelImage => 90,
            MemoryType.EarlyAllocatorAllocations => 85,
            MemoryType.BootServices => 60,
            MemoryType.AcpiReclaimable => 50,
            MemoryType.PersistentMemory => 40,
            MemoryType.UsableConventional => 10,
            _ => 0
        };
    }

    private static int GetAvailabilityPriority(MemoryAvailability availability)
    {
        return availability switch
        {
            MemoryAvailability.Defective => 60,
            MemoryAvailability.RuntimeOwned => 50,
            MemoryAvailability.PermanentlyReserved => 40,
            MemoryAvailability.Unavailable => 30,
            MemoryAvailability.ReclaimableAfterAcpiInitialization => 20,
            MemoryAvailability.AvailableAfterExitBootServices => 10,
            _ => 0
        };
    }

    private static void SortAndDeduplicate(ulong[] values, ref int count)
    {
        for (int index = 1; index < count; index++)
        {
            ulong value = values[index];
            int insert = index - 1;
            while (insert >= 0 && values[insert] > value)
            {
                values[insert + 1] = values[insert];
                insert--;
            }
            values[insert + 1] = value;
        }
        if (count < 2) return;
        int write = 1;
        for (int read = 1; read < count; read++)
        {
            if (values[read] == values[write - 1]) continue;
            values[write++] = values[read];
        }
        count = write;
    }

    private static MemoryMapNormalisationResult Result(MemoryMapNormalisationStatus status, int sourceCount, int reservationCount)
        => new(status, sourceCount, reservationCount, 0, 0, 0, 0);
}
