using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Networking;
using Inu.Kernel.Pci;

namespace Inu.Kernel.E1000;

/// <summary>Identifies the supported Intel gigabit controller generation.</summary>
public enum E1000ControllerFamily : Byte { Unknown=0, E1000=1, E1000e=2 }

/// <summary>Describes one started Intel E1000/E1000e network controller.</summary>
public readonly struct E1000DeviceInfo
{
    public E1000DeviceInfo(KernelDeviceHandle device,PciLocation location,E1000ControllerFamily family,KernelMacAddress mac,KernelNetworkInterfaceHandle networkInterface,UInt32 mtu,Boolean msiCapable,Boolean msixCapable)
    {Device=device;Location=location;Family=family;MacAddress=mac;NetworkInterface=networkInterface;Mtu=mtu;MsiCapable=msiCapable;MsixCapable=msixCapable;}
    public KernelDeviceHandle Device { get; }
    public PciLocation Location { get; }
    public E1000ControllerFamily Family { get; }
    public KernelMacAddress MacAddress { get; }
    public KernelNetworkInterfaceHandle NetworkInterface { get; }
    public UInt32 Mtu { get; }
    public Boolean MsiCapable { get; }
    public Boolean MsixCapable { get; }
}

/// <summary>Summarizes Intel E1000/E1000e driver state.</summary>
public readonly struct E1000Capabilities
{
    public E1000Capabilities(Boolean initialized,UInt32 controllers,UInt32 interfaces,UInt32 rxDescriptors,UInt32 txDescriptors)
    {Initialized=initialized;Controllers=controllers;Interfaces=interfaces;ReceiveDescriptors=rxDescriptors;TransmitDescriptors=txDescriptors;}
    public Boolean Initialized { get; }
    public UInt32 Controllers { get; }
    public UInt32 Interfaces { get; }
    public UInt32 ReceiveDescriptors { get; }
    public UInt32 TransmitDescriptors { get; }
}
