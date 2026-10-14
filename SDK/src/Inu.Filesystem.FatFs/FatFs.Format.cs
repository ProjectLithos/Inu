using System;
using Inu.Kernel.Heap;
using Inu.Kernel.Storage;

namespace Inu.Filesystem.FatFs;

public static unsafe partial class FatFs
{
    private const UInt32 FormatReservedSectors=32U;
    private const UInt32 FormatFatCount=2U;
    private const UInt32 FormatRootCluster=2U;

    /// <summary>
    /// Formats an unformatted writable volume as FAT32. Existing recognized FAT volumes are preserved.
    /// The first Inu data partition may be seeded with USERS, WORK and TEMP directories.
    /// </summary>
    public static Boolean EnsureFat32Formatted(KernelStorageVolumeHandle volume,Boolean seedDirectories,out Boolean formatted)
    {
        formatted=false;
        if(TryReadBoot(volume,out VolumeInfo existing,out KernelHeapAllocation existingScratch))
        {
            KernelHeap.TryRelease(existingScratch);
            return existing.Format==(Byte)FatFsFormat.Fat32;
        }
        if(!KernelStorage.TryGetVolume(volume,out KernelStorageDeviceHandle device,out KernelPartitionInfo partition)||
           !KernelStorage.TryGetGeometry(device,out KernelStorageGeometry geometry)||geometry.ReadOnly||geometry.LogicalBlockSize!=512U||
           partition.BlockCount>UInt32.MaxValue)return false;

        UInt32 totalSectors=(UInt32)partition.BlockCount;
        if(!TryChooseFat32Geometry(totalSectors,out Byte sectorsPerCluster,out UInt32 fatSectors,out UInt32 clusterCount))return false;
        UInt32 firstData=FormatReservedSectors+FormatFatCount*fatSectors;
        UInt32 clusterBytes=512U*sectorsPerCluster;
        if(!KernelHeap.TryAllocate(clusterBytes>16384U?clusterBytes:16384U,16UL,true,out KernelHeapAllocation scratchAllocation))return false;
        Byte* scratch=(Byte*)(nuint)scratchAllocation.Address;
        UInt32 scratchBytes=(UInt32)scratchAllocation.ByteCount;
        try
        {
            if(!ZeroVolumeRange(volume,0U,FormatReservedSectors,scratch,scratchBytes))return false;
            if(!ZeroVolumeRange(volume,FormatReservedSectors,FormatFatCount*fatSectors,scratch,scratchBytes))return false;

            Zero(scratch,512U);WriteFat32BootSector(scratch,totalSectors,sectorsPerCluster,fatSectors,partition.FirstBlock,seedDirectories);
            if(!KernelStorage.WriteVolumeBlocks(volume,0UL,1U,scratch,512U)||!KernelStorage.WriteVolumeBlocks(volume,6UL,1U,scratch,512U))return false;
            Zero(scratch,512U);WriteFsInfo(scratch,clusterCount,seedDirectories?4U:1U,seedDirectories?6U:3U);
            if(!KernelStorage.WriteVolumeBlocks(volume,1UL,1U,scratch,512U)||!KernelStorage.WriteVolumeBlocks(volume,7UL,1U,scratch,512U))return false;

            Zero(scratch,512U);Write32(scratch+0,0x0FFFFFF8U);Write32(scratch+4,0x0FFFFFFFU);Write32(scratch+8,0x0FFFFFFFU);
            if(seedDirectories){Write32(scratch+12,0x0FFFFFFFU);Write32(scratch+16,0x0FFFFFFFU);Write32(scratch+20,0x0FFFFFFFU);}
            if(!KernelStorage.WriteVolumeBlocks(volume,FormatReservedSectors,1U,scratch,512U)||
               !KernelStorage.WriteVolumeBlocks(volume,FormatReservedSectors+fatSectors,1U,scratch,512U))return false;

            if(!ZeroVolumeRange(volume,firstData,sectorsPerCluster*(seedDirectories?4U:1U),scratch,scratchBytes))return false;
            Zero(scratch,512U);
            if(seedDirectories)
            {
                WriteDirectoryEntry(scratch+0,"USERS      ",3U);WriteDirectoryEntry(scratch+32,"WORK       ",4U);WriteDirectoryEntry(scratch+64,"TEMP       ",5U);
            }
            if(!KernelStorage.WriteVolumeBlocks(volume,firstData,1U,scratch,512U))return false;
            if(seedDirectories)
            {
                if(!WriteEmptyDirectory(volume,firstData+sectorsPerCluster,3U,FormatRootCluster,scratch)||
                   !WriteEmptyDirectory(volume,firstData+sectorsPerCluster*2U,4U,FormatRootCluster,scratch)||
                   !WriteEmptyDirectory(volume,firstData+sectorsPerCluster*3U,5U,FormatRootCluster,scratch))return false;
            }
            if(!KernelStorage.Flush(device))return false;
            formatted=true;return true;
        }
        finally { KernelHeap.TryRelease(scratchAllocation); }
    }

