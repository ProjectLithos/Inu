using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Heap;
using Inu.Kernel.Storage;

namespace Inu.Filesystem.FatFs;

public static unsafe partial class FatFs
{
private static Boolean Open(UInt64 mountCookie,String path,UInt32 pathOffset,KernelFileAccess access,UInt64* cookie,KernelFileType* type,UInt64* length)
    {
        if(KernelFaultInjection.ShouldInject(KernelFaultKind.FilesystemError,"fatfs",out _))return false;if(mountCookie==0||path==null||cookie==null||type==null||length==null)return false;
        MountContext* mount=(MountContext*)(nuint)mountCookie;if(mount->Info.ReadOnly&&access!=KernelFileAccess.Read)return false;
        UInt32 cluster=RootCluster(mount);UInt64 fileLength=0,entrySector=0;UInt32 entryOffset=0;Byte attributes=0;KernelFileType fileType=KernelFileType.Directory;
        Int32 cursor=(Int32)pathOffset;while(cursor<path.Length&&path[cursor]=='/')cursor++;
        while(cursor<path.Length)
        {
            Int32 start=cursor;while(cursor<path.Length&&path[cursor]!='/')cursor++;Int32 count=cursor-start;
            if(count<=0||!FindEntry(mount,cluster,path,start,count,out UInt32 next,out fileType,out fileLength,out entrySector,out entryOffset,out attributes))return false;
            cluster=next;while(cursor<path.Length&&path[cursor]=='/')cursor++;if(cursor<path.Length&&fileType!=KernelFileType.Directory)return false;
        }
        if(access!=KernelFileAccess.Read&&(attributes&0x01)!=0)return false;return CreateOpenContext(mountCookie,cluster,fileLength,entrySector,entryOffset,fileType,attributes,cookie,type,length);
    }

private static Boolean OpenAscii(UInt64 mountCookie,Byte* path,UInt32 pathLength,UInt32 pathOffset,KernelFileAccess access,UInt64* cookie,KernelFileType* type,UInt64* length)
    {
        if(KernelFaultInjection.ShouldInject(KernelFaultKind.FilesystemError,"fatfs",out _))return false;if(mountCookie==0||path==null||pathLength==0U||cookie==null||type==null||length==null)return false;
        MountContext* mount=(MountContext*)(nuint)mountCookie;if(mount->Info.ReadOnly&&access!=KernelFileAccess.Read)return false;
        UInt32 cluster=RootCluster(mount);UInt64 fileLength=0,entrySector=0;UInt32 entryOffset=0;Byte attributes=0;KernelFileType fileType=KernelFileType.Directory;
        UInt32 cursor=pathOffset;while(cursor<pathLength&&path[cursor]=='/')cursor++;
        while(cursor<pathLength)
        {
            UInt32 start=cursor;while(cursor<pathLength&&path[cursor]!='/')cursor++;UInt32 count=cursor-start;
            if(count==0U||!FindEntryAscii(mount,cluster,path,start,count,out UInt32 next,out fileType,out fileLength,out entrySector,out entryOffset,out attributes))return false;
            cluster=next;while(cursor<pathLength&&path[cursor]=='/')cursor++;if(cursor<pathLength&&fileType!=KernelFileType.Directory)return false;
        }
        if(access!=KernelFileAccess.Read&&(attributes&0x01)!=0)return false;return CreateOpenContext(mountCookie,cluster,fileLength,entrySector,entryOffset,fileType,attributes,cookie,type,length);
    }

private static Boolean CreateOpenContext(UInt64 mountCookie,UInt32 cluster,UInt64 fileLength,UInt64 entrySector,UInt32 entryOffset,KernelFileType fileType,Byte attributes,UInt64* cookie,KernelFileType* type,UInt64* length)
    {
        if(!KernelHeap.TryAllocate((UInt64)sizeof(FileContext),16,true,out KernelHeapAllocation allocation))return false;FileContext* file=(FileContext*)(nuint)allocation.Address;
        file->Allocation=allocation;file->Mount=mountCookie;file->FirstCluster=cluster;file->Length=fileLength;file->DirectorySector=entrySector;file->DirectoryOffset=entryOffset;file->Type=(Byte)fileType;file->Attributes=attributes;
        *cookie=allocation.Address;*type=fileType;*length=fileLength;return true;
    }

private static Boolean Read(UInt64 fileCookie,UInt64 position,Byte* buffer,UInt32 bytesToRead,UInt32* bytesRead)
    {
        if(KernelFaultInjection.ShouldInject(KernelFaultKind.FilesystemError,"fatfs",out _))return false;if(fileCookie==0||buffer==null||bytesRead==null)return false;*bytesRead=0;
        FileContext* file=(FileContext*)(nuint)fileCookie;if(file->Type!=(Byte)KernelFileType.File||position>=file->Length||bytesToRead==0)return true;
        MountContext* mount=(MountContext*)(nuint)file->Mount;UInt64 remaining=file->Length-position;if(remaining>bytesToRead)remaining=bytesToRead;UInt32 clusterSize=ClusterSize(mount);UInt32 cluster=file->FirstCluster;if(cluster<2U)return false;
        for(UInt64 i=0;i<position/clusterSize;i++)if(!NextCluster(mount,cluster,out cluster)||IsEndOfChain(mount->Info,cluster))return false;
        UInt32 offset=(UInt32)(position%clusterSize);if(!KernelHeap.TryAllocate(clusterSize,16,false,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;UInt32 total=0;
        while(remaining>0&&!IsEndOfChain(mount->Info,cluster))
        {
            UInt64 sector=ClusterToSector(mount->Info,cluster);if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,mount->Info.SectorsPerCluster,data,clusterSize)){KernelHeap.TryRelease(scratch);return false;}
            UInt32 available=clusterSize-offset;UInt32 take=remaining<available?(UInt32)remaining:available;for(UInt32 i=0;i<take;i++)buffer[total+i]=data[offset+i];
            total+=take;remaining-=take;offset=0;if(remaining>0&&!NextCluster(mount,cluster,out cluster)){KernelHeap.TryRelease(scratch);return false;}
        }
        KernelHeap.TryRelease(scratch);*bytesRead=total;return true;
    }

private static Boolean Write(UInt64 fileCookie,UInt64 position,Byte* buffer,UInt32 bytesToWrite,UInt32* bytesWritten)
    {
        if(KernelFaultInjection.ShouldInject(KernelFaultKind.FilesystemError,"fatfs",out _))return false;if(fileCookie==0||buffer==null||bytesWritten==null)return false;*bytesWritten=0;
        FileContext* file=(FileContext*)(nuint)fileCookie;if(file->Type!=(Byte)KernelFileType.File||bytesToWrite==0)return true;MountContext* mount=(MountContext*)(nuint)file->Mount;
        if(mount->Info.ReadOnly||(file->Attributes&0x01)!=0||position>file->Length||UInt64.MaxValue-position<(UInt64)bytesToWrite)return false;UInt64 end=position+(UInt64)bytesToWrite;
        if(!EnsureFileCapacity(mount,file,end))return false;UInt32 clusterSize=ClusterSize(mount);UInt32 cluster=file->FirstCluster;if(cluster<2U)return false;
        for(UInt64 i=0;i<position/clusterSize;i++)if(!NextCluster(mount,cluster,out cluster)||IsEndOfChain(mount->Info,cluster))return false;
        UInt32 offset=(UInt32)(position%clusterSize);if(!KernelHeap.TryAllocate(clusterSize,16,false,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;UInt32 total=0,remaining=bytesToWrite;
        while(remaining>0U&&!IsEndOfChain(mount->Info,cluster))
        {
            UInt64 sector=ClusterToSector(mount->Info,cluster);if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,mount->Info.SectorsPerCluster,data,clusterSize)){KernelHeap.TryRelease(scratch);return false;}
            UInt32 available=clusterSize-offset;UInt32 take=remaining<available?remaining:available;for(UInt32 i=0;i<take;i++)data[offset+i]=buffer[total+i];
            if(!KernelStorage.WriteVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,mount->Info.SectorsPerCluster,data,clusterSize)){KernelHeap.TryRelease(scratch);return false;}
            total+=take;remaining-=take;offset=0;if(remaining>0U&&!NextCluster(mount,cluster,out cluster)){KernelHeap.TryRelease(scratch);return false;}
        }
        KernelHeap.TryRelease(scratch);if(total!=bytesToWrite)return false;if(end>file->Length){file->Length=end;if(!UpdateDirectoryEntry(mount,file))return false;}*bytesWritten=total;return true;
    }

