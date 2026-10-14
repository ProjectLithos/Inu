using System;
using Inu.Kernel.Console;
using Inu.Kernel.Contracts;
using Inu.Kernel.Platform.X64;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Memory;
using Inu.Kernel.VirtualMemory;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Heap;
using Inu.Runtime.NativeAot;
using Inu.Runtime.Conformance;
using Inu.Kernel.Acpi;
using Inu.Kernel.Power;
using Inu.Kernel.Time;
using Inu.Kernel.Smp;
using Inu.Kernel.Scheduler;
using Inu.Kernel.InterruptDispatch;
using Inu.Kernel.Protection;
using Inu.Kernel.Security;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Graphics;
using Inu.Kernel.Bootstrap;

namespace Inu.Kernel.Bootstrap.Boot;

/// <summary>Initializes graphics and, when selected, promotes the boot framebuffer into a kernel display.</summary>
public static unsafe class GraphicsStartup
{
    public static Boolean Initialize<TBoot>(TBoot boot)
        where TBoot : IBootFramebufferContext, ISystemAssetBundleContext
    {
        if (!KernelGraphics.Initialize()) return false;
        Boolean firmwareFramebufferAvailable = boot.GetFramebufferAddress() != 0UL && boot.GetFramebufferSize() != 0UL && boot.GetFramebufferWidth() != 0U && boot.GetFramebufferHeight() != 0U;
#if INU_COMPONENT_GRAPHICS_FIRMWARE_FRAMEBUFFER
        if (firmwareFramebufferAvailable)
        {
            if (!FirmwareFramebuffer.Register(boot.GetFramebufferAddress(), boot.GetFramebufferSize(), boot.GetFramebufferWidth(), boot.GetFramebufferHeight(), boot.GetFramebufferPitchInPixels(), boot.GetFramebufferPixelFormat(), out KernelGraphicsDisplayHandle firmwareDisplay)) return false;
            if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
            if (!KernelConsole.Write("Generic framebuffer registered: ")) return false;
            if (!KernelConsole.WriteUInt64(firmwareDisplay.Value)) return false;
            if (!KernelConsole.Write(" @ ")) return false;
            if (!KernelConsole.WriteUInt64(boot.GetFramebufferWidth())) return false;
            if (!KernelConsole.Write("x")) return false;
            if (!KernelConsole.WriteUInt64(boot.GetFramebufferHeight())) return false;
            if (!KernelConsole.WriteLine(" (UEFI GOP generic framebuffer target).")) return false;
            // Visible console readiness is a boot prerequisite, not a post-conformance reward.
            // Existing projects are refreshed to this SDK-owned stage, so the framebuffer/TrueType
            // console comes online before the managed runtime conformance gate without touching
            // coder-owned Kernel.cs.
            // Attach GOP to KernelConsole now, before querying the framebuffer byte count or
            // allocating software back buffers. Without this transition FrameByteCount remains 0.
            if (!KernelConsole.TryInitializeFramebuffer(boot))
            {
                KernelConsole.WriteLine("NOBT:FAIL:FRAMEBUFFER-CONSOLE");
                return false;
            }
            KernelConsole.WriteLine("NOBT:FRAMEBUFFER-CONSOLE");
            UInt64 framebufferBufferBytes = KernelConsole.GetFramebufferBufferByteCount();
            if (framebufferBufferBytes == 0UL) return false;
            if (!KernelHeap.TryAllocate(framebufferBufferBytes, 4096UL, true, out KernelHeapAllocation framebufferBackBufferA)) return false;
            if (!KernelHeap.TryAllocate(framebufferBufferBytes, 4096UL, true, out KernelHeapAllocation framebufferBackBufferB)) return false;
            if (!KernelConsole.ConfigureFramebufferBuffers(framebufferBackBufferA.Address, framebufferBackBufferB.Address, framebufferBufferBytes)) return false;
            // The TrueType face is loaded from the boot asset bundle. Open the
            // catalogue before font lookup; userland service registration happens later.
            if (!SystemAssetCatalog.Initialize(boot))
            {
                KernelConsole.WriteLine("NOBT:FAIL:ASSETS");
                KernelStructuredLogging.ErrorLine("console","GraphicsStartup.Initialize","The boot system asset bundle is missing or invalid; the TrueType console font cannot be loaded.");
                return false;
            }
            if (!Inu.Kernel.Console.Console.TryPromoteGraphics())
            {
                KernelConsole.WriteLine("NOBT:FAIL:TTF");
                KernelConsole.Write("TrueType activation state/font bytes: ");
                KernelConsole.WriteUInt64((UInt32)KernelConsole.GetTrueTypeConsoleState());
                KernelConsole.Write(" / ");
                KernelConsole.WriteUInt64(KernelConsole.GetTrueTypeFontLength());
                KernelConsole.WriteLine("");
                KernelStructuredLogging.ErrorLine("console","BootStartup.Initialize","Graphics console TrueType promotion failed; interactive graphics requires a valid TrueType face.");
                return false;
            }
            if (!KernelConsole.WriteLine("NOBT:TTF:OK")) return false;
            if (!KernelConsole.WriteHostControl("TTF_READY")) return false;
            FramebufferBufferCapabilities framebufferBuffers = KernelConsole.GetFramebufferBufferCapabilities();
            if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
            if (!KernelConsole.Write("Framebuffer buffers available/active: ")) return false;
            if (!KernelConsole.WriteUInt64(framebufferBuffers.AvailableBufferCount)) return false;
            if (!KernelConsole.Write(" / ")) return false;
            if (!KernelConsole.WriteUInt64((UInt32)framebufferBuffers.Mode)) return false;
            if (!KernelConsole.WriteLine(" (Auto policy selects double buffering for text; GOP scan-out is the front buffer).")) return false;
        }
        else
        {
            if (!KernelStructuredLogging.WarningLine("graphics","BootStartup.Initialize","UEFI GOP unavailable; boot continues on serial until a graphics driver publishes a display.")) return false;
        }
#endif
        // Run the managed semantic/ABI gate only after graphics had its chance to publish
        // TTF_READY. This call is idempotent and also serves as the compatibility migration
        // for kernels generated before 0.0.104 whose coder-owned Kernel.cs calls
        // MemoryRuntimeStartup followed by GraphicsStartup.
        if (!ManagedRuntimeConformanceStartup.Initialize()) return false;

        if (!KernelHeap.TryAllocate(256UL, 16UL, true, out KernelHeapAllocation heapSample)) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Kernel heap sample: ")) return false;
        if (!KernelConsole.WriteHex(heapSample.Address)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelHeap.TryRelease(heapSample)) return false;
        return true;
    }
}
