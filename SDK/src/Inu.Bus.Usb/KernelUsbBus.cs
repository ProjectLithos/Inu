using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
namespace Inu.Bus.Usb;
public static unsafe partial class KernelUsbBus
{
 private const UInt32 MaxHosts=16,InitialDevices=64,InitialInterfaces=128,ScratchBytes=4096;
 private struct HostRec{internal Byte Used;internal UInt32 Generic;internal UInt64 Context,Prepare,Control,Bulk,Interrupt,ConfigureEndpoint,ConfigureHub;}
 private struct DevRec{internal Byte Used;internal UInt32 Host,Generic;internal Byte Address,ParentAddress,Port,Speed,Configuration,Class,SubClass,Protocol,MaxPacket0,Configs;internal UInt16 Vendor,Product,UsbVersion,Release;}
 private struct IfRec{internal Byte Used;internal UInt32 Device,Generic;internal Byte Number,Alternate,EndpointCount,Class,SubClass,Protocol,StringIndex;internal fixed Byte EpAddress[4];internal fixed Byte EpAttributes[4];internal fixed UInt16 EpMaxPacket[4];internal fixed Byte EpInterval[4];}
 private static HostRec* _hosts;private static DevRec* _devices;private static IfRec* _interfaces;private static Byte* _scratch;private static KernelHeapAllocation _ha,_da,_ia,_sa;private static UInt32 _hostCount,_deviceCount,_interfaceCount,_deviceCapacity,_interfaceCapacity,_enumerations,_failures;private static Boolean _initialized;
 public static Boolean Initialize(){if(_initialized)return true;if(!KernelHeap.IsInitialized()||!KernelDrivers.IsInitialized())return false;if(!Alloc(MaxHosts,(UInt32)sizeof(HostRec),out _ha,out Byte* hp)||!Alloc(InitialDevices,(UInt32)sizeof(DevRec),out _da,out Byte* dp)||!Alloc(InitialInterfaces,(UInt32)sizeof(IfRec),out _ia,out Byte* ip)||!KernelHeap.TryAllocate(ScratchBytes,64,true,out _sa))return false;_hosts=(HostRec*)hp;_devices=(DevRec*)dp;_interfaces=(IfRec*)ip;_scratch=(Byte*)(nuint)_sa.Address;_deviceCapacity=InitialDevices;_interfaceCapacity=InitialInterfaces;if(!KernelUsbEnumerationBusContract.Register(&EnumerationPrepare,&EnumerationControl,&NextAddress,&EnumerationAddDevice,&EnumerationParseInterfaces,&EnumerationRemoveDevice,&EnumerationScratch,&EnumerationScratchBytes,&EnumerationCompleted,&Fail))return false;_initialized=true;return true;}
 public static UsbBusCapabilities GetCapabilities()=>new(_initialized,_hostCount,_deviceCount,_interfaceCount,_enumerations,_failures);public static UInt32 GetDeviceCount()=>_deviceCount;public static UInt32 GetInterfaceCount()=>_interfaceCount;
}
