using System;
using System.Runtime;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Heap;
using Inu.Kernel.Gui;
using KernelFaultInjection = Inu.Kernel.Contracts.KernelFaultInjection;
using Inu.Kernel.InterruptDispatch;
using Inu.Kernel.Storage;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Memory;
using Inu.Kernel.Protection;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Security;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Smp;
using Inu.Kernel.VirtualMemory;
using Inu.ApplicationFormat;

namespace Inu.Kernel.Processes;

public static unsafe partial class KernelProcesses
{
private static Boolean LoadSegments(Byte* image, UInt64 length, ProcessExecutableInfo executable, UInt64 root, KernelProcessRecordHandle record, KernelPhysicalAllocation* tables, ref UInt32 tableCount)
    {
        for(UInt32 s=0;s<executable.SegmentCount;s++)
        {
            if(!ProcessExecutableMath.TryGetSegment(image,length,executable,s,out ProcessImageSegment segment))return false; if((segment.Protection&ProcessSegmentProtection.Write)!=0 && (segment.Protection&ProcessSegmentProtection.Execute)!=0)return false; UInt64 basePage=ProcessExecutableMath.PageFloor(segment.VirtualAddress);
            if(segment.VirtualAddress>UInt64.MaxValue-segment.MemorySize || !ProcessExecutableMath.TryPageCeiling(segment.VirtualAddress+segment.MemorySize,out UInt64 endPage))return false;
            UInt64 pages=(endPage-basePage)/4096UL; if(KernelProcessRecordStore.GetAllocationCount(record)>=MaximumImageAllocations || !KernelPhysicalMemory.TryAllocate(pages,1UL,out KernelPhysicalAllocation allocation))return false;
            KernelProcessAddressSpaceServices.StoreAllocation(record,allocation); if(!ZeroAndCopy(allocation,segment.VirtualAddress-basePage,image+segment.FileOffset,segment.FileSize))return false;
            for(UInt64 p=0;p<pages;p++) if(!ProcessAddressSpace.TryMap(root,basePage+p*4096UL,allocation.StartAddress+p*4096UL,ToVirtualProtection(segment.Protection),tables,MaximumTablesPerProcess,ref tableCount))return false;
        }
        return true;
    }

private static Boolean CreateStack(UInt64 root, KernelProcessRecordHandle record, KernelPhysicalAllocation* tables, ref UInt32 tableCount)
    {
        UInt64 pages=DefaultStackBytes/4096UL; if(KernelProcessRecordStore.GetAllocationCount(record)>=MaximumImageAllocations || !KernelPhysicalMemory.TryAllocate(pages,1UL,out KernelPhysicalAllocation allocation))return false; KernelProcessAddressSpaceServices.StoreAllocation(record,allocation); UInt64 stackBase=UserStackTop-DefaultStackBytes; UInt64 guardBase=stackBase-4096UL;
        if(!ZeroAndCopy(allocation,0UL,(Byte*)0,0UL))return false; KernelVirtualMemoryProtection protection=KernelVirtualMemoryProtection.Read|KernelVirtualMemoryProtection.Write|KernelVirtualMemoryProtection.User;
        for(UInt64 p=0;p<pages;p++) if(!ProcessAddressSpace.TryMap(root,stackBase+p*4096UL,allocation.StartAddress+p*4096UL,protection,tables,MaximumTablesPerProcess,ref tableCount))return false;
        KernelProcessRecordStore.SetStack(record,stackBase,UserStackTop,guardBase); return KernelProtectionMath.IsValidUserStack(UserStackTop);
    }

private static Boolean ZeroAndCopy(KernelPhysicalAllocation allocation, UInt64 destinationOffset, Byte* source, UInt64 count)
    {
        if(!KernelAddressSpace.TryPhysicalToDirectMap(allocation.StartAddress,out UInt64 mapped))return false; Byte* destination=(Byte*)(nuint)mapped; UInt64 bytes=allocation.PageCount*4096UL;
        for(UInt64 i=0;i<bytes;i++)destination[i]=0; if(destinationOffset>bytes || count>bytes-destinationOffset)return false; for(UInt64 i=0;i<count;i++)destination[destinationOffset+i]=source[i]; return true;
    }

private static KernelVirtualMemoryProtection ToVirtualProtection(ProcessSegmentProtection protection)
    { KernelVirtualMemoryProtection result=KernelVirtualMemoryProtection.Read|KernelVirtualMemoryProtection.User; if((protection&ProcessSegmentProtection.Write)!=0)result|=KernelVirtualMemoryProtection.Write; if((protection&ProcessSegmentProtection.Execute)!=0)result|=KernelVirtualMemoryProtection.Execute; return result; }

}
