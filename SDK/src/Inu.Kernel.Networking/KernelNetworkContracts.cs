using System;
using Inu.Kernel.Drivers;

namespace Inu.Kernel.Networking;

public enum KernelNetworkRegistryMode : Byte { Dynamic=0, Fixed=1 }
public enum KernelNetworkInterfaceState : Byte { Down=0, Up=1 }
public enum KernelNetworkProtocol : Byte { Icmp=1, Tcp=6, Udp=17 }
public enum KernelSocketType : Byte { Datagram=1, Stream=2 }
public enum KernelSocketState : Byte { Closed=0, Created=1, Bound=2, Listening=3, Connecting=4, Connected=5 }
public enum KernelTcpState : Byte { Closed=0, Listen=1, SynSent=2, SynReceived=3, Established=4, FinWait1=5, FinWait2=6, CloseWait=7, Closing=8, LastAck=9, TimeWait=10 }
public enum KernelNetworkPacketState : Byte { Queued=1, Processed=2, Failed=3 }

public readonly struct KernelNetworkOptions
{
    public KernelNetworkOptions(KernelNetworkRegistryMode mode,UInt32 initialInterfaces,UInt32 initialRoutes,UInt32 initialNeighbors,UInt32 initialSockets,UInt32 initialPackets,UInt32 maximumInterfaces,UInt32 maximumRoutes,UInt32 maximumNeighbors,UInt32 maximumSockets,UInt32 maximumPackets)
    { RegistryMode=mode;InitialInterfaces=initialInterfaces;InitialRoutes=initialRoutes;InitialNeighbors=initialNeighbors;InitialSockets=initialSockets;InitialPackets=initialPackets;MaximumInterfaces=maximumInterfaces;MaximumRoutes=maximumRoutes;MaximumNeighbors=maximumNeighbors;MaximumSockets=maximumSockets;MaximumPackets=maximumPackets; }
    public KernelNetworkRegistryMode RegistryMode { get; } public UInt32 InitialInterfaces { get; } public UInt32 InitialRoutes { get; } public UInt32 InitialNeighbors { get; } public UInt32 InitialSockets { get; }
    public UInt32 InitialPackets { get; }
    public UInt32 MaximumInterfaces { get; } public UInt32 MaximumRoutes { get; } public UInt32 MaximumNeighbors { get; } public UInt32 MaximumSockets { get; } public UInt32 MaximumPackets { get; }
    public static KernelNetworkOptions DynamicDefault => new(KernelNetworkRegistryMode.Dynamic,8U,16U,64U,128U,64U,UInt32.MaxValue,UInt32.MaxValue,UInt32.MaxValue,UInt32.MaxValue,UInt32.MaxValue);
    public static KernelNetworkOptions Fixed(UInt32 interfaces,UInt32 routes,UInt32 neighbors,UInt32 sockets)=>new(KernelNetworkRegistryMode.Fixed,interfaces,routes,neighbors,sockets,sockets,interfaces,routes,neighbors,sockets,sockets);
}

public readonly struct KernelNetworkInterfaceHandle { public KernelNetworkInterfaceHandle(UInt32 value){Value=value;} public UInt32 Value { get; } }
public readonly struct KernelSocketHandle { public KernelSocketHandle(UInt32 value){Value=value;} public UInt32 Value { get; } }

public readonly struct KernelMacAddress
{
    public KernelMacAddress(Byte a,Byte b,Byte c,Byte d,Byte e,Byte f){A=a;B=b;C=c;D=d;E=e;F=f;}
    public Byte A { get; } public Byte B { get; } public Byte C { get; } public Byte D { get; } public Byte E { get; } public Byte F { get; }
    public Boolean IsZero => (A|B|C|D|E|F)==0;
}

public readonly struct KernelIpv4Address
{
    public KernelIpv4Address(UInt32 value){Value=value;}
    public UInt32 Value { get; }
    public Byte A => (Byte)(Value>>24); public Byte B => (Byte)(Value>>16); public Byte C => (Byte)(Value>>8); public Byte D => (Byte)Value;
    public static KernelIpv4Address FromBytes(Byte a,Byte b,Byte c,Byte d)=>new(((UInt32)a<<24)|((UInt32)b<<16)|((UInt32)c<<8)|d);
}


public readonly struct KernelIpv6Address
{
    public KernelIpv6Address(UInt64 high,UInt64 low){High=high;Low=low;}
    public UInt64 High { get; } public UInt64 Low { get; }
    public Boolean IsUnspecified => High==0&&Low==0;
}

public readonly struct KernelNetworkPacketHandle { public KernelNetworkPacketHandle(UInt32 value){Value=value;} public UInt32 Value { get; } }
public readonly struct KernelNetworkPacketInfo
{
    public KernelNetworkPacketInfo(KernelNetworkPacketHandle handle,KernelNetworkInterfaceHandle networkInterface,KernelNetworkPacketState state,UInt32 length){Handle=handle;Interface=networkInterface;State=state;Length=length;}
    public KernelNetworkPacketHandle Handle { get; } public KernelNetworkInterfaceHandle Interface { get; } public KernelNetworkPacketState State { get; } public UInt32 Length { get; }
}

