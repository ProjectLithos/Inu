using System;
using Inu.Kernel.Bootstrap.HAL;
using Inu.Kernel.Console;

namespace Inu.Kernel.Bootstrap.Startup
{
    /// <summary>Starts the OS shell as an ordinary ring-3 executable.</summary>
    public static class TextConsoleSessionStartup
    {
        public static Boolean Run()
        {
            if (!KernelConsole.WriteHostControl("TEXT_SESSION_ENTER")) return false;
            if (!KernelConsole.WriteHostControl("TEXT_SESSION_INPUT_ENTER")) return false;
            if (!InputHardwareStartup.EnsureTextInputProviders())
            {
                KernelConsole.WriteHostControl("TEXT_SESSION_INPUT_STARTUP_FAIL");
                return false;
            }
            if (!KernelConsole.WriteHostControl("TEXT_SESSION_INPUT_OK")) return false;
            if (!KernelConsole.WriteHostControl("TEXT_SESSION_SHELL_ENTER")) return false;
            Boolean shellResult = UserlandRuntimeStartup.RunShellSession();
            KernelConsole.WriteHostControl(shellResult ? "TEXT_SESSION_SHELL_RETURNED_TRUE" : "TEXT_SESSION_SHELL_RETURNED_FALSE");
            return shellResult;
        }
    }
}

namespace Inu.Kernel.Processes
{
    /// <summary><inu.api>Coder-facing text-session policy. Runs the configured shell as an ordinary isolated ring-3 process.</inu.api></summary>
    public static class TextSession
    {
        public static Boolean Run()=>global::Inu.Kernel.Bootstrap.Startup.TextConsoleSessionStartup.Run();
    }

    /// <summary>Compatibility facade for coder-owned Kernel.cs files generated before 0.0.73.</summary>
    public static class TextConsoleSessionStartup
    {
        public static Boolean Run()=>TextSession.Run();
    }
}
