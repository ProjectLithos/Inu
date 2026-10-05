using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Storage;

/// <summary>Heap-backed block-device and volume registry with MBR/GPT partition discovery.</summary>
public static unsafe partial class KernelStorage
{
    private struct DeviceRecord { internal Byte Used,Kind,ReadOnly,Removable,Contextual; internal UInt32 DriverDevice,LogicalBlockSize,PhysicalBlockSize; internal UInt64 BlockCount,Read,Write,Flush; }
    private struct VolumeRecord { internal Byte Used,Scheme; internal UInt32 Device,Index,MbrType; internal UInt64 FirstBlock,BlockCount,TypeGuidLow,TypeGuidHigh; }
    private static DeviceRecord* _devices; private static VolumeRecord* _volumes;
    private static KernelHeapAllocation _deviceAllocation,_volumeAllocation;
    private static UInt32 _deviceCapacity,_volumeCapacity,_maximumDevices,_maximumVolumes,_deviceCount,_volumeCount;
    private static KernelStorageRegistryMode _mode; private static Boolean _initialized;

    public static Boolean Initialize()=>Initialize(KernelStorageOptions.DynamicDefault);
    public static Boolean Initialize(KernelStorageOptions options)
    { if(_initialized)return true;if(!KernelHeap.IsInitialized()||!KernelDrivers.IsInitialized()||!KernelStorageMath.IsValidOptions(options))return false;_mode=options.RegistryMode;_maximumDevices=options.MaximumDevices;_maximumVolumes=options.MaximumVolumes;if(!AllocateDevices(options.InitialDevices,out _deviceAllocation,out _devices))return false;if(!AllocateVolumes(options.InitialVolumes,out _volumeAllocation,out _volumes)){KernelHeap.TryRelease(_deviceAllocation);_devices=null;return false;}_deviceCapacity=options.InitialDevices;_volumeCapacity=options.InitialVolumes;_deviceCount=0;_volumeCount=0;if(!KernelStoragePartitionContract.Register(&RegisterVolumeContract)||!KernelStorageQueue.Initialize(options)||!KernelVfs.Initialize(options)){KernelHeap.TryRelease(_volumeAllocation);KernelHeap.TryRelease(_deviceAllocation);_devices=null;_volumes=null;return false;}_initialized=true;return true; }
    public static Boolean IsInitialized()=>_initialized;
    public static KernelStorageCapabilities GetCapabilities()=>new(_initialized,_mode,_deviceCount,_volumeCount,KernelVfs.MountCount,KernelVfs.OpenFileCount,KernelStorageQueue.Count,_deviceCapacity,_volumeCapacity,KernelVfs.MountCapacity,KernelVfs.OpenFileCapacity,KernelStorageQueue.Capacity);


}
