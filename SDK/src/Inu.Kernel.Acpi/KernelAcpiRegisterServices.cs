using System;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Acpi;

/// <summary>Shared ACPI Generic Address Structure parsing and register access.</summary>
internal static unsafe class KernelAcpiRegisterServices
{
    internal static AcpiGenericAddress ReadGas(Byte* p) => new AcpiGenericAddress(p[0], p[1], p[2], p[3], Read64(p + 4U));

    internal static Boolean Read(AcpiGenericAddress gas, out UInt64 value)
    {
        value = 0UL;
        if (!gas.IsPresent() || gas.BitOffset != 0U) return false;
        Byte width = gas.BitWidth == 0U ? AccessWidth(gas.AccessSize) : gas.BitWidth;
        if (gas.AddressSpace == 1U)
        {
            if (gas.Address > 0xFFFFUL) return false;
            UInt16 port = (UInt16)gas.Address;
            if (width <= 8U) { if (!Native.ReadPort8(port, out Byte v)) return false; value = v; return true; }
            if (width <= 16U) { if (!Native.ReadPort16(port, out UInt16 v)) return false; value = v; return true; }
            if (width <= 32U) { if (!Native.ReadPort32(port, out UInt32 v)) return false; value = v; return true; }
            return false;
        }
        Byte* address = (Byte*)gas.Address;
        if (width <= 8U) { value = *address; return true; }
        if (width <= 16U) { value = *(UInt16*)address; return true; }
        if (width <= 32U) { value = *(UInt32*)address; return true; }
        if (width <= 64U) { value = *(UInt64*)address; return true; }
        return false;
    }

    internal static Boolean Write(AcpiGenericAddress gas, UInt64 value)
    {
        if (!gas.IsPresent() || gas.BitOffset != 0U) return false;
        Byte width = gas.BitWidth == 0U ? AccessWidth(gas.AccessSize) : gas.BitWidth;
        if (gas.AddressSpace == 1U)
        {
            if (gas.Address > 0xFFFFUL) return false;
            UInt16 port = (UInt16)gas.Address;
            if (width <= 8U) return Native.WritePort8(port, (Byte)value);
            if (width <= 16U) return Native.WritePort16(port, (UInt16)value);
            if (width <= 32U) return Native.WritePort32(port, (UInt32)value);
            return false;
        }
        Byte* address = (Byte*)gas.Address;
        if (width <= 8U) { *address = (Byte)value; return true; }
        if (width <= 16U) { *(UInt16*)address = (UInt16)value; return true; }
        if (width <= 32U) { *(UInt32*)address = (UInt32)value; return true; }
        if (width <= 64U) { *(UInt64*)address = value; return true; }
        return false;
    }

    private static Byte AccessWidth(Byte accessSize) => accessSize == 1U ? (Byte)8U : accessSize == 2U ? (Byte)16U : accessSize == 3U ? (Byte)32U : accessSize == 4U ? (Byte)64U : (Byte)8U;
    private static UInt32 Read32(Byte* p) => (UInt32)(p[0] | ((UInt32)p[1] << 8) | ((UInt32)p[2] << 16) | ((UInt32)p[3] << 24));
    private static UInt64 Read64(Byte* p) => (UInt64)Read32(p) | ((UInt64)Read32(p + 4U) << 32);
}
