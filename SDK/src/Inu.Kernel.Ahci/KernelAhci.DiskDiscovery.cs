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
 static Boolean StartDisk(UInt32 ci,Controller* c,Byte port){Int32 slot=FreeDisk();if(slot<0){if(!GrowDisks())return false;slot=FreeDisk();if(slot<0)return false;}Disk* d=_disks+slot;Clear((Byte*)d,(UInt64)sizeof(Disk));d->Controller=ci;d->Port=port;if(!AllocPage(out d->CommandList)||!AllocPage(out d->Fis)||!AllocPage(out d->CommandTable)||!AllocPage(out d->Bounce)){ReleaseDiskResources(d);Clear((Byte*)d,(UInt64)sizeof(Disk));return false;}Zero(d->CommandList);Zero(d->Fis);Zero(d->CommandTable);Zero(d->Bounce);UInt64 pr=c->Abar+PxBase+(UInt32)port*PxStride;if(!StopPort(pr)){ReleaseDiskResources(d);Clear((Byte*)d,(UInt64)sizeof(Disk));return false;}W32(pr,0,(UInt32)d->CommandList.StartAddress);W32(pr,4,(UInt32)(d->CommandList.StartAddress>>32));W32(pr,8,(UInt32)d->Fis.StartAddress);W32(pr,12,(UInt32)(d->Fis.StartAddress>>32));W32(pr,0x10,0xFFFFFFFFU);if(c->InterruptHandle!=0UL)W32(pr,0x14,0xFFFFFFFFU);if(!StartPort(pr)||!Identify(c,d)){ReleaseDiskResources(d);Clear((Byte*)d,(UInt64)sizeof(Disk));return false;}KernelDeviceIdentifier id=new(KernelDeviceBus.Logical,(UInt16)0x1D1D,(UInt16)0x5341,0,0,0x010601U,0,(ci<<8)|port);if(!KernelDrivers.DiscoverDevice(id,new KernelDeviceHandle(c->DeviceHandle),out KernelDeviceHandle dh)){ReleaseDiskResources(d);Clear((Byte*)d,(UInt64)sizeof(Disk));return false;}KernelDrivers.SetProperty(dh,new KernelDeviceProperty(KernelDevicePropertyKey.LogicalBlockSize,d->SectorSize,d->Sectors));KernelStorageGeometry geo=new(d->SectorSize,d->SectorSize,d->Sectors,false,false);KernelContextualBlockDeviceCallbacks cb=new(&ReadBlocks,&WriteBlocks,&Flush);if(!KernelStorage.RegisterBlockDevice(dh,KernelStorageDeviceKind.Physical,geo,cb,out KernelStorageDeviceHandle sh)){KernelDrivers.RemoveDevice(dh);ReleaseDiskResources(d);Clear((Byte*)d,(UInt64)sizeof(Disk));return false;}d->DeviceHandle=dh.Value;d->StorageHandle=sh.Value;d->Used=1;_diskCount++;return true;}
 static Boolean Identify(Controller* c,Disk* d){Zero(d->Bounce);if(!Command(c,d,0xEC,0,1,false,true))return false;UInt16* w=(UInt16*)Direct(d->Bounce);Boolean lba48=(w[83]&(1U<<10))!=0;UInt64 sectors=lba48?AhciMath.DecodeLba48(w[100],w[101],w[102],w[103]):((UInt32)w[60]|((UInt32)w[61]<<16));UInt32 ss=AhciMath.DecodeLogicalSectorSize(w[106],w[117],w[118]);if(sectors==0||ss==0||ss>4096U)return false;d->Lba48=lba48?(Byte)1:(Byte)0;d->Sectors=sectors;d->SectorSize=ss;return true;}
}
