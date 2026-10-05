using System;

namespace Inu.Kernel.Storage;

public static unsafe class KernelStorageMath
{
    public static Boolean IsValidOptions(KernelStorageOptions o)
    { if(o.InitialDevices==0||o.InitialVolumes==0||o.InitialProviders==0||o.InitialNamespaces==0||o.InitialMounts==0||o.InitialOpenFiles==0||o.InitialRequests==0)return false;if(o.MaximumDevices<o.InitialDevices||o.MaximumVolumes<o.InitialVolumes||o.MaximumProviders<o.InitialProviders||o.MaximumNamespaces<o.InitialNamespaces||o.MaximumMounts<o.InitialMounts||o.MaximumOpenFiles<o.InitialOpenFiles||o.MaximumRequests<o.InitialRequests)return false;return true; }
    public static UInt32 NextCapacity(UInt32 current,UInt32 maximum)
    { if(current>=maximum)return current;UInt64 doubled=(UInt64)current*2UL;if(doubled>maximum)doubled=maximum;return (UInt32)doubled; }
    public static Boolean IsValidGeometry(KernelStorageGeometry g)
    { if(g.LogicalBlockSize<512U||g.LogicalBlockSize>65536U||(g.LogicalBlockSize&(g.LogicalBlockSize-1U))!=0U||g.BlockCount==0UL)return false;return g.BlockCount<=UInt64.MaxValue/g.LogicalBlockSize; }
    public static Boolean TryParseMbrPartition(Byte* sector,UInt32 sectorBytes,UInt32 index,out KernelPartitionInfo info)
    { info=default;if(sector==null||sectorBytes<512U||index>=4U||sector[510]!=0x55||sector[511]!=0xAA)return false;Int32 p=446+(Int32)index*16;Byte type=sector[p+4];UInt32 first=Read32(sector+p+8);UInt32 count=Read32(sector+p+12);if(type==0||count==0)return false;info=new KernelPartitionInfo(KernelPartitionScheme.Mbr,index,first,count,type,0,0);return true; }
    public static Boolean IsProtectiveMbr(Byte* sector,UInt32 sectorBytes)
    { KernelPartitionInfo p;for(UInt32 i=0;i<4;i++)if(TryParseMbrPartition(sector,sectorBytes,i,out p)&&p.MbrType==0xEEU)return true;return false; }
    public static Boolean TryParseGptHeader(Byte* sector,UInt32 sectorBytes,out UInt64 entriesLba,out UInt32 entryCount,out UInt32 entrySize)
    { entriesLba=0;entryCount=0;entrySize=0;if(sector==null||sectorBytes<92U)return false;if(sector[0]!='E'||sector[1]!='F'||sector[2]!='I'||sector[3]!=' '||sector[4]!='P'||sector[5]!='A'||sector[6]!='R'||sector[7]!='T')return false;entriesLba=Read64(sector+72);entryCount=Read32(sector+80);entrySize=Read32(sector+84);return entriesLba>0&&entryCount>0&&entrySize>=128U&&entrySize<=4096U; }
    public static Boolean TryParseGptEntry(Byte* entry,UInt32 entryBytes,UInt32 index,out KernelPartitionInfo info)
    { info=default;if(entry==null||entryBytes<128U)return false;UInt64 typeLow=Read64(entry),typeHigh=Read64(entry+8);if(typeLow==0&&typeHigh==0)return false;UInt64 first=Read64(entry+32),last=Read64(entry+40);if(last<first)return false;info=new KernelPartitionInfo(KernelPartitionScheme.Gpt,index,first,last-first+1UL,0,typeLow,typeHigh);return true; }
    public static UInt16 Read16(Byte* p)=> (UInt16)(p[0]|(p[1]<<8));
    public static UInt32 Read32(Byte* p)=> (UInt32)(p[0]|((UInt32)p[1]<<8)|((UInt32)p[2]<<16)|((UInt32)p[3]<<24));
    public static UInt64 Read64(Byte* p)=>Read32(p)|((UInt64)Read32(p+4)<<32);
}
