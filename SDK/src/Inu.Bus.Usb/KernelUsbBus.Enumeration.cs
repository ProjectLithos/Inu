using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
namespace Inu.Bus.Usb;
public static unsafe partial class KernelUsbBus
{
 public static Boolean EnumeratePort(UsbHostHandle host,Byte port,UsbSpeed speed,out UsbDeviceHandle device)=>KernelUsbEnumerationServices.Enumerate(host,0,port,speed,out device);
 public static Boolean EnumeratePort(UsbHostHandle host,Byte parentAddress,Byte port,UsbSpeed speed,out UsbDeviceHandle device)=>KernelUsbEnumerationServices.Enumerate(host,parentAddress,port,speed,out device);
 public static Boolean TryGetDevice(UsbDeviceHandle h,out UsbDeviceInfo info){info=default;if(!TryDev(h,out DevRec* r))return false;UsbDeviceDescriptor d=new(r->UsbVersion,r->Class,r->SubClass,r->Protocol,r->MaxPacket0,r->Vendor,r->Product,r->Release,0,0,0,r->Configs);info=new UsbDeviceInfo(h,new KernelDeviceHandle(r->Generic),new UsbHostHandle(r->Host),r->Address,r->Port,(UsbSpeed)r->Speed,d,r->Configuration);return true;}
 public static Boolean TryGetInterface(UInt32 index,out UsbInterfaceInfo info){info=default;UInt32 found=0;for(UInt32 i=0;i<_interfaceCapacity;i++){IfRec* r=_interfaces+i;if(r->Used==0)continue;if(found++!=index)continue;UsbInterfaceDescriptor d=new(r->Number,r->Alternate,r->EndpointCount,r->Class,r->SubClass,r->Protocol,r->StringIndex);info=new UsbInterfaceInfo(new UsbDeviceHandle(r->Device),new KernelDeviceHandle(r->Generic),d,Ep(r,0),Ep(r,1),Ep(r,2),Ep(r,3));return true;}return false;}
 public static Boolean TryGetInterface(KernelDeviceHandle node,out UsbInterfaceInfo info){info=default;if(node.Value==0U)return false;for(UInt32 i=0;i<_interfaceCapacity;i++){IfRec* r=_interfaces+i;if(r->Used==0||r->Generic!=node.Value)continue;UsbInterfaceDescriptor d=new(r->Number,r->Alternate,r->EndpointCount,r->Class,r->SubClass,r->Protocol,r->StringIndex);info=new UsbInterfaceInfo(new UsbDeviceHandle(r->Device),node,d,Ep(r,0),Ep(r,1),Ep(r,2),Ep(r,3));return true;}return false;}
}
