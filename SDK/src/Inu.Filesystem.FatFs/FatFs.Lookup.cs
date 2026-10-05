using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Heap;
using Inu.Kernel.Storage;

namespace Inu.Filesystem.FatFs;

public static unsafe partial class FatFs
{
private static Boolean TryReadDirectoryEntry(MountContext* mount,UInt32 directoryCluster,UInt64 wanted,Char* nameBuffer,UInt32 capacity,UInt32* nameLength,KernelFileType* type,UInt64* length,KernelFilePermissions* permissions)
    {
        UInt32 bps=mount->Info.BytesPerSector;if(!KernelHeap.TryAllocate(bps,16,false,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;UInt64 seen=0;
        if(directoryCluster==0U&&mount->Info.Format!=(Byte)FatFsFormat.Fat32)
        {
            for(UInt32 s=0;s<mount->Info.RootDirectorySectors;s++){if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),(UInt64)mount->Info.FirstRootSector+s,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}if(TryDirectoryEntryInSector(data,bps,wanted,&seen,nameBuffer,capacity,nameLength,type,length,permissions,mount->Info.ReadOnly)){KernelHeap.TryRelease(scratch);return true;}if(ContainsDirectoryTerminator(data,bps)){KernelHeap.TryRelease(scratch);return false;}}KernelHeap.TryRelease(scratch);return false;
        }
        UInt32 current=directoryCluster;while(current>=2U&&!IsEndOfChain(mount->Info,current)){UInt64 first=ClusterToSector(mount->Info,current);for(UInt32 s=0;s<mount->Info.SectorsPerCluster;s++){if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),first+s,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}if(TryDirectoryEntryInSector(data,bps,wanted,&seen,nameBuffer,capacity,nameLength,type,length,permissions,mount->Info.ReadOnly)){KernelHeap.TryRelease(scratch);return true;}if(ContainsDirectoryTerminator(data,bps)){KernelHeap.TryRelease(scratch);return false;}}if(!NextCluster(mount,current,out current)){KernelHeap.TryRelease(scratch);return false;}}KernelHeap.TryRelease(scratch);return false;
    }

private static Boolean TryDirectoryEntryInSector(Byte* data,UInt32 bytes,UInt64 wanted,UInt64* seen,Char* nameBuffer,UInt32 capacity,UInt32* nameLength,KernelFileType* type,UInt64* length,KernelFilePermissions* permissions,Boolean volumeReadOnly)
    {
        Char* longName=stackalloc Char[256];UInt32 longLength=0;Byte longChecksum=0;Boolean longActive=false;
        for(UInt32 offset=0;offset+32U<=bytes;offset+=32U){Byte first=data[offset];if(first==0)return false;if(first==0xE5){longActive=false;longLength=0;continue;}Byte attr=data[offset+11];if(attr==0x0F){ConsumeLongNameEntry(data+offset,longName,&longLength,&longChecksum,&longActive);continue;}if((attr&0x08)!=0){longActive=false;longLength=0;continue;}if(*seen!=wanted){(*seen)++;longActive=false;longLength=0;continue;}UInt32 n=0;if(longActive&&ShortChecksum(data+offset)==longChecksum){if(longLength+1U>capacity)return false;for(UInt32 i=0;i<longLength;i++)nameBuffer[n++]=longName[i];}else{for(UInt32 i=0;i<8U&&data[offset+i]!=' ';i++){if(n+1U>=capacity)return false;nameBuffer[n++]=(Char)data[offset+i];}Boolean hasExt=false;for(UInt32 i=0;i<3U;i++)if(data[offset+8U+i]!=' '){hasExt=true;break;}if(hasExt){if(n+2U>=capacity)return false;nameBuffer[n++]='.';for(UInt32 i=0;i<3U&&data[offset+8U+i]!=' ';i++){if(n+1U>=capacity)return false;nameBuffer[n++]=(Char)data[offset+8U+i];}}}nameBuffer[n]='\0';*nameLength=n;*type=(attr&0x10)!=0?KernelFileType.Directory:KernelFileType.File;*length=Read32(data+offset+28);KernelFilePermissions p=KernelFilePermissions.OwnerRead|KernelFilePermissions.GroupRead|KernelFilePermissions.OtherRead;if(volumeReadOnly||(attr&0x01)!=0)p|=KernelFilePermissions.ReadOnly;else p|=KernelFilePermissions.OwnerWrite;if((attr&0x02)!=0)p|=KernelFilePermissions.Hidden;if((attr&0x04)!=0)p|=KernelFilePermissions.System;if(*type==KernelFileType.Directory)p|=KernelFilePermissions.OwnerExecute|KernelFilePermissions.GroupExecute|KernelFilePermissions.OtherExecute;*permissions=p;return true;}return false;
    }

