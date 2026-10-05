using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Memory;
using Inu.Kernel.Networking;
using Inu.Kernel.Pci;
using Inu.Kernel.Storage;
using Inu.Kernel.Time;

namespace Inu.Kernel.Virtio;

/// <summary>Provides the modern VirtIO PCI transport and built-in block, network, console, and entropy-source drivers.</summary>
public static unsafe partial class KernelVirtio
{
    private const UInt16 VirtioVendorId=0x1AF4;
    private const Byte VendorCapabilityId=0x09;
    private const Byte CommonConfigurationType=1,NotifyConfigurationType=2,IsrConfigurationType=3,DeviceConfigurationType=4;
    private const UInt64 FeatureVersion1=1UL<<32;
    private const UInt64 BlockFeatureReadOnly=1UL<<5,BlockFeatureBlockSize=1UL<<6,BlockFeatureFlush=1UL<<9;
    private const UInt64 NetworkFeatureMtu=1UL<<3,NetworkFeatureMac=1UL<<5,NetworkFeatureStatus=1UL<<16;
    private const UInt16 DescriptorNext=1,DescriptorWrite=2;
    private const UInt32 BlockRequestIn=0,BlockRequestOut=1,BlockRequestFlush=4;
    private const UInt64 SynchronousTimeoutNanoseconds=2000000000UL;
    private const UInt32 VirtioNetworkHeaderBytes=10U;

    private struct QueueRecord
    {
        internal Byte Ready;internal UInt16 Index,Size,LastUsed;internal UInt64 NotifyOffset,PhysicalBase,VirtualBase,DescriptorOffset,AvailableOffset,UsedOffset,AllocationToken,AllocationPages;
    }
    private struct DeviceRecord
    {
        internal Byte Used,Started,Type,ReceiveEnabled;internal UInt32 DeviceHandle;internal UInt16 Segment;internal Byte Bus,PciDevice,Function;internal UInt64 Common,Notify,Isr,DeviceConfig,NotifyMultiplier,DeviceFeatures,NegotiatedFeatures;
        internal UInt16 QueueCount;internal QueueRecord Queue0,Queue1;internal UInt32 StorageHandle,NetworkHandle,BlockSize,Mtu;internal UInt64 BlockCount,RxToken,RxPages,RxPhysical,RxVirtual,InterruptHandle,InterruptEpoch;internal UInt32 RxBytes;
    }
    private static DeviceRecord* _devices;private static KernelHeapAllocation _deviceAllocation;private static UInt32 _capacity,_count,_blockCount,_networkCount,_consoleCount,_rngCount;private static Boolean _initialized;private static KernelDriverHandle _driverHandle;

    /// <summary>Installs the VirtIO PCI driver family, binds supported discovered PCI functions, and starts their transport-specific drivers.</summary>
    public static Boolean Initialize()
    {
        if(_initialized)return BindDiscoveredDevices();if(!KernelPci.IsInitialized()||!KernelDrivers.IsInitialized()||!KernelHeap.IsInitialized()||!KernelStorage.IsInitialized())return false;
        if(!AllocateRecords(16U,out _deviceAllocation,out _devices))return false;_capacity=16U;_count=0U;_blockCount=0U;_networkCount=0U;_consoleCount=0U;_rngCount=0U;
        KernelDriverMatchRule rule=new(KernelDeviceBus.Pci,true,VirtioVendorId,true,0,false,0U,0U);KernelDriverCallbacks callbacks=new(&Probe,&Start,&Stop,&Remove,&Interrupt);KernelDriverCapabilityDeclaration declaration=new(KernelDriverCapability.Mmio|KernelDriverCapability.Dma|KernelDriverCapability.PciConfig|KernelDriverCapability.Interrupt|KernelDriverCapability.Msi|KernelDriverCapability.MsiX|KernelDriverCapability.Networking|KernelDriverCapability.Filesystem);if(!KernelDrivers.RegisterDriver("VirtIO PCI",rule,callbacks,declaration,out _driverHandle))return false;
        _initialized=true;return BindDiscoveredDevices();
    }

