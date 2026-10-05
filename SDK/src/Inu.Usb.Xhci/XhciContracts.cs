using System;
using Inu.Kernel.Pci;
namespace Inu.Usb.Xhci;
public enum XhciControllerState:Byte{Unknown=0,Discovered=1,Reset=2,Running=3,Failed=4}
public readonly struct XhciControllerInfo{public XhciControllerInfo(PciLocation location,UInt64 mmio,Byte ports,Byte slots,XhciControllerState state){Location=location;MmioBase=mmio;RootPorts=ports;MaximumSlots=slots;State=state;}public PciLocation Location{get;}public UInt64 MmioBase{get;}public Byte RootPorts{get;}public Byte MaximumSlots{get;}public XhciControllerState State{get;}}
public readonly struct XhciCapabilities{public XhciCapabilities(Boolean initialized,UInt32 controllers,UInt32 running,UInt32 connectedPorts,Boolean transferRings){Initialized=initialized;Controllers=controllers;RunningControllers=running;ConnectedPorts=connectedPorts;TransferRings=transferRings;}public Boolean Initialized{get;}public UInt32 Controllers{get;}public UInt32 RunningControllers{get;}public UInt32 ConnectedPorts{get;}public Boolean TransferRings{get;}}
