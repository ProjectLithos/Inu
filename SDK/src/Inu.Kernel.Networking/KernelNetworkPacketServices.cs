using System;

namespace Inu.Kernel.Networking;

/// <summary>Neutral registry for optional Ethernet/network-layer protocol components.</summary>
public static unsafe class KernelNetworkPacketServices
{
    private static delegate*<KernelNetworkInterfaceHandle,KernelNetworkInterfaceInfo,Byte*,UInt32,Boolean> _receiveArp;
    private static delegate*<KernelNetworkInterfaceHandle,KernelNetworkInterfaceInfo,Byte*,UInt32,Boolean> _receiveIpv4;
    private static delegate*<KernelNetworkInterfaceHandle,Byte*,UInt32,Boolean> _receiveIpv6;
    private static delegate*<KernelNetworkInterfaceHandle,Byte*,UInt32,Boolean> _receiveNdp;

    public static Boolean HasArp => _receiveArp != null;
    public static Boolean HasIpv4 => _receiveIpv4 != null;
    public static Boolean HasIpv6 => _receiveIpv6 != null;
    public static Boolean HasNdp => _receiveNdp != null;

    public static Boolean RegisterArp(delegate*<KernelNetworkInterfaceHandle,KernelNetworkInterfaceInfo,Byte*,UInt32,Boolean> receive)
    {
        if(receive==null||_receiveArp!=null)return false;
        _receiveArp=receive;
        return true;
    }

    public static Boolean RegisterIpv4(delegate*<KernelNetworkInterfaceHandle,KernelNetworkInterfaceInfo,Byte*,UInt32,Boolean> receive)
    {
        if(receive==null||_receiveIpv4!=null)return false;
        _receiveIpv4=receive;return true;
    }

    public static Boolean RegisterIpv6(
        delegate*<KernelNetworkInterfaceHandle,Byte*,UInt32,Boolean> receive,
        delegate*<KernelNetworkInterfaceHandle,Byte*,UInt32,Boolean> receiveNdp)
    {
        if(receive==null||receiveNdp==null||_receiveIpv6!=null)return false;
        _receiveIpv6=receive;_receiveNdp=receiveNdp;
        return true;
    }

    internal static Boolean ReceiveArp(KernelNetworkInterfaceHandle h,KernelNetworkInterfaceInfo info,Byte* packet,UInt32 length)
        =>_receiveArp!=null&&_receiveArp(h,info,packet,length);
    internal static Boolean ReceiveIpv4(KernelNetworkInterfaceHandle h,KernelNetworkInterfaceInfo info,Byte* packet,UInt32 length)
        =>_receiveIpv4!=null&&_receiveIpv4(h,info,packet,length);
    internal static Boolean ReceiveIpv6(KernelNetworkInterfaceHandle h,Byte* packet,UInt32 length)
        =>_receiveIpv6!=null&&_receiveIpv6(h,packet,length);
    internal static Boolean ReceiveNdp(KernelNetworkInterfaceHandle h,Byte* packet,UInt32 length)
        =>_receiveNdp!=null&&_receiveNdp(h,packet,length);
}
