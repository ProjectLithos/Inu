using Inu.Core;

namespace Inu.Memory.Heap;

/// <summary>Identifies a heap allocation policy supplied by Inu or an SDK consumer.</summary>
/// <nova.when>Use when selecting or reporting the raw-storage methodology behind a kernel heap.</nova.when>
/// <nova.depends>Inu.Core</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public enum KernelHeapMethodology
{
    /// <summary>Uses monotonic bump allocation; individual releases are not supported.</summary>
    Bump = 0,
    /// <summary>Uses address-ordered first-fit allocation with adjacent-block coalescing.</summary>
    FirstFit = 1,
    /// <summary>Uses an SDK-consumer implementation of the heap contract.</summary>
    Custom = 2
}

/// <summary>Describes one allocation request independently of a particular heap implementation.</summary>
/// <nova.when>Use when requesting raw kernel storage from an <see cref="IKernelHeap"/>.</nova.when>
/// <nova.depends>IKernelHeap</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public readonly struct KernelHeapAllocationRequest
{
    /// <summary>Creates a heap request.</summary>
    /// <nova.when>Use when the caller needs explicit byte alignment or zero-fill semantics.</nova.when>
    /// <nova.depends>IKernelHeap.TryAllocate</nova.depends>
    /// <param name="byteCount">Number of usable bytes required.</param>
    /// <param name="alignment">Power-of-two byte alignment.</param>
    /// <param name="zeroFill">Whether the returned bytes must be cleared before use.</param>
    /// <returns>A new immutable allocation request.</returns>
    /// <example><code>KernelHeapAllocationRequest request = new(4096, 64, true);</code></example>
    public KernelHeapAllocationRequest(ulong byteCount, ulong alignment, bool zeroFill)
    { ByteCount = byteCount; Alignment = alignment; ZeroFill = zeroFill; }

    /// <summary>Gets the requested usable byte count.</summary>
    /// <nova.when>Use when validating or executing an allocation request.</nova.when>
    /// <nova.depends>The value supplied to the constructor.</nova.depends>
    public ulong ByteCount { get; }

    /// <summary>Gets the requested power-of-two byte alignment.</summary>
    /// <nova.when>Use when positioning the returned raw storage.</nova.when>
    /// <nova.depends>The value supplied to the constructor.</nova.depends>
    public ulong Alignment { get; }

    /// <summary>Gets whether the implementation must clear returned storage.</summary>
    /// <nova.when>Use when deciding whether allocation completion requires zero filling.</nova.when>
    /// <nova.depends>The value supplied to the constructor.</nova.depends>
    public bool ZeroFill { get; }

    /// <summary>Creates a conventional 16-byte-aligned zero-filled request.</summary>
    /// <nova.when>Use for general kernel data that does not require a stronger alignment.</nova.when>
    /// <nova.depends>IKernelHeap.TryAllocate</nova.depends>
    /// <param name="byteCount">Number of usable bytes required.</param>
    /// <returns>A 16-byte-aligned zero-filled request.</returns>
    /// <example><code>KernelHeapAllocationRequest request = KernelHeapAllocationRequest.Default(128);</code></example>
    public static KernelHeapAllocationRequest Default(ulong byteCount) => new(byteCount, 16UL, true);
}

/// <summary>Identifies one live heap allocation without exposing allocator metadata.</summary>
/// <nova.when>Retain this value until the corresponding raw storage is released.</nova.when>
/// <nova.depends>IKernelHeap.TryAllocate, IKernelHeap.TryRelease</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public readonly struct KernelHeapAllocation
{
    /// <summary>Creates a heap allocation value.</summary>
    /// <nova.when>Use from heap implementations when returning a newly created live allocation.</nova.when>
    /// <nova.depends>IKernelHeap implementations</nova.depends>
    /// <param name="token">Opaque implementation-defined token.</param>
    /// <param name="address">First usable virtual byte.</param>
    /// <param name="byteCount">Usable allocation length.</param>
    /// <returns>A new immutable allocation descriptor.</returns>
    /// <example><code>KernelHeapAllocation allocation = new(1, 0x100000, 256);</code></example>
    public KernelHeapAllocation(ulong token, ulong address, ulong byteCount)
    { Token = token; Address = address; ByteCount = byteCount; }

    /// <summary>Gets the opaque allocation token.</summary>
    /// <nova.when>Use together with the address and length when validating an exact release.</nova.when>
    /// <nova.depends>The heap implementation that created the allocation.</nova.depends>
    public ulong Token { get; }

    /// <summary>Gets the first usable virtual byte.</summary>
    /// <nova.when>Use as the raw-storage address owned by this allocation.</nova.when>
    /// <nova.depends>The heap implementation that created the allocation.</nova.depends>
    public ulong Address { get; }

    /// <summary>Gets the usable allocation length.</summary>
    /// <nova.when>Use when bounding access to the allocation or validating an exact release.</nova.when>
    /// <nova.depends>The heap implementation that created the allocation.</nova.depends>
    public ulong ByteCount { get; }
}

