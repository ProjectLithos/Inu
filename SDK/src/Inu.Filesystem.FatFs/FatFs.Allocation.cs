using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Heap;
using Inu.Kernel.Storage;

namespace Inu.Filesystem.FatFs;

public static unsafe partial class FatFs
{
private static Boolean AllocateCluster(MountContext* mount,out UInt32 cluster)
    {
        cluster=0;if(mount==null)return false;UInt32 limit=mount->Info.ClusterCount+2U;if(limit<=2U)return false;
        UInt32 hint=mount->AllocationHint;if(hint<2U||hint>=limit)hint=2U;
        if(!FindFreeClusterFast(mount,hint,limit,out UInt32 candidate)&&hint>2U&&!FindFreeClusterFast(mount,2U,hint,out candidate))return false;
        if(!SetFatEntry(mount,candidate,EndOfChainValue(mount->Info)))return false;
        if(!ZeroCluster(mount,candidate)){SetFatEntry(mount,candidate,0U);return false;}
        UInt32 next=candidate+1U;mount->AllocationHint=next<limit?next:2U;cluster=candidate;return true;
    }

private static Boolean FindFreeClusterFast(MountContext* mount,UInt32 firstCluster,UInt32 limit,out UInt32 cluster)
    {
        cluster=0;if(firstCluster<2U)firstCluster=2U;if(firstCluster>=limit)return false;
        // FAT12 packing crosses entry boundaries, so retain the safe scalar reader there.
        if(mount->Info.Format==(Byte)FatFsFormat.Fat12)
        {
            for(UInt32 candidate=firstCluster;candidate<limit;candidate++){if(!ReadFatEntry(mount,candidate,out UInt32 value))return false;if(value==0U){cluster=candidate;return true;}}return false;
        }
        UInt32 bps=mount->Info.BytesPerSector;UInt32 bytesPerEntry=mount->Info.Format==(Byte)FatFsFormat.Fat16?2U:4U;UInt32 entriesPerSector=bps/bytesPerEntry;if(entriesPerSector==0U||!KernelHeap.TryAllocate(bps,16,false,out KernelHeapAllocation scratch))return false;
        Byte* data=(Byte*)(nuint)scratch.Address;UInt32 firstFatSector=firstCluster/entriesPerSector,lastFatSector=(limit-1U)/entriesPerSector;
        for(UInt32 relative=firstFatSector;relative<=lastFatSector;relative++)
        {
            UInt64 sector=(UInt64)mount->Info.ReservedSectors+relative;if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}
            UInt32 begin=relative==firstFatSector?firstCluster%entriesPerSector:0U;UInt32 end=entriesPerSector;if(relative==lastFatSector){UInt32 tail=limit%entriesPerSector;if(tail!=0U)end=tail;}
            for(UInt32 i=begin;i<end;i++)
            {
                UInt32 candidate=relative*entriesPerSector+i;if(candidate<2U||candidate>=limit)continue;UInt32 value=mount->Info.Format==(Byte)FatFsFormat.Fat16?Read16(data+i*2U):Read32(data+i*4U)&0x0FFFFFFFU;
                if(value==0U){KernelHeap.TryRelease(scratch);cluster=candidate;return true;}
            }
        }
        KernelHeap.TryRelease(scratch);return false;
    }

private static Boolean FreeClusterChain(MountContext* mount,UInt32 first)
    {
        UInt32 current=first;UInt32 guard=0;while(current>=2U&&!IsEndOfChain(mount->Info,current)&&guard++<=mount->Info.ClusterCount){if(!ReadFatEntry(mount,current,out UInt32 next)||!SetFatEntry(mount,current,0U))return false;if(IsEndOfChain(mount->Info,next)||next<2U)return true;current=next;}return true;
    }

private static Boolean ZeroCluster(MountContext* mount,UInt32 cluster)
    { UInt32 bytes=ClusterSize(mount);if(!KernelHeap.TryAllocate(bytes,16,true,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;Boolean ok=KernelStorage.WriteVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),ClusterToSector(mount->Info,cluster),mount->Info.SectorsPerCluster,data,bytes);KernelHeap.TryRelease(scratch);return ok; }

private static Boolean NextCluster(MountContext* mount,UInt32 cluster,out UInt32 next)=>ReadFatEntry(mount,cluster,out next);

