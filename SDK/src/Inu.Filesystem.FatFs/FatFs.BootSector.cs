using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Heap;
using Inu.Kernel.Storage;

namespace Inu.Filesystem.FatFs;

public static unsafe partial class FatFs
{
private static Boolean TryReadBoot(KernelStorageVolumeHandle volume,out VolumeInfo info,out KernelHeapAllocation scratch)
    {
        info=default;scratch=default;if(!KernelStorage.TryGetVolume(volume,out KernelStorageDeviceHandle device,out _)||!KernelStorage.TryGetGeometry(device,out KernelStorageGeometry geometry)||geometry.LogicalBlockSize>MaximumSectorSize)return false;
        if(!KernelHeap.TryAllocate(geometry.LogicalBlockSize,16,false,out scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;if(!KernelStorage.ReadVolumeBlocks(volume,0,1,data,geometry.LogicalBlockSize)||!TryParseBootSector(data,geometry.LogicalBlockSize,geometry.ReadOnly,out info)){KernelHeap.TryRelease(scratch);scratch=default;return false;}return info.BytesPerSector==geometry.LogicalBlockSize;
    }

public static Boolean TryParseBootSector(Byte* sector,UInt32 sectorBytes,Boolean readOnly,out FatFsFormat format,out UInt32 bytesPerSector,out UInt32 sectorsPerCluster,out UInt64 totalSectors)
    { format=FatFsFormat.Unknown;bytesPerSector=0;sectorsPerCluster=0;totalSectors=0;if(!TryParseBootSector(sector,sectorBytes,readOnly,out VolumeInfo info))return false;format=(FatFsFormat)info.Format;bytesPerSector=info.BytesPerSector;sectorsPerCluster=info.SectorsPerCluster;totalSectors=info.TotalSectors;return true; }

private static Boolean TryParseBootSector(Byte* sector,UInt32 sectorBytes,Boolean readOnly,out VolumeInfo info)
    {
        info=default;if(sector==null||sectorBytes<512U||sector[510]!=0x55||sector[511]!=0xAA)return false;UInt32 bps=Read16(sector+11);Byte spc=sector[13];UInt16 reserved=Read16(sector+14);Byte fats=sector[16];UInt16 rootEntries=Read16(sector+17);
        UInt32 total=Read16(sector+19);if(total==0U)total=Read32(sector+32);UInt32 fatSectors=Read16(sector+22);if(fatSectors==0U)fatSectors=Read32(sector+36);if(bps<512U||bps>MaximumSectorSize||(bps&(bps-1U))!=0U||spc==0U||(spc&(spc-1U))!=0U||reserved==0U||fats==0U||fatSectors==0U||total==0U)return false;
        UInt32 rootDirSectors=((UInt32)rootEntries*32U+(UInt32)bps-1U)/(UInt32)bps;UInt64 nonData=(UInt64)reserved+((UInt64)fats*fatSectors)+rootDirSectors;if(nonData>=total)return false;UInt64 clusters=((UInt64)total-nonData)/spc;FatFsFormat format=clusters<4085UL?FatFsFormat.Fat12:clusters<65525UL?FatFsFormat.Fat16:FatFsFormat.Fat32;
        UInt32 rootCluster=format==FatFsFormat.Fat32?Read32(sector+44):0U;if(format==FatFsFormat.Fat32&&(rootEntries!=0U||rootCluster<2U))return false;if(format!=FatFsFormat.Fat32&&rootEntries==0U)return false;
        info.Format=(Byte)format;info.BytesPerSector=(UInt16)bps;info.SectorsPerCluster=spc;info.ReservedSectors=reserved;info.FatCount=fats;info.RootEntryCount=rootEntries;info.SectorsPerFat=fatSectors;info.RootDirectorySectors=rootDirSectors;info.FirstRootSector=(UInt32)((UInt64)reserved+(UInt64)fats*fatSectors);info.FirstDataSector=(UInt32)nonData;info.ClusterCount=clusters>UInt32.MaxValue?UInt32.MaxValue:(UInt32)clusters;info.RootCluster=rootCluster;info.TotalSectors=total;info.ReadOnly=readOnly;return true;
    }

private static void SetEntryCluster(Byte* entry,UInt32 cluster){Write16(entry+20,(UInt16)(cluster>>16));Write16(entry+26,(UInt16)cluster);}

private static UInt16 Read16(Byte* p)=>(UInt16)(p[0]|((UInt16)p[1]<<8));

private static UInt32 Read32(Byte* p)=>(UInt32)(p[0]|((UInt32)p[1]<<8)|((UInt32)p[2]<<16)|((UInt32)p[3]<<24));

private static void Write16(Byte* p,UInt16 value){p[0]=(Byte)value;p[1]=(Byte)(value>>8);}

private static void Write32(Byte* p,UInt32 value){p[0]=(Byte)value;p[1]=(Byte)(value>>8);p[2]=(Byte)(value>>16);p[3]=(Byte)(value>>24);}

private static void Clear(Byte* p,UInt32 bytes){for(UInt32 i=0;i<bytes;i++)p[i]=0;}
}
