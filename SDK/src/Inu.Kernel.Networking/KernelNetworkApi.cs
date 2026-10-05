using System;
using Inu.Kernel.Drivers;

namespace Inu.Kernel.Networking;

public enum KernelNetworkAddressFamily : Byte { Unspecified=0, Ipv4=4, Ipv6=6 }
public enum KernelNeighborProtocol : Byte { Arp=1, Ndp=2 }
public enum KernelIcmpFamily : Byte { Icmpv4=4, Icmpv6=6 }
public enum KernelDnsRecordType : UInt16 { A=1, Aaaa=28 }
public enum KernelNetworkLayerState : Byte { Disabled=0, Ready=1, Degraded=2 }

public readonly struct KernelIpv6Endpoint
{
    public KernelIpv6Endpoint(KernelIpv6Address address, UInt16 port){Address=address;Port=port;}
    public KernelIpv6Address Address { get; }
    public UInt16 Port { get; }
}

public readonly struct KernelNetworkEndpoint
{
    public KernelNetworkEndpoint(KernelIpv4Endpoint ipv4){Family=KernelNetworkAddressFamily.Ipv4;Ipv4=ipv4;Ipv6=default;}
    public KernelNetworkEndpoint(KernelIpv6Endpoint ipv6){Family=KernelNetworkAddressFamily.Ipv6;Ipv4=default;Ipv6=ipv6;}
    public KernelNetworkAddressFamily Family { get; }
    public KernelIpv4Endpoint Ipv4 { get; }
    public KernelIpv6Endpoint Ipv6 { get; }
}

public readonly struct KernelNicInfo
{
    public KernelNicInfo(KernelNetworkInterfaceHandle handle, KernelDeviceHandle device, KernelMacAddress macAddress, UInt32 mtu, KernelNetworkInterfaceState state)
    { Handle=handle;Device=device;MacAddress=macAddress;Mtu=mtu;State=state; }
    public KernelNetworkInterfaceHandle Handle { get; }
    public KernelDeviceHandle Device { get; }
    public KernelMacAddress MacAddress { get; }
    public UInt32 Mtu { get; }
    public KernelNetworkInterfaceState State { get; }
}

public readonly struct KernelEthernetHeader
{
    public KernelEthernetHeader(KernelMacAddress destination,KernelMacAddress source,UInt16 etherType){Destination=destination;Source=source;EtherType=etherType;}
    public KernelMacAddress Destination { get; }
    public KernelMacAddress Source { get; }
    public UInt16 EtherType { get; }
}

public readonly struct KernelNetworkStackCapabilities
{
    public KernelNetworkStackCapabilities(Boolean nic,Boolean ethernet,Boolean arp,Boolean ndp,Boolean ipv4,Boolean ipv6,Boolean icmp,Boolean udp,Boolean tcp,Boolean sockets,Boolean dns)
    { Nic=nic;Ethernet=ethernet;Arp=arp;Ndp=ndp;Ipv4=ipv4;Ipv6=ipv6;Icmp=icmp;Udp=udp;Tcp=tcp;Sockets=sockets;Dns=dns; }
    public Boolean Nic { get; } public Boolean Ethernet { get; } public Boolean Arp { get; } public Boolean Ndp { get; }
    public Boolean Ipv4 { get; } public Boolean Ipv6 { get; } public Boolean Icmp { get; } public Boolean Udp { get; }
    public Boolean Tcp { get; } public Boolean Sockets { get; } public Boolean Dns { get; }
}

public static unsafe class KernelNetworkApi
{
    public static KernelNetworkStackCapabilities GetCapabilities()=>new(
        true,
        true,
        KernelNetworkPacketServices.HasArp,
        KernelNetworkPacketServices.HasNdp,
        KernelNetworkPacketServices.HasIpv4,
        KernelNetworkPacketServices.HasIpv6,
        KernelIpv4TransportServices.HasIcmpv4,
        KernelIpv4TransportServices.HasUdp,
        KernelIpv4TransportServices.HasTcpObservation,
        KernelSocketServices.IsRegistered,
        KernelNetworkProtocolServices.HasDnsCodec);

    public static Boolean TryGetNic(KernelNetworkInterfaceHandle handle,out KernelNicInfo nic)
    {
        nic=default;
        if(!KernelNetworking.TryGetInterface(handle,out KernelNetworkInterfaceInfo info))return false;
        nic=new KernelNicInfo(info.Handle,info.Device,info.MacAddress,info.Mtu,info.State);return true;
    }

