using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Networking;
using Inu.Kernel.Pci;

namespace Inu.Kernel.Rtl8168;

/// <summary>Identifies a supported Realtek PCIe gigabit controller family.</summary>
public enum Rtl8168ControllerFamily : Byte { Unknown=0, Rtl8168Or8111=1, Rtl8169=2 }

/// <summary>Describes one started RTL8168/RTL8111-class network controller.</summary>
public readonly struct Rtl8168DeviceInfo
{
    public Rtl8168DeviceInfo(KernelDeviceHandle device,PciLocation location,Rtl8168ControllerFamily family,KernelMacAddress mac,KernelNetworkInterfaceHandle networkInterface,UInt32 mtu,Boolean msiCapable)
    {Device=device;Location=location;Family=family;MacAddress=mac;NetworkInterface=networkInterface;Mtu=mtu;MsiCapable=msiCapable;}
    public KernelDeviceHandle Device { get; }
    public PciLocation Location { get; }
    public Rtl8168ControllerFamily Family { get; }
    public KernelMacAddress MacAddress { get; }
    public KernelNetworkInterfaceHandle NetworkInterface { get; }
    public UInt32 Mtu { get; }
    public Boolean MsiCapable { get; }
}

/// <summary>Summarizes Realtek RTL8168/RTL8111 driver state.</summary>
public readonly struct Rtl8168Capabilities
{
    public Rtl8168Capabilities(Boolean initialized,UInt32 controllers,UInt32 interfaces,UInt32 rxDescriptors,UInt32 txDescriptors)
    {Initialized=initialized;Controllers=controllers;Interfaces=interfaces;ReceiveDescriptors=rxDescriptors;TransmitDescriptors=txDescriptors;}
    public Boolean Initialized { get; }
    public UInt32 Controllers { get; }
    public UInt32 Interfaces { get; }
    public UInt32 ReceiveDescriptors { get; }
    public UInt32 TransmitDescriptors { get; }
}
