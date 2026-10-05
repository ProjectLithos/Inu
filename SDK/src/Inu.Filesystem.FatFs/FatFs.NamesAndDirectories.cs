using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Heap;
using Inu.Kernel.Storage;

namespace Inu.Filesystem.FatFs;

public static unsafe partial class FatFs
{
private static Boolean IsDotName(Byte* path,UInt32 start,UInt32 count)=>count==1U&&path[start]=='.'||count==2U&&path[start]=='.'&&path[start+1U]=='.';

private static Boolean FindEntryAscii(MountContext* mount,UInt32 directoryCluster,Byte* path,UInt32 start,UInt32 count,out UInt32 cluster,out KernelFileType type,out UInt64 length,out UInt64 entrySector,out UInt32 entryOffset,out Byte attributes)
    {
        cluster=0;type=KernelFileType.Unknown;length=0;entrySector=0;entryOffset=0;attributes=0;UInt32 bps=mount->Info.BytesPerSector;if(!KernelHeap.TryAllocate(bps,16,false,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;
        if(directoryCluster==0U&&mount->Info.Format!=(Byte)FatFsFormat.Fat32)
        {
            for(UInt32 s=0;s<mount->Info.RootDirectorySectors;s++){UInt64 sector=(UInt64)mount->Info.FirstRootSector+s;if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}if(FindEntryInSectorAscii(data,bps,path,start,count,out cluster,out type,out length,out UInt32 offset,out attributes)){entrySector=sector;entryOffset=offset;KernelHeap.TryRelease(scratch);return true;}if(ContainsDirectoryTerminator(data,bps)){KernelHeap.TryRelease(scratch);return false;}}
            KernelHeap.TryRelease(scratch);return false;
        }
        UInt32 current=directoryCluster;while(current>=2U&&!IsEndOfChain(mount->Info,current))
        {
            UInt64 first=ClusterToSector(mount->Info,current);for(UInt32 s=0;s<mount->Info.SectorsPerCluster;s++){UInt64 sector=first+s;if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}if(FindEntryInSectorAscii(data,bps,path,start,count,out cluster,out type,out length,out UInt32 offset,out attributes)){entrySector=sector;entryOffset=offset;KernelHeap.TryRelease(scratch);return true;}if(ContainsDirectoryTerminator(data,bps)){KernelHeap.TryRelease(scratch);return false;}}
            if(!NextCluster(mount,current,out current)){KernelHeap.TryRelease(scratch);return false;}
        }
        KernelHeap.TryRelease(scratch);return false;
    }

private static Boolean FindEntryInSectorAscii(Byte* data,UInt32 bytes,Byte* path,UInt32 start,UInt32 count,out UInt32 cluster,out KernelFileType type,out UInt64 length,out UInt32 entryOffset,out Byte attributes)
    {
        cluster=0;type=KernelFileType.Unknown;length=0;entryOffset=0;attributes=0;Char* longName=stackalloc Char[256];UInt32 longLength=0;Byte longChecksum=0;Boolean longActive=false;
        for(UInt32 offset=0;offset+32U<=bytes;offset+=32U){Byte first=data[offset];if(first==0)return false;if(first==0xE5){longActive=false;longLength=0;continue;}Byte attr=data[offset+11];if(attr==0x0F){ConsumeLongNameEntry(data+offset,longName,&longLength,&longChecksum,&longActive);continue;}if((attr&0x08)!=0){longActive=false;longLength=0;continue;}Boolean match=longActive&&ShortChecksum(data+offset)==longChecksum&&LongNameMatchesAscii(longName,longLength,path,start,count);if(!match)match=NameMatchesAscii(data+offset,path,start,count);longActive=false;longLength=0;if(!match)continue;UInt32 high=Read16(data+offset+20),low=Read16(data+offset+26);cluster=(high<<16)|low;length=Read32(data+offset+28);type=(attr&0x10)!=0?KernelFileType.Directory:KernelFileType.File;entryOffset=offset;attributes=attr;return true;}return false;
    }

private static Boolean WriteNamedDirectoryEntryAscii(MountContext* mount,UInt32 directoryCluster,Byte* path,UInt32 start,UInt32 count,Byte* shortEntry)
    {
        if(mount==null||path==null||shortEntry==null||count==0U||count>255U)return false;
        if(EncodeShortName(shortEntry,path,start,count))
            return FindFreeDirectorySlot(mount,directoryCluster,out UInt64 sector,out UInt32 offset)&&WriteDirectoryEntry(mount,sector,offset,shortEntry);
        if(!BuildUniqueShortAlias(mount,directoryCluster,path,start,count,shortEntry))return false;
        UInt32 longEntries=(count+12U)/13U,total=longEntries+1U;if(total>21U)return false;
        UInt64* sectors=stackalloc UInt64[21];UInt32* offsets=stackalloc UInt32[21];Byte* lfn=stackalloc Byte[32];
        if(!FindFreeDirectorySlots(mount,directoryCluster,total,sectors,offsets))return false;
        Byte checksum=ShortChecksum(shortEntry);UInt32 written=0U;
        for(UInt32 slot=0U;slot<longEntries;slot++)
        {
            UInt32 ordinal=longEntries-slot;for(UInt32 i=0;i<32U;i++)lfn[i]=0xFF;
            lfn[0]=(Byte)ordinal;if(ordinal==longEntries)lfn[0]|=0x40;lfn[11]=0x0F;lfn[12]=0;lfn[13]=checksum;lfn[26]=0;lfn[27]=0;
            UInt32 baseIndex=(ordinal-1U)*13U;
            for(UInt32 i=0;i<13U;i++)
            {
                UInt32 at=baseIndex+i;UInt16 value=at<count?(UInt16)path[start+at]:at==count?(UInt16)0:(UInt16)0xFFFF;
                Write16(lfn+LfnOffset(i),value);
            }
            if(!WriteDirectoryEntry(mount,sectors[slot],offsets[slot],lfn)){RollbackDirectorySlots(mount,sectors,offsets,written);return false;}written++;
        }
        if(!WriteDirectoryEntry(mount,sectors[longEntries],offsets[longEntries],shortEntry)){RollbackDirectorySlots(mount,sectors,offsets,written);return false;}
        return true;
    }

private static void RollbackDirectorySlots(MountContext* mount,UInt64* sectors,UInt32* offsets,UInt32 count)
    { for(UInt32 i=0;i<count;i++)MarkDirectoryEntryDeleted(mount,sectors[i],offsets[i]); }

private static Boolean BuildUniqueShortAlias(MountContext* mount,UInt32 directoryCluster,Byte* path,UInt32 start,UInt32 count,Byte* entry)
    {
        Byte* display=stackalloc Byte[13];
        for(UInt32 attempt=1U;attempt<=9999U;attempt++)
        {
            if(!BuildShortAlias(entry,path,start,count,attempt))return false;UInt32 n=ShortEntryToDisplay(entry,display);
            if(!FindEntryAscii(mount,directoryCluster,display,0U,n,out _,out _,out _,out _,out _,out _))return true;
        }
        return false;
    }

private static Boolean BuildShortAlias(Byte* entry,Byte* path,UInt32 start,UInt32 count,UInt32 attempt)
    {
        if(entry==null||path==null||count==0U)return false;for(UInt32 i=0;i<11U;i++)entry[i]=(Byte)' ';
        UInt32 lastDot=UInt32.MaxValue;for(UInt32 i=1U;i<count;i++)if(path[start+i]=='.')lastDot=i;
        UInt32 baseEnd=lastDot==UInt32.MaxValue?count:lastDot;UInt32 digits=DecimalDigits(attempt),stemLimit=digits+1U<8U?8U-digits-1U:1U,stem=0U;
        for(UInt32 i=0U;i<baseEnd&&stem<stemLimit;i++)
        {
            Byte c=UpperAscii(path[start+i]);if(c==' '||c=='.')continue;if(!ValidShortChar(c))c=(Byte)'_';entry[stem++]=c;
        }
        if(stem==0U)entry[stem++]=(Byte)'_';entry[stem++]=(Byte)'~';UInt32 divisor=Pow10(digits-1U);for(UInt32 i=0U;i<digits;i++){entry[stem++]=(Byte)('0'+(attempt/divisor)%10U);if(divisor>1U)divisor/=10U;}
        if(lastDot!=UInt32.MaxValue&&lastDot+1U<count){UInt32 ext=0U;for(UInt32 i=lastDot+1U;i<count&&ext<3U;i++){Byte c=UpperAscii(path[start+i]);if(c==' '||c=='.')continue;if(!ValidShortChar(c))c=(Byte)'_';entry[8U+ext++]=c;}}
        return true;
    }

private static UInt32 ShortEntryToDisplay(Byte* entry,Byte* output)
    {
        UInt32 n=0U;for(UInt32 i=0U;i<8U&&entry[i]!=' ';i++)output[n++]=entry[i];Boolean ext=false;for(UInt32 i=8U;i<11U;i++)if(entry[i]!=' '){ext=true;break;}if(ext){output[n++]=(Byte)'.';for(UInt32 i=8U;i<11U&&entry[i]!=' ';i++)output[n++]=entry[i];}return n;
    }

private static UInt32 DecimalDigits(UInt32 value){UInt32 n=1U;while(value>=10U){value/=10U;n++;}return n;}

private static UInt32 Pow10(UInt32 power){UInt32 value=1U;while(power--!=0U)value*=10U;return value;}

private static Boolean FindFreeDirectorySlots(MountContext* mount,UInt32 directoryCluster,UInt32 needed,UInt64* sectors,UInt32* offsets)
    {
        if(mount==null||needed==0U||needed>21U||sectors==null||offsets==null)return false;UInt32 bps=mount->Info.BytesPerSector,run=0U;
        if(!KernelHeap.TryAllocate(bps,16,false,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;
        if(directoryCluster==0U&&mount->Info.Format!=(Byte)FatFsFormat.Fat32)
        {
            for(UInt32 s=0U;s<mount->Info.RootDirectorySectors;s++){UInt64 sector=(UInt64)mount->Info.FirstRootSector+s;if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}for(UInt32 o=0U;o+32U<=bps;o+=32U){if(data[o]==0U||data[o]==0xE5){sectors[run]=sector;offsets[run]=o;if(++run==needed){KernelHeap.TryRelease(scratch);return true;}}else run=0U;}}KernelHeap.TryRelease(scratch);return false;
        }
        UInt32 current=directoryCluster,last=current;
        while(current>=2U&&!IsEndOfChain(mount->Info,current))
        {
            last=current;UInt64 first=ClusterToSector(mount->Info,current);for(UInt32 s=0U;s<mount->Info.SectorsPerCluster;s++){UInt64 sector=first+s;if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}for(UInt32 o=0U;o+32U<=bps;o+=32U){if(data[o]==0U||data[o]==0xE5){sectors[run]=sector;offsets[run]=o;if(++run==needed){KernelHeap.TryRelease(scratch);return true;}}else run=0U;}}if(!NextCluster(mount,current,out UInt32 next)){KernelHeap.TryRelease(scratch);return false;}if(IsEndOfChain(mount->Info,next))break;current=next;
        }
        KernelHeap.TryRelease(scratch);if(last<2U)return false;
        while(run<needed)
        {
            if(!AllocateCluster(mount,out UInt32 added)||!SetFatEntry(mount,last,added))return false;last=added;UInt64 first=ClusterToSector(mount->Info,added);
            for(UInt32 s=0U;s<mount->Info.SectorsPerCluster&&run<needed;s++)for(UInt32 o=0U;o+32U<=bps&&run<needed;o+=32U){sectors[run]=first+s;offsets[run]=o;run++;}
        }
        return true;
    }

private static Boolean FindFreeDirectorySlot(MountContext* mount,UInt32 directoryCluster,out UInt64 sector,out UInt32 offset)
    {
        sector=0;offset=0;UInt32 bps=mount->Info.BytesPerSector;if(!KernelHeap.TryAllocate(bps,16,false,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;
        if(directoryCluster==0U&&mount->Info.Format!=(Byte)FatFsFormat.Fat32)
        {
            for(UInt32 s=0;s<mount->Info.RootDirectorySectors;s++){UInt64 currentSector=(UInt64)mount->Info.FirstRootSector+s;if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),currentSector,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}for(UInt32 o=0;o+32U<=bps;o+=32U)if(data[o]==0U||data[o]==0xE5){sector=currentSector;offset=o;KernelHeap.TryRelease(scratch);return true;}}KernelHeap.TryRelease(scratch);return false;
        }
        UInt32 current=directoryCluster,last=current;while(current>=2U&&!IsEndOfChain(mount->Info,current))
        {
            last=current;UInt64 first=ClusterToSector(mount->Info,current);for(UInt32 s=0;s<mount->Info.SectorsPerCluster;s++){UInt64 currentSector=first+s;if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),currentSector,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}for(UInt32 o=0;o+32U<=bps;o+=32U)if(data[o]==0U||data[o]==0xE5){sector=currentSector;offset=o;KernelHeap.TryRelease(scratch);return true;}}
            if(!NextCluster(mount,current,out UInt32 next)){KernelHeap.TryRelease(scratch);return false;}if(IsEndOfChain(mount->Info,next))break;current=next;
        }
        KernelHeap.TryRelease(scratch);if(last<2U||!AllocateCluster(mount,out UInt32 added)||!SetFatEntry(mount,last,added))return false;sector=ClusterToSector(mount->Info,added);offset=0;return true;
    }

