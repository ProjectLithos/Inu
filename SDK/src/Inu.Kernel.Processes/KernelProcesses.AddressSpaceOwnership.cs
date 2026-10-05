using System;
using Inu.Kernel.Memory;

namespace Inu.Kernel.Processes;

public static unsafe partial class KernelProcesses
{
    internal static Boolean ReleaseOwnedImplementation(KernelProcessRecordHandle record)
    {
        Boolean ok=true;
        UInt32 allocationCount=KernelProcessRecordStore.GetAllocationCount(record);
        for(UInt32 i=allocationCount;i>0U;i--)if(KernelProcessRecordStore.TryGetAllocation(record,i-1U,out KernelPhysicalAllocation allocation)&&!KernelPhysicalMemory.TryRelease(allocation))ok=false;
        UInt32 tableCount=KernelProcessRecordStore.GetTableCount(record);
        for(UInt32 i=tableCount;i>0U;i--)if(KernelProcessRecordStore.TryGetTable(record,i-1U,out KernelPhysicalAllocation table)&&!KernelPhysicalMemory.TryRelease(table))ok=false;
        KernelProcessRecordStore.ClearOwnedResourceCounts(record);return ok;
    }

    internal static Boolean ReleaseTemporaryImplementation(KernelProcessRecordHandle record,KernelPhysicalAllocation* tables,UInt32 tableCount)
    {
        Boolean ok=true;UInt32 allocationCount=KernelProcessRecordStore.GetAllocationCount(record);
        for(UInt32 i=allocationCount;i>0U;i--)if(KernelProcessRecordStore.TryGetAllocation(record,i-1U,out KernelPhysicalAllocation allocation)&&!KernelPhysicalMemory.TryRelease(allocation))ok=false;
        if(!ProcessAddressSpace.TryReleaseTables(tables,tableCount))ok=false;
        KernelProcessRecordStore.ClearOwnedResourceCounts(record);KernelProcessRecordStore.Abandon(record);return ok;
    }

    internal static Boolean StoreAllocationImplementation(KernelProcessRecordHandle record,KernelPhysicalAllocation allocation)=>KernelProcessRecordStore.TryAppendAllocation(record,allocation);
}
