using System;
using Inu.Kernel.Drivers;

namespace Inu.Kernel.Graphics;

/// <summary>Registers bootloader, VESA-like, or platform-provided linear framebuffers independently of UEFI GOP.</summary>
public static unsafe class SimpleFramebuffer
{
    /// <summary>Registers a CPU-visible linear framebuffer whose physical and virtual addresses are already known.</summary>
    public static Boolean Register(UInt64 physicalAddress,UInt64 virtualAddress,UInt64 size,UInt32 width,UInt32 height,UInt32 pixelsPerScanLine,KernelGraphicsPixelFormat pixelFormat,Boolean makePrimary,out KernelGraphicsDisplayHandle handle)
    {
        handle=default;
        KernelGraphicsMode mode=new(width,height,pixelsPerScanLine,pixelFormat);
        KernelGraphicsFramebuffer framebuffer=new(physicalAddress,virtualAddress,size,mode);
        KernelGraphicsCallbacks callbacks=new(&Present,null);
        return KernelGraphics.RegisterDisplay(default,KernelGraphicsTargetKind.SimpleFramebuffer,framebuffer,callbacks,false,makePrimary,out handle);
    }

    /// <summary>Registers an identity/directly mapped linear framebuffer where the CPU-visible address equals the supplied physical address.</summary>
    public static Boolean Register(UInt64 address,UInt64 size,UInt32 width,UInt32 height,UInt32 pixelsPerScanLine,KernelGraphicsPixelFormat pixelFormat,Boolean makePrimary,out KernelGraphicsDisplayHandle handle)
        =>Register(address,address,size,width,height,pixelsPerScanLine,pixelFormat,makePrimary,out handle);

    private static Boolean Present(KernelGraphicsDisplayHandle display,UInt32 x,UInt32 y,UInt32 width,UInt32 height)
        =>display.Value!=0U&&width!=0U&&height!=0U;
}
