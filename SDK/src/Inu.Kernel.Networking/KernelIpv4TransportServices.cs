using System;
namespace Inu.Kernel.Networking;

/// <summary>Registry for optional IPv4 control/transport protocol components.</summary>
public static unsafe class KernelIpv4TransportServices
{
    private static delegate*<KernelNetworkInterfaceHandle,KernelNetworkInterfaceInfo,KernelIpv4Address,KernelIpv4Address,Byte*,UInt32,Boolean> _receiveIcmp;
    private static delegate*<KernelNetworkInterfaceHandle,KernelNetworkInterfaceInfo,KernelIpv4Address,KernelIpv4Address,Byte*,UInt32,Boolean> _receiveUdp;
    private static delegate*<KernelNetworkInterfaceHandle,KernelNetworkInterfaceInfo,KernelIpv4Address,KernelIpv4Address,Byte*,UInt32,Boolean> _receiveTcp;
    private static delegate*<KernelNetworkInterfaceHandle,KernelIpv4Address,UInt16,UInt16,Byte*,UInt32,Boolean> _sendIcmpEcho;
    private static delegate*<KernelIpv4Endpoint,KernelIpv4Endpoint,Byte*,UInt32,Boolean> _sendUdp;

    public static Boolean HasIcmpv4 => _receiveIcmp!=null&&_sendIcmpEcho!=null;
    public static Boolean HasUdp => _receiveUdp!=null&&_sendUdp!=null;
    public static Boolean HasTcpObservation => _receiveTcp!=null;

    public static Boolean RegisterIcmpv4(
        delegate*<KernelNetworkInterfaceHandle,KernelNetworkInterfaceInfo,KernelIpv4Address,KernelIpv4Address,Byte*,UInt32,Boolean> receive,
        delegate*<KernelNetworkInterfaceHandle,KernelIpv4Address,UInt16,UInt16,Byte*,UInt32,Boolean> sendEcho)
    {
        if(receive==null||sendEcho==null||_receiveIcmp!=null)return false;
        _receiveIcmp=receive;_sendIcmpEcho=sendEcho;return true;
    }

    public static Boolean RegisterUdp(
        delegate*<KernelNetworkInterfaceHandle,KernelNetworkInterfaceInfo,KernelIpv4Address,KernelIpv4Address,Byte*,UInt32,Boolean> receive,
        delegate*<KernelIpv4Endpoint,KernelIpv4Endpoint,Byte*,UInt32,Boolean> send)
    {
        if(receive==null||send==null||_receiveUdp!=null)return false;
        _receiveUdp=receive;_sendUdp=send;return true;
    }

    public static Boolean RegisterTcpObservation(
        delegate*<KernelNetworkInterfaceHandle,KernelNetworkInterfaceInfo,KernelIpv4Address,KernelIpv4Address,Byte*,UInt32,Boolean> receive)
    {
        if(receive==null||_receiveTcp!=null)return false;
        _receiveTcp=receive;return true;
    }

    internal static Boolean Receive(Byte protocol,KernelNetworkInterfaceHandle networkInterface,KernelNetworkInterfaceInfo info,KernelIpv4Address source,KernelIpv4Address destination,Byte* payload,UInt32 length)
    {
        if(protocol==(Byte)KernelNetworkProtocol.Icmp)return _receiveIcmp!=null&&_receiveIcmp(networkInterface,info,source,destination,payload,length);
        if(protocol==(Byte)KernelNetworkProtocol.Udp)return _receiveUdp!=null&&_receiveUdp(networkInterface,info,source,destination,payload,length);
        if(protocol==(Byte)KernelNetworkProtocol.Tcp)return _receiveTcp!=null&&_receiveTcp(networkInterface,info,source,destination,payload,length);
        return false;
    }

    internal static Boolean SendUdp(KernelIpv4Endpoint source,KernelIpv4Endpoint destination,Byte* payload,UInt32 length)
        =>_sendUdp!=null&&_sendUdp(source,destination,payload,length);

    internal static Boolean SendIcmpEcho(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address destination,UInt16 identifier,UInt16 sequence,Byte* payload,UInt32 length)
        =>_sendIcmpEcho!=null&&_sendIcmpEcho(networkInterface,destination,identifier,sequence,payload,length);
}
