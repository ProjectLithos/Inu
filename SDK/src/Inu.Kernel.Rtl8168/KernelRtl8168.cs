using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Memory;
using Inu.Kernel.Networking;
using Inu.Kernel.Pci;

namespace Inu.Kernel.Rtl8168;

/// <summary>Provides Realtek RTL8168/RTL8111-class PCIe gigabit Ethernet controllers using DMA descriptor rings.</summary>
public static unsafe partial class KernelRtl8168
{
    private const UInt16 RealtekVendor=0x10EC;
    private const UInt32 DefaultMtu=1500U,DescriptorCount=64U,BufferBytes=2048U;
    private const UInt32 Id0=0x00,TxDescLow=0x20,TxDescHigh=0x24,ChipCommand=0x37,TxPoll=0x38,InterruptMask=0x3C,InterruptStatus=0x3E,TxConfig=0x40,RxConfig=0x44,Cfg9346=0x50,RxMaxSize=0xDA,RxDescLow=0xE4,RxDescHigh=0xE8;
    private const Byte CommandReset=0x10,CommandRxEnable=0x08,CommandTxEnable=0x04;
    private const UInt32 DescOwn=1U<<31,DescEor=1U<<30,DescFs=1U<<29,DescLs=1U<<28,DescLengthMask=0x3FFFU;

    private struct DeviceRecord
    {
        internal Byte Used,Started,ReceiveEnabled,Family,Msi;internal UInt32 DeviceHandle,NetworkHandle,Mtu;internal UInt16 Segment;internal Byte Bus,PciDevice,Function,MacA,MacB,MacC,MacD,MacE,MacF;
        internal UInt64 Mmio,RxToken,RxPages,RxPhysical,RxVirtual,TxToken,TxPages,TxPhysical,TxVirtual,InterruptHandle;internal UInt32 RxIndex,TxIndex;
    }
    private static DeviceRecord* _devices;private static KernelHeapAllocation _allocation;private static UInt32 _capacity,_count;private static KernelDriverHandle _driver;private static Boolean _initialized;

    /// <summary>Installs the RTL8168/RTL8111 PCIe driver and starts already-discovered supported controllers.</summary>
    public static Boolean Initialize()
    {
        if(_initialized)return true;if(!KernelPci.IsInitialized()||!KernelDrivers.IsInitialized()||!KernelNetworking.IsInitialized()||!KernelHeap.IsInitialized())return false;
        if(!AllocateRecords(8U,out _allocation,out _devices))return false;_capacity=8U;KernelDriverMatchRule rule=new(KernelDeviceBus.Pci,true,RealtekVendor,true,0,false,0x020000U,0xFF0000U);KernelDriverCallbacks callbacks=new(&Probe,&Start,&Stop,&Remove,&Interrupt);KernelDriverCapabilityDeclaration declaration=new(KernelDriverCapability.Mmio|KernelDriverCapability.Interrupt|KernelDriverCapability.Msi|KernelDriverCapability.MsiX|KernelDriverCapability.Dma|KernelDriverCapability.PciConfig|KernelDriverCapability.Networking);if(!KernelDrivers.RegisterDriver("Realtek RTL8168",rule,callbacks,declaration,out _driver))return false;
        for(UInt32 i=0;i<KernelPci.GetDeviceCount();i++){if(!KernelPci.TryGetDevice(i,out PciDeviceInfo pci)||!Rtl8168Math.IsSupported(pci.VendorId,pci.DeviceId))continue;if(KernelDrivers.TryGetDevice(pci.DeviceHandle,out _,out _,out KernelDriverHandle bound)&&bound.Value!=0U)continue;if(KernelDrivers.TryBindDevice(pci.DeviceHandle,out KernelDriverHandle driver)&&driver.Value==_driver.Value)KernelDrivers.StartDevice(pci.DeviceHandle);} _initialized=true;return true;
    }
    public static Boolean IsInitialized()=>_initialized;
    public static Rtl8168Capabilities GetCapabilities()=>new(_initialized,_count,_count,DescriptorCount,DescriptorCount);
    public static UInt32 GetDeviceCount()=>_count;
    public static Boolean TryGetDevice(UInt32 index,out Rtl8168DeviceInfo info){info=default;if(index>=_count)return false;UInt32 found=0;for(UInt32 i=0;i<_capacity;i++){DeviceRecord* r=_devices+i;if(r->Used==0)continue;if(found++==index){info=Info(r);return true;}}return false;}
    public static Boolean ServiceAll(){if(!_initialized)return false;Boolean ok=true;for(UInt32 i=0;i<_capacity;i++){DeviceRecord* r=_devices+i;if(r->Used!=0&&r->Started!=0)ok=ServiceRecord(r)&ok;}return ok;}
    public static Boolean Service(KernelDeviceHandle device)=>TryRecord(device,out DeviceRecord* r)&&r->Started!=0&&ServiceRecord(r);



}