    public static Boolean ReceiveEthernet(KernelNetworkInterfaceHandle networkInterface,Byte* frame,UInt32 length)=>KernelNetworkStack.ReceiveEthernet(networkInterface,frame,length);
    public static Boolean ResolveArp(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address address,out KernelMacAddress mac)=>KernelNetworking.TryResolveNeighbor(networkInterface,address,out mac);
    public static Boolean UpdateArp(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address address,KernelMacAddress mac)=>KernelNetworking.UpdateNeighbor(networkInterface,address,mac);
    public static Boolean ConfigureIpv4(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address address,KernelIpv4Address mask,KernelIpv4Address gateway)=>KernelNetworking.ConfigureIpv4(networkInterface,address,mask,gateway);
    public static Boolean ConfigureIpv6(KernelNetworkInterfaceHandle networkInterface,KernelIpv6Address address,Byte prefixLength)=>KernelNetworking.ConfigureIpv6(networkInterface,address,prefixLength);
    public static Boolean TryGetIpv6Configuration(KernelNetworkInterfaceHandle networkInterface,out KernelIpv6Address address,out Byte prefixLength)=>KernelNetworking.TryGetIpv6Configuration(networkInterface,out address,out prefixLength);
    public static Boolean ProcessNdp(KernelNetworkInterfaceHandle networkInterface,Byte* ipv6Packet,UInt32 length)=>KernelNetworkStack.ReceiveNdp(networkInterface,ipv6Packet,length);

    public static Boolean SendIcmpEchoIpv4(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address destination,UInt16 identifier,UInt16 sequence,Byte* payload,UInt32 length)
        =>KernelNetworkStack.SendIcmpEchoIpv4(networkInterface,destination,identifier,sequence,payload,length);

    public static Boolean CreateSocket(KernelSocketType type,out KernelSocketHandle handle)=>KernelSocketServices.Create(type,out handle);
    public static Boolean Bind(KernelSocketHandle handle,KernelIpv4Endpoint endpoint)=>KernelSocketServices.Bind(handle,endpoint);
    public static Boolean Connect(KernelSocketHandle handle,KernelIpv4Endpoint endpoint)=>KernelSocketServices.Connect(handle,endpoint);
    public static Boolean Listen(KernelSocketHandle handle)=>KernelSocketServices.Listen(handle);
    public static Boolean SendUdp(KernelSocketHandle handle,KernelIpv4Endpoint destination,Byte* data,UInt32 length)=>KernelSocketServices.SendTo(handle,destination,data,length);
    public static Boolean Receive(KernelSocketHandle handle,Byte* buffer,UInt32 capacity,out UInt32 received)=>KernelSocketServices.Receive(handle,buffer,capacity,out received);
    public static Boolean Close(KernelSocketHandle handle)=>KernelSocketServices.Close(handle);

    public static Boolean BuildDhcpDiscover(Byte* buffer,UInt32 capacity,UInt32 transactionId,KernelMacAddress mac,out UInt32 length)=>KernelNetworkProtocolServices.BuildDhcpDiscover(buffer,capacity,transactionId,mac,out length);
    public static Boolean TryParseDhcpOffer(Byte* packet,UInt32 length,UInt32 transactionId,out KernelIpv4Address offeredAddress,out KernelIpv4Address serverAddress)=>KernelNetworkProtocolServices.TryParseDhcpOffer(packet,length,transactionId,out offeredAddress,out serverAddress);

    public static Boolean BuildDnsQuery(Byte* buffer,UInt32 capacity,UInt16 transactionId,String host,KernelDnsRecordType type,out UInt32 length)
        =>KernelNetworkProtocolServices.BuildDnsQuery(buffer,capacity,transactionId,host,type,out length);
    public static Boolean TryParseDnsAResponse(Byte* packet,UInt32 length,UInt16 transactionId,out KernelIpv4Address address)=>KernelNetworkProtocolServices.TryParseDnsAResponse(packet,length,transactionId,out address);
    public static Boolean TryParseDnsAaaaResponse(Byte* packet,UInt32 length,UInt16 transactionId,out KernelIpv6Address address)=>KernelNetworkProtocolServices.TryParseDnsAaaaResponse(packet,length,transactionId,out address);
}
