using System;
using Inu.Kernel.Protection;

namespace Inu.Kernel.Processes;

/// <summary>Validates and decodes x64 ELF64 and PE32+ executable metadata without allocations.</summary>
public static unsafe class ProcessExecutableMath
{
    public const UInt32 MaximumSegments = 16U;
    private const UInt32 ElfLoad = 1U;
    private const UInt16 ElfMachineX64 = 0x003E;
    private const UInt16 PeMachineX64 = 0x8664;

    /// <summary>Inspects one executable image and returns its validated entry metadata.</summary>
    public static Boolean TryInspect(Byte* image, UInt64 length, out ProcessExecutableInfo info)
    {
        info=default;
        if (image==null || length<64UL) return false;
        if (image[0]==0x7F && image[1]==(Byte)'E' && image[2]==(Byte)'L' && image[3]==(Byte)'F') return TryInspectElf(image,length,out info);
        if (image[0]==(Byte)'M' && image[1]==(Byte)'Z') return TryInspectPe(image,length,out info);
        return false;
    }

    /// <summary>Returns one loadable segment from a previously validated image.</summary>
    public static Boolean TryGetSegment(Byte* image, UInt64 length, ProcessExecutableInfo info, UInt32 segmentIndex, out ProcessImageSegment segment)
    {
        segment=default;
        if (image==null || segmentIndex>=info.SegmentCount) return false;
        if (info.Format==ProcessExecutableFormat.Elf64) return TryGetElfLoad(image,length,segmentIndex,out segment);
        if (info.Format==ProcessExecutableFormat.PortableExecutable64) return segmentIndex==0U ? TryGetPeHeaders(image,length,out segment) : TryGetPeSection(image,length,segmentIndex-1U,out segment);
        return false;
    }

    /// <summary>Rounds an address down to a 4 KiB page boundary.</summary>
    public static UInt64 PageFloor(UInt64 value) => value & ~4095UL;
    /// <summary>Rounds a non-overflowing byte endpoint up to a 4 KiB page boundary.</summary>
    public static Boolean TryPageCeiling(UInt64 value, out UInt64 result)
    { result=0UL; if (value>UInt64.MaxValue-4095UL) return false; result=(value+4095UL)&~4095UL; return true; }

    private static Boolean TryInspectElf(Byte* p, UInt64 n, out ProcessExecutableInfo info)
    {
        info=default;
        if (n<64UL || p[4]!=2 || p[5]!=1 || Read16(p,18)!=ElfMachineX64) return false;
        UInt16 type=Read16(p,16); if(type!=2 && type!=3) return false;
        UInt64 entry=Read64(p,24), phoff=Read64(p,32); UInt16 phsize=Read16(p,54), phcount=Read16(p,56);
        if (type!=2 || phsize<56 || phcount==0 || !Range(phoff,(UInt64)phsize*phcount,n) || !KernelProtectionMath.IsValidUserEntry(entry)) return false;
        UInt32 loads=0U;
        for(UInt32 i=0;i<phcount;i++) if(Read32(p,phoff+(UInt64)i*phsize)==ElfLoad) loads++;
        if(loads==0U || loads>MaximumSegments) return false;
        UInt64 imageBase=UInt64.MaxValue;Boolean entryExecutable=false; for(UInt32 i=0;i<loads;i++){if(!TryGetElfLoad(p,n,i,out ProcessImageSegment segment)) return false;if(segment.VirtualAddress<imageBase)imageBase=segment.VirtualAddress;if((segment.Protection&ProcessSegmentProtection.Execute)!=0 && entry>=segment.VirtualAddress && entry-segment.VirtualAddress<segment.MemorySize)entryExecutable=true;}
        ProcessExecutableInfo candidate=new(ProcessExecutableFormat.Elf64,entry,imageBase,loads,false);
        if(!entryExecutable||imageBase==UInt64.MaxValue||entry<imageBase||!TryValidateSegmentLayout(p,n,candidate))return false;
        info=new ProcessExecutableInfo(ProcessExecutableFormat.Elf64,entry,imageBase,loads,false); return true;
    }

