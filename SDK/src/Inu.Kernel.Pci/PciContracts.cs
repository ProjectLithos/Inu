using System;
using Inu.Kernel.Drivers;

namespace Inu.Kernel.Pci;

/// <summary>Identifies the configuration-space transport used for a PCI function.</summary>
public enum PciConfigurationTransport : Byte { LegacyIo=1, PcieEcam=2 }
/// <summary>Identifies a PCI BAR address-space type.</summary>
public enum PciBarType : Byte { None=0, Io=1, Memory32=2, Memory64=3 }

/// <summary>Identifies one PCI segment/bus/device/function address.</summary>
public readonly struct PciLocation
{
    /// <summary>Creates a PCI location.</summary>
    public PciLocation(UInt16 segment,Byte bus,Byte device,Byte function){Segment=segment;Bus=bus;Device=device;Function=function;}
    /// <summary>PCI segment group.</summary>
    public UInt16 Segment { get; }
    /// <summary>PCI bus number.</summary>
    public Byte Bus { get; }
    /// <summary>PCI device number.</summary>
    public Byte Device { get; }
    /// <summary>PCI function number.</summary>
    public Byte Function { get; }
    /// <summary>Packs the complete segment/bus/device/function address into 32 bits.</summary>
    public UInt32 Encode()=>((UInt32)Segment<<16)|((UInt32)Bus<<8)|((UInt32)Device<<3)|Function;
    /// <summary>Decodes a packed PCI location.</summary>
    public static PciLocation Decode(UInt32 value)=>new((UInt16)(value>>16),(Byte)(value>>8),(Byte)((value>>3)&31U),(Byte)(value&7U));
}

/// <summary>Describes one discovered PCI base-address register.</summary>
public readonly struct PciBarInfo
{
    /// <summary>Creates BAR metadata.</summary>
    public PciBarInfo(Byte index,PciBarType type,UInt64 address,UInt64 length,Boolean prefetchable){Index=index;Type=type;Address=address;Length=length;Prefetchable=prefetchable;}
    /// <summary>BAR index from zero through five.</summary>
    public Byte Index { get; }
    /// <summary>BAR address-space type.</summary>
    public PciBarType Type { get; }
    /// <summary>Decoded physical MMIO address or I/O port base.</summary>
    public UInt64 Address { get; }
    /// <summary>Size discovered by the standard BAR sizing transaction.</summary>
    public UInt64 Length { get; }
    /// <summary>Whether a memory BAR advertises prefetchable semantics.</summary>
    public Boolean Prefetchable { get; }
}

/// <summary>Describes one PCI capability in the conventional capability list.</summary>
public readonly struct PciCapabilityInfo
{
    /// <summary>Creates capability metadata.</summary>
    public PciCapabilityInfo(Byte id,UInt16 offset,Byte next){Id=id;Offset=offset;Next=next;}
    /// <summary>Capability identifier.</summary>
    public Byte Id { get; }
    /// <summary>Configuration-space offset.</summary>
    public UInt16 Offset { get; }
    /// <summary>Next conventional capability offset.</summary>
    public Byte Next { get; }
}

/// <summary>Describes one PCIe extended capability.</summary>
public readonly struct PciExtendedCapabilityInfo
{
    /// <summary>Creates extended capability metadata.</summary>
    public PciExtendedCapabilityInfo(UInt16 id,Byte version,UInt16 offset,UInt16 next){Id=id;Version=version;Offset=offset;Next=next;}
    /// <summary>Extended capability identifier.</summary>
    public UInt16 Id { get; }
    /// <summary>Capability version.</summary>
    public Byte Version { get; }
    /// <summary>Configuration-space offset.</summary>
    public UInt16 Offset { get; }
    /// <summary>Next extended capability offset.</summary>
    public UInt16 Next { get; }
}

/// <summary>Describes the standard PCI MSI capability.</summary>
public readonly struct PciMsiCapability
{
    /// <summary>Creates MSI capability metadata.</summary>
    public PciMsiCapability(UInt16 offset,Boolean address64,Boolean perVectorMask,Byte multipleMessageCapable){Offset=offset;Address64=address64;PerVectorMask=perVectorMask;MultipleMessageCapable=multipleMessageCapable;}
    /// <summary>Capability offset.</summary>
    public UInt16 Offset { get; }
    /// <summary>Whether the message address is 64 bit.</summary>
    public Boolean Address64 { get; }
    /// <summary>Whether per-vector masking is implemented.</summary>
    public Boolean PerVectorMask { get; }
    /// <summary>Log2 of the maximum supported message count.</summary>
    public Byte MultipleMessageCapable { get; }
}

