using System;
namespace Inu.Kernel.Networking;

/// <summary>Selectable ARP protocol component.</summary>
public static unsafe class KernelArpProtocol
{
    public static Boolean Register()=>KernelNetworkPacketServices.RegisterArp(&Receive);

    private static Boolean Receive(KernelNetworkInterfaceHandle h,KernelNetworkInterfaceInfo info,Byte* p,UInt32 length)
    {
        if(length<28||KernelNetworkMath.ReadUInt16Network(p)!=1||KernelNetworkMath.ReadUInt16Network(p+2)!=0x0800||p[4]!=6||p[5]!=4)return false;
        KernelIpv4Address sender=new(KernelNetworkMath.ReadUInt32Network(p+14));
        KernelMacAddress mac=new(p[8],p[9],p[10],p[11],p[12],p[13]);
        KernelNetworking.UpdateNeighbor(h,sender,mac);
        UInt16 op=KernelNetworkMath.ReadUInt16Network(p+6);
        KernelIpv4Address target=new(KernelNetworkMath.ReadUInt32Network(p+24));
        if(op!=1||target.Value!=info.Address.Value)return true;
        Byte* frame=stackalloc Byte[42];
        WriteMac(frame,mac);WriteMac(frame+6,info.MacAddress);
        KernelNetworkMath.WriteUInt16Network(frame+12,2,0x0806);
        Byte* a=frame+14;
        KernelNetworkMath.WriteUInt16Network(a,2,1);KernelNetworkMath.WriteUInt16Network(a+2,2,0x0800);
        a[4]=6;a[5]=4;KernelNetworkMath.WriteUInt16Network(a+6,2,2);
        WriteMac(a+8,info.MacAddress);KernelNetworkMath.WriteUInt32Network(a+14,4,info.Address.Value);
        WriteMac(a+18,mac);KernelNetworkMath.WriteUInt32Network(a+24,4,sender.Value);
        return KernelNetworking.Transmit(h,frame,42);
    }

    private static void WriteMac(Byte* p,KernelMacAddress m){p[0]=m.A;p[1]=m.B;p[2]=m.C;p[3]=m.D;p[4]=m.E;p[5]=m.F;}
}
