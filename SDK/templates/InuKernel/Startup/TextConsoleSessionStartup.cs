using System;

namespace Inu.Kernel.Bootstrap.Startup
{
    /// <summary>Starts the OS shell as an ordinary ring-3 executable.</summary>
    public static class TextConsoleSessionStartup
    {
        public static Boolean Run()=>UserlandRuntimeStartup.RunShellSession();
    }
}

namespace Inu.Kernel.Processes
{
    /// <summary><inu.api>Coder-facing text-session policy. Runs the configured shell as an ordinary isolated ring-3 process.</inu.api></summary>
    public static class TextSession
    {
        public static Boolean Run()=>global::Inu.Kernel.Bootstrap.Startup.UserlandRuntimeStartup.RunShellSession();
    }

    /// <summary>Compatibility facade for coder-owned Kernel.cs files generated before 0.0.73.</summary>
    public static class TextConsoleSessionStartup
    {
        public static Boolean Run()=>TextSession.Run();
    }
}
