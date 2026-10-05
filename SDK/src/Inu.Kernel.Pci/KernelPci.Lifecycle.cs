using System;
using Inu.Kernel.Acpi;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Pci;

public static unsafe partial class KernelPci
{
    /// <summary>Initializes PCI discovery and registers every discovered PCI function with the generic driver framework.</summary>
    /// <returns><see langword="true"/> when enumeration completes.</returns>
    public static Boolean Initialize()
    {
        if(_initialized)return true;
        if(!KernelDrivers.IsInitialized()||!KernelHeap.IsInitialized()||!KernelAddressSpace.IsInitialized()||!KernelVirtualMemory.IsInitialized())return false;
        if(!AllocateDeviceTable(64U,out _deviceAllocation,out _devices))return false;
        _deviceCapacity=64U;_deviceCount=0U;_ecamSegmentCount=KernelPciConfigurationServices.IsRegistered(PciConfigurationTransport.PcieEcam)?KernelAcpi.GetPciEcamCount():0U;_nextMmioVirtual=KernelAddressSpace.MmioBase+PageSize;
        Boolean legacy=KernelPciConfigurationServices.IsRegistered(PciConfigurationTransport.LegacyIo);
        Boolean ecamProvider=KernelPciConfigurationServices.IsRegistered(PciConfigurationTransport.PcieEcam);
        if(!legacy&&!ecamProvider)return false;
        Boolean ok=true;
        if(ecamProvider&&_ecamSegmentCount!=0U)
        {
            for(UInt32 i=0;i<_ecamSegmentCount;i++)
            {
                if(!KernelAcpi.TryGetPciEcam(i,out AcpiPciEcamInfo ecam)){ok=false;break;}
                for(UInt32 bus=ecam.StartBus;bus<=ecam.EndBus;bus++)
                {
                    PciConfigurationTransport transport=ecam.SegmentGroup==0U&&legacy?PciConfigurationTransport.LegacyIo:PciConfigurationTransport.PcieEcam;if(!EnumerateBus(new PciLocation(ecam.SegmentGroup,(Byte)bus,0,0),transport)){ok=false;break;}
                    if(bus==255U)break;
                }
                if(!ok)break;
            }
        }
        else if(legacy)
        {
            for(UInt32 bus=0;bus<256U;bus++)if(!EnumerateBus(new PciLocation(0,(Byte)bus,0,0),PciConfigurationTransport.LegacyIo)){ok=false;break;}
        }
        else ok=false;
        KernelPciConfigurationServices.FinishEnumeration();
        if(!ok)return false;
        _initialized=true;return true;
    }

    /// <summary>Gets whether PCI discovery completed.</summary>
    public static Boolean IsInitialized()=>_initialized;
    /// <summary>Gets PCI discovery statistics.</summary>
    public static PciCapabilities GetCapabilities()=>new(_initialized,_deviceCount,_ecamSegmentCount,KernelPciConfigurationServices.IsRegistered(PciConfigurationTransport.LegacyIo),_nextMmioVirtual);
    /// <summary>Gets the number of discovered PCI functions.</summary>
    public static UInt32 GetDeviceCount()=>_deviceCount;
}
