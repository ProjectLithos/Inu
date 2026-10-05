using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Networking;

public static unsafe partial class KernelNetworking
{
    public static Boolean ReceiveFrame(KernelNetworkInterfaceHandle networkInterface,Byte* frame,UInt32 length)=>KernelNetworkStack.ReceiveEthernet(networkInterface,frame,length);
    public static Boolean QueueReceivedFrame(KernelNetworkInterfaceHandle networkInterface,Byte* frame,UInt32 length,out KernelNetworkPacketHandle handle)=>KernelNetworkQueue.Enqueue(networkInterface,frame,length,out handle);
    internal static Boolean Transmit(KernelNetworkInterfaceHandle h,Byte* frame,UInt32 length){if(!TryInterface(h,out InterfaceRecord* r)||r->State!=(Byte)KernelNetworkInterfaceState.Up||frame==null||length==0)return false;if(r->Contextual!=0){delegate*<KernelDeviceHandle,Byte*,UInt32,Boolean> tx=(delegate*<KernelDeviceHandle,Byte*,UInt32,Boolean>)(void*)r->Tx;return tx(new KernelDeviceHandle(r->Device),frame,length);}return r->Tx(frame,length);}
}
