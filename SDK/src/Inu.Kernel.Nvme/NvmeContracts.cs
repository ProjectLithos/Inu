using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Pci;
using Inu.Kernel.Storage;
namespace Inu.Kernel.Nvme;
/// <summary>NVMe controller lifecycle state.</summary>
public enum NvmeControllerState : Byte { Unknown=0, Disabled=1, Ready=2, Failed=3 }
/// <summary>NVMe interrupt delivery selected for a controller.</summary>
public enum NvmeInterruptMode : Byte { Synchronous=0, BrokerManaged=1 }
/// <summary>Information about one initialized PCI NVMe controller.</summary>
public readonly struct NvmeControllerInfo
{
 public NvmeControllerInfo(PciLocation location,KernelDeviceHandle device,NvmeControllerState state,NvmeInterruptMode interruptMode,UInt16 adminQueueEntries,UInt16 ioQueueEntries,UInt32 namespaceCount,UInt32 version){Location=location;Device=device;State=state;InterruptMode=interruptMode;AdminQueueEntries=adminQueueEntries;IoQueueEntries=ioQueueEntries;NamespaceCount=namespaceCount;Version=version;}
 public PciLocation Location { get; } public KernelDeviceHandle Device { get; } public NvmeControllerState State { get; } public NvmeInterruptMode InterruptMode { get; } public UInt16 AdminQueueEntries { get; } public UInt16 IoQueueEntries { get; } public UInt32 NamespaceCount { get; } public UInt32 Version { get; }
}
/// <summary>Information about one active NVMe namespace exposed as a Inu block device.</summary>
public readonly struct NvmeNamespaceInfo
{
 public NvmeNamespaceInfo(UInt32 controllerIndex,UInt32 namespaceId,UInt32 logicalBlockSize,UInt64 blockCount,KernelDeviceHandle device,KernelStorageDeviceHandle storage){ControllerIndex=controllerIndex;NamespaceId=namespaceId;LogicalBlockSize=logicalBlockSize;BlockCount=blockCount;Device=device;Storage=storage;}
 public UInt32 ControllerIndex { get; } public UInt32 NamespaceId { get; } public UInt32 LogicalBlockSize { get; } public UInt64 BlockCount { get; } public KernelDeviceHandle Device { get; } public KernelStorageDeviceHandle Storage { get; }
}
/// <summary>Overall NVMe driver state.</summary>
public readonly struct NvmeCapabilities
{
 public NvmeCapabilities(Boolean initialized,UInt32 controllers,UInt32 namespaces){Initialized=initialized;Controllers=controllers;Namespaces=namespaces;} public Boolean Initialized { get; } public UInt32 Controllers { get; } public UInt32 Namespaces { get; }
}
