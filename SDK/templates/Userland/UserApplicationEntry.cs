using System;
using System.Runtime;
using Inu.Runtime.NativeAot;
using Inu.Userland.Runtime;

/// <summary>
/// Shared NativeAOT process entry used by every Inu ring-3 application.
/// Application-specific source supplies only InuUserApplicationBinding.Invoke().
/// This file is SDK runtime glue and is never copied into coder-owned command source.
/// </summary>
internal static unsafe class InuUserApplicationEntry
{
    [RuntimeExport("InuUserManagedEntry")]
    private static Int32 Entry(UInt64 imageBase, UInt64 readyToRunHeader)
    {
        if (!NativeAotRuntime.Initialize() || imageBase == 0UL || readyToRunHeader == 0UL)
            return -100;

        IntPtr* modules = stackalloc IntPtr[1];
        modules[0] = (IntPtr)(void*)(nuint)readyToRunHeader;
        global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.InitializeModules(
            (IntPtr)(void*)(nuint)imageBase, modules, 1, null, 0);

        if (!NativeAotExceptionRuntime.ConfigureImageBase(imageBase))
            return -101;

        Int32 code = InuUserApplicationBinding.Invoke();
        UserlandProcess.Exit(code);
        return code;
    }
}
