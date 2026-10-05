using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Pci;
using Inu.Kernel.Storage;
namespace Inu.Kernel.Ahci;
public enum AhciPortType : Byte { None=0, Sata=1, Satapi=2, Enclosure=3, PortMultiplier=4 }
public readonly struct AhciControllerInfo { public AhciControllerInfo(PciLocation location,KernelDeviceHandle device,UInt32 implementedPorts,UInt32 activeDisks){Location=location;Device=device;ImplementedPorts=implementedPorts;ActiveDisks=activeDisks;} public PciLocation Location { get; } public KernelDeviceHandle Device { get; } public UInt32 ImplementedPorts { get; } public UInt32 ActiveDisks { get; } }
public readonly struct AhciDiskInfo { public AhciDiskInfo(UInt32 controllerIndex,Byte port,AhciPortType type,UInt64 sectors,UInt32 logicalSectorSize,Boolean lba48,KernelDeviceHandle device,KernelStorageDeviceHandle storage){ControllerIndex=controllerIndex;Port=port;Type=type;Sectors=sectors;LogicalSectorSize=logicalSectorSize;Lba48=lba48;Device=device;Storage=storage;} public UInt32 ControllerIndex { get; } public Byte Port { get; } public AhciPortType Type { get; } public UInt64 Sectors { get; } public UInt32 LogicalSectorSize { get; } public Boolean Lba48 { get; } public KernelDeviceHandle Device { get; } public KernelStorageDeviceHandle Storage { get; } }
public readonly struct AhciCapabilities { public AhciCapabilities(Boolean initialized,UInt32 controllers,UInt32 disks){Initialized=initialized;Controllers=controllers;Disks=disks;} public Boolean Initialized { get; } public UInt32 Controllers { get; } public UInt32 Disks { get; } }
