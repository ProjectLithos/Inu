using System;

namespace Inu.Kernel.Scheduler;

/// <summary>Contains deterministic scheduler calculations shared by runtime code and host tests.</summary>
public static class KernelSchedulerMath
{
    /// <summary>Validates a requested kernel-thread stack size.</summary>
    public static Boolean IsValidStackSize(UInt64 bytes) => bytes >= 16384UL && bytes <= 1048576UL && (bytes & 4095UL) == 0UL;
    /// <summary>Returns whether a logical processor is admitted by a 64-bit affinity mask.</summary>
    public static Boolean AllowsProcessor(UInt64 mask, UInt32 processorIndex) => processorIndex < 64U && (mask & (1UL << (Int32)processorIndex)) != 0UL;
    /// <summary>Clamps a requested quantum to Inu's supported 100 microsecond through 1 second interval.</summary>
    public static UInt64 ClampQuantum(UInt64 nanoseconds)
    {
        if (nanoseconds < 100000UL) return 100000UL;
        if (nanoseconds > 1000000000UL) return 1000000000UL;
        return nanoseconds;
    }
    /// <summary>Computes the byte count for a fixed-size record table without overflow.</summary>
    public static Boolean TryGetTableBytes(UInt32 count, UInt32 recordBytes, out UInt64 bytes)
    {
        bytes = 0UL;
        if (count == 0U || recordBytes == 0U) return false;
        UInt64 result = (UInt64)count * (UInt64)recordBytes;
        if (result / (UInt64)recordBytes != (UInt64)count) return false;
        bytes = result; return true;
    }
}