private static Boolean InitializeDirectoryCluster(MountContext* mount,UInt32 cluster,UInt32 parent)
    {
        // AllocateCluster already zeroes a newly allocated cluster. Do not issue a second
        // whole-cluster write here; only write the first sector containing . and ...
        UInt32 bps=mount->Info.BytesPerSector;if(!KernelHeap.TryAllocate(bps,16,true,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;
        for(UInt32 i=0;i<11U;i++){data[i]=(Byte)' ';data[32U+i]=(Byte)' ';}data[0]=(Byte)'.';data[11]=0x10;SetEntryCluster(data,cluster);data[32]=(Byte)'.';data[33]=(Byte)'.';data[43]=0x10;SetEntryCluster(data+32,parent);
        Boolean ok=KernelStorage.WriteVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),ClusterToSector(mount->Info,cluster),1,data,bps);KernelHeap.TryRelease(scratch);return ok;
    }

private static Boolean UpdateDotDot(MountContext* mount,UInt32 cluster,UInt32 parent)
    {
        UInt32 bps=mount->Info.BytesPerSector;if(!KernelHeap.TryAllocate(bps,16,false,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;UInt64 sector=ClusterToSector(mount->Info,cluster);
        if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}if(data[32]!='.'||data[33]!='.'){KernelHeap.TryRelease(scratch);return false;}SetEntryCluster(data+32,parent);Boolean ok=KernelStorage.WriteVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps);KernelHeap.TryRelease(scratch);return ok;
    }

