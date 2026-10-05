using System;
namespace Inu.Kernel.Networking;

/// <summary>Selectable IPv4 network-layer component. Control and transport protocols register independently.</summary>
public static unsafe class KernelIpv4Protocol
{
    public static Boolean Register()
    {
        if(!KernelIpv4Services.Register(&SendRouted,&SendOnInterface))return false;
        return KernelNetworkPacketServices.RegisterIpv4(&Receive);
    }

    private static Boolean Receive(KernelNetworkInterfaceHandle networkInterface,KernelNetworkInterfaceInfo info,Byte* ip,UInt32 length)
    {
        if(length<20||(ip[0]>>4)!=4)return false;
        UInt32 header=(UInt32)(ip[0]&0x0F)*4U;if(header<20||header>length)return false;
        UInt16 total=KernelNetworkMath.ReadUInt16Network(ip+2);if(total<header||total>length)return false;
        if(KernelNetworkMath.InternetChecksum(ip,header)!=0)return false;
        KernelIpv4Address source=new(KernelNetworkMath.ReadUInt32Network(ip+12));
        KernelIpv4Address destination=new(KernelNetworkMath.ReadUInt32Network(ip+16));
        if(destination.Value!=info.Address.Value&&destination.Value!=0xFFFFFFFFU)return false;
        return KernelIpv4TransportServices.Receive(ip[9],networkInterface,info,source,destination,ip+header,(UInt32)total-header);
    }

    private static Boolean SendRouted(KernelIpv4Address source,KernelIpv4Address destination,Byte protocol,Byte* payload,UInt32 length)
    {
        if(payload==null&&length!=0)return false;
        if(!KernelNetworking.TryResolveRoute(destination,out KernelNetworkRoute route))return false;
        if(!KernelNetworking.TryGetInterface(route.Interface,out KernelNetworkInterfaceInfo info)||info.State!=KernelNetworkInterfaceState.Up)return false;
        KernelIpv4Address nextHop=route.Gateway.Value!=0?route.Gateway:destination;
        return BuildAndTransmit(route.Interface,info,nextHop,source.Value!=0?source:info.Address,destination,protocol,payload,length);
    }

    private static Boolean SendOnInterface(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address source,KernelIpv4Address destination,Byte protocol,Byte* payload,UInt32 length)
    {
        if(payload==null&&length!=0)return false;
        if(!KernelNetworking.TryGetInterface(networkInterface,out KernelNetworkInterfaceInfo info)||info.State!=KernelNetworkInterfaceState.Up)return false;
        return BuildAndTransmit(networkInterface,info,destination,source.Value!=0?source:info.Address,destination,protocol,payload,length);
    }

    private static Boolean BuildAndTransmit(KernelNetworkInterfaceHandle networkInterface,KernelNetworkInterfaceInfo info,KernelIpv4Address nextHop,KernelIpv4Address source,KernelIpv4Address destination,Byte protocol,Byte* payload,UInt32 length)
    {
        if(!KernelNetworking.TryResolveNeighbor(networkInterface,nextHop,out KernelMacAddress target))return false;
        UInt32 total=14U+20U+length;if(total>info.Mtu+14U||total>65535U)return false;
        Byte* frame=stackalloc Byte[(Int32)total];WriteMac(frame,target);WriteMac(frame+6,info.MacAddress);
        KernelNetworkMath.WriteUInt16Network(frame+12,2,0x0800);
        Byte* ip=frame+14;for(Int32 i=0;i<20;i++)ip[i]=0;ip[0]=0x45;
        KernelNetworkMath.WriteUInt16Network(ip+2,2,(UInt16)(20U+length));ip[8]=64;ip[9]=protocol;
        KernelNetworkMath.WriteUInt32Network(ip+12,4,source.Value);KernelNetworkMath.WriteUInt32Network(ip+16,4,destination.Value);
        KernelNetworkMath.WriteUInt16Network(ip+10,2,KernelNetworkMath.InternetChecksum(ip,20));
        for(UInt32 i=0;i<length;i++)ip[20+i]=payload[i];
        return KernelNetworking.Transmit(networkInterface,frame,total);
    }

    private static void WriteMac(Byte* p,KernelMacAddress m){p[0]=m.A;p[1]=m.B;p[2]=m.C;p[3]=m.D;p[4]=m.E;p[5]=m.F;}
}