    private static Boolean TryGetElfLoad(Byte* p, UInt64 n, UInt32 wanted, out ProcessImageSegment segment)
    {
        segment=default; UInt64 phoff=Read64(p,32); UInt16 phsize=Read16(p,54), phcount=Read16(p,56); UInt32 found=0U;
        for(UInt32 i=0;i<phcount;i++)
        {
            UInt64 o=phoff+(UInt64)i*phsize; if(Read32(p,o)!=ElfLoad) continue;
            if(found++!=wanted) continue;
            UInt32 flags=Read32(p,o+4); UInt64 fileOffset=Read64(p,o+8), vaddr=Read64(p,o+16), fileSize=Read64(p,o+32), memSize=Read64(p,o+40);
            if(memSize==0UL || fileSize>memSize || !Range(fileOffset,fileSize,n) || !KernelProtectionMath.IsUserRange(vaddr,memSize)) return false;
            ProcessSegmentProtection protection=ProcessSegmentProtection.Read;
            if((flags&2U)!=0U) protection|=ProcessSegmentProtection.Write; if((flags&1U)!=0U) protection|=ProcessSegmentProtection.Execute;
            segment=new ProcessImageSegment(vaddr,memSize,fileSize,fileOffset,protection); return true;
        }
        return false;
    }

    private static Boolean TryInspectPe(Byte* p, UInt64 n, out ProcessExecutableInfo info)
    {
        info=default; if(n<0x40UL) return false; UInt32 pe=Read32(p,0x3CUL);
        if(!Range(pe,24UL,n) || Read32(p,pe)!=0x00004550U || Read16(p,pe+4)!=PeMachineX64) return false;
        UInt16 sections=Read16(p,pe+6), optionalSize=Read16(p,pe+20); UInt64 opt=(UInt64)pe+24UL;
        if(sections==0 || sections>=MaximumSegments || optionalSize<112 || !Range(opt,optionalSize,n) || Read16(p,opt)!=0x020B) return false;
        UInt32 entryRva=Read32(p,opt+16), sizeOfImage=Read32(p,opt+56); UInt64 imageBase=Read64(p,opt+24), entry=imageBase+entryRva;
        if(entry<imageBase || sizeOfImage==0U || !KernelProtectionMath.IsValidUserEntry(entry)) return false;
        UInt64 table=opt+optionalSize; if(!Range(table,(UInt64)sections*40UL,n)) return false;
        if(!TryGetPeHeaders(p,n,out _)) return false;
        ProcessExecutableInfo temp=new(ProcessExecutableFormat.PortableExecutable64,entry,imageBase,(UInt32)sections+1U,false);
        Boolean entryExecutable=false; for(UInt32 i=0;i<sections;i++){if(!TryGetPeSection(p,n,i,out ProcessImageSegment segment)) return false; if((segment.Protection&ProcessSegmentProtection.Execute)!=0 && entry>=segment.VirtualAddress && entry-segment.VirtualAddress<segment.MemorySize)entryExecutable=true;}
        if(!entryExecutable||!TryValidateSegmentLayout(p,n,temp))return false; info=temp; return true;
    }

    private static Boolean TryGetPeHeaders(Byte* p, UInt64 n, out ProcessImageSegment segment)
    {
        segment=default; if(p==null || n<64UL) return false;
        UInt32 pe=Read32(p,0x3CUL); if(!Range(pe,24UL,n)) return false;
        UInt16 sections=Read16(p,pe+6), optionalSize=Read16(p,pe+20); UInt64 opt=(UInt64)pe+24UL;
        if(sections==0 || sections>=MaximumSegments || optionalSize<112 || !Range(opt,optionalSize,n)) return false;
        UInt64 headerBytes=Read32(p,opt+60), imageBytes=Read32(p,opt+56), imageBase=Read64(p,opt+24);
        UInt64 tableEnd=opt+(UInt64)optionalSize+(UInt64)sections*40UL;
        if(headerBytes<tableEnd || headerBytes>n || headerBytes>imageBytes ||
           (imageBase&4095UL)!=0UL || !KernelProtectionMath.IsUserRange(imageBase,headerBytes)) return false;
        segment=new ProcessImageSegment(imageBase,headerBytes,headerBytes,0UL,ProcessSegmentProtection.Read);
        return true;
    }

