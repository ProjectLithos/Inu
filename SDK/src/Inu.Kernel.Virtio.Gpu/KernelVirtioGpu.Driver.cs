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
    private static Boolean Probe(KernelDriverDeviceContext* context){if(context==null||context->Identifier.VendorId!=VirtioVendorId||!KernelPci.TryGetDevice(context->Device,out PciDeviceInfo pci)||!IsGpu(pci))return false;return TryFindTransportCapability(pci.Location,CommonConfigurationType,out _);}
    private static Boolean Start(KernelDriverDeviceContext* context)
    {
        if(context==null||!KernelPci.TryGetDevice(context->Device,out PciDeviceInfo pci)||!IsGpu(pci)||!EnablePci(pci.Location))return false;Int32 slot=Free();if(slot<0){if(!Grow())return false;slot=Free();if(slot<0)return false;}DeviceRecord* r=_devices+slot;Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));r->Used=1;r->DeviceHandle=context->Device.Value;r->Segment=pci.Location.Segment;r->Bus=pci.Location.Bus;r->PciDevice=pci.Location.Device;r->Function=pci.Location.Function;r->ResourceId=(UInt32)slot+1U;
        if(!InitializeTransport(r,pci.Location)||!SetupQueue(r,&r->Control,0,64)){ReleaseResources(r);Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));return false;}
        // DRIVER_OK must be visible before the control queue is used. Do not select a scanout yet:
        // firmware may still own the visible GOP surface and replacing it with our zero-filled
        // resource here would blank the QEMU window before the console has been redrawn.
        if(!SetStatus(r,(Byte)(Read8(r->Common+20)|4U))){ReleaseResources(r);Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));return false;}
        if(!GetPreferredMode(r,out UInt32 width,out UInt32 height,out UInt32 scanout)){SetStatus(r,0x80);ReleaseResources(r);Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));return false;}r->Scanout=scanout;
        if(!CreateScanout(r,width,height)){SetStatus(r,0x80);ReleaseResources(r);Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));return false;}
        r->Started=1;
        KernelGraphicsMode mode=new(r->Width,r->Height,r->Pitch,KernelGraphicsPixelFormat.BlueGreenRedReserved8);KernelGraphicsFramebuffer framebuffer=new(r->FramePhysical,r->FrameVirtual,r->FrameBytes,mode);KernelGraphicsCallbacks graphicsCallbacks=new(&GraphicsPresent,&GraphicsSetMode,&GraphicsGetModeCount,&GraphicsTryGetMode,&ActivateDisplay);if(!KernelGraphics.RegisterDisplay(context->Device,KernelGraphicsTargetKind.VirtioGpu,framebuffer,graphicsCallbacks,true,false,out KernelGraphicsDisplayHandle display)){SetStatus(r,0);ReleaseResources(r);Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));return false;}r->GraphicsDisplay=display.Value;_count++;_displayCount++;return true;
    }
    private static Boolean Stop(KernelDriverDeviceContext* context){if(context==null)return false;if(!TryRecord(context->Device,out DeviceRecord* r))return true;SetStatus(r,0);if(r->GraphicsDisplay!=0U&&!KernelGraphics.UnregisterDisplay(new KernelGraphicsDisplayHandle(r->GraphicsDisplay)))return false;r->GraphicsDisplay=0U;r->Started=0;r->ScanoutActive=0;if(!ReleaseResources(r))return false;Clear((Byte*)r,(UInt64)sizeof(DeviceRecord));if(_count!=0)_count--;if(_displayCount!=0)_displayCount--;return true;}
    private static Boolean Remove(KernelDriverDeviceContext* context)=>context!=null&&Stop(context);
    private static Boolean Interrupt(KernelDriverDeviceContext* context,UInt64 cookie)=>context!=null&&TryRecord(context->Device,out _);

    private static Boolean GraphicsPresent(KernelGraphicsDisplayHandle display,UInt32 x,UInt32 y,UInt32 width,UInt32 height){if(!TryDisplay(display,out DeviceRecord* r)||r->Started==0)return false;return PresentRecord(r,x,y,width,height);}
    private static Boolean GraphicsSetMode(KernelGraphicsDisplayHandle display,KernelGraphicsMode mode){if(!TryDisplay(display,out DeviceRecord* r)||r->Started==0||mode.PixelFormat!=KernelGraphicsPixelFormat.BlueGreenRedReserved8||mode.PixelsPerScanLine!=mode.Width)return false;return ChangeMode(r,mode.Width,mode.Height);}
    // VirtIO GPU resources are dynamically sized. Inu advertises the practical display
    // modes exposed to end users while retaining the driver's lower-level arbitrary-size API.
}
