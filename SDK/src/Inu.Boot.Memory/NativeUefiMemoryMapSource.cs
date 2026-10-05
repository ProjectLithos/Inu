using Inu.Boot.Contracts;
using Inu.Memory;
using Inu.Primitives;

namespace Inu.Boot.Memory;

/// <summary>Reads the retained native UEFI descriptor buffer exposed by a final boot context.</summary>
/// <nova.when>Use during identity-mapped early boot, or pass an explicitly mapped virtual address after paging changes.</nova.when>
/// <nova.depends>IUefiFinalMemoryMap final-map capability and a readable mapping of the retained descriptor buffer</nova.depends>
public sealed unsafe class NativeUefiMemoryMapSource : IMemoryMapSource
{
    private readonly ulong _mappedAddress;
    private readonly ulong _descriptorSize;

    private NativeUefiMemoryMapSource(
        ulong mappedAddress,
        int count,
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion)
    {
        _mappedAddress = mappedAddress;
        Count = count;
        MapKey = mapKey;
        _descriptorSize = descriptorSize;
        DescriptorVersion = descriptorVersion;
    }

    /// <summary>Gets whether this source was created from a final accepted UEFI map.</summary>
    /// <nova.when>Check before supplying the source to a normaliser.</nova.when>
    public bool IsFinal => true;
    /// <summary>Gets the retained descriptor count.</summary>
    /// <nova.when>Use to bound immutable firmware diagnostics.</nova.when>
    public int Count { get; }
    /// <summary>Gets the map key accepted by ExitBootServices.</summary>
    /// <nova.when>Use when diagnosing the final firmware hand-off.</nova.when>
    public ulong MapKey { get; }
    /// <summary>Gets the firmware descriptor size in bytes.</summary>
    /// <nova.when>Use when validating or displaying native UEFI layout metadata.</nova.when>
    public ulong DescriptorSize => _descriptorSize;
    /// <summary>Gets the firmware descriptor version.</summary>
    /// <nova.when>Use when interpreting future firmware descriptor extensions.</nova.when>
    public uint DescriptorVersion { get; }

    /// <summary>Creates a source while the retained physical buffer remains identity mapped.</summary>
    /// <nova.when>Use before replacing the firmware page tables.</nova.when>
    /// <nova.depends>IUefiFinalMemoryMap.MemoryMapAddress must be directly readable</nova.depends>
    /// <returns><see langword="true"/> when all final-map metadata is valid.</returns>
    /// <example><code>bool ready = NativeUefiMemoryMapSource.TryCreate(boot, out NativeUefiMemoryMapSource? source);</code></example>
    public static bool TryCreate(IUefiFinalMemoryMap boot, out NativeUefiMemoryMapSource? source)
        => TryCreate(boot, boot.MemoryMapAddress.Value, out source);

    /// <summary>Creates a source using the virtual address at which the retained physical buffer is mapped.</summary>
    /// <nova.when>Use after installing page tables that map the final UEFI buffer at a non-identity address.</nova.when>
    /// <nova.depends>The complete buffer must be readable at mappedAddress</nova.depends>
    /// <returns><see langword="true"/> when all final-map metadata and address arithmetic are valid.</returns>
    /// <example><code>bool ready = NativeUefiMemoryMapSource.TryCreate(boot, mappedAddress, out NativeUefiMemoryMapSource? source);</code></example>
    public static bool TryCreate(IUefiFinalMemoryMap boot, ulong mappedAddress, out NativeUefiMemoryMapSource? source)
    {
        source = null;
        if (mappedAddress == 0 || boot.MemoryMapLength == 0 || boot.MemoryDescriptorSize < 40) return false;
        if ((boot.MemoryDescriptorSize & 7UL) != 0 || boot.MemoryMapLength % boot.MemoryDescriptorSize != 0) return false;
        if (mappedAddress > ulong.MaxValue - boot.MemoryMapLength) return false;
        ulong count = boot.MemoryMapLength / boot.MemoryDescriptorSize;
        if (count == 0 || count > int.MaxValue) return false;
        source = new NativeUefiMemoryMapSource(mappedAddress, (int)count, boot.MemoryMapKey, boot.MemoryDescriptorSize, boot.MemoryDescriptorVersion);
        return true;
    }

    /// <summary>Attempts to translate one descriptor from the retained native buffer.</summary>
    /// <nova.when>Used by every memory-map normaliser implementation.</nova.when>
    /// <nova.depends>The buffer mapping must remain valid and immutable</nova.depends>
    /// <returns><see langword="true"/> when the index and descriptor are valid.</returns>
    /// <example><code>bool found = source.TryGetDescriptor(0, out MemoryDescriptor descriptor);</code></example>
    public bool TryGetDescriptor(int index, out MemoryDescriptor descriptor)
    {
        descriptor = default;
        if (!TryGetUefiDescriptor(index, out UefiMemoryDescriptor source)) return false;
        return UefiMemoryDescriptorMapper.TryMap(source, out descriptor);
    }

    /// <summary>Attempts to read one original UEFI descriptor from the retained native buffer.</summary>
    /// <nova.when>Use for immutable firmware diagnostics before reclaiming or unmapping the buffer.</nova.when>
    /// <nova.depends>UEFI descriptor layout version one fields at offsets 0 through 39</nova.depends>
    /// <returns><see langword="true"/> when the index exists and its address is representable.</returns>
    /// <example><code>bool found = source.TryGetUefiDescriptor(0, out UefiMemoryDescriptor descriptor);</code></example>
    public bool TryGetUefiDescriptor(int index, out UefiMemoryDescriptor descriptor)
    {
        descriptor = default;
        if ((uint)index >= (uint)Count) return false;
        ulong offset = (ulong)(uint)index * _descriptorSize;
        if (_mappedAddress > ulong.MaxValue - offset) return false;
        byte* address = (byte*)(nuint)(_mappedAddress + offset);
        UefiMemoryType type = (UefiMemoryType)(*(uint*)(address + 0));
        ulong physicalStart = *(ulong*)(address + 8);
        ulong virtualStart = *(ulong*)(address + 16);
        ulong pageCount = *(ulong*)(address + 24);
        UefiMemoryAttributes attributes = (UefiMemoryAttributes)(*(ulong*)(address + 32));
        descriptor = new UefiMemoryDescriptor(type, new PhysicalAddress(physicalStart), virtualStart, pageCount, attributes);
        return true;
    }
}
