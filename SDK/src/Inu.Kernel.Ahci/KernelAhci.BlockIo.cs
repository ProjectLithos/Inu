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
 static Boolean ReadBlocks(KernelDeviceHandle device,UInt64 lba,UInt32 count,Byte* buffer,UInt32 bytes)=>Transfer(device,lba,count,buffer,bytes,false);static Boolean WriteBlocks(KernelDeviceHandle device,UInt64 lba,UInt32 count,Byte* buffer,UInt32 bytes)=>Transfer(device,lba,count,buffer,bytes,true);
 static Boolean Transfer(KernelDeviceHandle device,UInt64 lba,UInt32 count,Byte* buffer,UInt32 bytes,Boolean write){if(buffer==null||count==0||!FindDisk(device,out Disk* d)||lba>=d->Sectors||count>d->Sectors-lba||(UInt64)bytes<(UInt64)count*d->SectorSize)return false;Controller* c=_controllers+d->Controller;Byte* bounce=Direct(d->Bounce);for(UInt32 i=0;i<count;i++){if(write)Copy(buffer+(UInt64)i*d->SectorSize,bounce,d->SectorSize);Byte op=write?(d->Lba48!=0?(Byte)0x35:(Byte)0xCA):(d->Lba48!=0?(Byte)0x25:(Byte)0xC8);if(!Command(c,d,op,lba+i,1,write,false))return false;if(!write)Copy(bounce,buffer+(UInt64)i*d->SectorSize,d->SectorSize);}return true;}
 static Boolean Flush(KernelDeviceHandle device){if(!FindDisk(device,out Disk* d))return false;return Command(_controllers+d->Controller,d,d->Lba48!=0?(Byte)0xEA:(Byte)0xE7,0,0,false,false);}
 static Boolean Command(Controller* c,Disk* d,Byte ata,UInt64 lba,UInt16 sectors,Boolean write,Boolean identify){UInt64 pr=c->Abar+PxBase+(UInt32)d->Port*PxStride;UInt32 wait=SpinLimit;while(wait-->0&&((R32(pr,0x20)&(0x80U|0x08U))!=0)){}if((R32(pr,0x20)&(0x80U|0x08U))!=0)return false;Byte* cl=Direct(d->CommandList);Byte* ct=Direct(d->CommandTable);for(UInt32 i=0;i<1024;i++)cl[i]=0;for(UInt32 i=0;i<4096;i++)ct[i]=0;*(UInt16*)cl=(UInt16)(5U|(write?1U<<6:0U));*(UInt16*)(cl+2)=sectors==0?(UInt16)0:(UInt16)1;*(UInt32*)(cl+8)=(UInt32)d->CommandTable.StartAddress;*(UInt32*)(cl+12)=(UInt32)(d->CommandTable.StartAddress>>32);Byte* fis=ct;fis[0]=0x27;fis[1]=0x80;fis[2]=ata;if(!identify&&sectors!=0){fis[4]=(Byte)lba;fis[5]=(Byte)(lba>>8);fis[6]=(Byte)(lba>>16);if(d->Lba48!=0){fis[7]=0x40;fis[8]=(Byte)(lba>>24);fis[9]=(Byte)(lba>>32);fis[10]=(Byte)(lba>>40);fis[12]=(Byte)sectors;fis[13]=(Byte)(sectors>>8);}else{if(lba>0x0FFFFFFFUL)return false;fis[7]=(Byte)(0x40U|((UInt32)(lba>>24)&15U));fis[12]=(Byte)sectors;}}if(sectors!=0){Byte* p=ct+0x80;UInt32 byteCount=identify?512U:d->SectorSize;*(UInt32*)p=(UInt32)d->Bounce.StartAddress;*(UInt32*)(p+4)=(UInt32)(d->Bounce.StartAddress>>32);*(UInt32*)(p+12)=(byteCount-1U)|(1U<<31);}W32(pr,0x10,0xFFFFFFFFU);W32(pr,0x38,1U);wait=SpinLimit;while(wait-->0){UInt32 ci=R32(pr,0x38);if((ci&1U)==0){UInt32 isr=R32(pr,0x10);return (isr&(1U<<30))==0;}}return false;}
}
