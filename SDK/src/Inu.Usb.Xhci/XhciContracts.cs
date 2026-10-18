using System;
using Inu.Kernel.Pci;
namespace Inu.Usb.Xhci;
public enum XhciControllerState:Byte{Unknown=0,Discovered=1,Reset=2,Running=3,Failed=4}
public readonly struct XhciControllerInfo{public XhciControllerInfo(PciLocation location,UInt64 mmio,Byte ports,Byte slots,XhciControllerState state){Location=location;MmioBase=mmio;RootPorts=ports;MaximumSlots=slots;State=state;}public PciLocation Location{get;}public UInt64 MmioBase{get;}public Byte RootPorts{get;}public Byte MaximumSlots{get;}public XhciControllerState State{get;}}
public readonly struct XhciCapabilities
{
 public XhciCapabilities(Boolean initialized,UInt32 controllers,UInt32 running,UInt32 connectedPorts,Boolean transferRings):this(initialized,controllers,running,connectedPorts,transferRings,0U,0U,0U,0U){}
 public XhciCapabilities(Boolean initialized,UInt32 controllers,UInt32 running,UInt32 connectedPorts,Boolean transferRings,UInt32 interruptConfiguredControllers,UInt32 interruptPreparedControllers,UInt32 interruptDrivenControllers,UInt32 pollingFallbackControllers){Initialized=initialized;Controllers=controllers;RunningControllers=running;ConnectedPorts=connectedPorts;TransferRings=transferRings;InterruptConfiguredControllers=interruptConfiguredControllers;InterruptPreparedControllers=interruptPreparedControllers;InterruptDrivenControllers=interruptDrivenControllers;PollingFallbackControllers=pollingFallbackControllers;}
 public Boolean Initialized{get;}public UInt32 Controllers{get;}public UInt32 RunningControllers{get;}public UInt32 ConnectedPorts{get;}public Boolean TransferRings{get;}public UInt32 InterruptConfiguredControllers{get;}public UInt32 InterruptPreparedControllers{get;}public UInt32 InterruptDrivenControllers{get;}public UInt32 PollingFallbackControllers{get;}
}