private static Boolean FindEntry(MountContext* mount,UInt32 directoryCluster,String path,Int32 start,Int32 count,out UInt32 cluster,out KernelFileType type,out UInt64 length,out UInt64 entrySector,out UInt32 entryOffset,out Byte attributes)
    {
        cluster=0;type=KernelFileType.Unknown;length=0;entrySector=0;entryOffset=0;attributes=0;UInt32 bps=mount->Info.BytesPerSector;if(!KernelHeap.TryAllocate(bps,16,false,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;
        if(directoryCluster==0U&&mount->Info.Format!=(Byte)FatFsFormat.Fat32){for(UInt32 s=0;s<mount->Info.RootDirectorySectors;s++){UInt64 sector=(UInt64)mount->Info.FirstRootSector+s;if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}if(FindEntryInSector(data,bps,path,start,count,out cluster,out type,out length,out UInt32 offset,out attributes)){entrySector=sector;entryOffset=offset;KernelHeap.TryRelease(scratch);return true;}if(ContainsDirectoryTerminator(data,bps)){KernelHeap.TryRelease(scratch);return false;}}KernelHeap.TryRelease(scratch);return false;}
        UInt32 current=directoryCluster;while(current>=2U&&!IsEndOfChain(mount->Info,current)){UInt64 first=ClusterToSector(mount->Info,current);for(UInt32 s=0;s<mount->Info.SectorsPerCluster;s++){UInt64 sector=first+s;if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}if(FindEntryInSector(data,bps,path,start,count,out cluster,out type,out length,out UInt32 offset,out attributes)){entrySector=sector;entryOffset=offset;KernelHeap.TryRelease(scratch);return true;}if(ContainsDirectoryTerminator(data,bps)){KernelHeap.TryRelease(scratch);return false;}}if(!NextCluster(mount,current,out current)){KernelHeap.TryRelease(scratch);return false;}}KernelHeap.TryRelease(scratch);return false;
    }

private static Boolean FindEntryInSector(Byte* data,UInt32 bytes,String path,Int32 start,Int32 count,out UInt32 cluster,out KernelFileType type,out UInt64 length,out UInt32 entryOffset,out Byte attributes)
    { cluster=0;type=KernelFileType.Unknown;length=0;entryOffset=0;attributes=0;Char* longName=stackalloc Char[256];UInt32 longLength=0;Byte longChecksum=0;Boolean longActive=false;for(UInt32 offset=0;offset+32U<=bytes;offset+=32U){Byte first=data[offset];if(first==0)return false;if(first==0xE5){longActive=false;longLength=0;continue;}Byte attr=data[offset+11];if(attr==0x0F){ConsumeLongNameEntry(data+offset,longName,&longLength,&longChecksum,&longActive);continue;}if((attr&0x08)!=0){longActive=false;longLength=0;continue;}Boolean match=longActive&&ShortChecksum(data+offset)==longChecksum&&LongNameMatches(longName,longLength,path,start,count);if(!match)match=NameMatches(data+offset,path,start,count);longActive=false;longLength=0;if(!match)continue;UInt32 high=Read16(data+offset+20),low=Read16(data+offset+26);cluster=(high<<16)|low;length=Read32(data+offset+28);type=(attr&0x10)!=0?KernelFileType.Directory:KernelFileType.File;entryOffset=offset;attributes=attr;return true;}return false; }

private static void ConsumeLongNameEntry(Byte* entry,Char* buffer,UInt32* length,Byte* checksum,Boolean* active)
    {
        Byte ordinal=(Byte)(entry[0]&0x1FU);if(ordinal==0U||ordinal>20U){*active=false;*length=0;return;}if((entry[0]&0x40U)!=0U){*active=true;*length=0;*checksum=entry[13];for(UInt32 i=0;i<256U;i++)buffer[i]='\0';}if(!*active||entry[13]!=*checksum)return;UInt32 baseIndex=(UInt32)(ordinal-1U)*13U;for(UInt32 i=0;i<13U;i++){UInt16 c=Read16(entry+LfnOffset(i));if(c==0U||c==0xFFFFU)break;UInt32 at=baseIndex+i;if(at>=255U){*active=false;*length=0;return;}buffer[at]=(Char)c;if(at+1U>*length)*length=at+1U;}
    }

private static UInt32 LfnOffset(UInt32 i)=>i<5U?1U+i*2U:i<11U?14U+(i-5U)*2U:28U+(i-11U)*2U;

private static Byte ShortChecksum(Byte* entry){Byte sum=0;for(UInt32 i=0;i<11U;i++)sum=(Byte)(((sum&1U)!=0U?0x80U:0U)+(sum>>1)+entry[i]);return sum;}

private static Boolean LongNameMatches(Char* name,UInt32 nameLength,String path,Int32 start,Int32 count){if(nameLength!=(UInt32)count)return false;for(Int32 i=0;i<count;i++)if(Upper(name[i])!=Upper(path[start+i]))return false;return true;}

private static Boolean LongNameMatchesAscii(Char* name,UInt32 nameLength,Byte* path,UInt32 start,UInt32 count){if(nameLength!=count)return false;for(UInt32 i=0;i<count;i++){Char p=(Char)path[start+i];if(Upper(name[i])!=Upper(p))return false;}return true;}

private static Boolean ContainsDirectoryTerminator(Byte* data,UInt32 bytes){for(UInt32 o=0;o+32U<=bytes;o+=32U)if(data[o]==0)return true;return false;}

private static Boolean NameMatches(Byte* entry,String path,Int32 start,Int32 count)
    { Int32 dot=-1;for(Int32 i=0;i<count;i++)if(path[start+i]=='.'){dot=i;break;}Int32 baseCount=dot>=0?dot:count,extCount=dot>=0?count-dot-1:0;if(baseCount<1||baseCount>8||extCount>3)return false;for(Int32 i=0;i<8;i++){Char expected=i<baseCount?Upper(path[start+i]):' ';if((Char)entry[i]!=expected)return false;}for(Int32 i=0;i<3;i++){Char expected=i<extCount?Upper(path[start+dot+1+i]):' ';if((Char)entry[8+i]!=expected)return false;}return true; }

private static Char Upper(Char c)=>c>='a'&&c<='z'?(Char)(c-32):c;
}
