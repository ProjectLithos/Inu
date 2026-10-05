using System;

namespace Inu.Kernel.Pci;

/// <summary>Registration and dispatch boundary between PCI discovery and selectable configuration-space transports.</summary>
public static unsafe class KernelPciConfigurationServices
{
    private struct Registration
    {
        public Byte Registered;
        public delegate*<PciLocation, UInt16, Boolean> CanAccess;
        public delegate*<PciLocation, UInt16, UInt32*, Boolean> Read32;
        public delegate*<PciLocation, UInt16, UInt32, Boolean> Write32;
        public delegate*<void> FinishEnumeration;
    }

    private static Registration _legacy;
    private static Registration _ecam;

    /// <summary>Registers one PCI configuration-space transport implementation.</summary>
    public static Boolean Register(
        PciConfigurationTransport transport,
        delegate*<PciLocation, UInt16, Boolean> canAccess,
        delegate*<PciLocation, UInt16, UInt32*, Boolean> read32,
        delegate*<PciLocation, UInt16, UInt32, Boolean> write32,
        delegate*<void> finishEnumeration)
    {
        if (canAccess == null || read32 == null || write32 == null || finishEnumeration == null) return false;
        Registration value = new Registration { Registered = 1, CanAccess = canAccess, Read32 = read32, Write32 = write32, FinishEnumeration = finishEnumeration };
        if (transport == PciConfigurationTransport.LegacyIo)
        {
            if (_legacy.Registered != 0) return false;
            _legacy = value;
            return true;
        }
        if (transport == PciConfigurationTransport.PcieEcam)
        {
            if (_ecam.Registered != 0) return false;
            _ecam = value;
            return true;
        }
        return false;
    }

    /// <summary>Gets whether a configuration transport has been registered.</summary>
    public static Boolean IsRegistered(PciConfigurationTransport transport) =>
        transport == PciConfigurationTransport.LegacyIo ? _legacy.Registered != 0 :
        transport == PciConfigurationTransport.PcieEcam && _ecam.Registered != 0;

    /// <summary>Reads one aligned 32-bit configuration-space value through the best registered transport.</summary>
    internal static Boolean TryRead32(PciLocation location, UInt16 offset, out UInt32 value)
    {
        value = 0U;
        Boolean preferLegacy = PciMath.ShouldUseLegacyConfiguration(location, offset) && _legacy.Registered != 0;
        if (preferLegacy)
        {
            if (TryRead(ref _legacy, location, offset, out value)) return true;
            return TryRead(ref _ecam, location, offset, out value);
        }
        if (TryRead(ref _ecam, location, offset, out value)) return true;
        return TryRead(ref _legacy, location, offset, out value);
    }

    /// <summary>Writes one aligned 32-bit configuration-space value through the best registered transport.</summary>
    internal static Boolean TryWrite32(PciLocation location, UInt16 offset, UInt32 value)
    {
        Boolean preferLegacy = PciMath.ShouldUseLegacyConfiguration(location, offset) && _legacy.Registered != 0;
        if (preferLegacy)
        {
            if (TryWrite(ref _legacy, location, offset, value)) return true;
            return TryWrite(ref _ecam, location, offset, value);
        }
        if (TryWrite(ref _ecam, location, offset, value)) return true;
        return TryWrite(ref _legacy, location, offset, value);
    }

    /// <summary>Releases transient mappings held by registered transports after discovery.</summary>
    internal static void FinishEnumeration()
    {
        if (_legacy.Registered != 0) _legacy.FinishEnumeration();
        if (_ecam.Registered != 0) _ecam.FinishEnumeration();
    }

    private static Boolean TryRead(ref Registration registration, PciLocation location, UInt16 offset, out UInt32 value)
    {
        value = 0U;
        if (registration.Registered == 0 || !registration.CanAccess(location, offset)) return false;
        UInt32 local = 0U;
        if (!registration.Read32(location, offset, &local)) return false;
        value = local;
        return true;
    }

    private static Boolean TryWrite(ref Registration registration, PciLocation location, UInt16 offset, UInt32 value) =>
        registration.Registered != 0 && registration.CanAccess(location, offset) && registration.Write32(location, offset, value);
}
