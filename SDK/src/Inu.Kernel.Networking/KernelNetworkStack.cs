using System;
namespace Inu.Kernel.Networking;

/// <summary>Ethernet framing facade and dispatch point for independently selected network-layer protocols.</summary>
public static unsafe class KernelNetworkStack
{
    private const UInt16 EtherTypeIpv4=0x0800;
    private const UInt16 EtherTypeArp=0x0806;
    private const UInt16 EtherTypeIpv6=0x86DD;

    public static Boolean ReceiveEthernet(KernelNetworkInterfaceHandle networkInterface,Byte* frame,UInt32 length)
    {
        if(frame==null||length<14||!KernelNetworking.TryGetInterface(networkInterface,out KernelNetworkInterfaceInfo info))return false;
        UInt16 type=KernelNetworkMath.ReadUInt16Network(frame+12);
        if(type==EtherTypeArp)return KernelNetworkPacketServices.ReceiveArp(networkInterface,info,frame+14,length-14);
        if(type==EtherTypeIpv4)return KernelNetworkPacketServices.ReceiveIpv4(networkInterface,info,frame+14,length-14);
        if(type==EtherTypeIpv6)return KernelNetworkPacketServices.ReceiveIpv6(networkInterface,frame+14,length-14);
        return false;
    }

    public static Boolean SendUdp(KernelIpv4Endpoint source,KernelIpv4Endpoint destination,Byte* payload,UInt32 length)
        =>KernelIpv4TransportServices.SendUdp(source,destination,payload,length);

    public static Boolean ReceiveNdp(KernelNetworkInterfaceHandle networkInterface,Byte* ipv6Packet,UInt32 length)
        =>KernelNetworkPacketServices.ReceiveNdp(networkInterface,ipv6Packet,length);

    public static Boolean SendIcmpEchoIpv4(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address destination,UInt16 identifier,UInt16 sequence,Byte* payload,UInt32 length)
        =>KernelIpv4TransportServices.SendIcmpEcho(networkInterface,destination,identifier,sequence,payload,length);
}
