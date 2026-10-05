using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Memory;
using Inu.Kernel.Heap;
using Inu.Kernel.Pci;
using Inu.Kernel.Storage;
namespace Inu.Kernel.Ahci;
/// <summary>AHCI 1.x PCI driver with SATA discovery, DMA command lists, IDENTIFY, read/write and flush.</summary>
public static unsafe partial class KernelAhci
{
 const UInt32 SpinLimit=40000000U; const UInt32 PxBase=0x100U,PxStride=0x80U;
 struct Controller{internal Byte Used;internal UInt16 Segment;internal Byte Bus,Device,Function;internal UInt32 DeviceHandle,Implemented,DiskCount;internal UInt64 Abar,InterruptHandle,InterruptEpoch;}
 struct Disk{internal Byte Used,Port,Lba48;internal UInt32 Controller,DeviceHandle,StorageHandle,SectorSize;internal UInt64 Sectors;internal KernelPhysicalAllocation CommandList,Fis,CommandTable,Bounce;}
 static Controller* _controllers;static Disk* _disks;static KernelHeapAllocation _controllerAllocation,_diskAllocation;static UInt32 _controllerCapacity,_diskCapacity,_controllerCount,_diskCount;static KernelDriverHandle _driver;static Boolean _initialized;
 public static Boolean Initialize(){if(_initialized)return true;if(!KernelPci.IsInitialized()||!KernelStorage.IsInitialized()||!KernelPhysicalMemory.IsInitialized()||!KernelAddressSpace.IsInitialized()||!KernelDrivers.IsInitialized())return false;if(!AllocateTables())return false;KernelDriverMatchRule rule=new(KernelDeviceBus.Pci,true,0,false,0,false,0x010601U,0xFFFFFFU);KernelDriverCallbacks cb=new(&Probe,&Start,&Stop,&Remove,&Interrupt);KernelDriverCapabilityDeclaration caps=new(KernelDriverCapability.Mmio|KernelDriverCapability.Dma|KernelDriverCapability.PciConfig|KernelDriverCapability.Interrupt|KernelDriverCapability.Msi|KernelDriverCapability.MsiX|KernelDriverCapability.Filesystem);if(!KernelDrivers.RegisterDriver("ahci",rule,cb,caps,out _driver))return false;_initialized=true;UInt32 count=KernelPci.GetDeviceCount();for(UInt32 i=0;i<count;i++){if(!KernelPci.TryGetDevice(i,out PciDeviceInfo pci)||!AhciMath.IsAhciClass(pci.ClassCode))continue;KernelDrivers.MatchAndStartDevice(pci.DeviceHandle);}return true;}
 public static Boolean IsInitialized()=>_initialized;public static AhciCapabilities GetCapabilities()=>new(_initialized,_controllerCount,_diskCount);
 public static Boolean TryGetController(UInt32 index,out AhciControllerInfo info){info=default;if(index>=_controllerCount)return false;UInt32 found=0;for(UInt32 i=0;i<_controllerCapacity;i++){Controller* c=_controllers+i;if(c->Used==0)continue;if(found++!=index)continue;info=new AhciControllerInfo(new PciLocation(c->Segment,c->Bus,c->Device,c->Function),new KernelDeviceHandle(c->DeviceHandle),c->Implemented,c->DiskCount);return true;}return false;}
 public static Boolean TryGetDisk(UInt32 index,out AhciDiskInfo info){info=default;if(index>=_diskCount)return false;UInt32 found=0;for(UInt32 i=0;i<_diskCapacity;i++){Disk* d=_disks+i;if(d->Used==0)continue;if(found++!=index)continue;info=new AhciDiskInfo(d->Controller,d->Port,AhciPortType.Sata,d->Sectors,d->SectorSize,d->Lba48!=0,new KernelDeviceHandle(d->DeviceHandle),new KernelStorageDeviceHandle(d->StorageHandle));return true;}return false;}
}