private static Boolean ReadFatEntry(MountContext* mount,UInt32 cluster,out UInt32 value)
    {
        value=0;UInt32 bps=mount->Info.BytesPerSector;UInt64 fatOffset=FatOffset(mount->Info,cluster);UInt64 fatSector=(UInt64)mount->Info.ReservedSectors+(fatOffset/bps);UInt32 offset=(UInt32)(fatOffset%bps);UInt32 required=(mount->Info.Format==(Byte)FatFsFormat.Fat12&&offset==bps-1U)?2U:1U,bytes=bps*required;
        if(!KernelHeap.TryAllocate(bytes,16,false,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),fatSector,required,data,bytes)){KernelHeap.TryRelease(scratch);return false;}
        if(mount->Info.Format==(Byte)FatFsFormat.Fat12){UInt16 pair=(UInt16)(data[offset]|((UInt16)data[offset+1U]<<8));value=(cluster&1U)==0U?(UInt32)(pair&0x0FFFU):(UInt32)(pair>>4);}else if(mount->Info.Format==(Byte)FatFsFormat.Fat16)value=Read16(data+offset);else value=Read32(data+offset)&0x0FFFFFFFU;
        KernelHeap.TryRelease(scratch);return true;
    }

private static Boolean SetFatEntry(MountContext* mount,UInt32 cluster,UInt32 value)
    {
        UInt32 bps=mount->Info.BytesPerSector;UInt64 fatOffset=FatOffset(mount->Info,cluster);UInt32 relativeSector=(UInt32)(fatOffset/bps),offset=(UInt32)(fatOffset%bps);UInt32 required=(mount->Info.Format==(Byte)FatFsFormat.Fat12&&offset==bps-1U)?2U:1U,bytes=bps*required;
        if(!KernelHeap.TryAllocate(bytes,16,false,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;
        for(UInt32 fat=0;fat<mount->Info.FatCount;fat++)
        {
            UInt64 sector=(UInt64)mount->Info.ReservedSectors+(UInt64)fat*mount->Info.SectorsPerFat+relativeSector;if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,required,data,bytes)){KernelHeap.TryRelease(scratch);return false;}
            if(mount->Info.Format==(Byte)FatFsFormat.Fat12){UInt16 pair=(UInt16)(data[offset]|((UInt16)data[offset+1U]<<8));UInt16 updated=(cluster&1U)==0U?(UInt16)((pair&0xF000U)|(value&0x0FFFU)):(UInt16)((pair&0x000FU)|((value&0x0FFFU)<<4));data[offset]=(Byte)updated;data[offset+1U]=(Byte)(updated>>8);}else if(mount->Info.Format==(Byte)FatFsFormat.Fat16)Write16(data+offset,(UInt16)value);else{UInt32 old=Read32(data+offset);Write32(data+offset,(old&0xF0000000U)|(value&0x0FFFFFFFU));}
            if(!KernelStorage.WriteVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,required,data,bytes)){KernelHeap.TryRelease(scratch);return false;}
        }
        KernelHeap.TryRelease(scratch);return true;
    }

private static UInt64 FatOffset(VolumeInfo info,UInt32 cluster)=>info.Format==(Byte)FatFsFormat.Fat12?(UInt64)cluster+(UInt64)(cluster/2U):info.Format==(Byte)FatFsFormat.Fat16?(UInt64)cluster*2UL:(UInt64)cluster*4UL;

private static UInt32 EndOfChainValue(VolumeInfo info)=>info.Format==(Byte)FatFsFormat.Fat12?0x0FFFU:info.Format==(Byte)FatFsFormat.Fat16?0xFFFFU:0x0FFFFFFFU;

private static UInt32 RootCluster(MountContext* mount)=>mount->Info.Format==(Byte)FatFsFormat.Fat32?mount->Info.RootCluster:0U;

private static UInt32 ClusterSize(MountContext* mount)=>(UInt32)mount->Info.BytesPerSector*(UInt32)mount->Info.SectorsPerCluster;

private static UInt64 ClusterToSector(VolumeInfo info,UInt32 cluster)=>cluster<2U?0UL:(UInt64)info.FirstDataSector+((UInt64)(cluster-2U)*(UInt64)info.SectorsPerCluster);

private static Boolean IsEndOfChain(VolumeInfo info,UInt32 value)=>info.Format==(Byte)FatFsFormat.Fat12?value>=0x0FF8U:info.Format==(Byte)FatFsFormat.Fat16?value>=0xFFF8U:(value&0x0FFFFFFFU)>=0x0FFFFFF8U;
}
