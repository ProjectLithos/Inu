using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Console;
using Inu.Kernel.Graphics;
using Inu.Kernel.Heap;
using Inu.Kernel.Virtio.Gpu;

namespace Inu.Kernel.Bootstrap.HAL;

/// <summary>Initializes and, when requested, promotes the selected graphics device provider.</summary>
public static unsafe class GraphicsHardwareStartup
{
    private static KernelGraphicsDisplayHandle _consoleGraphicsDisplay;

    public static Boolean Initialize()
    {
#if INU_KERNELAREA_DRIVERS
        if (!KernelVirtioGpu.Initialize()) return false;
        if (KernelVirtioGpu.GetCapabilities().Displays != 0U)
        {
            if (TryUsePreferredGraphicsDisplay())
            {
                FramebufferBufferCapabilities preferredBuffers = KernelConsole.GetFramebufferBufferCapabilities();
                if (preferredBuffers.AvailableBufferCount < 3U)
                {
                    UInt64 preferredBufferBytes = KernelConsole.GetFramebufferBufferByteCount();
                    if (preferredBufferBytes != 0UL && KernelHeap.TryAllocate(preferredBufferBytes, 4096UL, true, out KernelHeapAllocation preferredBackBufferA))
                    {
                        if (KernelHeap.TryAllocate(preferredBufferBytes, 4096UL, true, out KernelHeapAllocation preferredBackBufferB))
                        {
                            if (!KernelConsole.ConfigureFramebufferBuffers(preferredBackBufferA.Address, preferredBackBufferB.Address, preferredBufferBytes))
                            {
                                KernelHeap.TryRelease(preferredBackBufferA);
                                KernelHeap.TryRelease(preferredBackBufferB);
                            }
                        }
                        else KernelHeap.TryRelease(preferredBackBufferA);
                    }
                }
                if (!KernelStructuredLogging.InfoLine("graphics","GraphicsHardwareStartup.Initialize","Selected graphics device promoted; bootstrap framebuffer remains available as fallback metadata.")) return false;
            }
            else if (!KernelStructuredLogging.WarningLine("graphics","GraphicsHardwareStartup.Initialize","Selected graphics device could not be promoted; retaining the bootstrap framebuffer.")) return false;
        }
        VirtioGpuCapabilities virtioGpu = KernelVirtioGpu.GetCapabilities();
        KernelGraphicsCapabilities graphics = KernelGraphics.GetCapabilities();
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"hal-detail","GraphicsHardwareStartup.Initialize")) return false;
        if (!KernelConsole.Write("VirtIO GPU controllers/displays: ")) return false;
        if (!KernelConsole.WriteUInt64(virtioGpu.Controllers)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(virtioGpu.Displays)) return false;
        if (!KernelConsole.Write("; graphics displays total: ")) return false;
        if (!KernelConsole.WriteUInt64(graphics.Displays)) return false;
        if (!KernelConsole.WriteLine("")) return false;
#endif
        return true;
    }

    private static Boolean TryUsePreferredGraphicsDisplay()
    {
        if(!KernelConsole.IsInitialized()||!KernelGraphics.IsInitialized())return false;
        KernelGraphicsCapabilities capabilities=KernelGraphics.GetCapabilities();
        if(capabilities.Displays==0U)return false;
        Boolean havePrevious=KernelGraphics.TryGetPrimaryDisplay(out KernelGraphicsDisplayInfo previous);
        KernelGraphicsDisplayInfo selected=previous;Boolean found=havePrevious;
        for(UInt32 i=0U;i<capabilities.Displays;i++)
        {
            if(!KernelGraphics.TryGetDisplay(i,out KernelGraphicsDisplayInfo candidate))continue;
            if(!found){selected=candidate;found=true;}
            if(candidate.Kind==KernelGraphicsTargetKind.VirtioGpu){selected=candidate;found=true;break;}
        }
        if(!found||!TryGetConsolePixelFormat(selected.Framebuffer.Mode.PixelFormat,out UInt32 consolePixelFormat))return false;
        if(!KernelGraphics.Present(selected.Handle,0U,0U,1U,1U))return false;
        KernelGraphicsDisplayHandle oldConsoleDisplay=_consoleGraphicsDisplay;
        _consoleGraphicsDisplay=selected.Handle;
        KernelGraphicsFramebuffer framebuffer=selected.Framebuffer;
        if(!KernelConsole.ReconfigureFramebuffer(framebuffer.VirtualAddress,framebuffer.ByteLength,framebuffer.Mode.Width,framebuffer.Mode.Height,framebuffer.Mode.PixelsPerScanLine,consolePixelFormat,&PresentConsoleGraphics))
        {
            _consoleGraphicsDisplay=oldConsoleDisplay;
            RestorePreviousGraphicsDisplay(havePrevious,previous);
            return false;
        }
        if(selected.Kind==KernelGraphicsTargetKind.VirtioGpu&&!KernelVirtioGpu.ActivateDisplay(selected.Handle))
        {
            _consoleGraphicsDisplay=oldConsoleDisplay;
            RestorePreviousGraphicsDisplay(havePrevious,previous);
            return false;
        }
        if(KernelGraphics.SetPrimaryDisplay(selected.Handle))return true;
        _consoleGraphicsDisplay=oldConsoleDisplay;
        RestorePreviousGraphicsDisplay(havePrevious,previous);
        return false;
    }

    private static Boolean PresentConsoleGraphics(UInt32 x,UInt32 y,UInt32 width,UInt32 height)
        => _consoleGraphicsDisplay.Value!=0U&&KernelGraphics.Present(_consoleGraphicsDisplay,x,y,width,height);

    private static Boolean TryGetConsolePixelFormat(KernelGraphicsPixelFormat pixelFormat,out UInt32 consolePixelFormat)
    {
        consolePixelFormat=0U;
        if(pixelFormat==KernelGraphicsPixelFormat.RedGreenBlueReserved8)return true;
        if(pixelFormat==KernelGraphicsPixelFormat.BlueGreenRedReserved8){consolePixelFormat=1U;return true;}
        return false;
    }

    private static void RestorePreviousGraphicsDisplay(Boolean havePrevious,KernelGraphicsDisplayInfo previous)
    {
        if(!havePrevious)return;
        KernelGraphics.SetPrimaryDisplay(previous.Handle);
        _consoleGraphicsDisplay=previous.Handle;
        if(!TryGetConsolePixelFormat(previous.Framebuffer.Mode.PixelFormat,out UInt32 pixelFormat))return;
        KernelGraphicsFramebuffer framebuffer=previous.Framebuffer;
        KernelConsole.ReconfigureFramebuffer(framebuffer.VirtualAddress,framebuffer.ByteLength,framebuffer.Mode.Width,framebuffer.Mode.Height,framebuffer.Mode.PixelsPerScanLine,pixelFormat,&PresentConsoleGraphics);
    }
}
