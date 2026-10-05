using System;
namespace Inu.Kernel.Networking;

/// <summary>Selectable IPv6 receive and neighbour-discovery component.</summary>
public static unsafe class KernelIpv6NdpProtocol
{
    public static Boolean Register()=>KernelNetworkPacketServices.RegisterIpv6(&ReceiveIpv6,&ReceiveNdp);

    private static Boolean ReceiveIpv6(KernelNetworkInterfaceHandle networkInterface,Byte* packet,UInt32 length)
    {
        if(packet==null||length<40||(packet[0]>>4)!=6)return false;
        UInt16 payloadLength=KernelNetworkMath.ReadUInt16Network(packet+4);
        if((UInt32)payloadLength+40U>length)return false;
        KernelIpv6Address source=KernelNetworkMath.ReadIpv6Address(packet+8);
        KernelIpv6Address destination=KernelNetworkMath.ReadIpv6Address(packet+24);
        if(source.IsUnspecified||destination.IsUnspecified)return false;
        Byte nextHeader=packet[6];
        if(nextHeader==58)return ReceiveNdp(networkInterface,packet,length);
        return true;
    }

    private static Boolean ReceiveNdp(KernelNetworkInterfaceHandle networkInterface,Byte* ipv6Packet,UInt32 length)
    {
        if(ipv6Packet==null||length<48||(ipv6Packet[0]>>4)!=6||ipv6Packet[6]!=58)return false;
        Byte* icmp=ipv6Packet+40;UInt32 icmpLength=length-40;if(icmpLength<8)return false;
        Byte type=icmp[0];if(type!=135&&type!=136)return true;if(icmpLength<24)return false;
        KernelIpv6Address target=KernelNetworkMath.ReadIpv6Address(icmp+8);if(target.IsUnspecified)return false;
        UInt32 at=24;
        while(at+2<=icmpLength)
        {
            Byte optionType=icmp[at];Byte units=icmp[at+1];UInt32 optionLength=(UInt32)units*8U;
            if(units==0||at+optionLength>icmpLength)return false;
            if((optionType==1||optionType==2)&&optionLength>=8)
            {
                KernelMacAddress mac=new(icmp[at+2],icmp[at+3],icmp[at+4],icmp[at+5],icmp[at+6],icmp[at+7]);
                return KernelNetworking.UpdateIpv6Neighbor(networkInterface,target,mac);
            }
            at+=optionLength;
        }
        return true;
    }
}
