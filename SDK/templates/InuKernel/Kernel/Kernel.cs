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
        if (!InputHardwareStartup.EnableHardwareInterrupts()) return false;
        if (!GraphicsHardwareStartup.Initialize()) return false;
        if (!StorageHardwareStartup.Initialize()) return false;
        if (!NetworkingHardwareStartup.Initialize()) return false;
        if (!UsbHardwareStartup.Initialize()) return false;
        if (!DriverBindingStartup.Initialize()) return false;

        // Userland/session policy is also selected source, not a mandatory kernel behaviour.
        if (!UserlandCommandStartup.Initialize(boot)) return false;
        if (!InterruptRuntimeStartup.Enable()) return false;

        // Default Inu policy: prefer the graphical session, then fall back to text.
        // An OS author may replace this with TextConsoleSessionStartup.Run(), a custom
        // session component, or no interactive session at all.
        return DesktopOrTextSessionStartup.Run();
    }
}
