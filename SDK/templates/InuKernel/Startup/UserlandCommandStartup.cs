using System;
using Inu.Kernel.Console;

namespace Inu.Kernel.Bootstrap.Startup;

/// <summary>Initializes generic ring-3 process, console and file services for userland.</summary>
public static class UserlandCommandStartup
{
    public static Boolean Initialize<TBoot>(TBoot boot) where TBoot : ISystemAssetBundleContext
        => UserlandRuntimeStartup.Initialize(boot);
}
