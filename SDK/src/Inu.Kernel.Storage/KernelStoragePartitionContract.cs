using System;

namespace Inu.Kernel.Storage;

/// <summary>Explicit block-device and volume-registry contract consumed by partition-discovery components.</summary>
public static unsafe class KernelStoragePartitionContract
{
    private static delegate*<KernelStorageDeviceHandle,KernelPartitionInfo,KernelStorageVolumeHandle*,Boolean> _registerVolume;
    internal static Boolean Register(delegate*<KernelStorageDeviceHandle,KernelPartitionInfo,KernelStorageVolumeHandle*,Boolean> registerVolume){if(registerVolume==null)return false;if(_registerVolume!=null)return true;_registerVolume=registerVolume;return true;}
    public static Boolean TryGetGeometry(KernelStorageDeviceHandle device,out KernelStorageGeometry geometry)=>KernelStorage.TryGetGeometry(device,out geometry);
    public static Boolean ReadBlocks(KernelStorageDeviceHandle device,UInt64 firstBlock,UInt32 blockCount,Byte* buffer,UInt32 bufferBytes)=>KernelStorage.ReadBlocks(device,firstBlock,blockCount,buffer,bufferBytes);
    public static Boolean RegisterVolume(KernelStorageDeviceHandle device,KernelPartitionInfo partition,out KernelStorageVolumeHandle handle){handle=default;if(_registerVolume==null)return false;KernelStorageVolumeHandle value=default;if(!_registerVolume(device,partition,&value))return false;handle=value;return true;}
}
