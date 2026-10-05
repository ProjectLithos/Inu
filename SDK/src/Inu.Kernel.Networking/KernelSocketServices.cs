using System;

namespace Inu.Kernel.Networking;

/// <summary>Dependency-resolved registry for an optional kernel socket service.</summary>
public static unsafe class KernelSocketServices
{
    private static delegate*<KernelNetworkOptions,Boolean> _initialize;
    private static delegate*<UInt32> _count;
    private static delegate*<UInt32> _capacity;
    private static delegate*<KernelSocketType,KernelSocketHandle*,Boolean> _create;
    private static delegate*<KernelSocketHandle,KernelIpv4Endpoint,Boolean> _bind;
    private static delegate*<KernelSocketHandle,KernelIpv4Endpoint,Boolean> _connect;
    private static delegate*<KernelSocketHandle,Boolean> _listen;
    private static delegate*<KernelSocketHandle,KernelIpv4Endpoint,Byte*,UInt32,Boolean> _sendTo;
    private static delegate*<KernelSocketHandle,Byte*,UInt32,UInt32*,Boolean> _receive;
    private static delegate*<KernelSocketHandle,Boolean> _close;
    private static delegate*<KernelIpv4Endpoint,KernelIpv4Endpoint,Byte*,UInt32,Boolean> _deliverUdp;
    private static delegate*<KernelIpv4Endpoint,KernelIpv4Endpoint,Byte,Boolean> _observeTcp;

    public static Boolean IsRegistered => _initialize!=null;

    public static Boolean Register(
        delegate*<KernelNetworkOptions,Boolean> initialize,
        delegate*<UInt32> count,
        delegate*<UInt32> capacity,
        delegate*<KernelSocketType,KernelSocketHandle*,Boolean> create,
        delegate*<KernelSocketHandle,KernelIpv4Endpoint,Boolean> bind,
        delegate*<KernelSocketHandle,KernelIpv4Endpoint,Boolean> connect,
        delegate*<KernelSocketHandle,Boolean> listen,
        delegate*<KernelSocketHandle,KernelIpv4Endpoint,Byte*,UInt32,Boolean> sendTo,
        delegate*<KernelSocketHandle,Byte*,UInt32,UInt32*,Boolean> receive,
        delegate*<KernelSocketHandle,Boolean> close,
        delegate*<KernelIpv4Endpoint,KernelIpv4Endpoint,Byte*,UInt32,Boolean> deliverUdp,
        delegate*<KernelIpv4Endpoint,KernelIpv4Endpoint,Byte,Boolean> observeTcp)
    {
        if(initialize==null||count==null||capacity==null||create==null||bind==null||connect==null||listen==null||sendTo==null||receive==null||close==null||deliverUdp==null||observeTcp==null)return false;
        if(_initialize!=null)return false;
        _initialize=initialize;_count=count;_capacity=capacity;_create=create;_bind=bind;_connect=connect;_listen=listen;_sendTo=sendTo;_receive=receive;_close=close;_deliverUdp=deliverUdp;_observeTcp=observeTcp;
        return true;
    }

    internal static Boolean Initialize(KernelNetworkOptions options)=>_initialize==null||_initialize(options);
    internal static UInt32 GetCount()=>_count==null?0U:_count();
    internal static UInt32 GetCapacity()=>_capacity==null?0U:_capacity();
    internal static Boolean Create(KernelSocketType type,out KernelSocketHandle handle){handle=default;if(_create==null)return false;KernelSocketHandle h=default;if(!_create(type,&h))return false;handle=h;return true;}
    internal static Boolean Bind(KernelSocketHandle handle,KernelIpv4Endpoint endpoint)=>_bind!=null&&_bind(handle,endpoint);
    internal static Boolean Connect(KernelSocketHandle handle,KernelIpv4Endpoint endpoint)=>_connect!=null&&_connect(handle,endpoint);
    internal static Boolean Listen(KernelSocketHandle handle)=>_listen!=null&&_listen(handle);
    internal static Boolean SendTo(KernelSocketHandle handle,KernelIpv4Endpoint endpoint,Byte* data,UInt32 length)=>_sendTo!=null&&_sendTo(handle,endpoint,data,length);
    internal static Boolean Receive(KernelSocketHandle handle,Byte* buffer,UInt32 capacity,out UInt32 received){received=0;if(_receive==null)return false;UInt32 value=0;if(!_receive(handle,buffer,capacity,&value))return false;received=value;return true;}
    internal static Boolean Close(KernelSocketHandle handle)=>_close!=null&&_close(handle);
    internal static Boolean DeliverUdp(KernelIpv4Endpoint source,KernelIpv4Endpoint destination,Byte* data,UInt32 length)=>_deliverUdp!=null&&_deliverUdp(source,destination,data,length);
    internal static Boolean ObserveTcp(KernelIpv4Endpoint source,KernelIpv4Endpoint destination,Byte flags)=>_observeTcp!=null&&_observeTcp(source,destination,flags);
}