private static Boolean Flush(UInt64 fileCookie)
    { if(KernelFaultInjection.ShouldInject(KernelFaultKind.FilesystemError,"fatfs",out _))return false;if(fileCookie==0)return false;FileContext* file=(FileContext*)(nuint)fileCookie;MountContext* mount=(MountContext*)(nuint)file->Mount;if(!KernelStorage.TryGetVolume(new KernelStorageVolumeHandle(mount->Volume),out KernelStorageDeviceHandle device,out _))return false;return KernelStorage.Flush(device); }

private static Boolean Close(UInt64 fileCookie)
    { if(fileCookie==0)return false;FileContext* file=(FileContext*)(nuint)fileCookie;KernelHeapAllocation allocation=file->Allocation;return KernelHeap.TryRelease(allocation); }

private static Boolean ReadDirectory(UInt64 fileCookie,UInt64 entryIndex,Char* nameBuffer,UInt32 nameCapacityChars,UInt32* nameLength,KernelFileType* type,UInt64* length,KernelFilePermissions* permissions)
    {
        if(fileCookie==0||nameBuffer==null||nameCapacityChars<2U||nameLength==null||type==null||length==null||permissions==null)return false;FileContext* file=(FileContext*)(nuint)fileCookie;if(file->Type!=(Byte)KernelFileType.Directory)return false;
        MountContext* mount=(MountContext*)(nuint)file->Mount;return TryReadDirectoryEntry(mount,file->FirstCluster,entryIndex,nameBuffer,nameCapacityChars,nameLength,type,length,permissions);
    }

