using System;

namespace Inu.Kernel.Storage;

/// <summary>Optional MBR/GPT partition-discovery component.</summary>
public static unsafe class KernelPartitionDiscovery
{
    public static Boolean Initialize()=>KernelStoragePartitionServices.Register(&DiscoverService);
    private static Boolean DiscoverService(KernelStorageDeviceHandle device,Byte* scratch,UInt32 scratchBytes,UInt32* discovered){if(discovered==null)return false;UInt32 value=0U;if(!Discover(device,scratch,scratchBytes,out value))return false;*discovered=value;return true;}
    public static Boolean Discover(KernelStorageDeviceHandle device,Byte* scratch,UInt32 scratchBytes,out UInt32 discovered)
    {
        discovered=0U;if(scratch==null||!KernelStoragePartitionContract.TryGetGeometry(device,out KernelStorageGeometry geometry)||scratchBytes<geometry.LogicalBlockSize)return false;
        if(!KernelStoragePartitionContract.ReadBlocks(device,0,1,scratch,scratchBytes))return false;
        if(KernelStorageMath.IsProtectiveMbr(scratch,geometry.LogicalBlockSize))return DiscoverGpt(device,geometry,scratch,scratchBytes,out discovered);
        for(UInt32 i=0;i<4;i++){if(!KernelStorageMath.TryParseMbrPartition(scratch,geometry.LogicalBlockSize,i,out KernelPartitionInfo p))continue;if(KernelStoragePartitionContract.RegisterVolume(device,p,out _))discovered++;}
        if(discovered==0U){KernelPartitionInfo raw=new(KernelPartitionScheme.Raw,0,0,geometry.BlockCount,0,0,0);if(KernelStoragePartitionContract.RegisterVolume(device,raw,out _))discovered=1U;}
        return true;
    }
    private static Boolean DiscoverGpt(KernelStorageDeviceHandle device,KernelStorageGeometry geometry,Byte* scratch,UInt32 scratchBytes,out UInt32 discovered)
    {
        discovered=0U;if(!KernelStoragePartitionContract.ReadBlocks(device,1,1,scratch,scratchBytes)||!KernelStorageMath.TryParseGptHeader(scratch,geometry.LogicalBlockSize,out UInt64 entriesLba,out UInt32 entryCount,out UInt32 entrySize))return false;
        UInt64 entryBytes=(UInt64)entryCount*(UInt64)entrySize;UInt64 entryBlocks=(entryBytes+geometry.LogicalBlockSize-1UL)/geometry.LogicalBlockSize;if(entriesLba>=geometry.BlockCount||entryBlocks>geometry.BlockCount-entriesLba)return false;UInt32 perBlock=geometry.LogicalBlockSize/entrySize;if(perBlock==0U)return false;
        for(UInt32 i=0;i<entryCount;i++){UInt64 lba=entriesLba+(i/perBlock);UInt32 offset=(i%perBlock)*entrySize;if(!KernelStoragePartitionContract.ReadBlocks(device,lba,1,scratch,scratchBytes))return false;if(!KernelStorageMath.TryParseGptEntry(scratch+offset,entrySize,i,out KernelPartitionInfo p))continue;if(KernelStoragePartitionContract.RegisterVolume(device,p,out _))discovered++;}
        return true;
    }
}