private static Boolean IsDirectoryEmpty(MountContext* mount,UInt32 directoryCluster)
    {
        UInt32 bps=mount->Info.BytesPerSector;if(!KernelHeap.TryAllocate(bps,16,false,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;UInt32 current=directoryCluster;
        while(current>=2U&&!IsEndOfChain(mount->Info,current))
        {
            UInt64 first=ClusterToSector(mount->Info,current);for(UInt32 s=0;s<mount->Info.SectorsPerCluster;s++){if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),first+s,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}for(UInt32 o=0;o+32U<=bps;o+=32U){Byte firstByte=data[o];if(firstByte==0){KernelHeap.TryRelease(scratch);return true;}Byte attr=data[o+11];if(firstByte==0xE5||attr==0x0F||(attr&0x08)!=0)continue;if(data[o]=='.'&&(data[o+1]==' '||(data[o+1]=='.'&&data[o+2]==' ')))continue;KernelHeap.TryRelease(scratch);return false;}}
            if(!NextCluster(mount,current,out current)){KernelHeap.TryRelease(scratch);return false;}
        }
        KernelHeap.TryRelease(scratch);return true;
    }

private static Boolean ReadDirectoryEntryRaw(MountContext* mount,UInt64 sector,UInt32 offset,Byte* entry)
    { UInt32 bps=mount->Info.BytesPerSector;if(entry==null||offset+32U>bps||!KernelHeap.TryAllocate(bps,16,false,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}for(UInt32 i=0;i<32U;i++)entry[i]=data[offset+i];KernelHeap.TryRelease(scratch);return true; }

private static Boolean WriteDirectoryEntry(MountContext* mount,UInt64 sector,UInt32 offset,Byte* entry)
    { UInt32 bps=mount->Info.BytesPerSector;if(entry==null||offset+32U>bps||!KernelHeap.TryAllocate(bps,16,false,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}for(UInt32 i=0;i<32U;i++)data[offset+i]=entry[i];Boolean ok=KernelStorage.WriteVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps);KernelHeap.TryRelease(scratch);return ok; }

private static Boolean MarkDirectoryEntryDeleted(MountContext* mount,UInt64 sector,UInt32 offset)
    { UInt32 bps=mount->Info.BytesPerSector;if(offset+32U>bps||!KernelHeap.TryAllocate(bps,16,false,out KernelHeapAllocation scratch))return false;Byte* data=(Byte*)(nuint)scratch.Address;if(!KernelStorage.ReadVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps)){KernelHeap.TryRelease(scratch);return false;}data[offset]=0xE5;Boolean ok=KernelStorage.WriteVolumeBlocks(new KernelStorageVolumeHandle(mount->Volume),sector,1,data,bps);KernelHeap.TryRelease(scratch);return ok; }

