using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
namespace Inu.Bus.Usb;
public static unsafe partial class KernelUsbBus
{
 public static Boolean ConfigureHub(UsbDeviceHandle device,Byte portCount,Boolean multiTransactionTranslator){if(!TryDev(device,out DevRec* d)||!TryHost(new UsbHostHandle(d->Host),out HostRec* h))return false;var fn=(delegate*<UInt64,Byte,Byte,Boolean,Boolean>)(void*)h->ConfigureHub;return fn(h->Context,d->Address,portCount,multiTransactionTranslator);}
 public static Boolean ControlTransfer(UsbDeviceHandle device,UsbSetupPacket setup,Byte* buffer,UInt32 bytes,out UInt32 transferred){transferred=0;if(!TryDev(device,out DevRec* d)||!TryHost(new UsbHostHandle(d->Host),out HostRec* h))return false;UsbSetupPacket localSetup=setup;UInt32 localTransferred=0;Boolean ok=Ctl(h,d->Address,0,&localSetup,buffer,bytes,&localTransferred);transferred=localTransferred;return ok;}
 public static Boolean BulkTransfer(UsbDeviceHandle device,Byte endpoint,Byte* buffer,UInt32 bytes,out UInt32 transferred){transferred=0;if(!TryDev(device,out DevRec* d)||!TryHost(new UsbHostHandle(d->Host),out HostRec* h))return false;var fn=(delegate*<UInt64,Byte,Byte,Byte*,UInt32,UInt32*,Boolean>)(void*)h->Bulk;UInt32 localTransferred=0;Boolean ok=fn(h->Context,d->Address,endpoint,buffer,bytes,&localTransferred);transferred=localTransferred;return ok;}
 public static Boolean InterruptTransfer(UsbDeviceHandle device,Byte endpoint,Byte* buffer,UInt32 bytes,out UInt32 transferred){transferred=0;if(!TryDev(device,out DevRec* d)||!TryHost(new UsbHostHandle(d->Host),out HostRec* h))return false;var fn=(delegate*<UInt64,Byte,Byte,Byte*,UInt32,UInt32*,Boolean>)(void*)h->Interrupt;UInt32 localTransferred=0;Boolean ok=fn(h->Context,d->Address,endpoint,buffer,bytes,&localTransferred);transferred=localTransferred;return ok;}
}
