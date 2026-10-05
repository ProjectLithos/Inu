namespace Inu.Memory;

/// <summary>Provides immutable indexed and cursor-based diagnostics over a normalised memory map.</summary>
/// <nova.when>Use after normalisation and before constructing a physical allocator.</nova.when>
/// <nova.depends>MemoryDescriptor</nova.depends>
public sealed class NormalisedMemoryMap
{
    private readonly MemoryDescriptor[] _descriptors;

    internal NormalisedMemoryMap(MemoryDescriptor[] descriptors, int count, MemoryMapNormalisationMethod method)
    {
        _descriptors = new MemoryDescriptor[count];
        Array.Copy(descriptors, _descriptors, count);
        Method = method;
        ulong total = 0;
        ulong usable = 0;
        for (int index = 0; index < count; index++)
        {
            total += _descriptors[index].Length;
            if (_descriptors[index].Availability == MemoryAvailability.AvailableAfterExitBootServices) usable += _descriptors[index].Length;
        }
        TotalBytes = total;
        UsableBytes = usable;
    }

    /// <summary>Gets the number of immutable descriptors.</summary>
    /// <nova.when>Use to bound indexed diagnostics and allocator scans.</nova.when>
    public int Count => _descriptors.Length;
    /// <summary>Gets the normalisation implementation that produced the map.</summary>
    /// <nova.when>Use when reporting or auditing the selected overlap policy.</nova.when>
    public MemoryMapNormalisationMethod Method { get; }
    /// <summary>Gets the total represented byte count.</summary>
    /// <nova.when>Use for boot diagnostics and physical-memory accounting.</nova.when>
    public ulong TotalBytes { get; }
    /// <summary>Gets the total immediately allocatable byte count.</summary>
    /// <nova.when>Use to estimate memory available to the first physical allocator.</nova.when>
    public ulong UsableBytes { get; }

    /// <summary>Attempts to read one descriptor by index.</summary>
    /// <nova.when>Use for allocation scans and deterministic diagnostics.</nova.when>
    /// <nova.depends>Count</nova.depends>
    /// <returns><see langword="true"/> when the index exists.</returns>
    /// <example><code>bool found = map.TryGetDescriptor(0, out MemoryDescriptor descriptor);</code></example>
    public bool TryGetDescriptor(int index, out MemoryDescriptor descriptor)
    {
        descriptor = default;
        if ((uint)index >= (uint)_descriptors.Length) return false;
        descriptor = _descriptors[index];
        return true;
    }

    /// <summary>Creates a forward-only diagnostic cursor without exposing the backing array.</summary>
    /// <nova.when>Use when printing or auditing every descriptor.</nova.when>
    /// <nova.depends>MemoryMapDiagnosticCursor</nova.depends>
    /// <returns>A cursor positioned before the first descriptor.</returns>
    /// <example><code>MemoryMapDiagnosticCursor cursor = map.CreateDiagnosticCursor();</code></example>
    public MemoryMapDiagnosticCursor CreateDiagnosticCursor() => new(_descriptors);
}

/// <summary>Provides immutable forward enumeration without implementing mutable collection interfaces.</summary>
/// <nova.when>Use for boot diagnostics where exposing an array would permit mutation.</nova.when>
/// <nova.depends>NormalisedMemoryMap</nova.depends>
public struct MemoryMapDiagnosticCursor
{
    private readonly MemoryDescriptor[] _descriptors;
    private int _index;

    internal MemoryMapDiagnosticCursor(MemoryDescriptor[] descriptors)
    {
        _descriptors = descriptors;
        _index = -1;
        Current = default;
    }

    /// <summary>Gets the current descriptor after a successful move.</summary>
    /// <nova.when>Read only after MoveNext returns true.</nova.when>
    public MemoryDescriptor Current { get; private set; }

    /// <summary>Advances to the next diagnostic descriptor.</summary>
    /// <nova.when>Call until it returns <see langword="false"/>.</nova.when>
    /// <nova.depends>CreateDiagnosticCursor</nova.depends>
    /// <returns><see langword="true"/> when another descriptor is available.</returns>
    /// <example><code>while (cursor.MoveNext()) { MemoryDescriptor descriptor = cursor.Current; }</code></example>
    public bool MoveNext()
    {
        int next = _index + 1;
        if ((uint)next >= (uint)_descriptors.Length) return false;
        _index = next;
        Current = _descriptors[next];
        return true;
    }
}

