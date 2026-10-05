using System;

namespace Inu.Kernel.Pci;

/// <summary>Provides allocation-free PCI address, BAR, and capability calculations suitable for drivers and host tests.</summary>
public static class PciMath
{
    /// <summary>Validates a PCI segment/bus/device/function address.</summary>
    public static Boolean IsValidLocation(PciLocation location)=>location.Device<32U&&location.Function<8U;
    /// <summary>Gets whether conventional PCI configuration mechanism #1 can access the requested field without ECAM mapping.</summary>
    public static Boolean ShouldUseLegacyConfiguration(PciLocation location,UInt16 offset)=>location.Segment==0U&&offset<0x100U;
    /// <summary>Converts a standard 32-bit BAR sizing mask to a byte length.</summary>
    public static UInt64 BarLength32(UInt32 mask,Boolean ioSpace){UInt32 value=mask&(ioSpace?0xFFFFFFFCU:0xFFFFFFF0U);return value==0U?0UL:(UInt64)(~value+1U);}
    /// <summary>Converts a standard 64-bit memory BAR sizing mask to a byte length.</summary>
    public static UInt64 BarLength64(UInt64 mask){UInt64 value=mask&0xFFFFFFFFFFFFFFF0UL;return value==0UL?0UL:(~value)+1UL;}
    /// <summary>Validates a conventional capability-list pointer.</summary>
    public static Boolean IsConventionalCapabilityOffset(UInt16 offset)=>offset>=0x40U&&offset<=0xFCU&&(offset&3U)==0U;
    /// <summary>Validates a PCIe extended-capability pointer.</summary>
    public static Boolean IsExtendedCapabilityOffset(UInt16 offset)=>offset>=0x100U&&offset<=0xFFCU&&(offset&3U)==0U;
}
