using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Memory;

namespace Inu.Kernel.VirtualMemory;

public static unsafe partial class KernelVirtualMemory
{
    private static Boolean TryEncodeLeaf(UInt64 physicalAddress, KernelVirtualPageSize pageSize, KernelVirtualMemoryProtection protection, out UInt64 entry)
    {
        entry = 0UL;
        UInt64 mask = LeafAddressMask(pageSize);
        UInt64 size = (UInt64)pageSize;
        if (mask == 0UL || (physicalAddress & (size - 1UL)) != 0UL || (physicalAddress & ~mask) != 0UL) return false;
        UInt64 flags = Present;
        if (HasProtection(protection, KernelVirtualMemoryProtection.Write)) flags |= Writable;
        if (HasProtection(protection, KernelVirtualMemoryProtection.User)) flags |= User;
        if (HasProtection(protection, KernelVirtualMemoryProtection.Global)) flags |= Global;
        if (HasProtection(protection, KernelVirtualMemoryProtection.Device)) flags |= CacheDisable;
        if (HasProtection(protection, KernelVirtualMemoryProtection.WriteThrough)) flags |= WriteThrough;
        if (!HasProtection(protection, KernelVirtualMemoryProtection.Execute)) flags |= NoExecute;
        if (pageSize != KernelVirtualPageSize.Page4KiB) flags |= LargePage;
        entry = (physicalAddress & mask) | flags;
        return true;
    }

    private static KernelVirtualMemoryProtection DecodeProtection(UInt64 entry)
    {
        KernelVirtualMemoryProtection protection = KernelVirtualMemoryProtection.Read;
        if ((entry & Writable) != 0UL) protection = protection | KernelVirtualMemoryProtection.Write;
        if ((entry & User) != 0UL) protection = protection | KernelVirtualMemoryProtection.User;
        if ((entry & Global) != 0UL) protection = protection | KernelVirtualMemoryProtection.Global;
        if ((entry & CacheDisable) != 0UL) protection = protection | KernelVirtualMemoryProtection.Device;
        if ((entry & WriteThrough) != 0UL) protection = protection | KernelVirtualMemoryProtection.WriteThrough;
        if ((entry & NoExecute) == 0UL) protection = protection | KernelVirtualMemoryProtection.Execute;
        return protection;
    }

    private static UInt64 LeafAddressMask(KernelVirtualPageSize pageSize)
    {
        if (pageSize == KernelVirtualPageSize.Page4KiB) return AddressMask4KiB;
        if (pageSize == KernelVirtualPageSize.Page2MiB) return AddressMask2MiB;
        if (pageSize == KernelVirtualPageSize.Page1GiB) return AddressMask1GiB;
        return 0UL;
    }

    private static Boolean IsSupportedPageSize(UInt64 size) => size == 4096UL || size == 2097152UL || size == 1073741824UL;
    private static Boolean IsPresent(UInt64 entry) => (entry & Present) != 0UL;
    private static Boolean IsLarge(UInt64 entry) => (entry & LargePage) != 0UL;
    private static Boolean HasProtection(KernelVirtualMemoryProtection value, KernelVirtualMemoryProtection flag) => (((UInt64)value & (UInt64)flag) != 0UL);
    private static UInt64* TablePointer(UInt64 physicalAddress)
    {
        if (_directMapReady && !WasProtected(physicalAddress))
            return (UInt64*)(nuint)(_directMapBase + physicalAddress);
        return (UInt64*)(nuint)physicalAddress;
    }

    private static Boolean IsCanonical(UInt64 address)
    {
        UInt64 upper = address >> 48;
        Boolean high = ((address >> 47) & 1UL) != 0UL;
        return high ? upper == 0xFFFFUL : upper == 0UL;
    }

    private static Boolean IncrementMapped(KernelVirtualPageSize pageSize)
    {
        if (pageSize == KernelVirtualPageSize.Page4KiB) _mapped4KiB++;
        else if (pageSize == KernelVirtualPageSize.Page2MiB) _mapped2MiB++;
        else _mapped1GiB++;
        return true;
    }

    private static Boolean DecrementMapped(KernelVirtualPageSize pageSize)
    {
        if (pageSize == KernelVirtualPageSize.Page4KiB && _mapped4KiB != 0UL) _mapped4KiB--;
        else if (pageSize == KernelVirtualPageSize.Page2MiB && _mapped2MiB != 0UL) _mapped2MiB--;
        else if (pageSize == KernelVirtualPageSize.Page1GiB && _mapped1GiB != 0UL) _mapped1GiB--;
        return true;
    }
}
