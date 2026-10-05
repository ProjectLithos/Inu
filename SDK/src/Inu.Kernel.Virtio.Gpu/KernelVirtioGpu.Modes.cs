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
    private static UInt32 GraphicsGetModeCount(KernelGraphicsDisplayHandle display)
    {if(!TryDisplay(display,out DeviceRecord* r)||r->Started==0)return 0U;for(UInt32 i=0U;i<16U;i++){if(TryGetStandardMode(i,out UInt32 w,out UInt32 h)&&w==r->Width&&h==r->Height)return 16U;}return 17U;}
    private static Boolean GraphicsTryGetMode(KernelGraphicsDisplayHandle display,UInt32 index,KernelGraphicsMode* mode)
    {if(mode==null||!TryDisplay(display,out DeviceRecord* r)||r->Started==0)return false;if(index<16U){if(!TryGetStandardMode(index,out UInt32 w,out UInt32 h))return false;*mode=new KernelGraphicsMode(w,h,w,KernelGraphicsPixelFormat.BlueGreenRedReserved8);return true;}if(index==16U&&GraphicsGetModeCount(display)==17U){*mode=new KernelGraphicsMode(r->Width,r->Height,r->Width,KernelGraphicsPixelFormat.BlueGreenRedReserved8);return true;}return false;}
    private static Boolean TryGetStandardMode(UInt32 index,out UInt32 width,out UInt32 height)
    {width=0U;height=0U;switch(index){case 0:width=640;height=480;break;case 1:width=800;height=600;break;case 2:width=1024;height=768;break;case 3:width=1280;height=720;break;case 4:width=1280;height=800;break;case 5:width=1280;height=1024;break;case 6:width=1366;height=768;break;case 7:width=1440;height=900;break;case 8:width=1600;height=900;break;case 9:width=1680;height=1050;break;case 10:width=1920;height=1080;break;case 11:width=1920;height=1200;break;case 12:width=2560;height=1440;break;case 13:width=2560;height=1600;break;case 14:width=3440;height=1440;break;case 15:width=3840;height=2160;break;default:return false;}return width<=MaximumDimension&&height<=MaximumDimension;}

    private static Boolean ChangeMode(DeviceRecord* r,UInt32 width,UInt32 height)
    {
        if(width==0U||height==0U||width>MaximumDimension||height>MaximumDimension)return false;if(width>UInt64.MaxValue/4UL/height)return false;
        UInt64 oldToken=r->FrameToken,oldPages=r->FramePages,oldPhysical=r->FramePhysical;UInt32 oldWidth=r->Width,oldHeight=r->Height,oldPitch=r->Pitch,oldResource=r->ResourceId;UInt64 oldVirtual=r->FrameVirtual,oldBytes=r->FrameBytes;Byte oldScanoutActive=r->ScanoutActive;
        UInt32 nextResource=oldResource+0x10000U;if(nextResource==0U)nextResource=oldResource+1U;r->ResourceId=nextResource;r->FrameToken=0;r->FramePages=0;r->FramePhysical=0;r->FrameVirtual=0;r->FrameBytes=0;r->ScanoutActive=0;
        if(!CreateScanout(r,width,height)){r->ResourceId=oldResource;r->FrameToken=oldToken;r->FramePages=oldPages;r->FramePhysical=oldPhysical;r->FrameVirtual=oldVirtual;r->FrameBytes=oldBytes;r->Width=oldWidth;r->Height=oldHeight;r->Pitch=oldPitch;r->ScanoutActive=oldScanoutActive;return false;}
        // A mode change creates a new VirtIO resource. The old resource may have been the active
        // scanout, but that active bit cannot be inherited by the replacement resource. Leave the
        // new resource explicitly inactive so the console can redraw it and ActivateDisplay can
        // issue SET_SCANOUT for the new resource before the old backing is forgotten by QEMU.
        r->ScanoutActive=0;
        Boolean cleanup=UnrefResourceId(r,oldResource);if(oldToken!=0)cleanup=ReleaseDma(oldToken,oldPhysical,oldPages)&cleanup;KernelGraphicsMode mode=new(r->Width,r->Height,r->Pitch,KernelGraphicsPixelFormat.BlueGreenRedReserved8);KernelGraphicsFramebuffer fb=new(r->FramePhysical,r->FrameVirtual,r->FrameBytes,mode);return KernelGraphics.UpdateFramebuffer(new KernelGraphicsDisplayHandle(r->GraphicsDisplay),fb)&&cleanup;
    }

    private static Boolean CreateScanout(DeviceRecord* r,UInt32 width,UInt32 height)
    {
        UInt64 bytes=(UInt64)width*height*4UL;if(!AllocateDma(bytes,out r->FrameToken,out r->FramePages,out r->FramePhysical,out r->FrameVirtual))return false;r->FrameBytes=bytes;r->Width=width;r->Height=height;r->Pitch=width;Clear((Byte*)(nuint)r->FrameVirtual,r->FramePages*4096UL);
        // Create and attach the resource without selecting it. Bootstrap/HAL first redraws the
        // retained console into this backing store and then calls ActivateDisplay().
        if(!Create2D(r)||!AttachBacking(r)){ReleaseDma(r->FrameToken,r->FramePhysical,r->FramePages);r->FrameToken=0;return false;}return true;
    }
    private static Boolean PresentRecord(DeviceRecord* r,UInt32 x,UInt32 y,UInt32 width,UInt32 height){if(width==0U||height==0U||x>=r->Width||y>=r->Height||width>r->Width-x||height>r->Height-y)return false;return TransferToHost(r,x,y,width,height)&&Flush(r,x,y,width,height);}

    private static Boolean GetPreferredMode(DeviceRecord* r,out UInt32 width,out UInt32 height,out UInt32 scanout)
    {
        width=0;height=0;scanout=0;Byte* request=stackalloc Byte[24];Byte* response=stackalloc Byte[408];Clear(request,24);Clear(response,408);Write32((UInt64)(nuint)request,CommandGetDisplayInfo);UInt32 responseType;if(!Command(r,request,24,response,408,out responseType)||responseType!=ResponseOkDisplayInfo)return false;
        for(UInt32 i=0;i<16U;i++){UInt64 p=(UInt64)(nuint)response+24UL+(UInt64)i*24UL;UInt32 w=Read32(p+8),h=Read32(p+12),enabled=Read32(p+16);if(enabled==0U||w==0U||h==0U)continue;width=w;height=h;scanout=i;return true;}return false;
    }
}
