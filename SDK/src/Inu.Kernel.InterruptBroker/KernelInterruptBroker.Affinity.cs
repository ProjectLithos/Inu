using System;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Smp;

namespace Inu.Kernel.InterruptBroker;

/// <summary>Optional SMP-aware interrupt-affinity provider.</summary>
public static unsafe class KernelInterruptAffinityResolver
{
    private static Boolean _initialized;

    public static Boolean Initialize()
    {
        if (_initialized) return true;
        if (!KernelInterruptAffinityServices.Register(&ResolveInterruptTarget, &ResolveApicId)) return false;
        _initialized = true;
        return true;
    }

    private static UInt32 ResolveInterruptTarget(UInt32 requested)
    {
        if (KernelSmp.IsInitialized() && KernelSmp.ProcessorHasRole(requested, KernelCpuRole.Interrupts)) return requested;
        if (KernelSmp.TryGetFirstOnlineProcessorForRole(KernelCpuRole.Interrupts, out UInt32 allowed)) return allowed;
        return requested;
    }

    private static UInt32 ResolveApicId(UInt32 processor)
    {
        if (KernelSmp.IsInitialized() && KernelSmp.TryGetProcessor(processor, out KernelProcessorState state)) return state.ApicId;
        return Native.GetCurrentApicId();
    }
}
