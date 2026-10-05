using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Memory;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Processes;

internal static unsafe class ProcessAddressSpace
{
    private const UInt64 Present=1UL, Writable=2UL, User=4UL, LargePage=1UL<<7, NoExecute=1UL<<63, AddressMask=0x000FFFFFFFFFF000UL;
    private const UInt64 AddressMask2MiB=0x000FFFFFFFE00000UL, AddressMask1GiB=0x000FFFFFC0000000UL;

    internal static Boolean TryCreate(KernelPhysicalAllocation* tables, UInt32 capacity, out UInt32 tableCount, out UInt64 root)
    {
        tableCount=0U; root=0UL;
        if(!AllocateTable(tables,capacity,ref tableCount,out KernelPhysicalAllocation allocation)) return false;
        root=allocation.StartAddress; if(!TryPointer(root,out UInt64* dst))return false; UInt64 kernelRoot=KernelVirtualMemory.GetRootPhysicalAddress(); if(kernelRoot==0UL||!TryPointer(kernelRoot,out UInt64* src))return false;
        // Inu still executes the kernel from the firmware-established low mapping while the
        // higher-half layout is being brought online. A process CR3 therefore has to inherit
        // *all* current kernel mappings, not only PML4[256..511]. Clear U/S at the PML4 level
        // so those inherited mappings remain supervisor-only. User branches are privately
        // cloned on demand by Child before their path is made user-accessible.
        for(Int32 i=0;i<512;i++)dst[i]=src[i]&~User;
        return true;
    }

    internal static Boolean TryMap(UInt64 root, UInt64 virtualAddress, UInt64 physicalAddress, KernelVirtualMemoryProtection protection, KernelPhysicalAllocation* tables, UInt32 capacity, ref UInt32 tableCount)
    {
        if((virtualAddress&4095UL)!=0UL || (physicalAddress&4095UL)!=0UL || virtualAddress<KernelAddressSpace.UserBase || virtualAddress>=KernelAddressSpace.UserEndExclusive || ((protection&KernelVirtualMemoryProtection.Write)!=0 && (protection&KernelVirtualMemoryProtection.Execute)!=0)) return false;
        if(!TryPointer(root,out UInt64* pml4))return false; Int32 a=(Int32)((virtualAddress>>39)&511UL), b=(Int32)((virtualAddress>>30)&511UL), c=(Int32)((virtualAddress>>21)&511UL), d=(Int32)((virtualAddress>>12)&511UL);
        if(a>=256)return false;
        if(!Child(pml4,a,false,tables,capacity,ref tableCount,out UInt64* pdpt))return false;
        if(!Child(pdpt,b,true,tables,capacity,ref tableCount,out UInt64* pd))return false;
        if(!Child(pd,c,true,tables,capacity,ref tableCount,out UInt64* pt))return false;
        if((pt[d]&Present)!=0UL)return false;
        UInt64 flags=Present|User; if((protection&KernelVirtualMemoryProtection.Write)!=0)flags|=Writable; if((protection&KernelVirtualMemoryProtection.Execute)==0)flags|=NoExecute; pt[d]=(physicalAddress&AddressMask)|flags; return true;
    }

    // Inspects a candidate process CR3 without activating it. This is used immediately before
    // CR3 replacement so Inu can refuse a transition that would unmap the currently executing
    // kernel instruction/stack instead of discovering the mistake through #PF -> #DF -> reset.
    internal static Boolean TryInspectMapping(UInt64 root, UInt64 virtualAddress, out UInt64 physicalAddress, out UInt64 rawEntry, out UInt64 pageSize)
    {
        physicalAddress=0UL;rawEntry=0UL;pageSize=0UL;if(root==0UL||!TryPointer(root,out UInt64* pml4))return false;
        UInt64 e=pml4[(Int32)((virtualAddress>>39)&511UL)];if((e&Present)==0UL||!TryPointer(e&AddressMask,out UInt64* pdpt))return false;
        e=pdpt[(Int32)((virtualAddress>>30)&511UL)];if((e&Present)==0UL)return false;
        if((e&LargePage)!=0UL){pageSize=1073741824UL;rawEntry=e;physicalAddress=(e&AddressMask1GiB)|(virtualAddress&(pageSize-1UL));return true;}
        if(!TryPointer(e&AddressMask,out UInt64* pd))return false;e=pd[(Int32)((virtualAddress>>21)&511UL)];if((e&Present)==0UL)return false;
        if((e&LargePage)!=0UL){pageSize=2097152UL;rawEntry=e;physicalAddress=(e&AddressMask2MiB)|(virtualAddress&(pageSize-1UL));return true;}
        if(!TryPointer(e&AddressMask,out UInt64* pt))return false;e=pt[(Int32)((virtualAddress>>12)&511UL)];if((e&Present)==0UL)return false;
        pageSize=4096UL;rawEntry=e;physicalAddress=(e&AddressMask)|(virtualAddress&4095UL);return true;
    }

    internal static Boolean TryReleaseTables(KernelPhysicalAllocation* tables, UInt32 count)
    { Boolean ok=true; for(UInt32 i=count;i>0U;i--) if(!KernelPhysicalMemory.TryRelease(tables[i-1U]))ok=false; return ok; }

    private static Boolean Child(UInt64* table, Int32 index, Boolean largePageValid, KernelPhysicalAllocation* tables, UInt32 capacity, ref UInt32 count, out UInt64* child)
    {
        child=(UInt64*)0; UInt64 e=table[index];
        if((e&Present)!=0UL)
        {
            if(largePageValid&&(e&LargePage)!=0UL)return false;
            UInt64 existing=e&AddressMask;
            if(IsOwnedTable(existing,tables,count)){return TryPointer(existing,out child);}
            // Clone inherited kernel page-table state before making this branch user-traversable.
            // Sibling inherited entries remain supervisor-only because their U/S bits are cleared.
            if(!AllocateTable(tables,capacity,ref count,out KernelPhysicalAllocation clone))return false;
            if(!TryPointer(existing,out UInt64* source)||!TryPointer(clone.StartAddress,out UInt64* target))return false;for(Int32 i=0;i<512;i++)target[i]=source[i]&~User;
            table[index]=(clone.StartAddress&AddressMask)|Present|Writable|User;child=target;return true;
        }
        if(!AllocateTable(tables,capacity,ref count,out KernelPhysicalAllocation a)||!TryPointer(a.StartAddress,out child))return false; table[index]=(a.StartAddress&AddressMask)|Present|Writable|User; return true;
    }

    private static Boolean IsOwnedTable(UInt64 physical, KernelPhysicalAllocation* tables, UInt32 count)
    {
        for(UInt32 i=0U;i<count;i++)if(tables[i].StartAddress==physical&&tables[i].PageCount!=0UL)return true;return false;
    }

    private static Boolean AllocateTable(KernelPhysicalAllocation* tables, UInt32 capacity, ref UInt32 count, out KernelPhysicalAllocation allocation)
    {
        allocation=default; if(count>=capacity || !KernelPhysicalMemory.TryAllocate(1UL,1UL,out allocation))return false; tables[count++]=allocation; if(!TryPointer(allocation.StartAddress,out UInt64* p))return false; for(Int32 i=0;i<512;i++)p[i]=0UL; return true;
    }

    private static Boolean TryPointer(UInt64 physical,out UInt64* pointer)
    { pointer=(UInt64*)0;if(!KernelVirtualMemory.TryGetPageTableAccessAddress(physical,out UInt64 v))return false;pointer=(UInt64*)(nuint)v;return true; }
}