    private static Boolean TryChooseFat32Geometry(UInt32 totalSectors,out Byte sectorsPerCluster,out UInt32 fatSectors,out UInt32 clusterCount)
    {
        sectorsPerCluster=0;fatSectors=0;clusterCount=0;
        Byte candidate=1;
        while(candidate<=128U)
        {
            UInt32 fat=1U;
            for(UInt32 pass=0U;pass<32U;pass++)
            {
                UInt64 overhead=(UInt64)FormatReservedSectors+(UInt64)FormatFatCount*fat;
                if(overhead>=totalSectors)break;
                UInt32 clusters=(UInt32)(((UInt64)totalSectors-overhead)/candidate);
                UInt32 needed=(UInt32)((((UInt64)clusters+2UL)*4UL+511UL)/512UL);
                if(needed==fat)
                {
                    if(clusters>=65525U&&clusters<0x0FFFFFF5U){sectorsPerCluster=candidate;fatSectors=fat;clusterCount=clusters;return true;}
                    break;
                }
                fat=needed;
            }
            if(candidate==128U)break;candidate=(Byte)(candidate*2U);
        }
        return false;
    }

    private static Boolean ZeroVolumeRange(KernelStorageVolumeHandle volume,UInt32 first,UInt32 count,Byte* scratch,UInt32 scratchBytes)
    {
        if(count==0U)return true;UInt32 capacity=scratchBytes/512U;if(capacity==0U)return false;Zero(scratch,capacity*512U);
        UInt32 remaining=count;UInt32 current=first;
        while(remaining>0U){UInt32 batch=remaining<capacity?remaining:capacity;if(!KernelStorage.WriteVolumeBlocks(volume,current,batch,scratch,batch*512U))return false;current+=batch;remaining-=batch;}
        return true;
    }

    private static void WriteFat32BootSector(Byte* sector,UInt32 totalSectors,Byte sectorsPerCluster,UInt32 fatSectors,UInt64 hiddenSectors,Boolean firstPartition)
    {
        sector[0]=0xEB;sector[1]=0x58;sector[2]=0x90;WriteAscii(sector+3,"INUDATA ",8U);Write16(sector+11,(UInt16)512);sector[13]=sectorsPerCluster;
        Write16(sector+14,(UInt16)FormatReservedSectors);sector[16]=(Byte)FormatFatCount;sector[21]=0xF8;Write16(sector+24,(UInt16)63);Write16(sector+26,(UInt16)255);
        Write32(sector+28,hiddenSectors>UInt32.MaxValue?UInt32.MaxValue:(UInt32)hiddenSectors);Write32(sector+32,totalSectors);Write32(sector+36,fatSectors);Write32(sector+44,FormatRootCluster);
        Write16(sector+48,(UInt16)1);Write16(sector+50,(UInt16)6);sector[64]=0x80;sector[66]=0x29;Write32(sector+67,firstPartition?0x494E5501U:0x494E5502U);
        WriteAscii(sector+71,firstPartition?"INU DATA1  ":"INU DATA2  ",11U);WriteAscii(sector+82,"FAT32   ",8U);sector[510]=0x55;sector[511]=0xAA;
    }

    private static void WriteFsInfo(Byte* sector,UInt32 clusterCount,UInt32 usedClusters,UInt32 nextFreeCluster)
    {
        Write32(sector,0x41615252U);Write32(sector+484,0x61417272U);Write32(sector+488,clusterCount>usedClusters?clusterCount-usedClusters:0U);Write32(sector+492,nextFreeCluster);Write32(sector+508,0xAA550000U);
    }

    private static Boolean WriteEmptyDirectory(KernelStorageVolumeHandle volume,UInt32 firstSector,UInt32 self,UInt32 parent,Byte* scratch)
    {
        Zero(scratch,512U);WriteDirectoryEntry(scratch,".          ",self);WriteDirectoryEntry(scratch+32,"..         ",parent);return KernelStorage.WriteVolumeBlocks(volume,firstSector,1U,scratch,512U);
    }

    private static void WriteDirectoryEntry(Byte* entry,String shortName,UInt32 cluster)
    {
        WriteAscii(entry,shortName,11U);entry[11]=0x10;Write16(entry+20,(UInt16)(cluster>>16));Write16(entry+26,(UInt16)cluster);
    }

    private static void WriteAscii(Byte* destination,String value,UInt32 count){for(UInt32 i=0U;i<count;i++)destination[i]=(Byte)value[(Int32)i];}
    private static void Zero(Byte* buffer,UInt32 bytes){for(UInt32 i=0U;i<bytes;i++)buffer[i]=0U;}
}
