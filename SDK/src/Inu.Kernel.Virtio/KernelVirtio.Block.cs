using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Memory;
using Inu.Kernel.Networking;
using Inu.Kernel.Pci;
using Inu.Kernel.Storage;
using Inu.Kernel.Time;

namespace Inu.Kernel.Virtio;

public static unsafe partial class KernelVirtio
{
    private static Boolean BlockRead(KernelDeviceHandle device,UInt64 firstBlock,UInt32 blockCount,Byte* buffer,UInt32 bufferBytes)=>TransferBlocks(device,firstBlock,blockCount,buffer,bufferBytes,false);
    private static Boolean BlockWrite(KernelDeviceHandle device,UInt64 firstBlock,UInt32 blockCount,Byte* buffer,UInt32 bufferBytes)=>TransferBlocks(device,firstBlock,blockCount,buffer,bufferBytes,true);
    private static Boolean BlockFlush(KernelDeviceHandle device)
    {
        if(!TryRecord(device,out DeviceRecord* r)||(VirtioDeviceType)r->Type!=VirtioDeviceType.Block||r->Started==0)return false;if((r->NegotiatedFeatures&BlockFeatureFlush)==0)return true;UInt64 token,pages,physical,virtualAddress;if(!AllocateDma(17,out token,out pages,out physical,out virtualAddress))return false;Byte* p=(Byte*)(nuint)virtualAddress;Clear(p,pages*4096UL);Write32((UInt64)(nuint)p,BlockRequestFlush);p[16]=0xFF;SetDescriptor(&r->Queue0,0,physical,16,DescriptorNext,1);SetDescriptor(&r->Queue0,1,physical+16,1,DescriptorWrite,0);Boolean ok=SubmitAndWait(r,&r->Queue0,0,out _)&&p[16]==0;ReleaseDma(token,physical,pages);return ok;
    }

    private static Boolean TransferBlocks(KernelDeviceHandle device,UInt64 firstBlock,UInt32 blockCount,Byte* buffer,UInt32 bufferBytes,Boolean write)
    {
        if(buffer==null||blockCount==0||!TryRecord(device,out DeviceRecord* r)||(VirtioDeviceType)r->Type!=VirtioDeviceType.Block||r->Started==0||firstBlock>=r->BlockCount||blockCount>r->BlockCount-firstBlock)return false;UInt64 dataBytes=(UInt64)blockCount*r->BlockSize;if(dataBytes>bufferBytes||dataBytes>UInt32.MaxValue)return false;UInt64 total=16UL+dataBytes+1UL;UInt64 token,pages,physical,virtualAddress;if(!AllocateDma(total,out token,out pages,out physical,out virtualAddress))return false;Byte* p=(Byte*)(nuint)virtualAddress;Clear(p,pages*4096UL);Write32((UInt64)(nuint)p,write?BlockRequestOut:BlockRequestIn);Write64((UInt64)(nuint)(p+8),firstBlock*((UInt64)r->BlockSize/512UL));if(write)Copy(buffer,p+16,dataBytes);p[16+dataBytes]=0xFF;
        SetDescriptor(&r->Queue0,0,physical,16,DescriptorNext,1);SetDescriptor(&r->Queue0,1,physical+16,(UInt32)dataBytes,(UInt16)(DescriptorNext|(write?0:DescriptorWrite)),2);SetDescriptor(&r->Queue0,2,physical+16+dataBytes,1,DescriptorWrite,0);Boolean ok=SubmitAndWait(r,&r->Queue0,0,out _)&&p[16+dataBytes]==0;if(ok&&!write)Copy(p+16,buffer,dataBytes);ReleaseDma(token,physical,pages);return ok;
    }

}
