using System;
using Inu.Kernel.Console;
using Inu.Kernel.Contracts;
using Inu.Kernel.Platform.X64;
using Inu.Kernel.Memory;
using Inu.Kernel.VirtualMemory;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Heap;
using Inu.Runtime.NativeAot;
using Inu.Runtime.Conformance;
using Inu.Kernel.Acpi;
using Inu.Kernel.TimerDispatch;
using Inu.Kernel.InterruptDispatch;
using Inu.Kernel.InterruptBroker;
using Inu.Kernel.Time;
using Inu.Kernel.Smp;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Protection;
using Inu.Kernel.Security;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Ps2;
using Inu.Kernel.Processes;
using Inu.Kernel.Drivers;
using Inu.Kernel.Storage;
using Inu.Kernel.Networking;
using Inu.Kernel.Pci;
using Inu.Kernel.Nvme;
using Inu.Kernel.Ahci;
using Inu.Kernel.Virtio;
using Inu.Kernel.Virtio.Gpu;
using Inu.Kernel.Graphics;
using Inu.Kernel.Gui;
using Inu.Kernel.Audio;
using Inu.Kernel.E1000;
using Inu.Kernel.Rtl8168;
using Inu.Bus.Usb;
using Inu.Usb.Xhci;
using Inu.Usb.Hid;
using Inu.Usb.MassStorage;
using Inu.Usb.Hub;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Bootstrap;

public static unsafe partial class Kernel
{
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
        // The console redraw above populates the driver backing resource while the firmware GOP
        // remains visible. Only now may VirtIO-GPU take ownership of the physical scanout.
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
    { return _consoleGraphicsDisplay.Value!=0U&&KernelGraphics.Present(_consoleGraphicsDisplay,x,y,width,height); }

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
