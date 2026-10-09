using System;

namespace Inu.Kernel.Console;

/// <summary>Provides allocation-free access to files preloaded from the Inu FAT32 system partition by BOOTX64.EFI.</summary>
public static unsafe class SystemAssetCatalog
{
    private const UInt32 AssetHashSlots = 2048U;
    private const UInt32 AssetEntrySlots = 2048U;
    private struct AssetHashIndex { internal fixed UInt32 Offsets[(Int32)AssetHashSlots]; }
    private struct AssetEntryIndex { internal fixed UInt32 Offsets[(Int32)AssetEntrySlots]; }
    private static AssetHashIndex _hashIndex;
    private static AssetEntryIndex _entryIndex;
    private static Boolean _hashReady,_entryIndexReady;
    private static Byte* _base;
    private static UInt64 _length;
    private static UInt32 _count;
    private static Boolean _available;

    /// <summary>Registers and validates the partition-authored system asset bundle carried in the boot context.</summary>
    public static Boolean Initialize<TBoot>(TBoot boot) where TBoot : ISystemAssetBundleContext
    {
        if(_available)return true;
        if(!boot.HasSystemAssetBundle())return false;
        Byte* address=(Byte*)(nuint)boot.GetSystemAssetBundleAddress();UInt64 length=boot.GetSystemAssetBundleLength();
        if(address==null||length<16UL)return false;
        if(address[0]!='N'||address[1]!='O'||address[2]!='V'||address[3]!='A'||address[4]!='S'||address[5]!='S'||address[6]!='E'||address[7]!='T')return false;
        if(ReadU32(address+8)!=1U)return false;
        UInt32 count=ReadU32(address+12);Byte* cursor=address+16;UInt64 remaining=length-16UL;
        for(UInt32 i=0U;i<count;i++)
        {
            if(remaining<16UL)return false;UInt32 pathLength=ReadU32(cursor),dataLength=ReadU32(cursor+4);UInt64 total=16UL+(UInt64)pathLength+(UInt64)dataLength;
            if(pathLength==0U||total>remaining)return false;cursor+=total;remaining-=total;
        }
        _base=address;_length=length;_count=count;_available=true;BuildHashIndex();return true;
    }

    /// <summary>Gets whether the loader supplied a valid FAT32 system asset catalogue.</summary>
    public static Boolean IsAvailable()=>_available;

    /// <summary>Finds one partition-relative asset such as SYSTEM/HELP/CPU.MAN or SYSTEM/FONTS/DEJAVU.TTF.</summary>
    public static Boolean TryFind(String path,out UInt64 address,out UInt64 length)
    {
        address=0UL;length=0UL;if(!_available||path==null||path.Length==0)return false;
        if(_hashReady&&TryFindHashed(path,out Byte* hashedData,out UInt32 hashedLength)){address=(UInt64)(nuint)hashedData;length=hashedLength;return true;}
        Byte* cursor=_base+16;UInt64 remaining=_length-16UL;
        for(UInt32 i=0U;i<_count;i++)
        {
            if(remaining<16UL)return false;UInt32 pathLength=ReadU32(cursor),dataLength=ReadU32(cursor+4);UInt64 total=16UL+(UInt64)pathLength+(UInt64)dataLength;
            if(total>remaining)return false;Byte* pathBytes=cursor+16;Byte* data=pathBytes+pathLength;
            if(PathEquals(path,pathBytes,pathLength)){address=(UInt64)(nuint)data;length=dataLength;return true;}
            cursor+=total;remaining-=total;
        }
        return false;
    }

    /// <summary>Returns one raw loader-catalogue entry by index so command packages can be discovered without a shell-side command table.</summary>
    public static Boolean TryGetAsset(UInt32 index,out Byte* path,out UInt32 pathLength,out Byte* data,out UInt32 dataLength)
    {
        path=null;pathLength=0U;data=null;dataLength=0U;if(!_available||index>=_count)return false;
        if(_entryIndexReady)
        {
            fixed(UInt32* entries=_entryIndex.Offsets)
            {
                UInt32 stored=entries[index];if(stored==0U)return false;Byte* cursor=_base+(stored-1U);UInt32 pn=ReadU32(cursor),dn=ReadU32(cursor+4);
                path=cursor+16;pathLength=pn;data=path+pn;dataLength=dn;return true;
            }
        }
        Byte* cursor=_base+16;UInt64 remaining=_length-16UL;
        for(UInt32 i=0U;i<_count;i++)
        {
            if(remaining<16UL)return false;UInt32 pn=ReadU32(cursor),dn=ReadU32(cursor+4);UInt64 total=16UL+(UInt64)pn+(UInt64)dn;if(total>remaining)return false;
            if(i==index){path=cursor+16;pathLength=pn;data=path+pn;dataLength=dn;return true;}cursor+=total;remaining-=total;
        }
        return false;
    }

