using System;

namespace Inu.Kernel.Protection;

/// <summary>Provides architecture-policy validation for the x64 user/kernel boundary.</summary>
public static class KernelProtectionMath
{
    public const UInt64 MinimumUserAddress = 0x0000000000010000UL;
    public const UInt64 MaximumUserAddress = 0x00007FFFFFFFFFFFUL;
    public const UInt16 UserDataSelector = 0x001B;
    public const UInt16 UserCodeSelector = 0x0023;

    /// <summary>Determines whether an address belongs to the canonical lower-half user range.</summary>
    public static Boolean IsUserAddress(UInt64 address) => address >= MinimumUserAddress && address <= MaximumUserAddress;

    /// <summary>Determines whether a non-empty byte range is wholly contained in user space.</summary>
    public static Boolean IsUserRange(UInt64 start, UInt64 byteCount)
    {
        if (byteCount == 0UL || !IsUserAddress(start)) return false;
        UInt64 lastOffset = byteCount - 1UL;
        if (start > UInt64.MaxValue - lastOffset) return false;
        return IsUserAddress(start + lastOffset);
    }

    /// <summary>Determines whether an initial ring-3 stack pointer is canonical and Win64 ABI aligned.</summary>
    public static Boolean IsValidUserStack(UInt64 stackTop) => IsUserAddress(stackTop) && (stackTop & 0xFUL) == 0UL;

    /// <summary>Determines whether a ring-3 instruction pointer is canonical and outside the null guard.</summary>
    public static Boolean IsValidUserEntry(UInt64 entryPoint) => IsUserAddress(entryPoint);
}
