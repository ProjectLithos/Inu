using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Graphics;
using Inu.Kernel.Heap;
using Inu.Kernel.Memory;
using Inu.Kernel.Pci;
using Inu.Kernel.Time;

namespace Inu.Kernel.Virtio.Gpu;

public static unsafe partial class KernelVirtioGpu
{
    private static Boolean Create2D(DeviceRecord* r){Byte* request=stackalloc Byte[40];Byte* response=stackalloc Byte[24];Clear(request,40);Clear(response,24);Write32((UInt64)(nuint)request,CommandResourceCreate2D);Write32((UInt64)(nuint)request+24,r->ResourceId);Write32((UInt64)(nuint)request+28,FormatB8G8R8X8Unorm);Write32((UInt64)(nuint)request+32,r->Width);Write32((UInt64)(nuint)request+36,r->Height);return CommandOk(r,request,40,response,24);}
    private static Boolean AttachBacking(DeviceRecord* r){Byte* request=stackalloc Byte[48];Byte* response=stackalloc Byte[24];Clear(request,48);Clear(response,24);Write32((UInt64)(nuint)request,CommandResourceAttachBacking);Write32((UInt64)(nuint)request+24,r->ResourceId);Write32((UInt64)(nuint)request+28,1U);Write64((UInt64)(nuint)request+32,r->FramePhysical);Write32((UInt64)(nuint)request+40,(UInt32)r->FrameBytes);return CommandOk(r,request,48,response,24);}
    private static Boolean SetScanout(DeviceRecord* r){Byte* request=stackalloc Byte[48];Byte* response=stackalloc Byte[24];Clear(request,48);Clear(response,24);Write32((UInt64)(nuint)request,CommandSetScanout);Write32((UInt64)(nuint)request+32,r->Width);Write32((UInt64)(nuint)request+36,r->Height);Write32((UInt64)(nuint)request+40,r->Scanout);Write32((UInt64)(nuint)request+44,r->ResourceId);return CommandOk(r,request,48,response,24);}
    private static Boolean TransferToHost(DeviceRecord* r,UInt32 x,UInt32 y,UInt32 width,UInt32 height){Byte* request=stackalloc Byte[56];Byte* response=stackalloc Byte[24];Clear(request,56);Clear(response,24);Write32((UInt64)(nuint)request,CommandTransferToHost2D);Write32((UInt64)(nuint)request+24,x);Write32((UInt64)(nuint)request+28,y);Write32((UInt64)(nuint)request+32,width);Write32((UInt64)(nuint)request+36,height);Write64((UInt64)(nuint)request+40,((UInt64)y*r->Pitch+x)*4UL);Write32((UInt64)(nuint)request+48,r->ResourceId);return CommandOk(r,request,56,response,24);}
    private static Boolean Flush(DeviceRecord* r,UInt32 x,UInt32 y,UInt32 width,UInt32 height){Byte* request=stackalloc Byte[48];Byte* response=stackalloc Byte[24];Clear(request,48);Clear(response,24);Write32((UInt64)(nuint)request,CommandResourceFlush);Write32((UInt64)(nuint)request+24,x);Write32((UInt64)(nuint)request+28,y);Write32((UInt64)(nuint)request+32,width);Write32((UInt64)(nuint)request+36,height);Write32((UInt64)(nuint)request+40,r->ResourceId);return CommandOk(r,request,48,response,24);}
    private static Boolean UnrefResourceId(DeviceRecord* r,UInt32 resourceId){Byte* request=stackalloc Byte[32];Byte* response=stackalloc Byte[24];Clear(request,32);Clear(response,24);Write32((UInt64)(nuint)request,CommandResourceUnref);Write32((UInt64)(nuint)request+24,resourceId);return CommandOk(r,request,32,response,24);}
    private static Boolean CommandOk(DeviceRecord* r,Byte* request,UInt32 requestBytes,Byte* response,UInt32 responseBytes){UInt32 type;return Command(r,request,requestBytes,response,responseBytes,out type)&&type==ResponseOkNoData;}
    private static Boolean Command(DeviceRecord* r,Byte* request,UInt32 requestBytes,Byte* response,UInt32 responseBytes,out UInt32 responseType)
    {
        responseType=0;UInt64 total=(UInt64)requestBytes+responseBytes;if(!AllocateDma(total,out UInt64 token,out UInt64 pages,out UInt64 physical,out UInt64 virtualAddress))return false;Byte* dma=(Byte*)(nuint)virtualAddress;Copy(request,dma,requestBytes);Clear(dma+requestBytes,responseBytes);SetDescriptor(&r->Control,0,physical,requestBytes,DescriptorNext,1);SetDescriptor(&r->Control,1,physical+requestBytes,responseBytes,DescriptorWrite,0);Boolean ok=SubmitAndWait(r,&r->Control,0,out UInt32 used)&&used<=responseBytes;if(ok){Copy(dma+requestBytes,response,responseBytes);responseType=Read32((UInt64)(nuint)response);}ReleaseDma(token,physical,pages);return ok;
    }

}
