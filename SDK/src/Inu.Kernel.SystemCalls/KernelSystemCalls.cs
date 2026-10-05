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

/// <summary>Provides the shared protected syscall core for native Inu messages plus Linux/NT compatibility namespaces.</summary>
public static unsafe partial class KernelSystemCalls
{
    private const UInt64 SyscallStackBytes = 32768UL;
    private const Int32 CompatibilityRegistrySlots = 128;
    private const Int32 NativeRegistrySlots = 128;
    private const Int32 NativePrefixRegistrySlots = 16;
    private const Int32 MaximumNativeMessageBytes = 96;

    private unsafe struct Registry
    {
        internal fixed UInt64 NativeHandlers[NativeRegistrySlots];
        internal fixed UInt32 NativeMessageLengths[NativeRegistrySlots];
        internal fixed Byte NativeOperations[NativeRegistrySlots];
        internal fixed Byte NativeMessages[NativeRegistrySlots*MaximumNativeMessageBytes];
        internal fixed UInt64 NativePrefixHandlers[NativePrefixRegistrySlots];
        internal fixed UInt32 NativePrefixLengths[NativePrefixRegistrySlots];
        internal fixed Byte NativePrefixOperations[NativePrefixRegistrySlots];
        internal fixed Byte NativePrefixes[NativePrefixRegistrySlots*MaximumNativeMessageBytes];
        internal fixed UInt64 Linux[CompatibilityRegistrySlots];
        internal fixed UInt64 Nt[CompatibilityRegistrySlots];
    }

#pragma warning disable CS0169
    private static Registry _registry;
#pragma warning restore CS0169
    private static UInt32 _nativeRegistryCount;
    private static UInt32 _nativePrefixRegistryCount;
    private const UInt32 MaximumProcessors = 256U;
    private unsafe struct PerCpuSyscallTable
    {
        internal fixed UInt64 StateAddress[(Int32)MaximumProcessors];
        internal fixed UInt64 StackBase[(Int32)MaximumProcessors];
        internal fixed UInt64 StackTop[(Int32)MaximumProcessors];
        internal fixed UInt32 Configured[(Int32)MaximumProcessors];
    }
#pragma warning disable CS0169
    private static PerCpuSyscallTable _perCpu;
#pragma warning restore CS0169
    private static Boolean _initialized, _smapEnabled;
    private static UInt32 _configuredProcessors;
    private static UInt64 _diagnosticSequence;
    private static UInt64 _nativeCancellationHandler;
    private const UInt64 DiagnosticTraceLimit=128UL;

    /// <summary>Initializes the x64 SYSCALL/SYSRET boundary and the built-in semantic native messages.</summary>
    public static Boolean Initialize()
    {
        if (_initialized) return true;
        if (!KernelProtection.IsInitialized() || !KernelSecurity.IsInitialized() || !KernelHeap.IsInitialized() || !KernelSmp.IsInitialized()) return false;
        _initialized = true;
        if (!RegisterGet(KernelSystemCallMessages.SystemVersion,&GetSystemVersionMessage) ||
            !RegisterGet(KernelSystemCallMessages.TimeMonotonic,&GetMonotonicTimeMessage) ||
            !RegisterGet(KernelSystemCallMessages.CpuOnlineCount,&GetOnlineCpuCountMessage) ||
            !RegisterGet(KernelSystemCallMessages.SchedulerQuantum,&GetSchedulerQuantumMessage) ||
            !RegisterSet(KernelSystemCallMessages.SchedulerQuantum,&SetSchedulerQuantumMessage) ||
            !RegisterEvent(KernelSystemCallMessages.SchedulerYield,&SchedulerYieldMessage) ||
            !EnsureCurrentProcessorConfigured())
        { _initialized=false; return false; }
        return true;
    }

    /// <summary>Ensures the processor executing this call has its own syscall stack, GS state and SYSCALL/SYSRET MSRs.</summary>
    public static Boolean EnsureCurrentProcessorConfigured()
    {
        if (!_initialized || !KernelSmp.TryGetCurrentProcessorIndex(out UInt32 cpu) || cpu>=MaximumProcessors) return false;
        fixed(UInt32* configured=_perCpu.Configured) if(configured[cpu]!=0U) return true;
        if (!KernelHeap.TryAllocate(SyscallStackBytes,16UL,true,out KernelHeapAllocation stack)) return false;
        if (!KernelHeap.TryAllocate(128UL,16UL,true,out KernelHeapAllocation state)) return false;
        UInt64 stackTop=stack.Address+stack.ByteCount;
        if (!Native.ConfigureSystemCalls(state.Address,stackTop)) return false;
        KernelProtectionCapabilities protection=KernelProtection.GetCapabilities();
        Boolean smap=false;
        if(protection.SmapSupported){if(!Native.EnableSmap()||!Native.IsSmapEnabled())return false;smap=true;}
        fixed(UInt64* states=_perCpu.StateAddress,bases=_perCpu.StackBase,tops=_perCpu.StackTop) fixed(UInt32* configured=_perCpu.Configured)
        {states[cpu]=state.Address;bases[cpu]=stack.Address;tops[cpu]=stackTop;configured[cpu]=1U;}
        _configuredProcessors++;if(smap)_smapEnabled=true;return true;
    }

    public static Boolean IsInitialized() => _initialized;
    public static KernelSystemCallCapabilities GetCapabilities() => new(_initialized, _smapEnabled, _configuredProcessors, KernelSmp.GetProcessorCount(), SyscallStackBytes);
    public static UInt64 GetSyscallStateAddress() { if(!KernelSmp.TryGetCurrentProcessorIndex(out UInt32 cpu)||cpu>=MaximumProcessors)return 0UL;fixed(UInt64* p=_perCpu.StateAddress)return p[cpu]; }
    public static UInt64 GetSyscallStackBase() { if(!KernelSmp.TryGetCurrentProcessorIndex(out UInt32 cpu)||cpu>=MaximumProcessors)return 0UL;fixed(UInt64* p=_perCpu.StackBase)return p[cpu]; }
    public static UInt64 GetSyscallStackTop() { if(!KernelSmp.TryGetCurrentProcessorIndex(out UInt32 cpu)||cpu>=MaximumProcessors)return 0UL;fixed(UInt64* p=_perCpu.StackTop)return p[cpu]; }

    /// <summary>Registers a native Inu Get message. Message text, not a numeric service ID, selects the handler.</summary>
}
