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
    private static Boolean Probe(KernelDriverDeviceContext* context){if(context==null||!E1000Math.IsSupported(context->Identifier.VendorId,context->Identifier.DeviceId)||!KernelPci.TryGetDevice(context->Device,out PciDeviceInfo pci))return false;return (pci.ClassCode&0xFF0000U)==0x020000U;}
    private static Boolean Start(KernelDriverDeviceContext* context)
    {
        if(context==null||!KernelPci.TryGetDevice(context->Device,out PciDeviceInfo pci))return false;E1000ControllerFamily family=E1000Math.Identify(pci.VendorId,pci.DeviceId);if(family==E1000ControllerFamily.Unknown)return false;Int32 slot=FreeRecord();if(slot<0){if(!GrowRecords())return false;slot=FreeRecord();if(slot<0)return false;}DeviceRecord* r=_devices+slot;Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));r->Used=1;r->Family=(Byte)family;r->DeviceHandle=context->Device.Value;r->Segment=pci.Location.Segment;r->Bus=pci.Location.Bus;r->PciDevice=pci.Location.Device;r->Function=pci.Location.Function;r->Mtu=DefaultMtu;
        if(!EnablePci(pci.Location)||!KernelPci.TryMapBar(pci.Location,0,out PciBarInfo bar,out r->Mmio)||bar.Length<0x6000UL||!Reset(r)||!ReadMac(r,out KernelMacAddress mac)||!AllocateRings(r)||!InitializeReceive(r)||!InitializeTransmit(r)){ReleaseResources(r);Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));return false;}
        r->Msi=KernelPci.TryGetMsiCapability(pci.Location,out _)?(Byte)1:(Byte)0;r->Msix=KernelPci.TryGetMsixCapability(pci.Location,out _)?(Byte)1:(Byte)0;KernelContextualNetworkInterfaceCallbacks cb=new(&Transmit,&SetReceiveEnabled);if(!KernelNetworking.RegisterInterface(context->Device,mac,DefaultMtu,cb,out KernelNetworkInterfaceHandle network)){ReleaseResources(r);Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));return false;}r->NetworkHandle=network.Value;r->ReceiveEnabled=1;r->Started=1;KernelDriverInterruptRequest interruptRequest=new(context->Device,0U,8,0U,false,false,0UL);if(KernelDrivers.TryRequestInterrupt(interruptRequest,out KernelDriverInterruptHandle interrupt)){r->InterruptHandle=interrupt.Value;Write32(r,Ims,0x000000D0U);}if(!KernelNetworking.SetInterfaceState(network,KernelNetworkInterfaceState.Up)){if(r->InterruptHandle!=0UL)KernelDrivers.ReleaseInterrupt(new KernelDriverInterruptHandle(r->InterruptHandle));KernelNetworking.UnregisterInterface(network);r->Started=0;ReleaseResources(r);Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));return false;}_count++;return true;
    }
    private static Boolean Stop(KernelDriverDeviceContext* context){if(context==null)return false;if(!TryRecord(context->Device,out DeviceRecord* r))return true;Write32(r,Imc,0xFFFFFFFFU);if(r->InterruptHandle!=0UL){KernelDrivers.ReleaseInterrupt(new KernelDriverInterruptHandle(r->InterruptHandle));r->InterruptHandle=0UL;}Write32(r,Rctl,Read32(r,Rctl)&~RctlEnable);Write32(r,Tctl,Read32(r,Tctl)&~TctlEnable);if(r->NetworkHandle!=0U&&!KernelNetworking.UnregisterInterface(new KernelNetworkInterfaceHandle(r->NetworkHandle)))return false;r->NetworkHandle=0U;r->Started=0;if(!ReleaseResources(r))return false;Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));if(_count!=0)_count--;return true;}
    private static Boolean Remove(KernelDriverDeviceContext* context)=>context!=null&&Stop(context);
    private static Boolean Interrupt(KernelDriverDeviceContext* context,UInt64 cookie){return context!=null&&Service(context->Device);}
}