/// <summary>Describes the standard PCI MSI-X capability.</summary>
public readonly struct PciMsixCapability
{
    /// <summary>Creates MSI-X capability metadata.</summary>
    public PciMsixCapability(UInt16 offset,UInt16 tableSize,Byte tableBar,UInt32 tableOffset,Byte pendingBar,UInt32 pendingOffset){Offset=offset;TableSize=tableSize;TableBar=tableBar;TableOffset=tableOffset;PendingBar=pendingBar;PendingOffset=pendingOffset;}
    /// <summary>Capability offset.</summary>
    public UInt16 Offset { get; }
    /// <summary>Number of MSI-X table entries.</summary>
    public UInt16 TableSize { get; }
    /// <summary>BAR containing the MSI-X table.</summary>
    public Byte TableBar { get; }
    /// <summary>Byte offset of the table within its BAR.</summary>
    public UInt32 TableOffset { get; }
    /// <summary>BAR containing the pending-bit array.</summary>
    public Byte PendingBar { get; }
    /// <summary>Byte offset of the pending-bit array.</summary>
    public UInt32 PendingOffset { get; }
}

/// <summary>Describes one discovered PCI function and its driver-framework handle.</summary>
public readonly struct PciDeviceInfo
{
    /// <summary>Creates discovered PCI device metadata.</summary>
    public PciDeviceInfo(PciLocation location,PciConfigurationTransport transport,KernelDeviceHandle handle,UInt16 vendorId,UInt16 deviceId,UInt16 subsystemVendorId,UInt16 subsystemId,UInt32 classCode,Byte revision,Byte headerType)
    {Location=location;Transport=transport;DeviceHandle=handle;VendorId=vendorId;DeviceId=deviceId;SubsystemVendorId=subsystemVendorId;SubsystemId=subsystemId;ClassCode=classCode;Revision=revision;HeaderType=headerType;}
    /// <summary>PCI address.</summary>
    public PciLocation Location { get; }
    /// <summary>Configuration transport.</summary>
    public PciConfigurationTransport Transport { get; }
    /// <summary>Corresponding generic kernel device.</summary>
    public KernelDeviceHandle DeviceHandle { get; }
    /// <summary>PCI vendor identifier.</summary>
    public UInt16 VendorId { get; }
    /// <summary>PCI device identifier.</summary>
    public UInt16 DeviceId { get; }
    /// <summary>Subsystem vendor identifier where present.</summary>
    public UInt16 SubsystemVendorId { get; }
    /// <summary>Subsystem device identifier where present.</summary>
    public UInt16 SubsystemId { get; }
    /// <summary>24-bit base-class/subclass/programming-interface value.</summary>
    public UInt32 ClassCode { get; }
    /// <summary>Revision identifier.</summary>
    public Byte Revision { get; }
    /// <summary>Header type without the multifunction bit.</summary>
    public Byte HeaderType { get; }
}

/// <summary>Summarizes PCI/PCIe discovery state.</summary>
public readonly struct PciCapabilities
{
    /// <summary>Creates PCI capability summary data.</summary>
    public PciCapabilities(Boolean initialized,UInt32 deviceCount,UInt32 ecamSegments,Boolean legacyConfigurationAvailable,UInt64 nextMmioVirtualAddress){Initialized=initialized;DeviceCount=deviceCount;EcamSegments=ecamSegments;LegacyConfigurationAvailable=legacyConfigurationAvailable;NextMmioVirtualAddress=nextMmioVirtualAddress;}
    /// <summary>Whether PCI discovery has run.</summary>
    public Boolean Initialized { get; }
    /// <summary>Number of discovered functions.</summary>
    public UInt32 DeviceCount { get; }
    /// <summary>Number of MCFG ECAM allocations.</summary>
    public UInt32 EcamSegments { get; }
    /// <summary>Whether x64 legacy CF8/CFC configuration access is available.</summary>
    public Boolean LegacyConfigurationAvailable { get; }
    /// <summary>Next free address in the standard MMIO virtual window.</summary>
    public UInt64 NextMmioVirtualAddress { get; }
}
