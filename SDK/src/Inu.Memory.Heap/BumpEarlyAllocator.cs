using System;
using Inu.Core;

namespace Inu.Memory.Heap;

/// <summary>Implements a bounded monotonic early allocator over a caller-selected address range.</summary>
/// <nova.when>Use when bootstrap metadata needs aligned raw storage and individual releases are unnecessary.</nova.when>
/// <nova.depends>Inu.Memory.Heap.Contracts</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public sealed class BumpEarlyAllocator : IEarlyAllocator
{
    private readonly ulong _baseAddress;
    private readonly ulong _length;
    private ulong _offset;

    /// <summary>Creates a bump allocator over one half-open address range.</summary>
    /// <nova.when>Use after reserving a stable caller-owned range for bootstrap-lifetime allocations.</nova.when>
    /// <nova.depends>The supplied range must be non-empty and must not overflow the 64-bit address space.</nova.depends>
    /// <param name="baseAddress">First byte in the caller-owned range.</param>
    /// <param name="length">Range length in bytes.</param>
    /// <returns>A bump allocator positioned at the beginning of the supplied range.</returns>
    /// <example><code>BumpEarlyAllocator allocator = new(0x100000, 65536);</code></example>
    public BumpEarlyAllocator(ulong baseAddress, ulong length)
    {
        if (length == 0 || baseAddress > ulong.MaxValue - length) throw new ArgumentOutOfRangeException(nameof(length));
        _baseAddress = baseAddress;
        _length = length;
    }

    /// <summary>Attempts to allocate aligned bytes monotonically from the caller-owned range.</summary>
    /// <nova.when>Use for bootstrap-lifetime allocations that need no individual release.</nova.when>
    /// <nova.depends>A power-of-two alignment and remaining range capacity.</nova.depends>
    /// <returns><see langword="true"/> when the aligned allocation fits.</returns>
    /// <example><code>bool ok = allocator.TryAllocate(128, 16, out ulong address);</code></example>
    public bool TryAllocate(ulong byteCount, ulong alignment, out ulong address)
    {
        address = 0;
        if (byteCount == 0 || alignment == 0 || (alignment & (alignment - 1)) != 0) return false;
        ulong current = _baseAddress + _offset;
        if (current < _baseAddress) return false;
        ulong mask = alignment - 1;
        if (current > ulong.MaxValue - mask) return false;
        ulong aligned = (current + mask) & ~mask;
        if (aligned < _baseAddress) return false;
        ulong used = aligned - _baseAddress;
        if (used > _length || byteCount > _length - used) return false;
        _offset = used + byteCount;
        address = aligned;
        return true;
    }

    /// <summary>Gets the remaining unallocated byte count.</summary>
    /// <nova.when>Use to check remaining bootstrap capacity before requesting more storage.</nova.when>
    /// <nova.depends>The allocator's current monotonic offset.</nova.depends>
    /// <returns>The number of bytes not yet consumed by the bump allocator.</returns>
    /// <example><code>ulong remaining = allocator.GetRemainingBytes();</code></example>
    public ulong GetRemainingBytes() => _offset <= _length ? _length - _offset : 0UL;
}
