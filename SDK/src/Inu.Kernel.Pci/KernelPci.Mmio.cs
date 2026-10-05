using System;
using Inu.Kernel.Acpi;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Pci;

public static unsafe partial class KernelPci
{
    /// <summary>Maps an arbitrary physical MMIO range into the standard kernel MMIO window using device-memory page protections.</summary>
    public static Boolean TryMapMmio(UInt64 physicalAddress,UInt64 length,out UInt64 virtualAddress)
    {
        virtualAddress=0;if(length==0UL||physicalAddress>UInt64.MaxValue-(length-1UL))return false;UInt64 pageBase=physicalAddress&~0xFFFUL;UInt64 offset=physicalAddress-pageBase;UInt64 span=offset+length;if(span<length)return false;UInt64 pages=(span+0xFFFUL)>>12;if(pages>UInt64.MaxValue/PageSize)return false;UInt64 bytes=pages*PageSize;UInt64 start=AlignUp(_nextMmioVirtual,PageSize);if(start<KernelAddressSpace.MmioBase||start>KernelAddressSpace.MmioBase+KernelAddressSpace.MmioLength-bytes)return false;
        KernelVirtualMemoryProtection protection=KernelVirtualMemoryProtection.Read|KernelVirtualMemoryProtection.Write|KernelVirtualMemoryProtection.Device|KernelVirtualMemoryProtection.Global;
        UInt64 mapped=0;for(UInt64 i=0;i<pages;i++){if(!KernelVirtualMemory.TryMap(start+i*PageSize,pageBase+i*PageSize,KernelVirtualPageSize.Page4KiB,protection)){for(UInt64 j=0;j<mapped;j++)KernelVirtualMemory.TryUnmap(start+j*PageSize);return false;}mapped++;}
        _nextMmioVirtual=start+bytes;virtualAddress=start+offset;return true;
    }

    /// <summary>Discovers and maps a memory BAR into the kernel MMIO window.</summary>
    public static Boolean TryMapBar(PciLocation location,Byte barIndex,out PciBarInfo bar,out UInt64 virtualAddress)
    {virtualAddress=0;if(!TryGetBar(location,barIndex,out bar)||bar.Type==PciBarType.Io)return false;return TryMapMmio(bar.Address,bar.Length,out virtualAddress);}
}