private static Boolean EncodeShortName(Byte* entry,Byte* path,UInt32 start,UInt32 count)
    {
        if(entry==null||path==null||count==0U||IsDotName(path,start,count))return false;UInt32 dot=UInt32.MaxValue;for(UInt32 i=0;i<count;i++){if(path[start+i]=='.'){if(dot!=UInt32.MaxValue)return false;dot=i;}}
        UInt32 baseCount=dot==UInt32.MaxValue?count:dot,extCount=dot==UInt32.MaxValue?0U:count-dot-1U;if(baseCount<1U||baseCount>8U||extCount>3U)return false;
        for(UInt32 i=0;i<11U;i++)entry[i]=(Byte)' ';for(UInt32 i=0;i<baseCount;i++){Byte c=UpperAscii(path[start+i]);if(!ValidShortChar(c))return false;entry[i]=c;}for(UInt32 i=0;i<extCount;i++){Byte c=UpperAscii(path[start+dot+1U+i]);if(!ValidShortChar(c))return false;entry[8U+i]=c;}return true;
    }

private static Boolean ValidShortChar(Byte c)
    { if(c<33U||c>126U)return false;return c!='"'&&c!='*'&&c!='+'&&c!=','&&c!='/'&&c!=':'&&c!=';'&&c!='<'&&c!='='&&c!='>'&&c!='?'&&c!='['&&c!='\\'&&c!=']'&&c!='|'; }

private static Byte UpperAscii(Byte c)=>c>='a'&&c<='z'?(Byte)(c-32):c;

private static Boolean NameMatchesAscii(Byte* entry,Byte* path,UInt32 start,UInt32 count)
    { Byte* wanted=stackalloc Byte[11];for(UInt32 i=0;i<11U;i++)wanted[i]=(Byte)' ';if(!EncodeShortName(wanted,path,start,count))return false;for(UInt32 i=0;i<11U;i++)if(entry[i]!=wanted[i])return false;return true; }

private static Boolean PathsEqual(Byte* a,UInt32 an,Byte* b,UInt32 bn)
    { if(an!=bn)return false;for(UInt32 i=0;i<an;i++){Byte x=UpperAscii(a[i]),y=UpperAscii(b[i]);if(x!=y)return false;}return true; }

private static Boolean IsDescendantMove(Byte* source,UInt32 sourceLength,Byte* destination,UInt32 destinationNameStart)
    { if(sourceLength==0U||destinationNameStart<=sourceLength)return false;for(UInt32 i=0;i<sourceLength;i++)if(UpperAscii(source[i])!=UpperAscii(destination[i]))return false;return destination[sourceLength]=='/'; }
}
