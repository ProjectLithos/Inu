using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Networking;

public static unsafe partial class KernelNetworking
{
    private struct InterfaceRecord { public Byte Used;public Byte State;public Byte Contextual;public UInt32 Device;public Byte A,B,C,D,E,F;public UInt32 Address,Mask,Gateway,Mtu;public UInt64 Address6High,Address6Low;public Byte Prefix6;public delegate*<Byte*,UInt32,Boolean> Tx;public delegate*<Boolean,Boolean> RxEnable; }
    private static InterfaceRecord* _interfaces;
    private static UInt32 _interfaceCapacity,_interfacesUsed;private static KernelHeapAllocation _interfaceAllocation;private static KernelNetworkOptions _options;private static Boolean _initialized;

    public static Boolean Initialize()=>Initialize(KernelNetworkOptions.DynamicDefault);
    public static Boolean Initialize(KernelNetworkOptions options){if(_initialized||!KernelHeap.IsInitialized()||!KernelNetworkMath.IsValidOptions(options))return false;if(!Allocate(options.InitialInterfaces,sizeof(InterfaceRecord),out _interfaceAllocation,out Byte* ip))return false;_interfaces=(InterfaceRecord*)ip;_interfaceCapacity=options.InitialInterfaces;_options=options;if(!KernelNetworkInterfaceRegistryContract.Register(&HasInterface)||!KernelSocketServices.Initialize(options)||!KernelNetworkQueue.Initialize(options)){KernelHeap.TryRelease(_interfaceAllocation);_interfaces=null;return false;}_initialized=true;return true;}
    public static Boolean IsInitialized()=>_initialized;
    public static KernelNetworkCapabilities GetCapabilities()=>new(_initialized,_options.RegistryMode,_interfacesUsed,KernelNetworkExtensionServices.RouteCount(),KernelNetworkExtensionServices.NeighborCount(),KernelSocketServices.GetCount(),_interfaceCapacity,KernelNetworkExtensionServices.RouteCapacity(),KernelNetworkExtensionServices.NeighborCapacity(),KernelSocketServices.GetCapacity());

    internal static Boolean HasInterface(KernelNetworkInterfaceHandle handle)=>TryInterface(handle,out _);


}
