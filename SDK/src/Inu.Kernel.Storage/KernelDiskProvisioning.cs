using System;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Storage;

/// <summary>
/// Creates the default Inu two-partition GPT layout on an otherwise blank writable disk.
/// Existing partition tables are never overwritten.
/// </summary>
public static unsafe class KernelDiskProvisioning
{
    private const UInt32 SectorBytes=512U;
    private const UInt32 GptEntryCount=128U;
    private const UInt32 GptEntryBytes=128U;
    private const UInt32 GptEntrySectors=32U;
    private const UInt64 AlignmentSectors=2048UL;
    private const UInt64 MinimumPartitionSectors=65536UL;
    private static UInt64 _provisionedDeviceMask;

    /// <summary>Gets whether this boot created the GPT layout for the supplied device.</summary>
    public static Boolean WasProvisionedThisBoot(KernelStorageDeviceHandle device)
        =>device.Value is >0U and <=64U&&(_provisionedDeviceMask&(1UL<<(Int32)(device.Value-1U)))!=0UL;

    /// <summary>Compatibility helper for the original two-partition policy.</summary>
    public static Boolean EnsureTwoGptPartitions(KernelStorageDeviceHandle device,out Boolean created)
        =>EnsureGptPartitions(device,2U,out created);

    /// <summary>
    /// Ensures that a blank writable disk has the requested number of equal-sized GPT Basic Data partitions.
    /// Existing partition tables are preserved. A non-blank non-GPT disk is rejected rather than erased.
    /// </summary>
    public static Boolean EnsureGptPartitions(KernelStorageDeviceHandle device,UInt32 partitionCount,out Boolean created)
        =>CreateGptPartitions(device,partitionCount,false,out created);

    /// <summary><inu.api>Creates an equal-sized GPT layout. Set overwriteExisting only for an explicit destructive repartition request.</inu.api></summary>
    public static Boolean CreateGptPartitions(KernelStorageDeviceHandle device,UInt32 partitionCount,Boolean overwriteExisting,out Boolean created)
    {
        created=false;
        if(partitionCount==0U||partitionCount>16U)return false;
        if(!KernelStorage.TryGetGeometry(device,out KernelStorageGeometry geometry)||geometry.ReadOnly||geometry.LogicalBlockSize!=SectorBytes)return false;
        if(geometry.BlockCount<AlignmentSectors+MinimumPartitionSectors*(UInt64)partitionCount+64UL)return false;
        if(!KernelHeap.TryAllocate(SectorBytes,16UL,true,out KernelHeapAllocation sectorAllocation))return false;
        Byte* sector=(Byte*)(nuint)sectorAllocation.Address;
        try
        {
            if(!KernelStorage.ReadBlocks(device,1UL,1U,sector,SectorBytes))return false;
            if(IsGptHeader(sector)&&!overwriteExisting)
            {
                if(IsInuManagedHeader(sector)&&device.Value<=64U)_provisionedDeviceMask|=1UL<<(Int32)(device.Value-1U);
                return true;
            }
            if(!overwriteExisting)
            {
                if(!KernelStorage.ReadBlocks(device,0UL,1U,sector,SectorBytes)||!IsBlank(sector,SectorBytes))return false;
            }

            UInt64 lastBlock=geometry.BlockCount-1UL;
            UInt64 lastUsable=geometry.BlockCount-34UL;
            UInt64 firstStart=AlignmentSectors;
            UInt64 available=lastUsable-firstStart+1UL;
            UInt64 approximate=available/(UInt64)partitionCount;
            if(approximate<MinimumPartitionSectors)return false;

            Zero(sector,SectorBytes);
            WriteProtectiveMbr(sector,geometry.BlockCount);
            if(!KernelStorage.WriteBlocks(device,0UL,1U,sector,SectorBytes))return false;

            UInt32 entriesBytes=GptEntrySectors*SectorBytes;
            if(!KernelHeap.TryAllocate(entriesBytes,16UL,true,out KernelHeapAllocation entriesAllocation))return false;
            try
            {
                Byte* entries=(Byte*)(nuint)entriesAllocation.Address;
                Zero(entries,entriesBytes);
                UInt64 nextStart=firstStart;
                for(UInt32 index=0U;index<partitionCount;index++)
                {
                    UInt64 start=AlignUp(nextStart,AlignmentSectors);
                    UInt64 last=index+1U==partitionCount?lastUsable:AlignUp(firstStart+(available*(UInt64)(index+1U))/(UInt64)partitionCount,AlignmentSectors)-1UL;
                    if(last>lastUsable)last=lastUsable;
                    if(last<start||last-start+1UL<MinimumPartitionSectors)return false;
                    WritePartitionEntry(entries+index*GptEntryBytes,index,start,last,device.Value,index+1U,"Inu Data");
                    nextStart=last+1UL;
                }
                UInt32 entriesCrc=Crc32(entries,entriesBytes);
                UInt64 backupEntriesLba=geometry.BlockCount-33UL;
                if(!KernelStorage.WriteBlocks(device,2UL,GptEntrySectors,entries,entriesBytes))return false;
                if(!KernelStorage.WriteBlocks(device,backupEntriesLba,GptEntrySectors,entries,entriesBytes))return false;

                Zero(sector,SectorBytes);
                WriteGptHeader(sector,1UL,lastBlock,2UL,entriesCrc,device.Value,geometry.BlockCount);
                if(!KernelStorage.WriteBlocks(device,1UL,1U,sector,SectorBytes))return false;
                Zero(sector,SectorBytes);
                WriteGptHeader(sector,lastBlock,1UL,backupEntriesLba,entriesCrc,device.Value,geometry.BlockCount);
                if(!KernelStorage.WriteBlocks(device,lastBlock,1U,sector,SectorBytes))return false;
            }
            finally { KernelHeap.TryRelease(entriesAllocation); }

            if(!KernelStorage.Flush(device)||!KernelStorage.RefreshPartitions(device))return false;
            if(device.Value<=64U)_provisionedDeviceMask|=1UL<<(Int32)(device.Value-1U);
            created=true;
            return true;
        }
        finally { KernelHeap.TryRelease(sectorAllocation); }
    }

