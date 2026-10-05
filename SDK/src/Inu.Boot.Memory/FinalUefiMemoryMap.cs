using Inu.Memory;

namespace Inu.Boot.Memory;

/// <summary>Owns all storage needed before the final GetMemoryMap and ExitBootServices sequence.</summary>
/// <nova.when>Create with headroom before the final firmware call so no allocation is required between map capture and exit.</nova.when>
/// <nova.depends>IUefiMemoryMapProvider</nova.depends>
public sealed class UefiMemoryMapWorkspace
{
    private readonly UefiMemoryDescriptor[] _captureDescriptors;
    private readonly UefiMemoryDescriptor[] _snapshotDescriptors;
    private readonly FinalUefiMemoryMapSnapshot _snapshot;

    /// <summary>Creates a fixed-capacity final-map workspace.</summary>
    /// <nova.when>Create before the last firmware allocations and retain through ExitBootServices.</nova.when>
    /// <nova.depends>Caller-selected UEFI descriptor headroom</nova.depends>
    /// <returns>A workspace with separate capture and immutable snapshot storage.</returns>
    /// <example><code>UefiMemoryMapWorkspace workspace = new(256);</code></example>
    /// <param name="descriptorCapacity">Maximum retained UEFI descriptor count.</param>
    public UefiMemoryMapWorkspace(int descriptorCapacity)
    {
        if (descriptorCapacity < 1) throw new ArgumentOutOfRangeException(nameof(descriptorCapacity));
        _captureDescriptors = new UefiMemoryDescriptor[descriptorCapacity];
        _snapshotDescriptors = new UefiMemoryDescriptor[descriptorCapacity];
        _snapshot = new FinalUefiMemoryMapSnapshot(_snapshotDescriptors);
    }

    /// <summary>Gets the preallocated descriptor capacity.</summary>
    /// <nova.when>Use to verify final-map capture headroom before firmware exit.</nova.when>
    public int DescriptorCapacity => _captureDescriptors.Length;

    internal UefiMemoryDescriptor[] Descriptors => _captureDescriptors;
    internal FinalUefiMemoryMapSnapshot Snapshot => _snapshot;
}

/// <summary>Retains the exact typed UEFI map whose key succeeded in ExitBootServices.</summary>
/// <nova.when>Use as the sole firmware source for memory-map normalisation.</nova.when>
/// <nova.depends>FinalUefiMemoryMapAcquirer</nova.depends>
public sealed class FinalUefiMemoryMapSnapshot : IMemoryMapSource
{
    private readonly UefiMemoryDescriptor[] _storage;
    private int _count;

    internal FinalUefiMemoryMapSnapshot(UefiMemoryDescriptor[] storage) => _storage = storage;

    /// <summary>Gets whether ExitBootServices succeeded with this exact map key.</summary>
    /// <nova.when>Check before normalisation or diagnostic enumeration.</nova.when>
    public bool IsFinal { get; private set; }
    /// <summary>Gets the retained descriptor count.</summary>
    /// <nova.when>Use to bound immutable final-map diagnostics.</nova.when>
    public int Count => _count;
    /// <summary>Gets the map key accepted by ExitBootServices.</summary>
    /// <nova.when>Use to audit the exact final firmware hand-off.</nova.when>
    public ulong MapKey { get; private set; }
    /// <summary>Gets the firmware descriptor version.</summary>
    /// <nova.when>Use when diagnosing firmware map format compatibility.</nova.when>
    public uint DescriptorVersion { get; private set; }
    /// <summary>Gets the number of GetMemoryMap/ExitBootServices attempts.</summary>
    /// <nova.when>Use to report stale-key retries.</nova.when>
    public int CaptureAttempts { get; private set; }

    /// <summary>Attempts to translate one retained UEFI descriptor.</summary>
    /// <nova.when>Used by all normaliser implementations and diagnostic tools.</nova.when>
    /// <nova.depends>UefiMemoryDescriptorMapper</nova.depends>
    /// <returns><see langword="true"/> when the index and descriptor are valid.</returns>
    /// <example><code>bool found = snapshot.TryGetDescriptor(0, out MemoryDescriptor descriptor);</code></example>
    public bool TryGetDescriptor(int index, out MemoryDescriptor descriptor)
    {
        descriptor = default;
        if (!IsFinal || (uint)index >= (uint)_count) return false;
        return UefiMemoryDescriptorMapper.TryMap(_storage[index], out descriptor);
    }

