#if INU_COMPONENT_ACPI_FADT
using System;

namespace Inu.Kernel.Acpi;

/// <summary>Selectable parser/provider for the Fixed ACPI Description Table.</summary>
public static unsafe class KernelAcpiFadtProvider
{
    private static Boolean _initialized;
    private static AcpiFadtInfo _info;

    public static Boolean Register() => KernelAcpiFadtServices.Register(&InitializeProvider, &GetInfo);

    private static Boolean InitializeProvider()
    {
        if (_initialized) return true;
        if (!KernelAcpi.TryGetTable(KernelAcpi.FadtSignature, out UInt64 address, out UInt32 length) || length < 116U) return false;
        Byte* t = (Byte*)address;
        UInt64 firmwareControl = Read32(t + 36U);
        UInt64 dsdt = Read32(t + 40U);
        if (length >= 140U) { UInt64 xf = Read64(t + 132U); if (xf != 0UL) firmwareControl = xf; }
        if (length >= 148U) { UInt64 x = Read64(t + 140U); if (x != 0UL) dsdt = x; }
        AcpiGenericAddress pm1aEvent = LegacyGas(Read32(t + 56U), (Byte)1U, t[88]);
        AcpiGenericAddress pm1bEvent = LegacyGas(Read32(t + 60U), (Byte)1U, t[88]);
        AcpiGenericAddress pm1aControl = LegacyGas(Read32(t + 64U), (Byte)1U, t[89]);
        AcpiGenericAddress pm1bControl = LegacyGas(Read32(t + 68U), (Byte)1U, t[89]);
        if (length >= 196U)
        {
            AcpiGenericAddress x1e = KernelAcpiRegisterServices.ReadGas(t + 148U); if (x1e.IsPresent()) pm1aEvent = x1e;
            AcpiGenericAddress x2e = KernelAcpiRegisterServices.ReadGas(t + 160U); if (x2e.IsPresent()) pm1bEvent = x2e;
            AcpiGenericAddress x1c = KernelAcpiRegisterServices.ReadGas(t + 172U); if (x1c.IsPresent()) pm1aControl = x1c;
            AcpiGenericAddress x2c = KernelAcpiRegisterServices.ReadGas(t + 184U); if (x2c.IsPresent()) pm1bControl = x2c;
        }
        AcpiGenericAddress reset = length >= 129U ? KernelAcpiRegisterServices.ReadGas(t + 116U) : default;
        Byte resetValue = length >= 129U ? t[128] : (Byte)0U;
        _info = new AcpiFadtInfo(Read32(t + 112U), Read16(t + 46U), Read32(t + 48U), t[52], t[53], t[88], t[89], pm1aEvent, pm1bEvent, pm1aControl, pm1bControl, reset, resetValue, firmwareControl, dsdt, length > 108U ? t[108] : (Byte)0U);
        _initialized = true;
        return true;
    }

    private static Boolean GetInfo(AcpiFadtInfo* value) { if (value == null || !_initialized) return false; *value = _info; return true; }
    private static AcpiGenericAddress LegacyGas(UInt32 address, Byte space, Byte bytes) => address == 0U ? default : new AcpiGenericAddress(space, (Byte)(bytes * 8U), (Byte)0, bytes == 1U ? (Byte)1 : bytes == 2U ? (Byte)2 : (Byte)3, address);
    private static UInt16 Read16(Byte* p) => (UInt16)(p[0] | ((UInt16)p[1] << 8));
    private static UInt32 Read32(Byte* p) => (UInt32)(p[0] | ((UInt32)p[1] << 8) | ((UInt32)p[2] << 16) | ((UInt32)p[3] << 24));
    private static UInt64 Read64(Byte* p) => (UInt64)Read32(p) | ((UInt64)Read32(p + 4U) << 32);
}
#endif