    /// <summary>Finds an asset by an allocation-free ASCII path.</summary>
    public static Boolean TryFindAscii(Byte* path,UInt32 pathLength,out Byte* data,out UInt32 dataLength)
    {
        data=null;dataLength=0U;if(!_available||path==null||pathLength==0U)return false;
        if(_hashReady&&TryFindHashedAscii(path,pathLength,out data,out dataLength))return true;
        Byte* cursor=_base+16;UInt64 remaining=_length-16UL;
        for(UInt32 i=0U;i<_count;i++)
        {
            if(remaining<16UL)return false;UInt32 pn=ReadU32(cursor),dn=ReadU32(cursor+4);UInt64 total=16UL+(UInt64)pn+(UInt64)dn;if(total>remaining)return false;
            Byte* candidate=cursor+16;Byte* asset=candidate+pn;
            if(AsciiEquals(path,pathLength,candidate,pn)){data=asset;dataLength=dn;return true;}
            cursor+=total;remaining-=total;
        }
        return false;
    }

    /// <summary>Reports whether one canonical absolute directory is represented by the preloaded system asset tree.</summary>
    public static Boolean DirectoryExistsAscii(Byte* canonical,UInt32 canonicalLength)
    {
        if(!_available||canonical==null||canonicalLength==0U||canonical[0]!=(Byte)'/')return false;
        if(canonicalLength==1U)return _available;
        UInt32 prefixLength=canonicalLength-1U;Byte* prefix=canonical+1;
        Byte* cursor=_base+16;UInt64 remaining=_length-16UL;
        for(UInt32 i=0U;i<_count;i++)
        {
            if(remaining<16UL)return false;UInt32 pn=ReadU32(cursor),dn=ReadU32(cursor+4);UInt64 total=16UL+(UInt64)pn+(UInt64)dn;if(total>remaining)return false;Byte* candidate=cursor+16;
            if(pn>prefixLength&&AsciiPrefixEquals(candidate,prefix,prefixLength)&&candidate[prefixLength]==(Byte)'/')return true;
            cursor+=total;remaining-=total;
        }
        return false;
    }

    /// <summary>Enumerates unique direct children of a canonical directory in catalogue order.</summary>
    public static Boolean TryGetDirectoryEntryAscii(Byte* canonical,UInt32 canonicalLength,UInt32 wanted,Byte* name,UInt32 capacity,out UInt32 nameLength,out Boolean directory)
    {
        nameLength=0U;directory=false;if(!_available||canonical==null||canonicalLength==0U||canonical[0]!=(Byte)'/'||name==null||capacity<2U)return false;
        UInt32 prefixLength=canonicalLength==1U?0U:canonicalLength-1U;Byte* prefix=canonical+1;UInt32 unique=0U;
        Byte* lastChild=null;UInt32 lastChildLength=0U;
        Byte* cursor=_base+16;UInt64 remaining=_length-16UL;
        for(UInt32 i=0U;i<_count;i++)
        {
            if(remaining<16UL)return false;UInt32 pn=ReadU32(cursor),dn=ReadU32(cursor+4);UInt64 total=16UL+(UInt64)pn+(UInt64)dn;if(total>remaining)return false;Byte* candidate=cursor+16;
            UInt32 start=0U;
            if(prefixLength!=0U)
            {
                if(pn<=prefixLength||!AsciiPrefixEquals(candidate,prefix,prefixLength)||candidate[prefixLength]!=(Byte)'/'){cursor+=total;remaining-=total;continue;}
                start=prefixLength+1U;
            }
            if(start>=pn){cursor+=total;remaining-=total;continue;}
            UInt32 childLength=0U;while(start+childLength<pn&&candidate[start+childLength]!=(Byte)'/')childLength++;if(childLength==0U){cursor+=total;remaining-=total;continue;}
            Byte* child=candidate+start;
            Boolean duplicate=lastChild!=null&&lastChildLength==childLength&&AsciiEquals(child,childLength,lastChild,lastChildLength);
            if(!duplicate)
            {
                lastChild=child;lastChildLength=childLength;
                if(unique==wanted)
                {
                    if(childLength+1U>capacity)return false;for(UInt32 c=0U;c<childLength;c++)name[c]=child[c];nameLength=childLength;directory=start+childLength<pn;return true;
                }
                unique++;
            }
            cursor+=total;remaining-=total;
        }
        return false;
    }


