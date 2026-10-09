using System;

namespace Inu.Kernel.Console;

/// <summary>Provides allocation-free access to files preloaded from the Inu FAT32 system partition by BOOTX64.EFI.</summary>
public static unsafe class SystemAssetCatalog
{
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
        _base=address;_length=length;_count=count;_available=true;return true;
    }

    /// <summary>Gets whether the loader supplied a valid FAT32 system asset catalogue.</summary>
    public static Boolean IsAvailable()=>_available;

    /// <summary>Finds one partition-relative asset such as SYSTEM/HELP/CPU.MAN or SYSTEM/FONTS/DEJAVU.TTF.</summary>
    public static Boolean TryFind(String path,out UInt64 address,out UInt64 length)
    {
        address=0UL;length=0UL;if(!_available||path==null||path.Length==0)return false;
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
        path=null;pathLength=0U;data=null;dataLength=0U;if(!_available||index>=_count)return false;Byte* cursor=_base+16;UInt64 remaining=_length-16UL;
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
        // Keep command/file lookup O(n). The former implementation called TryGetAsset(i),
        // which rescanned from entry zero for every i and made every userland command launch
        // O(n^2) in the number of boot assets.
        data=null;dataLength=0U;if(!_available||path==null||pathLength==0U)return false;
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
        if(canonicalLength==1U)return _count!=0U;
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
            // Ignore duplicate directory names contributed by multiple files below the same child.
            Boolean duplicate=false;Byte* earlier=_base+16;UInt64 earlierRemaining=_length-16UL;
            for(UInt32 j=0U;j<i;j++)
            {
                if(earlierRemaining<16UL)break;UInt32 epn=ReadU32(earlier),edn=ReadU32(earlier+4);UInt64 etotal=16UL+(UInt64)epn+(UInt64)edn;if(etotal>earlierRemaining)break;Byte* ec=earlier+16;UInt32 es=0U;
                if(prefixLength!=0U){if(epn<=prefixLength||!AsciiPrefixEquals(ec,prefix,prefixLength)||ec[prefixLength]!=(Byte)'/'){earlier+=etotal;earlierRemaining-=etotal;continue;}es=prefixLength+1U;}
                UInt32 el=0U;while(es+el<epn&&ec[es+el]!=(Byte)'/')el++;if(el==childLength&&AsciiEquals(candidate+start,childLength,ec+es,el)){duplicate=true;break;}
                earlier+=etotal;earlierRemaining-=etotal;
            }
            if(!duplicate)
            {
                if(unique==wanted)
                {
                    if(childLength+1U>capacity)return false;for(UInt32 c=0U;c<childLength;c++)name[c]=candidate[start+c];nameLength=childLength;directory=start+childLength<pn;return true;
                }
                unique++;
            }
            cursor+=total;remaining-=total;
        }
        return false;
    }

    /// <summary>Gets a text asset by an allocation-free ASCII path.</summary>
    public static Boolean TryGetTextAscii(Byte* path,UInt32 pathLength,out Byte* text,out UInt32 length)=>TryFindAscii(path,pathLength,out text,out length);

    /// <summary>Writes one text asset through a caller-provided byte sink without allocating a managed string.</summary>
    public static Boolean TryGetText(String path,out Byte* text,out UInt32 length)
    {
        text=null;length=0U;if(!TryFind(path,out UInt64 address,out UInt64 bytes)||bytes>UInt32.MaxValue)return false;
        text=(Byte*)(nuint)address;length=(UInt32)bytes;return true;
    }

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
