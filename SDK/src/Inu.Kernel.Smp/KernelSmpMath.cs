using System;

namespace Inu.Kernel.Smp;

/// <summary>Contains deterministic SMP layout and startup-vector calculations.</summary>
public static class KernelSmpMath
{
    /// <summary>Gets whether a physical address can be encoded as an x86 SIPI startup vector.</summary>
    public static Boolean IsValidStartupTrampoline(UInt64 address) => address >= 0x1000UL && address < 0x100000UL && (address & 0xFFFUL) == 0UL;

    /// <summary>Converts one validated low-memory trampoline address to its SIPI vector.</summary>
    public static Boolean TryGetStartupVector(UInt64 address, out Byte vector)
    {
        vector = (Byte)0U;
        if (!IsValidStartupTrampoline(address)) return false;
        UInt64 value = address >> 12;
        if (value == 0UL || value > 0xFFUL) return false;
        vector = (Byte)value;
        return true;
    }

    /// <summary>Gets whether an APIC identifier can be targeted through the xAPIC destination field.</summary>
    public static Boolean IsXApicDestination(UInt32 apicId) => apicId <= 0xFFU;

    /// <summary>Calculates a checked per-CPU table byte requirement.</summary>
    public static Boolean TryGetStateTableBytes(UInt32 processorCount, UInt32 recordBytes, out UInt64 requiredBytes)
    {
        requiredBytes = 0UL;
        if (processorCount == 0U || recordBytes == 0U) return false;
        UInt64 count = processorCount;
        if (count > 0xFFFFFFFFFFFFFFFFUL / recordBytes) return false;
        requiredBytes = count * recordBytes;
        return requiredBytes != 0UL;
    }
}
