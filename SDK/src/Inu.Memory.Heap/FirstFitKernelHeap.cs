using System;
using Inu.Core;

namespace Inu.Memory.Heap;

/// <summary>Provides a caller-backed first-fit heap over an existing raw address range.</summary>
/// <nova.when>Use when a kernel wants a replaceable first-fit heap methodology over storage it already owns.</nova.when>
/// <nova.depends>Inu.Memory.Heap.Contracts and a valid writable caller-owned address range.</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public sealed unsafe class FirstFitKernelHeap : IKernelHeap
{
    private readonly ulong[] _starts;
    private readonly ulong[] _lengths;
    private readonly ulong[] _tokens;
    private readonly byte[] _states;
    private readonly ulong _length;
    private ulong _nextToken = 1;
    private ulong _allocated;
    private ulong _peak;
    private int _live;

    /// <summary>Creates a first-fit allocator using caller-selected metadata capacity.</summary>
    /// <nova.when>Use after reserving a writable raw range that this allocator may subdivide and zero-fill.</nova.when>
    /// <nova.depends>The range must be non-empty, non-overflowing, writable, and remain valid for the allocator lifetime.</nova.depends>
    /// <param name="baseAddress">First writable byte in the caller-owned range.</param>
    /// <param name="length">Range length in bytes.</param>
    /// <param name="maximumBlocks">Maximum number of allocation/free-list metadata records.</param>
    /// <returns>A first-fit heap over the supplied range.</returns>
    /// <example><code>FirstFitKernelHeap heap = new(baseAddress, 65536, 128);</code></example>
    public FirstFitKernelHeap(ulong baseAddress, ulong length, int maximumBlocks)
    {
        if (length == 0 || maximumBlocks < 4) throw new ArgumentOutOfRangeException(nameof(maximumBlocks));
        if (baseAddress > ulong.MaxValue - length) throw new ArgumentOutOfRangeException(nameof(length));
        _length = length;
        _starts = new ulong[maximumBlocks];
        _lengths = new ulong[maximumBlocks];
        _tokens = new ulong[maximumBlocks];
        _states = new byte[maximumBlocks];
        _starts[0] = baseAddress;
        _lengths[0] = length;
        _states[0] = 1;
    }

    /// <summary>Gets the first-fit methodology identifier.</summary>
    /// <nova.when>Use to identify this heap as the first-fit methodology.</nova.when>
    /// <nova.depends>This concrete allocator type.</nova.depends>
    public KernelHeapMethodology Methodology => KernelHeapMethodology.FirstFit;

    /// <summary>Attempts to allocate one aligned raw byte range.</summary>
    /// <nova.when>Use to allocate aligned raw storage and optionally zero-fill the returned bytes.</nova.when>
    /// <nova.depends>A valid request, writable backing range, and available metadata capacity.</nova.depends>
    /// <returns><see langword="true"/> when a live allocation is created.</returns>
    /// <example><code>bool ok = heap.TryAllocate(KernelHeapAllocationRequest.Default(256), out KernelHeapAllocation allocation);</code></example>
    public bool TryAllocate(KernelHeapAllocationRequest request, out KernelHeapAllocation allocation)
    {
        allocation = default;
        if (request.ByteCount == 0 || request.Alignment == 0 || (request.Alignment & (request.Alignment - 1)) != 0) return false;
        for (int i = 0; i < _states.Length; i++)
        {
            if (_states[i] != 1) continue;
            ulong start = _starts[i];
            ulong length = _lengths[i];
            ulong mask = request.Alignment - 1;
            if (start > ulong.MaxValue - mask) continue;
            ulong aligned = (start + mask) & ~mask;
            ulong prefix = aligned - start;
            if (prefix > length || request.ByteCount > length - prefix) continue;
            ulong suffix = length - prefix - request.ByteCount;
            int firstSlot = prefix > 0 ? FindUnused(i, -1) : -1;
            int secondSlot = suffix > 0 ? FindUnused(i, firstSlot) : -1;
            if ((prefix > 0 && firstSlot < 0) || (suffix > 0 && secondSlot < 0)) return false;

            ulong token = NextToken();
            _states[i] = 2;
            _starts[i] = aligned;
            _lengths[i] = request.ByteCount;
            _tokens[i] = token;
            if (prefix > 0)
            {
                _states[firstSlot] = 1;
                _starts[firstSlot] = start;
                _lengths[firstSlot] = prefix;
            }
            if (suffix > 0)
            {
                _states[secondSlot] = 1;
                _starts[secondSlot] = aligned + request.ByteCount;
                _lengths[secondSlot] = suffix;
            }
            _allocated += request.ByteCount;
            if (_allocated > _peak) _peak = _allocated;
            _live++;
            if (request.ZeroFill)
            {
                byte* pointer = (byte*)(nuint)aligned;
                for (ulong n = 0; n < request.ByteCount; n++) pointer[n] = 0;
            }
            allocation = new KernelHeapAllocation(token, aligned, request.ByteCount);
            return true;
        }
        return false;
    }

    /// <summary>Attempts to release one exact live allocation.</summary>
    /// <nova.when>Use when an exact allocation returned by this heap is no longer needed.</nova.when>
    /// <nova.depends>A live token/address/length tuple created by this heap.</nova.depends>
    /// <returns><see langword="true"/> when the allocation was live and is now reusable.</returns>
    /// <example><code>bool released = heap.TryRelease(allocation);</code></example>
    public bool TryRelease(KernelHeapAllocation allocation)
    {
        for (int i = 0; i < _states.Length; i++)
        {
            if (_states[i] != 2 || _tokens[i] != allocation.Token || _starts[i] != allocation.Address || _lengths[i] != allocation.ByteCount) continue;
            _states[i] = 1;
            _tokens[i] = 0;
            _allocated -= _lengths[i];
            _live--;
            Coalesce();
            return true;
        }
        return false;
    }

    /// <summary>Gets current first-fit allocation accounting.</summary>
    /// <nova.when>Use to inspect allocation pressure and free-list fragmentation.</nova.when>
    /// <nova.depends>The allocator's current block metadata.</nova.depends>
    /// <returns>An immutable statistics snapshot.</returns>
    /// <example><code>KernelHeapStatistics statistics = heap.GetStatistics();</code></example>
    public KernelHeapStatistics GetStatistics()
    {
        ulong free = 0;
        int blocks = 0;
        for (int i = 0; i < _states.Length; i++)
        {
            if (_states[i] != 1) continue;
            free += _lengths[i];
            blocks++;
        }
        return new KernelHeapStatistics(_length, _length, _allocated, free, _peak, _live, blocks);
    }

    private int FindUnused(int excluded, int excluded2)
    {
        for (int i = 0; i < _states.Length; i++) if (i != excluded && i != excluded2 && _states[i] == 0) return i;
        return -1;
    }

    private ulong NextToken()
    {
        ulong token = _nextToken++;
        if (token == 0) token = _nextToken++;
        return token;
    }

    private void Coalesce()
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < _states.Length && !changed; i++)
            {
                if (_states[i] != 1) continue;
                for (int j = 0; j < _states.Length; j++)
                {
                    if (i == j || _states[j] != 1) continue;
                    if (_starts[i] > ulong.MaxValue - _lengths[i] || _starts[i] + _lengths[i] != _starts[j]) continue;
                    _lengths[i] += _lengths[j];
                    _states[j] = 0;
                    _starts[j] = 0;
                    _lengths[j] = 0;
                    changed = true;
                    break;
                }
            }
        }
    }
}
