using System;

namespace Inu.Kernel.VirtualMemory;

/// <summary>Raw x64 page-table walk information for one virtual address.</summary>
public readonly struct KernelPageTableInspection
{
    internal KernelPageTableInspection(UInt64 virtualAddress, Boolean mapped, UInt64 physicalAddress, KernelVirtualPageSize pageSize, KernelVirtualMemoryProtection protection, UInt64 pml4, UInt64 pdpt, UInt64 pd, UInt64 pt, UInt64 leaf)
    { VirtualAddress=virtualAddress; Mapped=mapped; PhysicalAddress=physicalAddress; PageSize=pageSize; Protection=protection; Pml4Entry=pml4; PdptEntry=pdpt; PdEntry=pd; PtEntry=pt; LeafEntry=leaf; }
    public UInt64 VirtualAddress { get; }
    public Boolean Mapped { get; }
    public UInt64 PhysicalAddress { get; }
    public KernelVirtualPageSize PageSize { get; }
    public KernelVirtualMemoryProtection Protection { get; }
    public UInt64 Pml4Entry { get; }
    public UInt64 PdptEntry { get; }
    public UInt64 PdEntry { get; }
    public UInt64 PtEntry { get; }
    public UInt64 LeafEntry { get; }
}

public static unsafe partial class KernelVirtualMemory
{
    /// <summary>Walks the active x64 page tables and returns raw ancestor/leaf entries without modifying them.</summary>
    public static Boolean TryInspectPageTable(UInt64 virtualAddress, out KernelPageTableInspection inspection)
    {
        inspection=default;
        if(!_initialized || !IsCanonical(virtualAddress)) return false;
        UInt64* pml4=TablePointer(_rootPhysicalAddress);
        UInt64 pml4e=pml4[(Int32)((virtualAddress>>39)&0x1FFUL)];
        if(!IsPresent(pml4e)){ inspection=new KernelPageTableInspection(virtualAddress,false,0UL,KernelVirtualPageSize.Page4KiB,KernelVirtualMemoryProtection.None,pml4e,0UL,0UL,0UL,0UL); return true; }
        UInt64* pdpt=TablePointer(pml4e&AddressMask4KiB); UInt64 pdpte=pdpt[(Int32)((virtualAddress>>30)&0x1FFUL)];
        if(!IsPresent(pdpte)){ inspection=new KernelPageTableInspection(virtualAddress,false,0UL,KernelVirtualPageSize.Page4KiB,KernelVirtualMemoryProtection.None,pml4e,pdpte,0UL,0UL,0UL); return true; }
        if(IsLarge(pdpte))
        {
            UInt64 pa=(pdpte&AddressMask1GiB)+(virtualAddress&((UInt64)KernelVirtualPageSize.Page1GiB-1UL));
            inspection=new KernelPageTableInspection(virtualAddress,true,pa,KernelVirtualPageSize.Page1GiB,DecodeProtection(pdpte),pml4e,pdpte,0UL,0UL,pdpte); return true;
        }
        UInt64* pd=TablePointer(pdpte&AddressMask4KiB); UInt64 pde=pd[(Int32)((virtualAddress>>21)&0x1FFUL)];
        if(!IsPresent(pde)){ inspection=new KernelPageTableInspection(virtualAddress,false,0UL,KernelVirtualPageSize.Page4KiB,KernelVirtualMemoryProtection.None,pml4e,pdpte,pde,0UL,0UL); return true; }
        if(IsLarge(pde))
        {
            UInt64 pa=(pde&AddressMask2MiB)+(virtualAddress&((UInt64)KernelVirtualPageSize.Page2MiB-1UL));
            inspection=new KernelPageTableInspection(virtualAddress,true,pa,KernelVirtualPageSize.Page2MiB,DecodeProtection(pde),pml4e,pdpte,pde,0UL,pde); return true;
        }
        UInt64* pt=TablePointer(pde&AddressMask4KiB); UInt64 pte=pt[(Int32)((virtualAddress>>12)&0x1FFUL)];
        if(!IsPresent(pte)){ inspection=new KernelPageTableInspection(virtualAddress,false,0UL,KernelVirtualPageSize.Page4KiB,KernelVirtualMemoryProtection.None,pml4e,pdpte,pde,pte,0UL); return true; }
        UInt64 physical=(pte&AddressMask4KiB)+(virtualAddress&0xFFFUL);
        inspection=new KernelPageTableInspection(virtualAddress,true,physical,KernelVirtualPageSize.Page4KiB,DecodeProtection(pte),pml4e,pdpte,pde,pte,pte); return true;
    }
}
