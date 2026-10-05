using System;
using System.Runtime;
using Inu.Kernel.Heap;
using Inu.Kernel.Smp;
using Inu.Kernel.Time;
using Inu.Kernel.Internal.X64;
using Inu.Runtime.NativeAot;

namespace Inu.Kernel.Scheduler;

/// <summary>Owns kernel-thread records, per-CPU run state, priority queues, affinity and preemption decisions.</summary>
public static unsafe partial class KernelScheduler
{
    private const UInt32 MaximumThreads = 256U;
    private const UInt64 DefaultStackBytes = 65536UL;
    private const UInt64 DefaultQuantumNanoseconds = 5000000UL;
    private const UInt64 ApplicationProcessorSchedulerTickNanoseconds = 1000000UL;
    private const UInt64 ApplicationProcessorSchedulerReadyTimeoutNanoseconds = 5000000000UL;
    // No stop-the-world mark/sweep may monopolise all CPUs indefinitely.  This is also
    // the upper bound before a pending Ctrl-C can regain an interrupt-capable CPU.
    private const UInt64 ManagedHeapCollectionTimeoutNanoseconds = 2000000000UL;
    private const UInt32 NoProcessor = 0xFFFFFFFFU;
    private const UInt64 ContextBytes = 256UL;
    private const Byte RescheduleIpiVector = 0xF0;

    private struct CpuScheduleState
    {
        internal UInt32 CurrentSlot, IdleSlot;
        internal UInt64 DispatcherContextAddress, PendingRetireThreadId;
        internal UInt64 SwitchCount, PreemptionCount, LastDispatchNanoseconds, BusyNanoseconds, IdleNanoseconds, LastAccountingNanoseconds;
        internal UInt64 KernelNanoseconds,UserlandNanoseconds,GuiNanoseconds,DriverNanoseconds,InterruptNanoseconds,NetworkingNanoseconds,StorageNanoseconds,RealtimeNanoseconds,BackgroundNanoseconds;
        internal Byte AccountingState,ExecutionClass,PreInterruptExecutionClass,PreInterruptAccountingState,InterruptDepth,RuntimeInterruptWake;
    }

    private static CpuScheduleState* _cpus;
    private static UInt32 _processorCount;
    private static UInt64 _quantum = DefaultQuantumNanoseconds, _schedulerLock;
    private static UInt64 _gcStopRequested, _gcParkedProcessors, _gcOwnerProcessor = NoProcessor, _gcAbortRequested, _gcCollectionDeadline;

#if DEBUG
    private static UInt64 _apSchedulerDebugSerialGate;

    private static void DebugApSchedulerTrace(String message) => DebugApSchedulerTrace(message, 0UL, false);
    private static void DebugApSchedulerTrace(String message, UInt64 value) => DebugApSchedulerTrace(message, value, true);

    private static unsafe void DebugApSchedulerTrace(String message, UInt64 value, Boolean hasValue)
    {
        // Static fields must be pinned before their addresses are passed to C# / NativeAOTode.
        fixed(UInt64* gate=&_apSchedulerDebugSerialGate)
        {
            UInt64 observed=0UL;
            for(UInt32 spin=0U;spin<100000U;spin++)
            {
                observed=0UL;
                if(Native.AtomicCompareExchange64(gate,0UL,1UL,&observed)&&observed==0UL)break;
                Native.Pause();
            }
            if(observed!=0UL)return;
            if(!Native.BeginSerialRecord()){Native.AtomicStore64(gate,0UL);return;}
            DebugSchedulerSerialWrite("[SMP-SCHED] ");
            DebugSchedulerSerialWrite(message);
            if(hasValue){DebugSchedulerSerialWrite("0x");DebugSchedulerSerialWriteHex(value);}
            Native.WriteSerial((Byte)'\r');Native.WriteSerial((Byte)'\n');
            Native.EndSerialRecord();
            Native.AtomicStore64(gate,0UL);
        }
    }

    private static void DebugSchedulerSerialWrite(String value)
    {
        if(value==null)return;
        for(Int32 i=0;i<value.Length;i++){Char c=value[i];Native.WriteSerial((Byte)((UInt32)c<=0x7FU?c:'?'));}
    }

