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
    private static Boolean EnumerateBus(PciLocation busLocation,PciConfigurationTransport transport)
    {
        for(Byte device=0;device<32;device++)
        {
            PciLocation first=new(busLocation.Segment,busLocation.Bus,device,0);if(!TryRead16(first,0x00,out UInt16 vendor)||vendor==0xFFFFU)continue;
            if(!EnumerateFunction(first,transport))return false;if(!TryRead8(first,0x0E,out Byte header))return false;if((header&0x80U)==0U)continue;
            for(Byte function=1;function<8;function++){PciLocation location=new(busLocation.Segment,busLocation.Bus,device,function);if(!TryRead16(location,0x00,out vendor))return false;if(vendor!=0xFFFFU&&!EnumerateFunction(location,transport))return false;}
        }
        return true;
    }

    private static Boolean EnumerateFunction(PciLocation location,PciConfigurationTransport transport)
    {
        if(!TryRead32(location,0x00,out UInt32 ids)||!TryRead32(location,0x08,out UInt32 classRevision)||!TryRead8(location,0x0E,out Byte rawHeader))return false;
        UInt16 vendor=(UInt16)ids,device=(UInt16)(ids>>16);if(vendor==0xFFFFU)return true;Byte header=(Byte)(rawHeader&0x7FU);UInt16 subsystemVendor=0,subsystem=0;if(header==0&&TryRead32(location,0x2C,out UInt32 subsystems)){subsystemVendor=(UInt16)subsystems;subsystem=(UInt16)(subsystems>>16);}
        UInt32 classCode=classRevision>>8;Byte revision=(Byte)classRevision;KernelDeviceIdentifier identifier=new(KernelDeviceBus.Pci,vendor,device,subsystemVendor,subsystem,classCode,revision,location.Encode());if(!KernelDrivers.RegisterDevice(identifier,out KernelDeviceHandle handle))return false;
        Byte barMaximum=header==0?(Byte)6:header==1?(Byte)2:(Byte)0;for(Byte barIndex=0;barIndex<barMaximum;barIndex++){if(!TryGetBar(location,barIndex,out PciBarInfo bar))continue;KernelDeviceResourceType resourceType=bar.Type==PciBarType.Io?KernelDeviceResourceType.IoPort:KernelDeviceResourceType.Memory;UInt64 flags=((UInt64)bar.Index)|((UInt64)bar.Type<<8)|(bar.Prefetchable?1UL<<16:0UL);if(!KernelDrivers.AddResource(handle,new KernelDeviceResource(resourceType,bar.Address,bar.Length,flags)))return false;if(bar.Type==PciBarType.Memory64)barIndex++;}
        Boolean hasInterruptResource=false;
        if(TryRead8(location,0x3C,out Byte interruptLine)&&interruptLine!=0xFFU&&interruptLine!=0U){if(!KernelDrivers.AddResource(handle,new KernelDeviceResource(KernelDeviceResourceType.Interrupt,interruptLine,0UL,0UL)))return false;hasInterruptResource=true;}
        if(!KernelDrivers.AddResource(handle,new KernelDeviceResource(KernelDeviceResourceType.Dma,0UL,0UL,0UL)))return false;
        if(!AddDeviceRecord(location,transport,handle,vendor,device,subsystemVendor,subsystem,classCode,revision,header))return false;
        if(!hasInterruptResource&&(TryGetMsiCapability(location,out _)||TryGetMsixCapability(location,out _)))if(!KernelDrivers.AddResource(handle,new KernelDeviceResource(KernelDeviceResourceType.Interrupt,0UL,0UL,1UL)))return false;
        return true;
    }

    private static Boolean AddDeviceRecord(PciLocation location,PciConfigurationTransport transport,KernelDeviceHandle handle,UInt16 vendor,UInt16 device,UInt16 subsystemVendor,UInt16 subsystem,UInt32 classCode,Byte revision,Byte header)
    {
        Int32 slot=FreeDevice();if(slot<0){if(!GrowDevices())return false;slot=FreeDevice();if(slot<0)return false;}DeviceRecord* r=_devices+slot;Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));r->Used=1;r->Transport=(Byte)transport;r->Segment=location.Segment;r->Bus=location.Bus;r->Device=location.Device;r->Function=location.Function;r->VendorId=vendor;r->DeviceId=device;r->SubsystemVendorId=subsystemVendor;r->SubsystemId=subsystem;r->ClassCode=classCode;r->Revision=revision;r->HeaderType=header;r->Handle=handle.Value;_deviceCount++;return true;
    }
}