    private static Boolean TryGetPeSection(Byte* p, UInt64 n, UInt32 index, out ProcessImageSegment segment)
    {
        segment=default; UInt32 pe=Read32(p,0x3CUL); UInt16 sections=Read16(p,pe+6), optionalSize=Read16(p,pe+20); if(index>=sections)return false;
        UInt64 opt=(UInt64)pe+24UL, imageBase=Read64(p,opt+24), table=opt+optionalSize, o=table+(UInt64)index*40UL;
        if(!Range(o,40UL,n)) return false;
        UInt64 virtualSize=Read32(p,o+8), rva=Read32(p,o+12), rawSize=Read32(p,o+16), rawOffset=Read32(p,o+20); UInt32 c=Read32(p,o+36);
        UInt64 memorySize=virtualSize>rawSize?virtualSize:rawSize; if(memorySize==0UL) return false;
        UInt64 vaddr=imageBase+rva; if(vaddr<imageBase || !Range(rawOffset,rawSize,n) || !KernelProtectionMath.IsUserRange(vaddr,memorySize)) return false;
        ProcessSegmentProtection protection=ProcessSegmentProtection.Read;
        if((c&0x80000000U)!=0U) protection|=ProcessSegmentProtection.Write; if((c&0x20000000U)!=0U) protection|=ProcessSegmentProtection.Execute;
        segment=new ProcessImageSegment(vaddr,memorySize,rawSize,rawOffset,protection); return true;
    }

    /// <summary>Validates the complete mapped layout before the process loader allocates any pages.</summary>
    public static Boolean TryValidateSegmentLayout(Byte* image,UInt64 length,ProcessExecutableInfo info)
    {
        if(image==null||info.SegmentCount==0U||info.SegmentCount>MaximumSegments)return false;
        for(UInt32 i=0U;i<info.SegmentCount;i++)
        {
            if(!TryGetSegment(image,length,info,i,out ProcessImageSegment a))return false;
            if((a.Protection&ProcessSegmentProtection.Write)!=0&&(a.Protection&ProcessSegmentProtection.Execute)!=0)return false;
            UInt64 aStart=PageFloor(a.VirtualAddress);if(a.VirtualAddress>UInt64.MaxValue-a.MemorySize||!TryPageCeiling(a.VirtualAddress+a.MemorySize,out UInt64 aEnd)||aEnd<=aStart)return false;
            for(UInt32 j=i+1U;j<info.SegmentCount;j++)
            {
                if(!TryGetSegment(image,length,info,j,out ProcessImageSegment b))return false;
                UInt64 bStart=PageFloor(b.VirtualAddress);if(b.VirtualAddress>UInt64.MaxValue-b.MemorySize||!TryPageCeiling(b.VirtualAddress+b.MemorySize,out UInt64 bEnd)||bEnd<=bStart)return false;
                // Distinct sections may not claim the same user page with conflicting ownership/permissions.
                if(aStart<bEnd&&bStart<aEnd)return false;
            }
        }
        return true;
    }

    private static Boolean Range(UInt64 offset, UInt64 count, UInt64 length) => offset<=length && count<=length-offset;
    private static UInt16 Read16(Byte* p, UInt64 o) => (UInt16)(p[o]|((UInt16)p[o+1]<<8));
    private static UInt32 Read32(Byte* p, UInt64 o) => (UInt32)(p[o]|((UInt32)p[o+1]<<8)|((UInt32)p[o+2]<<16)|((UInt32)p[o+3]<<24));
    private static UInt64 Read64(Byte* p, UInt64 o) => (UInt64)Read32(p,o)|((UInt64)Read32(p,o+4)<<32);
}
