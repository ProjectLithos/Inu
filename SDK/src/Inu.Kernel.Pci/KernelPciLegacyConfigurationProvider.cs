using System;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Pci;

/// <summary>Selectable PCI configuration mechanism #1 provider using the x86 CF8/CFC I/O ports.</summary>
public static unsafe class KernelPciLegacyConfigurationProvider
{
    private const UInt16 ConfigurationAddressPort = 0x0CF8;
    private const UInt16 ConfigurationDataPort = 0x0CFC;

    /// <summary>Registers legacy PCI configuration-space access with the PCI core.</summary>
    public static Boolean Register() => KernelPciConfigurationServices.Register(
        PciConfigurationTransport.LegacyIo, &CanAccess, &Read32, &Write32, &FinishEnumeration);

    private static Boolean CanAccess(PciLocation location, UInt16 offset) => PciMath.ShouldUseLegacyConfiguration(location, offset);

    private static Boolean Read32(PciLocation location, UInt16 offset, UInt32* value)
    {
        if (value == null || !CanAccess(location, offset) || (offset & 3U) != 0U) return false;
        UInt32 address = 0x80000000U | ((UInt32)location.Bus << 16) | ((UInt32)location.Device << 11) | ((UInt32)location.Function << 8) | (UInt32)(offset & 0xFC);
        UInt32 local = 0U;
        if (!Native.WritePort32(ConfigurationAddressPort, address) || !Native.ReadPort32(ConfigurationDataPort, out local)) return false;
        *value = local;
        return true;
    }

    private static Boolean Write32(PciLocation location, UInt16 offset, UInt32 value)
    {
        if (!CanAccess(location, offset) || (offset & 3U) != 0U) return false;
        UInt32 address = 0x80000000U | ((UInt32)location.Bus << 16) | ((UInt32)location.Device << 11) | ((UInt32)location.Function << 8) | (UInt32)(offset & 0xFC);
        return Native.WritePort32(ConfigurationAddressPort, address) && Native.WritePort32(ConfigurationDataPort, value);
    }

    private static void FinishEnumeration() { }
}