    private static void DebugSchedulerSerialWriteHex(UInt64 value)
    {
        const String digits="0123456789ABCDEF";Boolean started=false;
        for(Int32 shift=60;shift>=0;shift-=4){UInt32 n=(UInt32)((value>>shift)&0xFUL);if(!started&&n==0U&&shift!=0)continue;started=true;Native.WriteSerial((Byte)digits[(Int32)n]);}
    }
#endif
    private const Byte StateStopped=0,StateReady=1,StateRunning=2,StatePaused=3;
    private static volatile Boolean _initialized;
    private static Boolean _timerPreemption;
    private static volatile Byte _lifecycle;
    private static UInt64 _kernelWorkerCallback,_userlandWorkerCallback,_guiWorkerCallback,_driverWorkerCallback,_interruptWorkerCallback,_networkingWorkerCallback,_storageWorkerCallback,_realtimeWorkerCallback,_backgroundWorkerCallback;
    private static UInt64 _kernelWorkerThread,_userlandWorkerThread,_guiWorkerThread,_driverWorkerThread,_interruptWorkerThread,_networkingWorkerThread,_storageWorkerThread,_realtimeWorkerThread,_backgroundWorkerThread;
    // A wake must survive the short interval in which a persistent role worker has observed
    // no work but has not yet published Blocked.  Without this latch a producer can notify a
    // still-Running worker, the IPI can be consumed, and the worker can then block after the
    // notification.  The request remains queued forever.
    private static UInt64 _kernelWorkerPending,_userlandWorkerPending,_guiWorkerPending,_driverWorkerPending,_interruptWorkerPending,_networkingWorkerPending,_storageWorkerPending,_realtimeWorkerPending,_backgroundWorkerPending;

    /// <summary>Initializes scheduler tables after SMP, heap and clock initialization.</summary>
    public static Boolean Initialize()
    {
        if (_initialized) return true;
        // NativeAOT uses --nopreinitstatics: establish non-zero runtime defaults explicitly.
        _nextThreadId = 1UL;
        _quantum = DefaultQuantumNanoseconds;
        if(!KernelSchedulingPolicyServices.IsRegistered || !KernelThreadPlacementServices.IsRegistered) return false;
        if (!KernelSmp.IsInitialized() || !KernelTime.IsInitialized) return false;
        // 0xE0-0xFE are kernel-reserved vectors; bind the scheduler reschedule IPI
        // before any role worker can attempt to wake an application processor.
        if (!KernelSmp.ConfigureIpiVector(KernelIpiPurpose.Reschedule, RescheduleIpiVector)) return false;
        _processorCount = KernelSmp.GetProcessorCount();
        if (_processorCount == 0U) return false;
        if (!KernelSchedulerMath.TryGetTableBytes(MaximumThreads, (UInt32)sizeof(ThreadRecord), out UInt64 threadBytes)) return false;
        if (!KernelSchedulerMath.TryGetTableBytes(_processorCount, (UInt32)sizeof(CpuScheduleState), out UInt64 cpuBytes)) return false;
        if (!KernelHeap.TryAllocate(threadBytes, 64UL, true, out KernelHeapAllocation threadAlloc)) return false;
        if (!KernelHeap.TryAllocate(cpuBytes, 64UL, true, out KernelHeapAllocation cpuAlloc)) return false;
        _threads = (ThreadRecord*)threadAlloc.Address; _cpus = (CpuScheduleState*)cpuAlloc.Address;
        UInt64 accountingStart=KernelTime.GetMonotonicNanoseconds();
        UInt32 bootstrapCpu=KernelSmp.GetBootstrapProcessorIndex();
        for (UInt32 cpu=0U; cpu<_processorCount; cpu++)
        {
            CpuScheduleState* state=_cpus+cpu; state->CurrentSlot=0xFFFFFFFFU; state->IdleSlot=0xFFFFFFFFU;
            if (!KernelHeap.TryAllocate(ContextBytes, 16UL, true, out KernelHeapAllocation dispatcherContext)) return false;
            state->DispatcherContextAddress=dispatcherContext.Address;
            state->BusyNanoseconds=0UL; state->IdleNanoseconds=0UL; state->LastAccountingNanoseconds=accountingStart; state->AccountingState=(Byte)(cpu==bootstrapCpu?0:1);
            state->ExecutionClass=(Byte)(cpu==bootstrapCpu?KernelCpuExecutionClass.Kernel:KernelCpuExecutionClass.Idle);state->PreInterruptExecutionClass=(Byte)KernelCpuExecutionClass.Idle;state->PreInterruptAccountingState=state->AccountingState;state->InterruptDepth=0;state->RuntimeInterruptWake=(Byte)(cpu==bootstrapCpu?1:0);
            if (!KernelSmp.TrySetSchedulerContext(cpu, (UInt64)state)) return false;
        }
        if (!CreateBootstrapThread()) return false;
        if (!CreateApplicationProcessorIdleThreads()) return false;
        if (!NativeAotGarbageCollector.RegisterCollectionCoordinator(&CollectManagedHeapStopTheWorld)) return false;
        if (!NativeAotGarbageCollector.RegisterCollectionAbortProbe(&ProbeManagedHeapCollectionAbort)) return false;
        _timerPreemption = KernelTime.GetCapabilities().HasLocalApicTimer;
        _lifecycle=StateReady; _initialized=true; return true;
    }

