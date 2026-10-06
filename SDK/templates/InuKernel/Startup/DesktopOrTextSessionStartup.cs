using System;
using Inu.Kernel.Console;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Processes;
using Inu.Kernel.Bootstrap.HAL;

namespace Inu.Kernel.Bootstrap.Startup
{
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
}

namespace Inu.Kernel.Processes
{
    /// <summary><inu.api>Coder-facing graphical-session policy. Runs the selected desktop/login session with text-console fallback.</inu.api></summary>
    public static class DesktopSession
    {
        public static Boolean Run()=>global::Inu.Kernel.Bootstrap.Startup.DesktopOrTextSessionStartup.Run();
    }

    /// <summary>Compatibility facade for coder-owned Kernel.cs files generated before 0.0.73.</summary>
    public static class DesktopOrTextSessionStartup
    {
        public static Boolean Run()=>DesktopSession.Run();
    }
}
