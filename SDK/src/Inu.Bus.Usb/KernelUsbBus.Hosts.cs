using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
namespace Inu.Bus.Usb;
public static unsafe partial class KernelUsbBus
{
 public static Boolean RegisterHost(UsbHostCallbacks cb,out UsbHostHandle handle)=>RegisterHost(default,cb,out handle);
 public static Boolean RegisterHost(KernelDeviceHandle owner,UsbHostCallbacks cb,out UsbHostHandle handle){handle=default;if(!_initialized||cb.PrepareDevice==null||cb.Control==null||cb.Bulk==null||cb.Interrupt==null||cb.ConfigureEndpoint==null||cb.ConfigureHub==null)return false;if(owner.Value!=0U&&!KernelDrivers.TryGetDevice(owner,out _,out _,out _))return false;for(UInt32 i=0;i<MaxHosts;i++){HostRec* r=_hosts+i;if(r->Used!=0)continue;r->Used=1;r->Generic=owner.Value;r->Context=cb.Context;r->Prepare=(UInt64)(void*)cb.PrepareDevice;r->Control=(UInt64)(void*)cb.Control;r->Bulk=(UInt64)(void*)cb.Bulk;r->Interrupt=(UInt64)(void*)cb.Interrupt;r->ConfigureEndpoint=(UInt64)(void*)cb.ConfigureEndpoint;r->ConfigureHub=(UInt64)(void*)cb.ConfigureHub;_hostCount++;handle=new UsbHostHandle(i+1);return true;}return false;}
 public static Boolean UnregisterHost(UsbHostHandle host){if(!TryHost(host,out HostRec* h))return false;for(UInt32 i=0;i<_deviceCapacity;i++){DevRec* d=_devices+i;if(d->Used==0||d->Host!=host.Value)continue;if(!RemoveDevice(new UsbDeviceHandle(i+1U)))return false;}Clear((Byte*)h,(UInt32)sizeof(HostRec));if(_hostCount>0U)_hostCount--;return true;}
}