    /// <summary>Advances a directory cursor through the sorted boot asset catalogue without rescanning earlier assets.</summary>
    public static Boolean TryGetNextDirectoryEntryAscii(Byte* canonical,UInt32 canonicalLength,ref UInt32 assetIndex,Byte* name,UInt32 capacity,out UInt32 nameLength,out Boolean directory)
    {
        nameLength=0U;directory=false;if(!_available||canonical==null||canonicalLength==0U||canonical[0]!=(Byte)'/'||name==null||capacity<2U)return false;
        UInt32 prefixLength=canonicalLength==1U?0U:canonicalLength-1U;Byte* prefix=canonical+1;
        for(UInt32 i=assetIndex;i<_count;i++)
        {
            if(!TryGetAsset(i,out Byte* candidate,out UInt32 pn,out _,out _)){assetIndex=_count;return false;}
            UInt32 start=0U;
            if(prefixLength!=0U)
            {
                if(pn<=prefixLength||!AsciiPrefixEquals(candidate,prefix,prefixLength)||candidate[prefixLength]!=(Byte)'/')continue;
                start=prefixLength+1U;
            }
            if(start>=pn)continue;
            UInt32 childLength=0U;while(start+childLength<pn&&candidate[start+childLength]!=(Byte)'/')childLength++;if(childLength==0U)continue;
            if(childLength+1U>capacity){assetIndex=i+1U;return false;}
            for(UInt32 c=0U;c<childLength;c++)name[c]=candidate[start+c];
            directory=start+childLength<pn;nameLength=childLength;

            // The image builder globally sorts asset paths. Skip all following assets that
            // belong to this same direct child so the next call starts at the next child.
            UInt32 next=i+1U;
            for(;next<_count;next++)
            {
                if(!TryGetAsset(next,out Byte* following,out UInt32 followingLength,out _,out _))break;
                UInt32 followingStart=0U;
                if(prefixLength!=0U)
                {
                    if(followingLength<=prefixLength||!AsciiPrefixEquals(following,prefix,prefixLength)||following[prefixLength]!=(Byte)'/')break;
                    followingStart=prefixLength+1U;
                }
                if(followingStart>=followingLength)break;
                UInt32 followingChildLength=0U;while(followingStart+followingChildLength<followingLength&&following[followingStart+followingChildLength]!=(Byte)'/')followingChildLength++;
                if(followingChildLength!=childLength||!AsciiEquals(following+followingStart,followingChildLength,candidate+start,childLength))break;
            }
            assetIndex=next;return true;
        }
        assetIndex=_count;return false;
    }

    /// <summary>Gets a text asset by an allocation-free ASCII path.</summary>
    public static Boolean TryGetTextAscii(Byte* path,UInt32 pathLength,out Byte* text,out UInt32 length)=>TryFindAscii(path,pathLength,out text,out length);

    /// <summary>Writes one text asset through a caller-provided byte sink without allocating a managed string.</summary>
    public static Boolean TryGetText(String path,out Byte* text,out UInt32 length)
    {
        text=null;length=0U;if(!TryFind(path,out UInt64 address,out UInt64 bytes)||bytes>UInt32.MaxValue)return false;
        text=(Byte*)(nuint)address;length=(UInt32)bytes;return true;
    }

