using System;
using Inu.Kernel.Console;
using Inu.Kernel.Processes;

namespace Inu.Kernel.Bootstrap.Startup;

/// <summary>Initializes generic ring-3 process, console and file services for userland and exposes the installed command surface to kernel policy.</summary>
public static class UserlandCommandStartup
{
    public static Boolean Initialize<TBoot>(TBoot boot) where TBoot : ISystemAssetBundleContext
        => UserlandRuntimeStartup.Initialize(boot);

    /// <summary>Creates any command currently installed under the configured CommandsPath(s). No command-name list is embedded in the kernel.</summary>
    public static Boolean TryCreateCommand(String command,KernelProcessOwnership ownership,out KernelProcessInfo process)
        =>UserlandRuntimeStartup.TryCreateCommandProcess(command,ownership,out process);

    /// <summary>Runs any installed command as an ordinary isolated ring-3 process under kernel-selected ownership.</summary>
    public static Boolean TryRunCommand(String command,KernelProcessOwnership ownership,out KernelProcessInfo process)
        =>UserlandRuntimeStartup.TryRunCommand(command,ownership,out process);
}