/// <summary>Reports normalisation status and diagnostic counters.</summary>
/// <nova.when>Use to identify rejected inputs and policy-specific overlap handling.</nova.when>
/// <nova.depends>MemoryMapNormalisationStatus</nova.depends>
public readonly record struct MemoryMapNormalisationResult(
    MemoryMapNormalisationStatus Status,
    int InputDescriptorCount,
    int ReservationCount,
    int OutputDescriptorCount,
    int SplitCount,
    int MergeCount,
    int ResolvedOverlapCount)
{
    /// <summary>Gets whether normalisation completed successfully.</summary>
    /// <nova.when>Use before consuming the output map.</nova.when>
    /// <nova.depends>Status</nova.depends>
    /// <returns><see langword="true"/> only for a successful result.</returns>
    /// <example><code>bool ready = result.Succeeded();</code></example>
    public bool Succeeded() => Status == MemoryMapNormalisationStatus.Success;
}

/// <summary>Defines a replaceable memory-map normalisation implementation.</summary>
/// <nova.when>Use when an SDK consumer selects strict, priority, or conservative overlap semantics.</nova.when>
/// <nova.depends>FinalUefiMemoryMapSnapshot supplied by Inu.Boot.Memory</nova.depends>
public interface IMemoryMapNormaliser
{
    /// <summary>Gets the overlap-resolution implementation.</summary>
    MemoryMapNormalisationMethod Method { get; }

    /// <summary>Normalises a final boot map and overlays explicit reservations.</summary>
    /// <nova.when>Call only with the map captured immediately before successful ExitBootServices.</nova.when>
    /// <nova.depends>MemoryMapNormalisationWorkspace</nova.depends>
    /// <returns>A status and diagnostic counter set.</returns>
    /// <example><code>MemoryMapNormalisationResult result = normaliser.Normalise(source, reservations, workspace, out NormalisedMemoryMap? map);</code></example>
    MemoryMapNormalisationResult Normalise(IMemoryMapSource source, MemoryReservation[] reservations, MemoryMapNormalisationWorkspace workspace, out NormalisedMemoryMap? map);
}

/// <summary>Exposes an immutable final boot map to normaliser implementations.</summary>
/// <nova.when>Implemented by final UEFI snapshots and future boot-protocol adapters.</nova.when>
/// <nova.depends>MemoryDescriptor</nova.depends>
public interface IMemoryMapSource
{
    /// <summary>Gets whether this source represents the final post-ExitBootServices map.</summary>
    bool IsFinal { get; }
    /// <summary>Gets the source descriptor count.</summary>
    int Count { get; }
    /// <summary>Attempts to translate one source descriptor.</summary>
    /// <nova.when>Used by a normaliser to read source ranges without exposing mutable storage.</nova.when>
    /// <nova.depends>Count</nova.depends>
    /// <returns><see langword="true"/> when the descriptor is valid and translatable.</returns>
    /// <example><code>bool read = source.TryGetDescriptor(0, out MemoryDescriptor descriptor);</code></example>
    bool TryGetDescriptor(int index, out MemoryDescriptor descriptor);
}

/// <summary>Provides caller-owned scratch storage so boot normalisation has bounded memory use.</summary>
/// <nova.when>Allocate before obtaining the final firmware map, then reuse for all implementations.</nova.when>
/// <nova.depends>Maximum expected descriptor and reservation counts</nova.depends>
public sealed class MemoryMapNormalisationWorkspace
{
    internal readonly MemoryDescriptor[] Inputs;
    internal readonly ulong[] Boundaries;
    internal readonly MemoryDescriptor[] Outputs;

    /// <summary>Creates bounded scratch storage.</summary>
    /// <nova.when>Create before final-map capture so normalisation requires no unbounded allocation.</nova.when>
    /// <nova.depends>Validated capacity limits</nova.depends>
    /// <returns>A reusable bounded normalisation workspace.</returns>
    /// <example><code>MemoryMapNormalisationWorkspace workspace = new(256, 512);</code></example>
    /// <param name="descriptorCapacity">Maximum source plus reservation descriptors.</param>
    /// <param name="outputCapacity">Maximum split output descriptors.</param>
    public MemoryMapNormalisationWorkspace(int descriptorCapacity, int outputCapacity)
    {
        if (descriptorCapacity < 1) throw new ArgumentOutOfRangeException(nameof(descriptorCapacity));
        if (outputCapacity < 1) throw new ArgumentOutOfRangeException(nameof(outputCapacity));
        Inputs = new MemoryDescriptor[descriptorCapacity];
        Boundaries = new ulong[checked(descriptorCapacity * 2)];
        Outputs = new MemoryDescriptor[outputCapacity];
    }

    /// <summary>Gets the maximum source plus reservation count.</summary>
    /// <nova.when>Use to verify workspace headroom before normalisation.</nova.when>
    public int DescriptorCapacity => Inputs.Length;
    /// <summary>Gets the maximum normalised output count.</summary>
    /// <nova.when>Use to verify split-range headroom before normalisation.</nova.when>
    public int OutputCapacity => Outputs.Length;
}
