using System;
using Inu.Kernel.Console;
using Inu.Kernel.Contracts;
using Inu.Runtime.NativeAot;
using Inu.Runtime.Conformance;
using Inu.Kernel.Bootstrap;

namespace Inu.Kernel.Bootstrap.Boot;

/// <summary>Runs the required managed runtime semantic/ABI gate after visible-console startup.</summary>
public static class ManagedRuntimeConformanceStartup
{
    private static Boolean _prepared;
    private static Boolean _completed;
    private static Boolean _running;
    private static UInt64 _kernelImageBase;

    public static Boolean Prepare(UInt64 kernelImageBase)
    {
        if (kernelImageBase == 0UL) return false;
        if (_completed) return true;
        _kernelImageBase = kernelImageBase;
        _prepared = true;
        return true;
    }

    public static Boolean Initialize()
    {
        if (_completed) return true;
        if (!_prepared || _kernelImageBase == 0UL || _running) return false;
        _running = true;

        KernelConsole.WriteLine("NOBT:CONF:BASE");
        if (!NativeAotExceptionRuntime.ConfigureImageBase(_kernelImageBase))
        {
            KernelConsole.WriteLine("NOBT:FAIL:EHBASE");
            _running = false;
            return false;
        }
        KernelConsole.WriteLine("NOBT:CONF:RUN");

        if (!ManagedRuntimeConformance.Run(out UInt32 runtimePassed, out UInt32 runtimeFailed, out UInt32 abiPassed, out UInt32 abiFailed))
        {
            if (!KernelStructuredLogging.Begin(KernelLogLevel.Critical,"runtime","ManagedRuntimeConformanceStartup.Initialize")) { _running=false; return false; }
            if (!KernelConsole.Write(".NET conformance passed/failed: ")) { _running=false; return false; }
            if (!KernelConsole.WriteUInt64(runtimePassed)) { _running=false; return false; }
            if (!KernelConsole.Write("/")) { _running=false; return false; }
            if (!KernelConsole.WriteUInt64(runtimeFailed)) { _running=false; return false; }
            if (!KernelConsole.WriteLine("")) { _running=false; return false; }
            if (!KernelStructuredLogging.Begin(KernelLogLevel.Critical,"runtime","ManagedRuntimeConformanceStartup.Initialize")) { _running=false; return false; }
            if (!KernelConsole.Write(".NET ABI baseline passed/failed: ")) { _running=false; return false; }
            if (!KernelConsole.WriteUInt64(abiPassed)) { _running=false; return false; }
            if (!KernelConsole.Write("/")) { _running=false; return false; }
            if (!KernelConsole.WriteUInt64(abiFailed)) { _running=false; return false; }
            if (!KernelConsole.WriteLine("")) { _running=false; return false; }
            _running=false;
            return false;
        }

        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"runtime","ManagedRuntimeConformanceStartup.Initialize")) { _running=false; return false; }
        if (!KernelConsole.Write(".NET conformance passed/failed: ")) { _running=false; return false; }
        if (!KernelConsole.WriteUInt64(runtimePassed)) { _running=false; return false; }
        if (!KernelConsole.Write("/")) { _running=false; return false; }
        if (!KernelConsole.WriteUInt64(runtimeFailed)) { _running=false; return false; }
        if (!KernelConsole.WriteLine("")) { _running=false; return false; }
        if (!KernelStructuredLogging.Begin(abiFailed == 0U ? KernelLogLevel.Info : KernelLogLevel.Warning,"runtime","ManagedRuntimeConformanceStartup.Initialize")) { _running=false; return false; }
        if (!KernelConsole.Write(".NET ABI baseline passed/failed: ")) { _running=false; return false; }
        if (!KernelConsole.WriteUInt64(abiPassed)) { _running=false; return false; }
        if (!KernelConsole.Write("/")) { _running=false; return false; }
        if (!KernelConsole.WriteUInt64(abiFailed)) { _running=false; return false; }
        if (!KernelConsole.WriteLine("")) { _running=false; return false; }

        _completed = true;
        _running = false;
        return true;
    }
}