private static Boolean GetPermissions(UInt64 mountCookie,String path,UInt32 pathOffset,KernelFilePermissions* permissions)
    {
        if(mountCookie==0||path==null||permissions==null)return false;UInt64 cookie=0,length=0;KernelFileType type=KernelFileType.Unknown;if(!Open(mountCookie,path,pathOffset,KernelFileAccess.Read,&cookie,&type,&length))return false;
        MountContext* mount=(MountContext*)(nuint)mountCookie;KernelFilePermissions value=KernelFilePermissions.OwnerRead|KernelFilePermissions.GroupRead|KernelFilePermissions.OtherRead;FileContext* file=(FileContext*)(nuint)cookie;Boolean readOnly=mount->Info.ReadOnly||(file->Attributes&0x01)!=0;
        if(!readOnly)value|=KernelFilePermissions.OwnerWrite;else value|=KernelFilePermissions.ReadOnly;if((file->Attributes&0x02)!=0)value|=KernelFilePermissions.Hidden;if((file->Attributes&0x04)!=0)value|=KernelFilePermissions.System;if(type==KernelFileType.Directory)value|=KernelFilePermissions.OwnerExecute|KernelFilePermissions.GroupExecute|KernelFilePermissions.OtherExecute;
        Boolean closed=Close(cookie);if(!closed)return false;*permissions=value;return true;
    }

private static Boolean SetPermissions(UInt64 mountCookie,String path,UInt32 pathOffset,KernelFilePermissions permissions)=>false;
}