/// <summary>Provides implementation-independent heap accounting.</summary>
/// <nova.when>Use for diagnostics, capacity planning, and allocator instrumentation.</nova.when>
/// <nova.depends>IKernelHeap.GetStatistics</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public readonly struct KernelHeapStatistics
{
    /// <summary>Creates one statistics snapshot.</summary>
    /// <nova.when>Use from heap implementations when reporting their current accounting.</nova.when>
    /// <nova.depends>IKernelHeap.GetStatistics</nova.depends>
    /// <returns>A new immutable statistics snapshot.</returns>
    /// <example><code>KernelHeapStatistics stats = new(65536, 65536, 4096, 61440, 8192, 1, 2);</code></example>
    public KernelHeapStatistics(ulong reservedBytes, ulong committedBytes, ulong allocatedBytes, ulong freeBytes, ulong peakAllocatedBytes, int liveAllocations, int freeBlocks)
    { ReservedBytes=reservedBytes; CommittedBytes=committedBytes; AllocatedBytes=allocatedBytes; FreeBytes=freeBytes; PeakAllocatedBytes=peakAllocatedBytes; LiveAllocations=liveAllocations; FreeBlocks=freeBlocks; }

    /// <summary>Gets the virtual reservation capacity.</summary>
    /// <nova.when>Use when comparing committed heap backing with the total address reservation.</nova.when>
    /// <nova.depends>The reporting heap implementation.</nova.depends>
    public ulong ReservedBytes { get; }

    /// <summary>Gets bytes currently backed by physical memory.</summary>
    /// <nova.when>Use when measuring current committed heap capacity.</nova.when>
    /// <nova.depends>The reporting heap implementation.</nova.depends>
    public ulong CommittedBytes { get; }

    /// <summary>Gets usable bytes currently owned by live allocations.</summary>
    /// <nova.when>Use when measuring current heap consumption.</nova.when>
    /// <nova.depends>The reporting heap implementation.</nova.depends>
    public ulong AllocatedBytes { get; }

    /// <summary>Gets committed bytes currently available for reuse.</summary>
    /// <nova.when>Use when deciding whether heap growth is likely to be required.</nova.when>
    /// <nova.depends>The reporting heap implementation.</nova.depends>
    public ulong FreeBytes { get; }

    /// <summary>Gets the maximum simultaneously allocated byte count observed.</summary>
    /// <nova.when>Use for high-water-mark diagnostics.</nova.when>
    /// <nova.depends>The reporting heap implementation.</nova.depends>
    public ulong PeakAllocatedBytes { get; }

    /// <summary>Gets the live allocation count.</summary>
    /// <nova.when>Use for allocation-lifetime diagnostics.</nova.when>
    /// <nova.depends>The reporting heap implementation.</nova.depends>
    public int LiveAllocations { get; }

    /// <summary>Gets the current free-block count.</summary>
    /// <nova.when>Use as a simple fragmentation indicator.</nova.when>
    /// <nova.depends>The reporting heap implementation.</nova.depends>
    public int FreeBlocks { get; }
}

/// <summary>Defines the common raw-storage contract for kernel heap methodologies.</summary>
/// <nova.when>Implement to provide a custom heap while retaining Inu-compatible allocation semantics.</nova.when>
/// <nova.depends>KernelHeapAllocationRequest, KernelHeapAllocation, KernelHeapStatistics</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public interface IKernelHeap
{
    /// <summary>Gets the selected methodology.</summary>
    /// <nova.when>Use for diagnostics or methodology-specific policy decisions.</nova.when>
    /// <nova.depends>The concrete heap implementation.</nova.depends>
    KernelHeapMethodology Methodology { get; }

    /// <summary>Attempts to allocate one raw byte range.</summary>
    /// <nova.when>Use whenever a kernel subsystem needs heap-owned raw storage.</nova.when>
    /// <nova.depends>A valid request and sufficient implementation capacity.</nova.depends>
    /// <returns><see langword="true"/> when a live allocation was created.</returns>
    /// <example><code>bool ok = heap.TryAllocate(KernelHeapAllocationRequest.Default(128), out KernelHeapAllocation allocation);</code></example>
    bool TryAllocate(KernelHeapAllocationRequest request, out KernelHeapAllocation allocation);

    /// <summary>Attempts to release one exact live allocation.</summary>
    /// <nova.when>Use when storage returned by this heap is no longer required.</nova.when>
    /// <nova.depends>An allocation value previously returned by this heap.</nova.depends>
    /// <returns><see langword="true"/> when the token and range identify a live allocation.</returns>
    /// <example><code>bool released = heap.TryRelease(allocation);</code></example>
    bool TryRelease(KernelHeapAllocation allocation);

    /// <summary>Gets current allocation accounting.</summary>
    /// <nova.when>Use when reporting heap capacity, consumption, or fragmentation.</nova.when>
    /// <nova.depends>The concrete heap implementation.</nova.depends>
    /// <returns>An immutable heap statistics snapshot.</returns>
    /// <example><code>KernelHeapStatistics statistics = heap.GetStatistics();</code></example>
    KernelHeapStatistics GetStatistics();
}

/// <summary>Defines monotonic storage used before the full kernel heap is available.</summary>
/// <nova.when>Implement when bootstrap code requires bounded raw storage before the normal kernel heap is online.</nova.when>
/// <nova.depends>A caller-owned storage range with bootstrap lifetime.</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public interface IEarlyAllocator
{
    /// <summary>Attempts to allocate aligned bytes without requiring a corresponding release.</summary>
    /// <nova.when>Use only for bootstrap-lifetime data that does not require individual deallocation.</nova.when>
    /// <nova.depends>A non-zero byte count, power-of-two alignment, and remaining arena capacity.</nova.depends>
    /// <returns><see langword="true"/> when the early arena has sufficient space.</returns>
    /// <example><code>bool ok = allocator.TryAllocate(256, 16, out ulong address);</code></example>
    bool TryAllocate(ulong byteCount, ulong alignment, out ulong address);

    /// <summary>Gets the remaining unallocated byte count.</summary>
    /// <nova.when>Use to determine whether bootstrap metadata can still fit in the early arena.</nova.when>
    /// <nova.depends>The allocator's current monotonic offset.</nova.depends>
    /// <returns>Available bytes in the bounded early arena.</returns>
    /// <example><code>ulong remaining = allocator.GetRemainingBytes();</code></example>
    ulong GetRemainingBytes();
}
