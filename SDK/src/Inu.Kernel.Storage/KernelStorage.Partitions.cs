using System;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Storage;

/// <summary>Volume facade and optional partition-discovery dispatch.</summary>
public static unsafe partial class KernelStorage
{
    public static Boolean DiscoverPartitions(KernelStorageDeviceHandle device,Byte* scratch,UInt32 scratchBytes,out UInt32 discovered)=>KernelStoragePartitionServices.Discover(device,scratch,scratchBytes,out discovered);
    public static Boolean TryGetVolume(KernelStorageVolumeHandle handle,out KernelStorageDeviceHandle device,out KernelPartitionInfo partition){device=default;partition=default;if(!TryVolume(handle,out VolumeRecord* r))return false;device=new KernelStorageDeviceHandle(r->Device);partition=new KernelPartitionInfo((KernelPartitionScheme)r->Scheme,r->Index,r->FirstBlock,r->BlockCount,r->MbrType,r->TypeGuidLow,r->TypeGuidHigh);return true;}
    /// <summary>Returns the Nth registered volume without exposing registry slots to callers.</summary>
    public static Boolean TryGetVolumeByOrdinal(UInt32 ordinal,out KernelStorageVolumeHandle handle)
    {
        handle=default;if(!_initialized||ordinal>=_volumeCount)return false;UInt32 seen=0U;
        for(UInt32 i=0U;i<_volumeCapacity;i++){VolumeRecord* r=_volumes+i;if(r->Used==0)continue;if(seen++!=ordinal)continue;handle=new KernelStorageVolumeHandle(i+1U);return true;}
        return false;
    }
    public static Boolean ReadVolumeBlocks(KernelStorageVolumeHandle volume,UInt64 relativeBlock,UInt32 count,Byte* buffer,UInt32 bufferBytes){if(!TryVolume(volume,out VolumeRecord* r)||relativeBlock>=r->BlockCount||count==0||count>r->BlockCount-relativeBlock)return false;return ReadBlocks(new KernelStorageDeviceHandle(r->Device),r->FirstBlock+relativeBlock,count,buffer,bufferBytes);}
    public static Boolean WriteVolumeBlocks(KernelStorageVolumeHandle volume,UInt64 relativeBlock,UInt32 count,Byte* buffer,UInt32 bufferBytes){if(!TryVolume(volume,out VolumeRecord* r)||relativeBlock>=r->BlockCount||count==0||count>r->BlockCount-relativeBlock)return false;return WriteBlocks(new KernelStorageDeviceHandle(r->Device),r->FirstBlock+relativeBlock,count,buffer,bufferBytes);}
    private static Boolean RegisterVolumeContract(KernelStorageDeviceHandle device,KernelPartitionInfo partition,KernelStorageVolumeHandle* handle){if(handle==null)return false;KernelStorageVolumeHandle value=default;if(!RegisterVolume(device,partition,out value))return false;*handle=value;return true;}
    private static Boolean RegisterVolume(KernelStorageDeviceHandle device,KernelPartitionInfo p,out KernelStorageVolumeHandle handle){handle=default;if(!TryDevice(device,out DeviceRecord* owner)||p.BlockCount==0||p.FirstBlock>=owner->BlockCount||p.BlockCount>owner->BlockCount-p.FirstBlock)return false;Int32 slot=FreeVolume();if(slot<0){if(!GrowVolumes())return false;slot=FreeVolume();if(slot<0)return false;}VolumeRecord* r=_volumes+slot;Clear((Byte*)r,sizeof(VolumeRecord));r->Used=1;r->Scheme=(Byte)p.Scheme;r->Device=device.Value;r->Index=p.Index;r->FirstBlock=p.FirstBlock;r->BlockCount=p.BlockCount;r->MbrType=p.MbrType;r->TypeGuidLow=p.TypeGuidLow;r->TypeGuidHigh=p.TypeGuidHigh;_volumeCount++;handle=new KernelStorageVolumeHandle((UInt32)slot+1U);return true;}
}
