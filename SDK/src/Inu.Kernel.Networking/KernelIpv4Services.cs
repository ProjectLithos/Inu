using System;
namespace Inu.Kernel.Networking;

/// <summary>Source-neutral send contract exposed by the selected IPv4 implementation.</summary>
public static unsafe class KernelIpv4Services
{
    private static delegate*<KernelIpv4Address,KernelIpv4Address,Byte,Byte*,UInt32,Boolean> _sendRouted;
    private static delegate*<KernelNetworkInterfaceHandle,KernelIpv4Address,KernelIpv4Address,Byte,Byte*,UInt32,Boolean> _sendOnInterface;

    public static Boolean IsRegistered => _sendRouted!=null&&_sendOnInterface!=null;

    public static Boolean Register(
        delegate*<KernelIpv4Address,KernelIpv4Address,Byte,Byte*,UInt32,Boolean> sendRouted,
        delegate*<KernelNetworkInterfaceHandle,KernelIpv4Address,KernelIpv4Address,Byte,Byte*,UInt32,Boolean> sendOnInterface)
    {
        if(sendRouted==null||sendOnInterface==null||_sendRouted!=null)return false;
        _sendRouted=sendRouted;_sendOnInterface=sendOnInterface;return true;
    }

    internal static Boolean SendRouted(KernelIpv4Address source,KernelIpv4Address destination,Byte protocol,Byte* payload,UInt32 length)
        =>_sendRouted!=null&&_sendRouted(source,destination,protocol,payload,length);

    internal static Boolean SendOnInterface(KernelNetworkInterfaceHandle networkInterface,KernelIpv4Address source,KernelIpv4Address destination,Byte protocol,Byte* payload,UInt32 length)
        =>_sendOnInterface!=null&&_sendOnInterface(networkInterface,source,destination,protocol,payload,length);
}