    private static UInt64 AlignUp(UInt64 value,UInt64 alignment)=>(value+alignment-1UL)&~(alignment-1UL);
    private static Boolean IsGptHeader(Byte* sector)=>sector[0]=='E'&&sector[1]=='F'&&sector[2]=='I'&&sector[3]==' '&&sector[4]=='P'&&sector[5]=='A'&&sector[6]=='R'&&sector[7]=='T';
    private static Boolean IsInuManagedHeader(Byte* sector)=>IsGptHeader(sector)&&Read32(sector+68)==0x47505431U;
    private static UInt32 Read32(Byte* p)=>(UInt32)(p[0]|((UInt32)p[1]<<8)|((UInt32)p[2]<<16)|((UInt32)p[3]<<24));
    private static Boolean IsBlank(Byte* buffer,UInt32 bytes){for(UInt32 i=0U;i<bytes;i++)if(buffer[i]!=0U)return false;return true;}
    private static void Zero(Byte* buffer,UInt32 bytes){for(UInt32 i=0U;i<bytes;i++)buffer[i]=0U;}
    private static void Write16(Byte* p,UInt16 value){p[0]=(Byte)value;p[1]=(Byte)(value>>8);}
    private static void Write32(Byte* p,UInt32 value){p[0]=(Byte)value;p[1]=(Byte)(value>>8);p[2]=(Byte)(value>>16);p[3]=(Byte)(value>>24);}
    private static void Write64(Byte* p,UInt64 value){Write32(p,(UInt32)value);Write32(p+4,(UInt32)(value>>32));}

    private static void WriteProtectiveMbr(Byte* sector,UInt64 blocks)
    {
        UInt32 entry=446U;sector[entry+4U]=0xEE;Write32(sector+entry+8U,1U);
        UInt64 protectedBlocks=blocks>1UL?blocks-1UL:0UL;Write32(sector+entry+12U,protectedBlocks>UInt32.MaxValue?UInt32.MaxValue:(UInt32)protectedBlocks);
        sector[510]=0x55;sector[511]=0xAA;
    }

    private static void WritePartitionEntry(Byte* entry,UInt32 index,UInt64 first,UInt64 last,UInt32 deviceValue,UInt32 partitionNumber,String name)
    {
        // Microsoft Basic Data partition type GUID EBD0A0A2-B9E5-4433-87C0-68B6B72699C7, in GPT byte order.
        entry[0]=0xA2;entry[1]=0xA0;entry[2]=0xD0;entry[3]=0xEB;entry[4]=0xE5;entry[5]=0xB9;entry[6]=0x33;entry[7]=0x44;
        entry[8]=0x87;entry[9]=0xC0;entry[10]=0x68;entry[11]=0xB6;entry[12]=0xB7;entry[13]=0x26;entry[14]=0x99;entry[15]=0xC7;
        // Stable per-boot unique partition identifier. It need only be non-zero and distinct for each entry.
        Write32(entry+16,0x494E5500U|partitionNumber);Write32(entry+20,deviceValue);Write32(entry+24,partitionNumber);Write32(entry+28,0xA1730000U|index);
        Write64(entry+32,first);Write64(entry+40,last);Write64(entry+48,0UL);
        UInt32 maximum=name.Length>36?36U:(UInt32)name.Length;
        for(UInt32 i=0U;i<maximum;i++){UInt16 c=name[(Int32)i];Write16(entry+56U+i*2U,c);}
    }

    private static void WriteGptHeader(Byte* sector,UInt64 current,UInt64 backup,UInt64 entriesLba,UInt32 entriesCrc,UInt32 deviceValue,UInt64 blockCount)
    {
        sector[0]=(Byte)'E';sector[1]=(Byte)'F';sector[2]=(Byte)'I';sector[3]=(Byte)' ';sector[4]=(Byte)'P';sector[5]=(Byte)'A';sector[6]=(Byte)'R';sector[7]=(Byte)'T';
        Write32(sector+8,0x00010000U);Write32(sector+12,92U);Write32(sector+16,0U);Write32(sector+20,0U);
        Write64(sector+24,current);Write64(sector+32,backup);Write64(sector+40,34UL);Write64(sector+48,blockCount-34UL);
        Write32(sector+56,0x494E5500U|deviceValue);Write32(sector+60,0x00000173U);Write32(sector+64,deviceValue);Write32(sector+68,0x47505431U);
        Write64(sector+72,entriesLba);Write32(sector+80,GptEntryCount);Write32(sector+84,GptEntryBytes);Write32(sector+88,entriesCrc);
        Write32(sector+16,Crc32(sector,92U));
    }

    private static UInt32 Crc32(Byte* data,UInt32 bytes)
    {
        UInt32 crc=0xFFFFFFFFU;
        for(UInt32 i=0U;i<bytes;i++)
        {
            crc^=data[i];
            for(UInt32 bit=0U;bit<8U;bit++)crc=(crc&1U)!=0U?(crc>>1)^0xEDB88320U:crc>>1;
        }
        return ~crc;
    }
}
