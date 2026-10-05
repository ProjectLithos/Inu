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
 static Boolean ReadBlocks(KernelDeviceHandle device,UInt64 lba,UInt32 count,Byte* buffer,UInt32 bytes)=>Transfer(device,lba,count,buffer,bytes,false);
 static Boolean WriteBlocks(KernelDeviceHandle device,UInt64 lba,UInt32 count,Byte* buffer,UInt32 bytes)=>Transfer(device,lba,count,buffer,bytes,true);
 static Boolean Flush(KernelDeviceHandle device){if(!FindNamespace(device,out Namespace* n))return false;Controller* c=_controllers+n->Controller;return SubmitIo(c,n->Nsid,0,0,0,0);}
 static Boolean Transfer(KernelDeviceHandle device,UInt64 lba,UInt32 count,Byte* buffer,UInt32 bytes,Boolean write){if(buffer==null||count==0||!FindNamespace(device,out Namespace* n)||lba>=n->Blocks||count>n->Blocks-lba||(UInt64)bytes<(UInt64)count*n->BlockSize)return false;Controller* c=_controllers+n->Controller;if(!AllocPage(out KernelPhysicalAllocation bounce))return false;Byte* b=Direct(bounce);Boolean ok=true;for(UInt32 i=0;i<count&&ok;i++){if(write)Copy(buffer+(UInt64)i*n->BlockSize,b,n->BlockSize);ok=SubmitIo(c,n->Nsid,write?(Byte)1:(Byte)2,lba+i,bounce.StartAddress,1);if(ok&&!write)Copy(b,buffer+(UInt64)i*n->BlockSize,n->BlockSize);}KernelPhysicalMemory.TryRelease(bounce);return ok;}
}
