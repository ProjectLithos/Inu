using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Storage;

/// <summary>Heap-backed block-device and volume registry with MBR/GPT partition discovery.</summary>
public static unsafe partial class KernelStorage
{
    private static Int32 FreeDevice(){for(Int32 i=0;i<(Int32)_deviceCapacity;i++)if((_devices+i)->Used==0)return i;return -1;} private static Int32 FreeVolume(){for(Int32 i=0;i<(Int32)_volumeCapacity;i++)if((_volumes+i)->Used==0)return i;return -1;}
    private static Boolean GrowDevices(){if(_mode!=KernelStorageRegistryMode.Dynamic)return false;UInt32 next=KernelStorageMath.NextCapacity(_deviceCapacity,_maximumDevices);if(next<=_deviceCapacity||!AllocateDevices(next,out KernelHeapAllocation a,out DeviceRecord* n))return false;Copy((Byte*)_devices,(Byte*)n,(UInt64)_deviceCapacity*(UInt64)sizeof(DeviceRecord));KernelHeapAllocation old=_deviceAllocation;_deviceAllocation=a;_devices=n;_deviceCapacity=next;return KernelHeap.TryRelease(old);}
    private static Boolean GrowVolumes(){if(_mode!=KernelStorageRegistryMode.Dynamic)return false;UInt32 next=KernelStorageMath.NextCapacity(_volumeCapacity,_maximumVolumes);if(next<=_volumeCapacity||!AllocateVolumes(next,out KernelHeapAllocation a,out VolumeRecord* n))return false;Copy((Byte*)_volumes,(Byte*)n,(UInt64)_volumeCapacity*(UInt64)sizeof(VolumeRecord));KernelHeapAllocation old=_volumeAllocation;_volumeAllocation=a;_volumes=n;_volumeCapacity=next;return KernelHeap.TryRelease(old);}
    private static Boolean AllocateDevices(UInt32 count,out KernelHeapAllocation a,out DeviceRecord* p){a=default;p=null;if(!KernelHeap.TryAllocate((UInt64)count*(UInt64)sizeof(DeviceRecord),64,true,out a))return false;p=(DeviceRecord*)(nuint)a.Address;return true;}
    private static Boolean AllocateVolumes(UInt32 count,out KernelHeapAllocation a,out VolumeRecord* p){a=default;p=null;if(!KernelHeap.TryAllocate((UInt64)count*(UInt64)sizeof(VolumeRecord),64,true,out a))return false;p=(VolumeRecord*)(nuint)a.Address;return true;}
    private static Boolean TryDevice(KernelStorageDeviceHandle h,out DeviceRecord* r){r=null;Int32 i=(Int32)h.Value-1;if(!_initialized||i<0||(UInt32)i>=_deviceCapacity||(_devices+i)->Used==0)return false;r=_devices+i;return true;}
    private static Boolean TryVolume(KernelStorageVolumeHandle h,out VolumeRecord* r){r=null;Int32 i=(Int32)h.Value-1;if(!_initialized||i<0||(UInt32)i>=_volumeCapacity||(_volumes+i)->Used==0)return false;r=_volumes+i;return true;}
    private static void Clear(Byte* p,Int32 bytes){for(Int32 i=0;i<bytes;i++)p[i]=0;} private static void Copy(Byte* s,Byte* d,UInt64 bytes){for(UInt64 i=0;i<bytes;i++)d[i]=s[i];}
}
