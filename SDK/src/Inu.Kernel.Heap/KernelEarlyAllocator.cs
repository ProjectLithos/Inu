using System;

namespace Inu.Kernel.Heap;

/// <summary>Reports the state of the bounded no-heap bootstrap allocator.</summary>
public enum KernelEarlyAllocatorStatus
{
    /// <summary>The operation completed successfully.</summary>
    Success = 0,
    /// <summary>The early allocator has not been initialized.</summary>
    NotInitialized = 1,
    /// <summary>The early allocator was already initialized.</summary>
    AlreadyInitialized = 2,
    /// <summary>The request has an invalid size or alignment.</summary>
    InvalidParameter = 3,
    /// <summary>The fixed early arena cannot satisfy the request.</summary>
    OutOfMemory = 4
}

/// <summary>Provides a fixed 64 KiB monotonic arena for metadata required before the page-backed heap is online.</summary>
public static unsafe class KernelEarlyAllocator
{
    private const UInt64 Capacity = 65536UL;

    private unsafe struct State
    {
        internal fixed Byte Buffer[65536];
    }

#pragma warning disable CS0169 // Fixed-buffer member access is not counted as use of the containing freestanding state field.
    private static State _state;
#pragma warning restore CS0169
    private static UInt64 _offset;
    private static Boolean _initialized;
    private static KernelEarlyAllocatorStatus _status;

    /// <summary>Gets whether the early arena is ready.</summary>
    public static Boolean IsInitialized() => _initialized;

    /// <summary>Gets the most recent early-allocation status.</summary>
    public static KernelEarlyAllocatorStatus GetLastStatus() => _status;

    /// <summary>Initializes the fixed early arena.</summary>
    /// <returns><see langword="true"/> on first initialization.</returns>
    public static Boolean Initialize()
    {
        if (_initialized)
        {
            _status = KernelEarlyAllocatorStatus.AlreadyInitialized;
            return false;
        }
        _offset = 0UL;
        _initialized = true;
        _status = KernelEarlyAllocatorStatus.Success;
        return true;
    }

    /// <summary>Allocates aligned bytes monotonically from fixed kernel storage.</summary>
    /// <returns><see langword="true"/> when capacity remains.</returns>
    public static Boolean TryAllocate(UInt64 byteCount, UInt64 alignment, out UInt64 address)
    {
        address = 0UL;
        if (!_initialized)
        {
            _status = KernelEarlyAllocatorStatus.NotInitialized;
            return false;
        }
        if (byteCount == 0UL || alignment == 0UL || (alignment & (alignment - 1UL)) != 0UL)
        {
            _status = KernelEarlyAllocatorStatus.InvalidParameter;
            return false;
        }

        fixed (Byte* buffer = _state.Buffer)
        {
            UInt64 baseAddress = (UInt64)(nuint)buffer;
            UInt64 current = baseAddress + _offset;
            UInt64 mask = alignment - 1UL;
            if (current > 0xFFFFFFFFFFFFFFFFUL - mask)
            {
                _status = KernelEarlyAllocatorStatus.InvalidParameter;
                return false;
            }
            UInt64 aligned = (current + mask) & ~mask;
            UInt64 used = aligned - baseAddress;
            if (used > Capacity || byteCount > Capacity - used)
            {
                _status = KernelEarlyAllocatorStatus.OutOfMemory;
                return false;
            }
            _offset = used + byteCount;
            address = aligned;
        }
        _status = KernelEarlyAllocatorStatus.Success;
        return true;
    }

    /// <summary>Gets unused early-arena capacity.</summary>
    /// <returns>Remaining bytes.</returns>
    public static UInt64 GetRemainingBytes() => _initialized ? Capacity - _offset : 0UL;
}
