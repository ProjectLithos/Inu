using System;
using Inu.Kernel.Drivers;

namespace Inu.Kernel.Graphics;

/// <summary>Registers firmware-provided linear framebuffers as generic graphics targets.</summary>
public static unsafe class FirmwareFramebuffer
{
    /// <summary>Registers a GOP/VESA-like framebuffer. CPU writes are already scan-out visible, so presentation is a validated no-op.</summary>
    public static Boolean Register(UInt64 address,UInt64 size,UInt32 width,UInt32 height,UInt32 pixelsPerScanLine,UInt32 firmwarePixelFormat,out KernelGraphicsDisplayHandle handle)
    {handle=default;KernelGraphicsPixelFormat format=firmwarePixelFormat==0U?KernelGraphicsPixelFormat.RedGreenBlueReserved8:firmwarePixelFormat==1U?KernelGraphicsPixelFormat.BlueGreenRedReserved8:firmwarePixelFormat==2U?KernelGraphicsPixelFormat.DirectBitMask32:KernelGraphicsPixelFormat.Unknown;KernelGraphicsMode mode=new(width,height,pixelsPerScanLine,format);KernelGraphicsFramebuffer fb=new(address,address,size,mode);KernelGraphicsCallbacks callbacks=new(&Present,null);return KernelGraphics.RegisterDisplay(default,KernelGraphicsTargetKind.FirmwareFramebuffer,fb,callbacks,false,true,out handle);}
    private static Boolean Present(KernelGraphicsDisplayHandle display,UInt32 x,UInt32 y,UInt32 width,UInt32 height)=>display.Value!=0U&&width!=0U&&height!=0U;
}
