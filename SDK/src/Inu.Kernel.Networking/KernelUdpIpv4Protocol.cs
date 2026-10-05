using System;
namespace Inu.Kernel.Networking;

/// <summary>Selectable UDP-over-IPv4 framing and delivery component.</summary>
public static unsafe class KernelUdpIpv4Protocol
{
    public static Boolean Register()=>KernelIpv4TransportServices.RegisterUdp(&Receive,&Send);

    private static Boolean Receive(KernelNetworkInterfaceHandle networkInterface,KernelNetworkInterfaceInfo info,KernelIpv4Address source,KernelIpv4Address destination,Byte* payload,UInt32 length)
    {
        _=networkInterface;_=info;
        if(length<8)return false;
        UInt16 sourcePort=KernelNetworkMath.ReadUInt16Network(payload);UInt16 destinationPort=KernelNetworkMath.ReadUInt16Network(payload+2);
        UInt16 udpLength=KernelNetworkMath.ReadUInt16Network(payload+4);if(udpLength<8||udpLength>length)return false;
        return KernelSocketServices.DeliverUdp(new(source,sourcePort),new(destination,destinationPort),payload+8,(UInt32)udpLength-8U);
    }

    private static Boolean Send(KernelIpv4Endpoint source,KernelIpv4Endpoint destination,Byte* payload,UInt32 length)
    {
        if(payload==null||length==0||length>65507U)return false;
        UInt32 udpLength=8U+length;Byte* udp=stackalloc Byte[(Int32)udpLength];
        KernelNetworkMath.WriteUInt16Network(udp,2,source.Port);KernelNetworkMath.WriteUInt16Network(udp+2,2,destination.Port);
        KernelNetworkMath.WriteUInt16Network(udp+4,2,(UInt16)udpLength);udp[6]=0;udp[7]=0;
        for(UInt32 i=0;i<length;i++)udp[8+i]=payload[i];
        return KernelIpv4Services.SendRouted(source.Address,destination.Address,(Byte)KernelNetworkProtocol.Udp,udp,udpLength);
    }
}