public readonly struct KernelIpv4Endpoint
{
    public KernelIpv4Endpoint(KernelIpv4Address address,UInt16 port){Address=address;Port=port;}
    public KernelIpv4Address Address { get; } public UInt16 Port { get; }
}

public readonly struct KernelNetworkRoute
{
    public KernelNetworkRoute(KernelIpv4Address network,KernelIpv4Address mask,KernelIpv4Address gateway,KernelNetworkInterfaceHandle networkInterface,UInt32 metric)
    { Network=network;Mask=mask;Gateway=gateway;Interface=networkInterface;Metric=metric; }
    public KernelIpv4Address Network { get; } public KernelIpv4Address Mask { get; } public KernelIpv4Address Gateway { get; } public KernelNetworkInterfaceHandle Interface { get; } public UInt32 Metric { get; }
}

public readonly struct KernelNetworkInterfaceInfo
{
    public KernelNetworkInterfaceInfo(KernelNetworkInterfaceHandle handle,KernelDeviceHandle device,KernelMacAddress mac,KernelIpv4Address address,KernelIpv4Address mask,KernelIpv4Address gateway,UInt32 mtu,KernelNetworkInterfaceState state)
    { Handle=handle;Device=device;MacAddress=mac;Address=address;Mask=mask;Gateway=gateway;Mtu=mtu;State=state; }
    public KernelNetworkInterfaceHandle Handle { get; } public KernelDeviceHandle Device { get; } public KernelMacAddress MacAddress { get; } public KernelIpv4Address Address { get; } public KernelIpv4Address Mask { get; } public KernelIpv4Address Gateway { get; } public UInt32 Mtu { get; } public KernelNetworkInterfaceState State { get; }
}

public readonly struct KernelSocketInfo
{
    public KernelSocketInfo(KernelSocketHandle handle,KernelSocketType type,KernelSocketState state,KernelIpv4Endpoint local,KernelIpv4Endpoint remote,KernelTcpState tcpState)
    { Handle=handle;Type=type;State=state;Local=local;Remote=remote;TcpState=tcpState; }
    public KernelSocketHandle Handle { get; } public KernelSocketType Type { get; } public KernelSocketState State { get; } public KernelIpv4Endpoint Local { get; } public KernelIpv4Endpoint Remote { get; } public KernelTcpState TcpState { get; }
}

public readonly struct KernelNetworkCapabilities
{
    public KernelNetworkCapabilities(Boolean initialized,KernelNetworkRegistryMode mode,UInt32 interfaces,UInt32 routes,UInt32 neighbors,UInt32 sockets,UInt32 interfaceCapacity,UInt32 routeCapacity,UInt32 neighborCapacity,UInt32 socketCapacity)
    { Initialized=initialized;RegistryMode=mode;Interfaces=interfaces;Routes=routes;Neighbors=neighbors;Sockets=sockets;InterfaceCapacity=interfaceCapacity;RouteCapacity=routeCapacity;NeighborCapacity=neighborCapacity;SocketCapacity=socketCapacity; }
    public Boolean Initialized { get; } public KernelNetworkRegistryMode RegistryMode { get; } public UInt32 Interfaces { get; } public UInt32 Routes { get; } public UInt32 Neighbors { get; } public UInt32 Sockets { get; }
    public UInt32 InterfaceCapacity { get; } public UInt32 RouteCapacity { get; } public UInt32 NeighborCapacity { get; } public UInt32 SocketCapacity { get; }
}

public readonly unsafe struct KernelNetworkInterfaceCallbacks
{
    public readonly delegate*<Byte*,UInt32,Boolean> TransmitFrame;
    public readonly delegate*<Boolean,Boolean> SetReceiveEnabled;
    public KernelNetworkInterfaceCallbacks(delegate*<Byte*,UInt32,Boolean> transmitFrame,delegate*<Boolean,Boolean> setReceiveEnabled){TransmitFrame=transmitFrame;SetReceiveEnabled=setReceiveEnabled;}
}

/// <summary>Provides network callbacks that receive the owning generic device, allowing one driver implementation to serve multiple interfaces.</summary>
public readonly unsafe struct KernelContextualNetworkInterfaceCallbacks
{
    /// <summary>Transmits one Ethernet frame for the supplied generic device.</summary>
    public readonly delegate*<KernelDeviceHandle,Byte*,UInt32,Boolean> TransmitFrame;
    /// <summary>Changes receive enablement for the supplied generic device.</summary>
    public readonly delegate*<KernelDeviceHandle,Boolean,Boolean> SetReceiveEnabled;
    /// <summary>Creates contextual network-interface callbacks.</summary>
    public KernelContextualNetworkInterfaceCallbacks(delegate*<KernelDeviceHandle,Byte*,UInt32,Boolean> transmitFrame,delegate*<KernelDeviceHandle,Boolean,Boolean> setReceiveEnabled){TransmitFrame=transmitFrame;SetReceiveEnabled=setReceiveEnabled;}
}
