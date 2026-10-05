using System;
using Inu.Kernel.Architecture;
using Inu.Kernel.Internal.X64;

namespace Inu.Arch.X64;

/// <summary>
/// Canonical x64 hardware-abstraction boundary. Generic kernel code must consume
/// Inu.Kernel.Architecture or higher-level kernel services, never Native directly.
/// </summary>
public static unsafe class X64ArchitectureBoundary
{
    private static Boolean _installed;

    public static Boolean EnsureInstalled()
    {
        if (_installed) return true;
        KernelArchitectureCapabilities capabilities = new(true, true, true, true, true, true);
        if (!KernelArchitecture.Install(KernelArchitectureKind.X64, capabilities)) return false;
        _installed = true;
        return true;
    }

    public static Boolean InitializeDescriptors() => EnsureInstalled() && Native.InitializeBootstrapDescriptors();
    public static Boolean InitializeInterrupts() => EnsureInstalled() && Native.InitializeBootstrapInterrupts();
    public static Boolean DisableLegacyPic() => EnsureInstalled() && Native.DisableLegacyPic();
    public static Boolean Halt() => Native.Halt();
    public static Boolean SpinWaitHint() => Native.Pause();
    public static Boolean MemoryBarrier() => Native.MemoryBarrier();
    public static Boolean AtomicCompareExchange64(UInt64* location, UInt64 expected, UInt64 replacement, out UInt64 previous)
    { UInt64 observed=0UL; Boolean ok=Native.AtomicCompareExchange64(location,expected,replacement,&observed); previous=observed; return ok; }
    public static Boolean AtomicExchange64(UInt64* location, UInt64 value, out UInt64 previous)
    { UInt64 observed=0UL; Boolean ok=Native.AtomicExchange64(location,value,&observed); previous=observed; return ok; }
    public static Boolean AtomicFetchAdd64(UInt64* location, UInt64 delta, out UInt64 previous)
    { UInt64 observed=0UL; Boolean ok=Native.AtomicFetchAdd64(location,delta,&observed); previous=observed; return ok; }
    public static Boolean AtomicLoad64(UInt64* location, out UInt64 value)
    { UInt64 observed=0UL; Boolean ok=Native.AtomicLoad64(location,&observed); value=observed; return ok; }
    public static Boolean AtomicStore64(UInt64* location, UInt64 value) => Native.AtomicStore64(location,value);

    public static Boolean CapturePanicContext(out UInt64 instructionPointer, out UInt64 stackPointer, out UInt64 framePointer, out UInt64 flags, out UInt64 pageTableRoot)
    {
        EnsureInstalled();
        return Native.CapturePanicContext(out instructionPointer, out stackPointer, out framePointer, out flags, out pageTableRoot);
    }

    public static Boolean PanicDebuggerBreak() => Native.PanicDebuggerBreak();
}
