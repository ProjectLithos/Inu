using System;
using Inu.Kernel.Acpi;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Pci;

/// <summary>Selectable PCIe ECAM configuration-space provider discovered through ACPI MCFG.</summary>
public static unsafe class KernelPciEcamConfigurationProvider
{
    private static UInt32 _mappedLocation = UInt32.MaxValue;

    /// <summary>Registers ACPI MCFG/ECAM configuration-space access with the PCI core.</summary>
    public static Boolean Register() => KernelPciConfigurationServices.Register(
        PciConfigurationTransport.PcieEcam, &CanAccess, &Read32, &Write32, &FinishEnumeration);

    private static Boolean CanAccess(PciLocation location, UInt16 offset) => offset < 4096U && TryFindEcam(location, out _);

    private static Boolean Read32(PciLocation location, UInt16 offset, UInt32* value)
    {
        if (value == null || (offset & 3U) != 0U || !TryFindEcam(location, out AcpiPciEcamInfo ecam) || !TryMap(location, ecam, out UInt64 virtualBase)) return false;
        *value = *(UInt32*)(nuint)(virtualBase + offset);
        return true;
    }

    private static Boolean Write32(PciLocation location, UInt16 offset, UInt32 value)
    {
        if ((offset & 3U) != 0U || !TryFindEcam(location, out AcpiPciEcamInfo ecam) || !TryMap(location, ecam, out UInt64 virtualBase)) return false;
        *(UInt32*)(nuint)(virtualBase + offset) = value;
        return true;
    }

    private static Boolean TryFindEcam(PciLocation location, out AcpiPciEcamInfo ecam)
    {
        ecam = default;
        UInt32 count = KernelAcpi.GetPciEcamCount();
        for (UInt32 i = 0; i < count; i++)
        {
            if (KernelAcpi.TryGetPciEcam(i, out AcpiPciEcamInfo current) && current.SegmentGroup == location.Segment && location.Bus >= current.StartBus && location.Bus <= current.EndBus)
            {
                ecam = current;
                return true;
            }
        }
        return false;
    }

    private static Boolean TryMap(PciLocation location, AcpiPciEcamInfo ecam, out UInt64 virtualAddress)
    {
        virtualAddress = 0UL;
        UInt32 encoded = location.Encode();
        if (_mappedLocation == encoded) { virtualAddress = KernelAddressSpace.MmioBase; return true; }
        if (_mappedLocation != UInt32.MaxValue)
        {
            if (!KernelVirtualMemory.TryUnmap(KernelAddressSpace.MmioBase)) return false;
            _mappedLocation = UInt32.MaxValue;
        }
        UInt64 busOffset = (UInt64)(location.Bus - ecam.StartBus) << 20;
        UInt64 deviceOffset = (UInt64)location.Device << 15;
        UInt64 functionOffset = (UInt64)location.Function << 12;
        if (ecam.BaseAddress > UInt64.MaxValue - busOffset - deviceOffset - functionOffset) return false;
        UInt64 physical = ecam.BaseAddress + busOffset + deviceOffset + functionOffset;
        KernelVirtualMemoryProtection protection = KernelVirtualMemoryProtection.Read | KernelVirtualMemoryProtection.Write | KernelVirtualMemoryProtection.Device | KernelVirtualMemoryProtection.Global;
        if (!KernelVirtualMemory.TryMap(KernelAddressSpace.MmioBase, physical, KernelVirtualPageSize.Page4KiB, protection)) return false;
        _mappedLocation = encoded;
        virtualAddress = KernelAddressSpace.MmioBase;
        return true;
    }

    private static void FinishEnumeration()
    {
        if (_mappedLocation == UInt32.MaxValue) return;
        KernelVirtualMemory.TryUnmap(KernelAddressSpace.MmioBase);
        _mappedLocation = UInt32.MaxValue;
    }
}
