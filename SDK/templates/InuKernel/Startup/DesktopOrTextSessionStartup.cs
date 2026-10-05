using System;
using Inu.Kernel.Console;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Processes;
using Inu.Kernel.Bootstrap.HAL;

namespace Inu.Kernel.Bootstrap.Startup;

/// <summary>Default Inu session policy: try the selected graphical session and fall back to text.</summary>
public static class DesktopOrTextSessionStartup
{
    public static Boolean Run()
    {
        KernelConsole.WriteLine("NOKMAIN:SESSION");
        Boolean sessionReady = true;
        if (sessionReady && !InputHardwareStartup.EnsureGraphicalInputProviders())
        {
            KernelConsole.WriteLine("NOKMAIN:GUI-INPUT:FAIL");
            sessionReady = false;
        }

        Boolean consoleFramebufferReleased = !sessionReady || !KernelConsole.HasFramebuffer() || KernelConsole.SetFramebufferPresentationEnabled(false);
        KernelConsole.WriteLine("NOKMAIN:GUI-AUTOSTART");
        if (sessionReady && consoleFramebufferReleased && KernelProcesses.BeginGraphicalSessionAsync())
        {
            KernelConsole.WriteLine("NOKMAIN:GUI-AUTOSTART:QUEUED");
            while (KernelProcesses.IsGraphicalSessionStarting())
                if (!Native.WaitForInterrupt()) return false;

            if (KernelProcesses.IsGraphicalSessionActive())
            {
                KernelConsole.WriteLine("NOKMAIN:GUI-AUTOSTART:OK");
                for (;;) if (!Native.WaitForInterrupt()) return false;
            }
            KernelConsole.WriteLine("NOKMAIN:GUI-AUTOSTART:FAILED");
        }

        if (consoleFramebufferReleased && KernelConsole.HasFramebuffer() && !KernelConsole.SetFramebufferPresentationEnabled(true))
            return false;

        KernelConsole.WriteLine("NOKMAIN:GUI-AUTOSTART:FALLBACK");
        return UserlandRuntimeStartup.RunShellSession();
    }
}
