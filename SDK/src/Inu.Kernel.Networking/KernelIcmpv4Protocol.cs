using System;
namespace Inu.Kernel.Networking;

/// <summary>Selectable ICMPv4 control protocol component.</summary>
public static unsafe class KernelIcmpv4Protocol
{
    public static Boolean Register()=>KernelIpv4TransportServices.RegisterIcmpv4(&Receive,&SendEcho);

    private static Boolean Receive(KernelNetworkInterfaceHandle networkInterface,KernelNetworkInterfaceInfo info,KernelIpv4Address source,KernelIpv4Address destination,Byte* payload,UInt32 length)
    {
        if(length<8||KernelNetworkMath.InternetChecksum(payload,length)!=0)return false;
        if(payload[0]!=8||payload[1]!=0)return true;
        Byte* reply=stackalloc Byte[(Int32)length];for(UInt32 i=0;i<length;i++)reply[i]=payload[i];
        reply[0]=0;reply[2]=0;reply[3]=0;
        KernelNetworkMath.WriteUInt16Network(reply+2,2,KernelNetworkMath.InternetChecksum(reply,length));
        return KernelIpv4Services.SendOnInterface(networkInterface,destination,source,(Byte)KernelNetworkProtocol.Icmp,reply,length);
    }

    private static Boolean SendEcho(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address destination,UInt16 identifier,UInt16 sequence,Byte* payload,UInt32 length)
    {
        if(payload==null&&length!=0)return false;
        UInt32 icmpLength=8U+length;if(icmpLength>65515U)return false;
        Byte* icmp=stackalloc Byte[(Int32)icmpLength];for(UInt32 i=0;i<icmpLength;i++)icmp[i]=0;icmp[0]=8;
        KernelNetworkMath.WriteUInt16Network(icmp+4,2,identifier);KernelNetworkMath.WriteUInt16Network(icmp+6,2,sequence);
        for(UInt32 i=0;i<length;i++)icmp[8+i]=payload[i];
        KernelNetworkMath.WriteUInt16Network(icmp+2,2,KernelNetworkMath.InternetChecksum(icmp,icmpLength));
        return KernelIpv4Services.SendOnInterface(networkInterface,default,destination,(Byte)KernelNetworkProtocol.Icmp,icmp,icmpLength);
    }
}