    /// <summary>Attempts to read the unmodified typed UEFI descriptor.</summary>
    /// <nova.when>Use for firmware diagnostics that need original UEFI type and attribute values.</nova.when>
    /// <nova.depends>Count</nova.depends>
    /// <returns><see langword="true"/> when the index exists in the final snapshot.</returns>
    /// <example><code>bool found = snapshot.TryGetUefiDescriptor(0, out UefiMemoryDescriptor descriptor);</code></example>
    public bool TryGetUefiDescriptor(int index, out UefiMemoryDescriptor descriptor)
    {
        descriptor = default;
        if (!IsFinal || (uint)index >= (uint)_count) return false;
        descriptor = _storage[index];
        return true;
    }

    internal bool Seal(UefiMemoryDescriptor[] source, int count, ulong mapKey, uint descriptorVersion, int captureAttempts)
    {
        if (IsFinal || source is null || count < 1 || count > source.Length || count > _storage.Length) return false;
        Array.Copy(source, _storage, count);
        _count = count;
        MapKey = mapKey;
        DescriptorVersion = descriptorVersion;
        CaptureAttempts = captureAttempts;
        IsFinal = true;
        return true;
    }
}

/// <summary>Performs the required final-map retry sequence without allocating between firmware calls.</summary>
/// <nova.when>Call once after all boot-services allocations and immediately before managed post-UEFI boot.</nova.when>
/// <nova.depends>IUefiMemoryMapProvider and UefiMemoryMapWorkspace</nova.depends>
public static class FinalUefiMemoryMapAcquirer
{
    /// <summary>Captures the final UEFI map and exits boot services with its exact key.</summary>
    /// <nova.when>Use after the framebuffer, loaded image, and all Inu boot structures are final.</nova.when>
    /// <nova.depends>No allocation or firmware call between provider methods</nova.depends>
    /// <returns><see langword="true"/> when ExitBootServices accepted the retained map key.</returns>
    /// <example><code>bool exited = FinalUefiMemoryMapAcquirer.TryCaptureAndExit(provider, imageHandle, workspace, 8, out FinalUefiMemoryMapSnapshot? snapshot, out UefiMemoryMapStatus mapStatus, out UefiExitBootServicesStatus exitStatus);</code></example>
    public static bool TryCaptureAndExit(
        IUefiMemoryMapProvider provider,
        ulong imageHandle,
        UefiMemoryMapWorkspace workspace,
        int maximumAttempts,
        out FinalUefiMemoryMapSnapshot? snapshot,
        out UefiMemoryMapStatus mapStatus,
        out UefiExitBootServicesStatus exitStatus)
    {
        snapshot = null;
        mapStatus = UefiMemoryMapStatus.InvalidParameter;
        exitStatus = UefiExitBootServicesStatus.FirmwareError;
        if (provider is null || workspace is null || imageHandle == 0 || maximumAttempts < 1 || workspace.Snapshot.IsFinal) return false;

        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            mapStatus = provider.GetMemoryMap(workspace.Descriptors, out int count, out ulong mapKey, out uint descriptorVersion);
            if (mapStatus != UefiMemoryMapStatus.Success) return false;
            if (count < 1 || count > workspace.DescriptorCapacity)
            {
                mapStatus = UefiMemoryMapStatus.InvalidParameter;
                return false;
            }

            exitStatus = provider.ExitBootServices(imageHandle, mapKey);
            if (exitStatus == UefiExitBootServicesStatus.InvalidMapKey) continue;
            if (exitStatus != UefiExitBootServicesStatus.Success) return false;
            if (!workspace.Snapshot.Seal(workspace.Descriptors, count, mapKey, descriptorVersion, attempt)) return false;
            snapshot = workspace.Snapshot;
            return true;
        }
        return false;
    }
}
