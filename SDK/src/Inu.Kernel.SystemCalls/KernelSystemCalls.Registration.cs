using System;
using System.Runtime;
using Inu.Kernel.Heap;
using Inu.Kernel.Contracts;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Protection;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Smp;
using Inu.Kernel.Security;
using Inu.Kernel.Time;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.SystemCalls;

public static unsafe partial class KernelSystemCalls
{
    public static Boolean RegisterGet(String message, delegate*<KernelSystemCallFrame*, Int64> handler) => RegisterNative(KernelSystemCallOperation.Get,message,handler);
    /// <summary>Registers a native Inu Set message.</summary>
    public static Boolean RegisterSet(String message, delegate*<KernelSystemCallFrame*, Int64> handler) => RegisterNative(KernelSystemCallOperation.Set,message,handler);
    /// <summary>Registers a native Inu Event message.</summary>
    public static Boolean RegisterEvent(String message, delegate*<KernelSystemCallFrame*, Int64> handler) => RegisterNative(KernelSystemCallOperation.Event,message,handler);
    /// <summary>Registers a native Inu Get message namespace prefix. The complete user Message still selects the semantic request.</summary>
    public static Boolean RegisterGetPrefix(String prefix, delegate*<KernelSystemCallFrame*, Int64> handler) => RegisterNativePrefix(KernelSystemCallOperation.Get,prefix,handler);
    /// <summary>Registers a native Inu Set message namespace prefix.</summary>
    public static Boolean RegisterSetPrefix(String prefix, delegate*<KernelSystemCallFrame*, Int64> handler) => RegisterNativePrefix(KernelSystemCallOperation.Set,prefix,handler);
    /// <summary>Registers a native Inu Event message namespace prefix.</summary>
    public static Boolean RegisterEventPrefix(String prefix, delegate*<KernelSystemCallFrame*, Int64> handler) => RegisterNativePrefix(KernelSystemCallOperation.Event,prefix,handler);
    /// <summary>Registers the process-runtime cancellation boundary consulted before every native Inu message handler.</summary>
    public static Boolean RegisterCSharpancellationHandler(delegate*<KernelSystemCallFrame*, Boolean> handler)
    {
        if(!_initialized||handler==null||_nativeCancellationHandler!=0UL)return false;
        _nativeCancellationHandler=(UInt64)(void*)handler;
        return true;
    }

    /// <summary>Registers a Linux-style numeric syscall handler in the bounded compatibility table.</summary>
    public static Boolean RegisterLinux(UInt32 syscallNumber, delegate*<KernelSystemCallFrame*, Int64> handler) => RegisterAbi(KernelSystemCallAbi.Linux, syscallNumber, handler);
    /// <summary>Registers an NT-style numeric service handler in the bounded compatibility table.</summary>
    public static Boolean RegisterNt(UInt32 serviceNumber, delegate*<KernelSystemCallFrame*, Int64> handler) => RegisterAbi(KernelSystemCallAbi.Nt, serviceNumber, handler);

    /// <summary>Copies bytes from a validated readable user range while honoring SMAP when enabled.</summary>
}
