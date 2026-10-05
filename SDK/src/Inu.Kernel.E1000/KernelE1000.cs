using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Memory;
using Inu.Kernel.Networking;
using Inu.Kernel.Pci;

namespace Inu.Kernel.E1000;

/// <summary>Provides Intel E1000/E1000e PCI gigabit Ethernet controllers using DMA descriptor rings.</summary>
public static unsafe partial class KernelE1000
{
    private const UInt16 IntelVendor=0x8086;
    private const UInt32 DefaultMtu=1500U,DescriptorCount=64U,BufferBytes=2048U;
    private const UInt32 Ctrl=0x0000,Status=0x0008,Icr=0x00C0,Ims=0x00D0,Imc=0x00D8,Rctl=0x0100,Tctl=0x0400,Tipg=0x0410;
    private const UInt32 Rdbal=0x2800,Rdbah=0x2804,Rdlen=0x2808,Rdh=0x2810,Rdt=0x2818,Tdbal=0x3800,Tdbah=0x3804,Tdlen=0x3808,Tdh=0x3810,Tdt=0x3818;
    private const UInt32 Ral0=0x5400,Rah0=0x5404;
    private const UInt32 CtrlReset=1U<<26,RctlEnable=1U<<1,RctlBroadcast=1U<<15,RctlStripCrc=1U<<26,TctlEnable=1U<<1,TctlPadShort=1U<<3;
    private const Byte RxDone=1,TxDone=1,TxEopIfcsRs=0x0B;

    private struct DeviceRecord
    {
        internal Byte Used,Started,ReceiveEnabled,Family,Msi,Msix;internal UInt32 DeviceHandle,NetworkHandle,Mtu;internal UInt16 Segment;internal Byte Bus,PciDevice,Function,MacA,MacB,MacC,MacD,MacE,MacF;
        internal UInt64 Mmio,RxToken,RxPages,RxPhysical,RxVirtual,TxToken,TxPages,TxPhysical,TxVirtual,InterruptHandle;internal UInt32 RxIndex,TxIndex;
    }
    private static DeviceRecord* _devices;private static KernelHeapAllocation _allocation;private static UInt32 _capacity,_count;private static KernelDriverHandle _driver;private static Boolean _initialized;

    /// <summary>Installs the Intel E1000/E1000e PCI driver and starts already-discovered supported controllers.</summary>
    public static Boolean Initialize()
    {
        if(_initialized)return true;if(!KernelPci.IsInitialized()||!KernelDrivers.IsInitialized()||!KernelNetworking.IsInitialized()||!KernelHeap.IsInitialized())return false;
        if(!AllocateRecords(8U,out _allocation,out _devices))return false;_capacity=8U;
        KernelDriverMatchRule rule=new(KernelDeviceBus.Pci,true,IntelVendor,true,0,false,0x020000U,0xFF0000U);KernelDriverCallbacks callbacks=new(&Probe,&Start,&Stop,&Remove,&Interrupt);KernelDriverCapabilityDeclaration declaration=new(KernelDriverCapability.Mmio|KernelDriverCapability.Interrupt|KernelDriverCapability.Msi|KernelDriverCapability.MsiX|KernelDriverCapability.Dma|KernelDriverCapability.PciConfig|KernelDriverCapability.Networking);if(!KernelDrivers.RegisterDriver("Intel E1000",rule,callbacks,declaration,out _driver))return false;
        for(UInt32 i=0;i<KernelPci.GetDeviceCount();i++){if(!KernelPci.TryGetDevice(i,out PciDeviceInfo pci)||!E1000Math.IsSupported(pci.VendorId,pci.DeviceId))continue;if(KernelDrivers.TryGetDevice(pci.DeviceHandle,out _,out _,out KernelDriverHandle bound)&&bound.Value!=0U)continue;if(KernelDrivers.TryBindDevice(pci.DeviceHandle,out KernelDriverHandle driver)&&driver.Value==_driver.Value)KernelDrivers.StartDevice(pci.DeviceHandle);} _initialized=true;return true;
    }
    public static Boolean IsInitialized()=>_initialized;
    public static E1000Capabilities GetCapabilities()=>new(_initialized,_count,_count,DescriptorCount,DescriptorCount);
    public static UInt32 GetDeviceCount()=>_count;
    public static Boolean TryGetDevice(UInt32 index,out E1000DeviceInfo info){info=default;if(index>=_count)return false;UInt32 found=0;for(UInt32 i=0;i<_capacity;i++){DeviceRecord* r=_devices+i;if(r->Used==0)continue;if(found++==index){info=Info(r);return true;}}return false;}
    public static Boolean ServiceAll(){if(!_initialized)return false;Boolean ok=true;for(UInt32 i=0;i<_capacity;i++){DeviceRecord* r=_devices+i;if(r->Used!=0&&r->Started!=0)ok=ServiceRecord(r)&ok;}return ok;}
    public static Boolean Service(KernelDeviceHandle device){return TryRecord(device,out DeviceRecord* r)&&r->Started!=0&&ServiceRecord(r);}



}
