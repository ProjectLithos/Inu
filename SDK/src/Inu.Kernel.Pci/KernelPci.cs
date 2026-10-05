using System;
using Inu.Kernel.Acpi;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Pci;

/// <summary>Enumerates PCI/PCIe functions, exposes configuration-space access, discovers BARs and capabilities, and maps device MMIO.</summary>
public static unsafe partial class KernelPci
{
    private const UInt16 PciStatusCapabilities=0x0010;
    private const UInt64 PageSize=4096UL;
    private struct DeviceRecord { internal Byte Used,Transport,Bus,Device,Function,Revision,HeaderType; internal UInt16 Segment,VendorId,DeviceId,SubsystemVendorId,SubsystemId; internal UInt32 ClassCode,Handle; }
    private static DeviceRecord* _devices;
    private static KernelHeapAllocation _deviceAllocation;
    private static UInt32 _deviceCapacity,_deviceCount,_ecamSegmentCount;
    private static Boolean _initialized;
    private static UInt64 _nextMmioVirtual=KernelAddressSpace.MmioBase+PageSize;
}