    private static Boolean BindDiscoveredDevices()
    {
        UInt32 pciCount=KernelPci.GetDeviceCount();for(UInt32 i=0;i<pciCount;i++)
        {
            if(!KernelPci.TryGetDevice(i,out PciDeviceInfo pci)||pci.VendorId!=VirtioVendorId)continue;
            if(KernelDrivers.TryGetDevice(pci.DeviceHandle,out _,out _,out KernelDriverHandle bound)&&bound.Value!=0U)
            {
                if(bound.Value==_driverHandle.Value)KernelDrivers.StartDevice(pci.DeviceHandle);continue;
            }
            if(KernelDrivers.TryBindDevice(pci.DeviceHandle,out KernelDriverHandle driver)&&driver.Value==_driverHandle.Value)KernelDrivers.StartDevice(pci.DeviceHandle);
        }
        return true;
    }

    /// <summary>Gets whether the VirtIO driver family was installed.</summary>
    public static Boolean IsInitialized()=>_initialized;
    /// <summary>Gets started VirtIO device counts by built-in driver type.</summary>
    public static VirtioCapabilities GetCapabilities()=>new(_initialized,_count,_blockCount,_networkCount,_consoleCount,_rngCount);
    /// <summary>Gets the number of started VirtIO devices.</summary>
    public static UInt32 GetDeviceCount()=>_count;

    /// <summary>Gets one started VirtIO device by zero-based discovery index.</summary>
    public static Boolean TryGetDevice(UInt32 index,out VirtioDeviceInfo info)
    {info=default;if(index>=_count)return false;UInt32 found=0;for(UInt32 i=0;i<_capacity;i++){DeviceRecord* r=_devices+i;if(r->Used==0)continue;if(found++==index){info=Info(r);return true;}}return false;}

    /// <summary>Gets VirtIO metadata for a generic device handle.</summary>
    public static Boolean TryGetDevice(KernelDeviceHandle device,out VirtioDeviceInfo info)
    {info=default;if(!TryRecord(device,out DeviceRecord* r))return false;info=Info(r);return true;}

    /// <summary>Services all started VirtIO network devices for receive-side work when interrupt delivery is not installed.</summary>
    public static Boolean ServiceAll()
    {if(!_initialized)return false;Boolean ok=true;for(UInt32 i=0;i<_capacity;i++){DeviceRecord* r=_devices+i;if(r->Used!=0&&r->Started!=0&&(VirtioDeviceType)r->Type==VirtioDeviceType.Network)ok=ServiceNetwork(r)&ok;}return ok;}

    /// <summary>Services one VirtIO device for receive-side work. This also serves systems that have not yet installed MSI/MSI-X delivery.</summary>
    public static Boolean Service(KernelDeviceHandle device)
    {if(!TryRecord(device,out DeviceRecord* r)||r->Started==0)return false;if((VirtioDeviceType)r->Type==VirtioDeviceType.Network)return ServiceNetwork(r);return true;}

    /// <summary>Writes bytes synchronously to a started VirtIO console transmit queue.</summary>
    public static Boolean WriteConsole(KernelDeviceHandle device,Byte* buffer,UInt32 length)
    {if(buffer==null||length==0||!TryRecord(device,out DeviceRecord* r)||(VirtioDeviceType)r->Type!=VirtioDeviceType.Console||r->Started==0)return false;return TransferSimple(r,&r->Queue1,buffer,length,false);}

    /// <summary>Reads bytes synchronously from a started VirtIO console receive queue.</summary>
    public static Boolean ReadConsole(KernelDeviceHandle device,Byte* buffer,UInt32 capacity,out UInt32 bytesRead)
    {bytesRead=0;if(buffer==null||capacity==0||!TryRecord(device,out DeviceRecord* r)||(VirtioDeviceType)r->Type!=VirtioDeviceType.Console||r->Started==0)return false;return TransferSimpleRead(r,&r->Queue0,buffer,capacity,out bytesRead);}

    /// <summary>Obtains entropy bytes synchronously from a started VirtIO RNG device.</summary>
    public static Boolean FillRandom(KernelDeviceHandle device,Byte* buffer,UInt32 length)
    {if(buffer==null||length==0||!TryRecord(device,out DeviceRecord* r)||(VirtioDeviceType)r->Type!=VirtioDeviceType.EntropySource||r->Started==0)return false;UInt32 read;return TransferSimpleRead(r,&r->Queue0,buffer,length,out read)&&read==length;}

