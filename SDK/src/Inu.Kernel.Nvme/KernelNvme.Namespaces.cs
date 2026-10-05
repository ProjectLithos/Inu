using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Memory;
using Inu.Kernel.Heap;
using Inu.Kernel.Pci;
using Inu.Kernel.Storage;
namespace Inu.Kernel.Nvme;
/// <summary>PCI NVMe 1.x controller driver with admin/I/O queues, namespace discovery and synchronous block I/O.</summary>
public static unsafe partial class KernelNvme
{
 static Boolean CreateIoQueues(Controller* c){if(!AllocPage(out c->IoSq)||!AllocPage(out c->IoCq))return false;Zero(c->IoSq);Zero(c->IoCq);if(!AdminCreateCq(c,1,c->IoEntries,c->IoCq.StartAddress))return false;if(!AdminCreateSq(c,1,c->IoEntries,c->IoSq.StartAddress,1))return false;return true;}
 static Boolean DiscoverActiveNamespaces(UInt32 ci,Controller* c,UInt32 nn){UInt32 start=0;for(;;){Zero(c->Identify);if(!AdminIdentify(c,start,2,c->Identify.StartAddress)){if(start!=0)return false;for(UInt32 nsid=1;nsid<=nn;nsid++)DiscoverNamespace(ci,c,nsid);return true;}UInt32* ids=(UInt32*)Direct(c->Identify);UInt32 last=start;Boolean full=true;for(UInt32 i=0;i<1024U;i++){UInt32 nsid=ids[i];if(nsid==0){full=false;break;}if(nsid<=last)return false;last=nsid;DiscoverNamespace(ci,c,nsid);}if(!full)return true;start=last;}}
 static Boolean DiscoverNamespace(UInt32 ci,Controller* c,UInt32 nsid){Zero(c->Identify);if(!AdminIdentify(c,nsid,0,c->Identify.StartAddress))return false;Byte* d=Direct(c->Identify);UInt64 nsze=*(UInt64*)d;if(nsze==0)return false;Byte flbas=d[26];Byte format=(Byte)(flbas&15U);Byte lbads=d[128U+(UInt32)format*4U+2U];UInt32 blockSize=NvmeMath.NamespaceBlockSize(lbads);if(blockSize==0||blockSize>4096U)return false;Int32 slot=FreeNamespace();if(slot<0){if(!GrowNamespaces())return false;slot=FreeNamespace();if(slot<0)return false;}KernelDeviceIdentifier ident=new(KernelDeviceBus.Logical,(UInt16)0x1D1D,(UInt16)0x4E56,0,0,0x010802U,0,(ci<<16)|(nsid&0xFFFFU));if(!KernelDrivers.DiscoverDevice(ident,new KernelDeviceHandle(c->DeviceHandle),out KernelDeviceHandle dh))return false;KernelDrivers.SetProperty(dh,new KernelDeviceProperty(KernelDevicePropertyKey.LogicalBlockSize,blockSize,nsze));KernelStorageGeometry geometry=new(blockSize,blockSize,nsze,false,false);KernelContextualBlockDeviceCallbacks callbacks=new(&ReadBlocks,&WriteBlocks,&Flush);if(!KernelStorage.RegisterBlockDevice(dh,KernelStorageDeviceKind.Physical,geometry,callbacks,out KernelStorageDeviceHandle sh)){KernelDrivers.RemoveDevice(dh);return false;}Namespace* n=_namespaces+slot;Clear((Byte*)n,(UInt64)sizeof(Namespace));n->Used=1;n->Controller=ci;n->Nsid=nsid;n->BlockSize=blockSize;n->Blocks=nsze;n->DeviceHandle=dh.Value;n->StorageHandle=sh.Value;_namespaceCount++;c->NamespaceCount++;return true;}
}
