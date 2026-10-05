using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Heap;
using Inu.Kernel.Storage;

namespace Inu.Filesystem.FatFs;

public static unsafe partial class FatFs
{
private static Boolean CreateFileAscii(UInt64 mountCookie,Byte* path,UInt32 pathLength,UInt32 pathOffset,Boolean overwrite)
    {
        if(mountCookie==0||path==null)return false;MountContext* mount=(MountContext*)(nuint)mountCookie;if(mount->Info.ReadOnly)return false;
        if(!ResolveParentAscii(mount,path,pathLength,pathOffset,out UInt32 parent,out UInt32 nameStart,out UInt32 nameLength))return false;
        if(FindEntryAscii(mount,parent,path,nameStart,nameLength,out UInt32 cluster,out KernelFileType type,out _,out UInt64 sector,out UInt32 offset,out Byte attributes))
        {
            if(type!=KernelFileType.File||!overwrite||(attributes&0x01)!=0)return false;if(cluster>=2U&&!FreeClusterChain(mount,cluster))return false;
            return WriteEntryClusterLength(mount,sector,offset,0U,0U,true);
        }
        Byte* entry=stackalloc Byte[32];Clear(entry,32);entry[11]=0x20;
        return WriteNamedDirectoryEntryAscii(mount,parent,path,nameStart,nameLength,entry);
    }

private static Boolean CreateDirectoryAscii(UInt64 mountCookie,Byte* path,UInt32 pathLength,UInt32 pathOffset)
    {
        if(mountCookie==0||path==null)return false;MountContext* mount=(MountContext*)(nuint)mountCookie;if(mount->Info.ReadOnly)return false;
        if(!ResolveParentAscii(mount,path,pathLength,pathOffset,out UInt32 parent,out UInt32 nameStart,out UInt32 nameLength))return false;
        if(FindEntryAscii(mount,parent,path,nameStart,nameLength,out _,out _,out _,out _,out _,out _))return false;Byte* entry=stackalloc Byte[32];Clear(entry,32);
        if(!AllocateCluster(mount,out UInt32 cluster))return false;if(!InitializeDirectoryCluster(mount,cluster,parent)){FreeClusterChain(mount,cluster);return false;}
        entry[11]=0x10;SetEntryCluster(entry,cluster);if(!WriteNamedDirectoryEntryAscii(mount,parent,path,nameStart,nameLength,entry)){FreeClusterChain(mount,cluster);return false;}return true;
    }

private static Boolean DeleteFileAscii(UInt64 mountCookie,Byte* path,UInt32 pathLength,UInt32 pathOffset)
    {
        if(mountCookie==0||path==null)return false;MountContext* mount=(MountContext*)(nuint)mountCookie;if(mount->Info.ReadOnly)return false;
        if(!ResolveParentAscii(mount,path,pathLength,pathOffset,out UInt32 parent,out UInt32 start,out UInt32 count))return false;
        if(!FindEntryAscii(mount,parent,path,start,count,out UInt32 cluster,out KernelFileType type,out _,out UInt64 sector,out UInt32 offset,out Byte attributes)||type!=KernelFileType.File||(attributes&0x01)!=0)return false;
        if(cluster>=2U&&!FreeClusterChain(mount,cluster))return false;return MarkDirectoryEntryDeleted(mount,sector,offset);
    }

private static Boolean RemoveDirectoryAscii(UInt64 mountCookie,Byte* path,UInt32 pathLength,UInt32 pathOffset)
    {
        if(mountCookie==0||path==null)return false;MountContext* mount=(MountContext*)(nuint)mountCookie;if(mount->Info.ReadOnly)return false;
        if(!ResolveParentAscii(mount,path,pathLength,pathOffset,out UInt32 parent,out UInt32 start,out UInt32 count))return false;
        if(!FindEntryAscii(mount,parent,path,start,count,out UInt32 cluster,out KernelFileType type,out _,out UInt64 sector,out UInt32 offset,out _ )||type!=KernelFileType.Directory||cluster<2U)return false;
        if(!IsDirectoryEmpty(mount,cluster))return false;if(!FreeClusterChain(mount,cluster))return false;return MarkDirectoryEntryDeleted(mount,sector,offset);
    }

private static Boolean RenameAscii(UInt64 mountCookie,Byte* source,UInt32 sourceLength,UInt32 sourceOffset,Byte* destination,UInt32 destinationLength,UInt32 destinationOffset)
    {
        if(mountCookie==0||source==null||destination==null)return false;MountContext* mount=(MountContext*)(nuint)mountCookie;if(mount->Info.ReadOnly)return false;if(PathsEqual(source,sourceLength,destination,destinationLength))return true;
        if(!ResolveParentAscii(mount,source,sourceLength,sourceOffset,out UInt32 sourceParent,out UInt32 sourceStart,out UInt32 sourceCount)||!ResolveParentAscii(mount,destination,destinationLength,destinationOffset,out UInt32 destinationParent,out UInt32 destinationStart,out UInt32 destinationCount))return false;
        if(IsDescendantMove(source,sourceLength,destination,destinationStart))return false;
        if(!FindEntryAscii(mount,sourceParent,source,sourceStart,sourceCount,out UInt32 cluster,out KernelFileType type,out _,out UInt64 sourceSector,out UInt32 sourceEntryOffset,out _))return false;
        if(FindEntryAscii(mount,destinationParent,destination,destinationStart,destinationCount,out _,out _,out _,out _,out _,out _))return false;
        Byte* entry=stackalloc Byte[32];if(!ReadDirectoryEntryRaw(mount,sourceSector,sourceEntryOffset,entry)||!EncodeShortName(entry,destination,destinationStart,destinationCount))return false;
        if(!FindFreeDirectorySlot(mount,destinationParent,out UInt64 targetSector,out UInt32 targetOffset)||!WriteDirectoryEntry(mount,targetSector,targetOffset,entry))return false;
        if(type==KernelFileType.Directory&&cluster>=2U&&sourceParent!=destinationParent&&!UpdateDotDot(mount,cluster,destinationParent))return false;
        return MarkDirectoryEntryDeleted(mount,sourceSector,sourceEntryOffset);
    }

private static Boolean EnsureFileCapacity(MountContext* mount,FileContext* file,UInt64 length)
    {
        if(length==0UL)return true;UInt32 clusterSize=ClusterSize(mount);UInt64 needed=(length+(UInt64)clusterSize-1UL)/(UInt64)clusterSize;if(needed>mount->Info.ClusterCount)return false;
        UInt32 first=file->FirstCluster,last=0,count=0;if(first>=2U)
        {
            UInt32 current=first;while(current>=2U&&!IsEndOfChain(mount->Info,current)){count++;last=current;if((UInt64)count>=needed)break;if(!NextCluster(mount,current,out UInt32 next))return false;if(IsEndOfChain(mount->Info,next))break;current=next;}
        }
        if(first<2U)
        {
            if(!AllocateCluster(mount,out first))return false;file->FirstCluster=first;last=first;count=1;if(!UpdateDirectoryEntry(mount,file))return false;
        }
        while((UInt64)count<needed)
        {
            if(!AllocateCluster(mount,out UInt32 next))return false;if(!SetFatEntry(mount,last,next)){FreeClusterChain(mount,next);return false;}last=next;count++;
        }
        return true;
    }

private static Boolean UpdateDirectoryEntry(MountContext* mount,FileContext* file)
    { if(file->DirectorySector==0UL)return false;return WriteEntryClusterLength(mount,file->DirectorySector,file->DirectoryOffset,file->FirstCluster,file->Length,false); }

private static Boolean WriteEntryClusterLength(MountContext* mount,UInt64 sector,UInt32 offset,UInt32 cluster,UInt64 length,Boolean clearAttributes)
    {
        UInt32 bps=mount->Info.BytesPerSector;if(offset+32U>bps||length>0xFFFFFFFFUL||!KernelHeap.TryAllocate(bps,16,false,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;
        if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}SetEntryCluster(data+offset,cluster);Write32(data+offset+28,(UInt32)length);if(clearAttributes)data[offset+11]=0x20;
        Boolean ok=KernelStorage.WriteVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps);KernelHeap.TryRelease(scratch);return ok;
    }

private static Boolean ResolveParentAscii(MountContext* mount,Byte* path,UInt32 length,UInt32 pathOffset,out UInt32 parentCluster,out UInt32 nameStart,out UInt32 nameLength)
    {
        parentCluster=RootCluster(mount);nameStart=0;nameLength=0;if(path==null||length<=pathOffset)return false;UInt32 cursor=pathOffset;while(cursor<length&&path[cursor]=='/')cursor++;if(cursor>=length)return false;
        while(cursor<length)
        {
            UInt32 start=cursor;while(cursor<length&&path[cursor]!='/')cursor++;UInt32 count=cursor-start;while(cursor<length&&path[cursor]=='/')cursor++;
            if(cursor>=length){nameStart=start;nameLength=count;return count>0U&&!IsDotName(path,start,count);}
            if(count==0U||!FindEntryAscii(mount,parentCluster,path,start,count,out UInt32 next,out KernelFileType type,out _,out _,out _,out _)||type!=KernelFileType.Directory)return false;parentCluster=next;
        }
        return false;
    }
}
