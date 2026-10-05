using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
namespace Inu.Bus.Usb;
public static unsafe partial class KernelUsbBus
{
 private static KernelDeviceHandle hOwner(UsbHostHandle host){if(TryHost(host,out HostRec* h)&&h->Generic!=0U)return new KernelDeviceHandle(h->Generic);return default;}
 private static Boolean Ctl(HostRec* h,Byte addr,Byte ep,UsbSetupPacket* s,Byte* b,UInt32 n,UInt32* x){var fn=(delegate*<UInt64,Byte,Byte,UsbSetupPacket*,Byte*,UInt32,UInt32*,Boolean>)(void*)h->Control;return fn(h->Context,addr,ep,s,b,n,x);}private static Byte NextAddress(){for(Byte a=1;a<128;a++){Boolean used=false;for(UInt32 i=0;i<_deviceCapacity;i++)if((_devices+i)->Used!=0&&(_devices+i)->Address==a){used=true;break;}if(!used)return a;}return 0;}
 private static Boolean TryHost(UsbHostHandle h,out HostRec* r){r=null;if(h.Value==0||h.Value>MaxHosts)return false;r=_hosts+h.Value-1;return r->Used!=0;}private static Boolean TryDev(UsbDeviceHandle h,out DevRec* r){r=null;if(h.Value==0||h.Value>_deviceCapacity)return false;r=_devices+h.Value-1;return r->Used!=0;}private static Int32 FreeDev(){for(Int32 i=0;i<(Int32)_deviceCapacity;i++)if((_devices+i)->Used==0)return i;return -1;}private static Int32 FreeIf(){for(Int32 i=0;i<(Int32)_interfaceCapacity;i++)if((_interfaces+i)->Used==0)return i;return -1;}
 private static Boolean Alloc(UInt32 count,UInt32 size,out KernelHeapAllocation a,out Byte* p){a=default;p=null;if(!KernelHeap.TryAllocate((UInt64)count*size,64,true,out a))return false;p=(Byte*)(nuint)a.Address;return true;}private static void Clear(Byte* p,UInt32 n){for(UInt32 i=0;i<n;i++)p[i]=0;}private static Boolean Fail(){_failures++;return false;}
 private static Boolean EnumerationPrepare(UsbHostHandle host,Byte parentAddress,Byte port,UsbSpeed speed,Byte address){if(!TryHost(host,out HostRec* h))return false;var prepare=(delegate*<UInt64,Byte,Byte,UsbSpeed,Byte,Boolean>)(void*)h->Prepare;return prepare(h->Context,parentAddress,port,speed,address);}
 private static Boolean EnumerationControl(UsbHostHandle host,Byte address,Byte endpoint,UsbSetupPacket* setup,Byte* buffer,UInt32 bytes,UInt32* transferred){if(!TryHost(host,out HostRec* h))return false;return Ctl(h,address,endpoint,setup,buffer,bytes,transferred);}
 private static Boolean EnumerationAddDevice(UsbHostHandle host,Byte address,Byte parentAddress,Byte port,UsbSpeed speed,UsbDeviceDescriptor descriptor,Byte maxPacket0,Byte configuration,UsbDeviceHandle* device){if(device==null||!TryHost(host,out HostRec* h))return false;UsbDeviceHandle value=default;if(!AddDevice(host,address,parentAddress,port,speed,descriptor,maxPacket0,configuration,out value))return false;*device=value;return true;}
 private static Boolean EnumerationParseInterfaces(UsbHostHandle host,UsbDeviceHandle device,Byte* data,UInt32 bytes){if(!TryHost(host,out HostRec* h))return false;return ParseInterfaces(h,device,data,bytes);}
 private static Boolean EnumerationRemoveDevice(UsbDeviceHandle device)=>RemoveDevice(device);
 private static Byte* EnumerationScratch()=>_scratch;
 private static UInt32 EnumerationScratchBytes()=>ScratchBytes;
 private static void EnumerationCompleted()=>_enumerations++;
}