    /// <summary>Gets whether scheduler state has been initialized.</summary>
    public static Boolean IsInitialized() => _initialized;
    /// <summary>Gets scheduler lifecycle state: 0 stopped, 1 ready, 2 running, 3 paused.</summary>
    public static Byte GetLifecycleState()=>_lifecycle;
    /// <summary>Starts scheduling from the ready or stopped state without rebuilding scheduler tables.</summary>
    public static Boolean Start()
    {
        if(!_initialized||(_lifecycle!=StateReady&&_lifecycle!=StateStopped))return false;
        _lifecycle=StateRunning;RebaseAccounting();WakeApplicationProcessors();
        // Do not report a running SMP scheduler merely because SIPI reached permanent native
        // code. Every online AP must first execute the scheduler idle thread, proving both the
        // managed entry and a real thread-context transfer on that CPU. This prevents Userland
        // and service workers from being assigned to an AP that can never dispatch them.
        if(!WaitForApplicationProcessorSchedulersReady()){_lifecycle=StateStopped;return false;}
        return true;
    }
    /// <summary>Runs the scheduler. Starts when ready/stopped and resumes when paused.</summary>
    public static Boolean Run(){if(!_initialized)return false;if(_lifecycle==StatePaused)return Resume();if(_lifecycle==StateRunning)return true;return Start();}
    /// <summary>Pauses scheduling decisions while preserving threads, run queues, affinity and accounting state.</summary>
    public static Boolean Pause(){if(!_initialized||_lifecycle!=StateRunning)return false;AccountAllProcessors();_lifecycle=StatePaused;return true;}
    /// <summary>Resumes a paused scheduler without rebuilding its runtime state.</summary>
    public static Boolean Resume(){if(!_initialized||_lifecycle!=StatePaused)return false;_lifecycle=StateRunning;RebaseAccounting();WakeApplicationProcessors();return true;}
    /// <summary>Stops future scheduling decisions while retaining initialized scheduler state for Start/Run.</summary>
    public static Boolean Stop(){if(!_initialized||_lifecycle==StateStopped)return false;AccountAllProcessors();_lifecycle=StateStopped;return true;}
    /// <summary>Gets scheduler capabilities and current usage.</summary>
    public static KernelSchedulerCapabilities GetCapabilities() => new KernelSchedulerCapabilities(_processorCount, MaximumThreads, _activeThreads, _quantum, _timerPreemption);
    /// <summary>Gets the number of active thread records.</summary>
    public static UInt32 GetActiveThreadCount() => _activeThreads;
    /// <summary>Gets the configured scheduler quantum.</summary>
    public static UInt64 GetQuantumNanoseconds() => _quantum;
    /// <summary>Sets the scheduler quantum and re-arms timer preemption when available.</summary>
    public static Boolean SetQuantumNanoseconds(UInt64 nanoseconds)
    {
        if (!_initialized) return false; _quantum=KernelSchedulerMath.ClampQuantum(nanoseconds); return true;
    }

}
