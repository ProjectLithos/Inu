using System;

namespace Inu.Kernel.Bootstrap.Startup;

/// <summary>Starts the OS shell as an ordinary ring-3 executable.</summary>
public static class TextConsoleSessionStartup
{
    public static Boolean Run()=>UserlandRuntimeStartup.RunShellSession();
}
