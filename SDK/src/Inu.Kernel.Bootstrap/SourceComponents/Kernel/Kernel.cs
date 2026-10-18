using System;
using Inu.Kernel.Console;
using Inu.Kernel.Contracts;
using Inu.Kernel.Bootstrap.Boot;
using Inu.Kernel.Bootstrap.HAL;
using Inu.Kernel.Bootstrap.Startup;

namespace Inu.Kernel.Bootstrap;

/// <summary>
/// OS-owned kernel entry composition. Each call below is a separately selectable source component.
/// Delete, replace, reorder or implement a stage differently when the OS architecture requires it.
/// </summary>
public static class Kernel
{
    public static Boolean KMain<TBoot>(TBoot boot)
        where TBoot : IBootFramebufferContext, IFinalMemoryMapBufferContext, IMemoryDescriptorLayoutContext, IBootstrapPageTableWorkspaceContext, IAcpiRootPointerContext, IApplicationProcessorTrampolineContext, ISystemAssetBundleContext, IKernelImageContext
    {
        // Boot stages: the selected capabilities determine the generic constraints above.
        if (!BootDiagnosticsStartup.Initialize(boot)) return false;
        if (!PlatformTablesStartup.Initialize()) return false;
        if (!AcpiStartup.Initialize(boot)) return false;
        if (!TimeStartup.Initialize()) return false;
        if (!MemoryRuntimeStartup.Initialize(boot)) return false;
        if (!GraphicsStartup.Initialize(boot)) return false;
        if (!SmpStartup.Initialize(boot)) return false;
        if (!SchedulerRuntimeStartup.Initialize()) return false;
        if (!ProtectionStartup.Initialize()) return false;

        // Runtime facilities are explicit choices; they are no longer hidden inside the HAL.
        if (!ProcessRuntimeStartup.Initialize()) return false;
        if (!TimerDispatchStartup.Initialize()) return false;

        // Hardware capability startup is explicit and replaceable. Remove any stage the OS does not select.
        if (!BootstrapGraphicsTransportStartup.Initialize()) return false;
        if (!InputHardwareStartup.Initialize()) return false;
        if (!DriverInfrastructureStartup.Initialize()) return false;
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_GRAPHICS_HW")) return false;
        if (!GraphicsHardwareStartup.Initialize()) return false;
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_STORAGE")) return false;
        if (!StorageHardwareStartup.Initialize()) return false;
#if INU_KERNELAREA_NETWORKING
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_NETWORK")) return false;
        if (!NetworkingHardwareStartup.Initialize()) return false;
#endif
#if INU_KERNELAREA_USB
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_USB")) return false;
        if (!UsbHardwareStartup.Initialize()) return false;
#endif
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_BINDING")) return false;
        if (!DriverBindingStartup.Initialize()) return false;
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_FILESYSTEM")) return false;
        if (!FilesystemHardwareStartup.Initialize()) return false;

        // Userland/session policy is also selected source, not a mandatory kernel behaviour.
        // Do not arm PS/2 hardware IRQ delivery until all synchronous hardware/service startup
        // has completed. Earlier arming allows device callbacks to re-enter the still-running
        // boot path and can stall CLI/Test kernels before the shell can publish INTERACTIVE_READY.
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_USERLAND")) return false;
        if (!UserlandCommandStartup.Initialize(boot)) return false;
#if INU_KERNELAREA_USB
        // Prepare the xHCI interrupt callbacks before STI.  The device interrupter remains
        // disabled until after global maskable interrupts are live, but any already-routed
        // interrupt delivered at the STI boundary can now be accepted safely.
        if (!KernelConsole.WriteHostControl("USB_IRQ_PREPARE_ENTER")) return false;
        if (!UsbHardwareStartup.PrepareHardwareInterrupts())
        {
            KernelConsole.WriteHostControl("USB_IRQ_PREPARE_FAIL");
            return false;
        }
        if (!KernelConsole.WriteHostControl("USB_IRQ_PREPARE_OK")) return false;
#endif
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_INTERRUPTS")) return false;
        if (!KernelConsole.WriteHostControl("INTERRUPTS_ENABLE_ENTER")) return false;
        if (!InterruptRuntimeStartup.Enable())
        {
            KernelConsole.WriteHostControl("INTERRUPTS_ENABLE_FAIL");
            return false;
        }
        if (!KernelConsole.WriteHostControl("INTERRUPTS_ENABLE_OK")) return false;
#if INU_KERNELAREA_USB
        if (!KernelConsole.WriteHostControl("USB_IRQ_ACTIVATE_ENTER")) return false;
        if (!UsbHardwareStartup.EnableHardwareInterrupts())
        {
            KernelConsole.WriteHostControl("USB_IRQ_ACTIVATE_FAIL");
            return false;
        }
        if (!KernelConsole.WriteHostControl("USB_IRQ_ACTIVATE_OK")) return false;
#endif
        if (!KernelConsole.WriteHostControl("INPUT_IRQ_ENABLE_ENTER")) return false;
        if (!InputHardwareStartup.EnableHardwareInterrupts())
        {
            KernelConsole.WriteHostControl("INPUT_IRQ_ENABLE_FAIL");
            return false;
        }
        if (!KernelConsole.WriteHostControl("INPUT_IRQ_ENABLE_OK")) return false;
        if (!KernelConsole.WriteHostControl("TIMER_IRQ_ENABLE_ENTER")) return false;
        if (!TimerDispatchStartup.EnableHardwareInterrupts())
        {
            KernelConsole.WriteHostControl("TIMER_IRQ_ENABLE_FAIL");
            return false;
        }
        if (!KernelConsole.WriteHostControl("TIMER_IRQ_ENABLE_OK")) return false;
        if (!KernelConsole.WriteHostControl("BOOT_STAGE_SESSION")) return false;
        if (!KernelConsole.WriteHostControl("SESSION_DISPATCH_ENTER")) return false;

        // Do not route a CLI-only OS through the graphical policy wrapper.  Dispatch
        // directly to the selected session so failures before shell entry are visible
        // and so a graphical component can never influence a text-only boot.
        if (Inu.Kernel.Bootstrap.GeneratedConfiguration.Gui == "none")
        {
            if (!KernelConsole.WriteHostControl("SESSION_TEXT_SELECTED")) return false;
            Boolean textResult = TextConsoleSessionStartup.Run();
            KernelConsole.WriteHostControl(textResult ? "SESSION_TEXT_RETURNED_TRUE" : "SESSION_TEXT_RETURNED_FALSE");
            return textResult;
        }

        if (!KernelConsole.WriteHostControl("SESSION_GRAPHICAL_SELECTED")) return false;
        Boolean desktopResult = DesktopOrTextSessionStartup.Run();
        KernelConsole.WriteHostControl(desktopResult ? "SESSION_GRAPHICAL_RETURNED_TRUE" : "SESSION_GRAPHICAL_RETURNED_FALSE");
        return desktopResult;
    }
}
