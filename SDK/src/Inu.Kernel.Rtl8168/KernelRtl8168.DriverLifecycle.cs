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
    private static Boolean Probe(KernelDriverDeviceContext* context){if(context==null||!Rtl8168Math.IsSupported(context->Identifier.VendorId,context->Identifier.DeviceId)||!KernelPci.TryGetDevice(context->Device,out PciDeviceInfo pci))return false;return (pci.ClassCode&0xFF0000U)==0x020000U;}
    private static Boolean Start(KernelDriverDeviceContext* context)
    {
        if(context==null||!KernelPci.TryGetDevice(context->Device,out PciDeviceInfo pci))return false;Rtl8168ControllerFamily family=Rtl8168Math.Identify(pci.VendorId,pci.DeviceId);if(family==Rtl8168ControllerFamily.Unknown)return false;Int32 slot=FreeRecord();if(slot<0){if(!GrowRecords())return false;slot=FreeRecord();if(slot<0)return false;}DeviceRecord* r=_devices+slot;Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));r->Used=1;r->Family=(Byte)family;r->DeviceHandle=context->Device.Value;r->Segment=pci.Location.Segment;r->Bus=pci.Location.Bus;r->PciDevice=pci.Location.Device;r->Function=pci.Location.Function;r->Mtu=DefaultMtu;
        if(!EnablePci(pci.Location)||!KernelPci.TryMapBar(pci.Location,2,out PciBarInfo bar,out r->Mmio)){if(!KernelPci.TryMapBar(pci.Location,0,out bar,out r->Mmio)){Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));return false;}}if(bar.Length<0x100UL||!Reset(r)||!ReadMac(r,out KernelMacAddress mac)||!AllocateRings(r)||!InitializeHardware(r)){ReleaseResources(r);Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));return false;}
        r->Msi=KernelPci.TryGetMsiCapability(pci.Location,out _)?(Byte)1:(Byte)0;KernelContextualNetworkInterfaceCallbacks cb=new(&Transmit,&SetReceiveEnabled);if(!KernelNetworking.RegisterInterface(context->Device,mac,DefaultMtu,cb,out KernelNetworkInterfaceHandle network)){ReleaseResources(r);Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));return false;}r->NetworkHandle=network.Value;r->ReceiveEnabled=1;r->Started=1;KernelDriverInterruptRequest interruptRequest=new(context->Device,0U,8,0U,false,false,0UL);if(KernelDrivers.TryRequestInterrupt(interruptRequest,out KernelDriverInterruptHandle interrupt)){r->InterruptHandle=interrupt.Value;Write16(r,InterruptMask,(UInt16)0x002F);}if(!KernelNetworking.SetInterfaceState(network,KernelNetworkInterfaceState.Up)){if(r->InterruptHandle!=0UL)KernelDrivers.ReleaseInterrupt(new KernelDriverInterruptHandle(r->InterruptHandle));KernelNetworking.UnregisterInterface(network);r->Started=0;ReleaseResources(r);Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));return false;}_count++;return true;
    }
    private static Boolean Stop(KernelDriverDeviceContext* context){if(context==null)return false;if(!TryRecord(context->Device,out DeviceRecord* r))return true;Write16(r,InterruptMask,0);if(r->InterruptHandle!=0UL){KernelDrivers.ReleaseInterrupt(new KernelDriverInterruptHandle(r->InterruptHandle));r->InterruptHandle=0UL;}Write8(r,ChipCommand,0);if(r->NetworkHandle!=0U&&!KernelNetworking.UnregisterInterface(new KernelNetworkInterfaceHandle(r->NetworkHandle)))return false;r->NetworkHandle=0U;r->Started=0;if(!ReleaseResources(r))return false;Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));if(_count!=0)_count--;return true;}
    private static Boolean Remove(KernelDriverDeviceContext* context)=>context!=null&&Stop(context);
    private static Boolean Interrupt(KernelDriverDeviceContext* context,UInt64 cookie)=>context!=null&&Service(context->Device);
}