    private static void BuildHashIndex()
    {
        _hashReady=false;_entryIndexReady=false;
        fixed(UInt32* slots=_hashIndex.Offsets)for(UInt32 i=0U;i<AssetHashSlots;i++)slots[i]=0U;
        fixed(UInt32* entries=_entryIndex.Offsets)for(UInt32 i=0U;i<AssetEntrySlots;i++)entries[i]=0U;
        if(!_available||_count==0U)return;
        Byte* cursor=_base+16;UInt64 remaining=_length-16UL;Boolean canHash=_count*2U<AssetHashSlots;Boolean canIndex=_count<=AssetEntrySlots;
        fixed(UInt32* slots=_hashIndex.Offsets)fixed(UInt32* entries=_entryIndex.Offsets)
        {
            for(UInt32 i=0U;i<_count;i++)
            {
                if(remaining<16UL)return;UInt32 pn=ReadU32(cursor),dn=ReadU32(cursor+4);UInt64 total=16UL+(UInt64)pn+(UInt64)dn;if(total>remaining)return;
                UInt32 offset=(UInt32)(cursor-_base);if(canIndex)entries[i]=offset+1U;
                if(canHash)
                {
                    UInt32 slot=HashAscii(cursor+16,pn)&(AssetHashSlots-1U);UInt32 probes=0U;
                    while(slots[slot]!=0U&&probes<AssetHashSlots){slot=(slot+1U)&(AssetHashSlots-1U);probes++;}
                    if(probes>=AssetHashSlots)canHash=false;else slots[slot]=offset+1U;
                }
                cursor+=total;remaining-=total;
            }
        }
        _entryIndexReady=canIndex;_hashReady=canHash;
    }
    private static Boolean TryFindHashedAscii(Byte* path,UInt32 pathLength,out Byte* data,out UInt32 dataLength)
    {
        data=null;dataLength=0U;UInt32 slot=HashAscii(path,pathLength)&(AssetHashSlots-1U);
        fixed(UInt32* slots=_hashIndex.Offsets)for(UInt32 probes=0U;probes<AssetHashSlots;probes++)
        {
            UInt32 stored=slots[slot];if(stored==0U)return false;Byte* entry=_base+(stored-1U);UInt32 pn=ReadU32(entry),dn=ReadU32(entry+4);Byte* candidate=entry+16;
            if(AsciiEquals(path,pathLength,candidate,pn)){data=candidate+pn;dataLength=dn;return true;}slot=(slot+1U)&(AssetHashSlots-1U);
        }
        return false;
    }
    private static Boolean TryFindHashed(String path,out Byte* data,out UInt32 dataLength)
    {
        data=null;dataLength=0U;UInt32 slot=HashString(path)&(AssetHashSlots-1U);
        fixed(UInt32* slots=_hashIndex.Offsets)for(UInt32 probes=0U;probes<AssetHashSlots;probes++)
        {
            UInt32 stored=slots[slot];if(stored==0U)return false;Byte* entry=_base+(stored-1U);UInt32 pn=ReadU32(entry),dn=ReadU32(entry+4);Byte* candidate=entry+16;
            if(PathEquals(path,candidate,pn)){data=candidate+pn;dataLength=dn;return true;}slot=(slot+1U)&(AssetHashSlots-1U);
        }
        return false;
    }
    private static UInt32 HashAscii(Byte* value,UInt32 length){UInt32 hash=2166136261U;for(UInt32 i=0U;i<length;i++){Byte b=value[i];if(b>='a'&&b<='z')b=(Byte)(b-32);hash^=b;hash*=16777619U;}return hash;}
    private static UInt32 HashString(String value){UInt32 hash=2166136261U;for(Int32 i=0;i<value.Length;i++){Char c=value[i];if(c>='a'&&c<='z')c=(Char)(c-32);hash^=(Byte)c;hash*=16777619U;}return hash;}

    private static Boolean PathEquals(String path,Byte* bytes,UInt32 length)
    {
        if((UInt32)path.Length!=length)return false;
        for(UInt32 i=0U;i<length;i++)
        {
            Char c=path[(Int32)i];Byte b=bytes[i];if(c>='a'&&c<='z')c=(Char)(c-32);if(b>='a'&&b<='z')b=(Byte)(b-32);if(c!=(Char)b)return false;
        }
        return true;
    }
    private static Boolean AsciiEquals(Byte* a,UInt32 an,Byte* b,UInt32 bn){if(a==null||b==null||an!=bn)return false;for(UInt32 i=0U;i<an;i++){Byte x=a[i],y=b[i];if(x>='a'&&x<='z')x=(Byte)(x-32);if(y>='a'&&y<='z')y=(Byte)(y-32);if(x!=y)return false;}return true;}
    private static Boolean AsciiPrefixEquals(Byte* value,Byte* prefix,UInt32 length){if(value==null||prefix==null)return false;for(UInt32 i=0U;i<length;i++){Byte x=value[i],y=prefix[i];if(x>='a'&&x<='z')x=(Byte)(x-32);if(y>='a'&&y<='z')y=(Byte)(y-32);if(x!=y)return false;}return true;}
    private static UInt32 ReadU32(Byte* p)=>(UInt32)(p[0]|((UInt32)p[1]<<8)|((UInt32)p[2]<<16)|((UInt32)p[3]<<24));
}