    private static Boolean Probe(KernelDriverDeviceContext* context)
    {if(context==null||context->Identifier.VendorId!=VirtioVendorId||!KernelPci.TryGetDevice(context->Device,out PciDeviceInfo pci))return false;VirtioDeviceType type=VirtioMath.IdentifyDeviceType(pci.DeviceId,pci.SubsystemId);return type>=VirtioDeviceType.Network&&type<=VirtioDeviceType.EntropySource&&TryFindTransportCapability(pci.Location,CommonConfigurationType,out _);}

    private static Boolean Start(KernelDriverDeviceContext* context)
    {
        if(context==null||!KernelPci.TryGetDevice(context->Device,out PciDeviceInfo pci)||!EnablePci(pci.Location))return false;VirtioDeviceType type=VirtioMath.IdentifyDeviceType(pci.DeviceId,pci.SubsystemId);if(type==VirtioDeviceType.Unknown)return false;
        Int32 slot=FreeRecord();if(slot<0){if(!GrowRecords())return false;slot=FreeRecord();if(slot<0)return false;}DeviceRecord* r=_devices+slot;Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));r->Used=1;r->Type=(Byte)type;r->DeviceHandle=context->Device.Value;r->Segment=pci.Location.Segment;r->Bus=pci.Location.Bus;r->PciDevice=pci.Location.Device;r->Function=pci.Location.Function;
        if(!InitializeTransport(r,pci.Location,type)){Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));return false;}
        Boolean started=type==VirtioDeviceType.Block?InitializeBlock(r):type==VirtioDeviceType.Network?InitializeNetwork(r):type==VirtioDeviceType.Console?InitializeConsole(r):InitializeEntropy(r);
        if(!started){SetStatus(r,VirtioDeviceStatus.Failed);ReleaseRecordResources(r);Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));return false;}SetStatus(r,(VirtioDeviceStatus)(Read8(r->Common+20)|((Byte)VirtioDeviceStatus.DriverOk)));r->Started=1;r->QueueCount=(UInt16)((r->Queue0.Ready!=0?1:0)+(r->Queue1.Ready!=0?1:0));KernelDriverInterruptRequest interruptRequest=new(context->Device,0U,8,0U,false,false,(UInt64)(UInt32)slot+1UL);if(KernelDrivers.TryRequestInterrupt(interruptRequest,out KernelDriverInterruptHandle interrupt))r->InterruptHandle=interrupt.Value;_count++;if(type==VirtioDeviceType.Block)_blockCount++;else if(type==VirtioDeviceType.Network)_networkCount++;else if(type==VirtioDeviceType.Console)_consoleCount++;else _rngCount++;return true;
    }

    private static Boolean Stop(KernelDriverDeviceContext* context)
    {if(context==null)return false;if(!TryRecord(context->Device,out DeviceRecord* r))return true;VirtioDeviceType type=(VirtioDeviceType)r->Type;if(r->InterruptHandle!=0UL&&!KernelDrivers.ReleaseInterrupt(new KernelDriverInterruptHandle(r->InterruptHandle)))return false;r->InterruptHandle=0UL;SetStatus(r,VirtioDeviceStatus.Reset);if(r->StorageHandle!=0U&&!KernelStorage.UnregisterBlockDevice(new KernelStorageDeviceHandle(r->StorageHandle)))return false;if(r->NetworkHandle!=0U&&!KernelNetworking.UnregisterInterface(new KernelNetworkInterfaceHandle(r->NetworkHandle)))return false;r->StorageHandle=0U;r->NetworkHandle=0U;r->Started=0;if(!ReleaseRecordResources(r))return false;Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));if(_count!=0)_count--;if(type==VirtioDeviceType.Block&&_blockCount!=0)_blockCount--;else if(type==VirtioDeviceType.Network&&_networkCount!=0)_networkCount--;else if(type==VirtioDeviceType.Console&&_consoleCount!=0)_consoleCount--;else if(type==VirtioDeviceType.EntropySource&&_rngCount!=0)_rngCount--;return true;}
    private static Boolean Remove(KernelDriverDeviceContext* context)=>context!=null&&Stop(context);
    private static Boolean Interrupt(KernelDriverDeviceContext* context,UInt64 cookie){if(context==null||!TryRecord(context->Device,out DeviceRecord* r))return false;if(r->Isr!=0UL)_=Read8(r->Isr);r->InterruptEpoch++;return Service(context->Device);}

}
