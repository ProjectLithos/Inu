using System;
using System.Runtime;
using Inu.Kernel.Heap;
using Inu.Kernel.Smp;
using Inu.Kernel.Time;
using Inu.Kernel.Internal.X64;
using Inu.Runtime.NativeAot;

namespace Inu.Kernel.Scheduler;

public static unsafe partial class KernelScheduler
{

    // Serialize runnable-thread claiming across APs. The lock is released before
    // switching contexts, so a CPU can never claim the same Ready thread as a
    // peer while another processor is executing that thread.
    private static void EnterSchedulerLock()
    {
        fixed(UInt64* gate=&_schedulerLock)
        {
            for(;;){UInt64 observed=0UL;if(Native.AtomicCompareExchange64(gate,0UL,1UL,&observed)&&observed==0UL)return;Native.Pause();}
        }
    }
    private static void ExitSchedulerLock()
    { fixed(UInt64* gate=&_schedulerLock) Native.AtomicStore64(gate,0UL); }

    private static void AccountUntil(CpuScheduleState* state,UInt64 now)
    { if(state==null)return;UInt64 previous=state->LastAccountingNanoseconds;UInt64 delta=now>=previous?now-previous:0UL;if(state->AccountingState==0){state->BusyNanoseconds+=delta;AddExecutionDelta(state,(KernelCpuExecutionClass)state->ExecutionClass,delta);}else state->IdleNanoseconds+=delta;state->LastAccountingNanoseconds=now; }

    /// <summary>Marks entry into an interrupt. Interrupt-role accounting is charged only to CPUs selected for the Interrupts role; kernel-local timer/IPI trap overhead on other CPUs remains kernel/platform work.</summary>
    /// <summary>Requests that the active stop-the-world collector abort at its next bounded checkpoint.</summary>
    public static Boolean RequestGarbageCollectionAbort()
    {
        if(!_initialized)return false;fixed(UInt64* p=&_gcAbortRequested)return Native.AtomicStore64(p,1UL);
    }

    /// <summary>Clears a foreground cancellation after its command lifetime has ended.</summary>
    public static Boolean ClearGarbageCollectionAbortRequest()
    {
        fixed(UInt64* p=&_gcAbortRequested)return Native.AtomicStore64(p,0UL);
    }

    private static NativeAotGcFailure ProbeManagedHeapCollectionAbort()
    {
        UInt64 abort=0UL;fixed(UInt64* p=&_gcAbortRequested)Native.AtomicLoad64(p,&abort);
        if(abort!=0UL)return NativeAotGcFailure.CollectionCancelled;
        UInt64 deadline=0UL;fixed(UInt64* p=&_gcCollectionDeadline)Native.AtomicLoad64(p,&deadline);
        if(deadline!=0UL&&KernelTime.GetMonotonicNanoseconds()>=deadline)return NativeAotGcFailure.CollectionTimedOut;
        return NativeAotGcFailure.None;
    }

    private static void ArmManagedHeapCollectionDeadline()
    {
        UInt64 now=KernelTime.GetMonotonicNanoseconds();UInt64 deadline=now>UInt64.MaxValue-ManagedHeapCollectionTimeoutNanoseconds?UInt64.MaxValue:now+ManagedHeapCollectionTimeoutNanoseconds;
        fixed(UInt64* p=&_gcCollectionDeadline)Native.AtomicStore64(p,deadline);
    }

    /// <summary>Scheduler-owned SMP stop-the-world rendezvous used by the tracing collector.</summary>
    private static Boolean CollectManagedHeapStopTheWorld()
    {
        NativeAotExceptionRuntime.TraceStage(0x150UL); // GC coordinator entry
        ArmManagedHeapCollectionDeadline();
        if(!_initialized||_cpus==null||_threads==null)
        {
            NativeAotExceptionRuntime.TraceStage(0x151UL); // pre-scheduler fallback
            Boolean fallback=NativeAotGarbageCollector.CollectAtSafepoint();
            fixed(UInt64* deadline=&_gcCollectionDeadline)Native.AtomicStore64(deadline,0UL);
            NativeAotExceptionRuntime.TraceValue(0x152UL,fallback?1UL:0UL);
            return fallback;
        }
        if(!KernelSmp.TryGetCurrentProcessorIndex(out UInt32 owner)||owner>=_processorCount){NativeAotExceptionRuntime.TraceStage(0x153UL);return false;}
        NativeAotExceptionRuntime.TraceValue(0x154UL,owner);
        UInt64 observed=0UL;
        fixed(UInt64* request=&_gcStopRequested)
            if(!Native.AtomicCompareExchange64(request,0UL,1UL,&observed)||observed!=0UL){NativeAotExceptionRuntime.TraceStage(0x155UL);NativeAotExceptionRuntime.TraceValue(0x156UL,observed);return false;}
        fixed(UInt64* ownerPtr=&_gcOwnerProcessor)Native.AtomicStore64(ownerPtr,owner);
        fixed(UInt64* parked=&_gcParkedProcessors)Native.AtomicStore64(parked,0UL);

        // First mark every non-executing scheduler stack stable. Current CPU stacks are
        // then marked unstable until the owner or the remote IPI explicitly parks them.
        for(UInt32 slot=0U;slot<MaximumThreads;slot++)
        {
            ThreadRecord* r=_threads+slot;if(r->Id==0UL||r->State==(UInt32)KernelThreadState.Unused)continue;
            UInt64 savedSp=r->ContextAddress!=0UL?*(UInt64*)(nuint)(r->ContextAddress+64UL):r->StackBase;
            if(savedSp<r->StackBase||savedSp>r->StackTop)savedSp=r->StackBase;
            NativeAotGarbageCollector.SetThreadAtSafepoint(r->Id,true,(void*)(nuint)savedSp);
        }
        for(UInt32 cpu=0U;cpu<_processorCount;cpu++)
        {
            UInt32 slot=(_cpus+cpu)->CurrentSlot;if(slot==0xFFFFFFFFU||slot>=MaximumThreads)continue;ThreadRecord* r=_threads+slot;if(r->Id!=0UL)NativeAotGarbageCollector.SetThreadAtSafepoint(r->Id,false);
        }
        UInt32 ownerSlot=(_cpus+owner)->CurrentSlot;if(ownerSlot!=0xFFFFFFFFU&&ownerSlot<MaximumThreads){ThreadRecord* current=_threads+ownerSlot;if(current->Id!=0UL)NativeAotGarbageCollector.SetThreadAtSafepoint(current->Id,true,(void*)(nuint)Native.GetCurrentStackPointer());}

        UInt32 expected=0U;
        for(UInt32 cpu=0U;cpu<_processorCount;cpu++)
        {
            if(cpu==owner)continue;if(!KernelSmp.TryGetProcessor(cpu,out KernelProcessorState state))continue;
            if(state.StartupState!=KernelProcessorStartupState.OnlineParked)continue;
            NativeAotExceptionRuntime.TraceStage(0x157UL);NativeAotExceptionRuntime.TraceValue(0x158UL,cpu);
            if(!KernelSmp.TrySendIpi(cpu,KernelIpiPurpose.Reschedule)){NativeAotExceptionRuntime.TraceStage(0x159UL);ReleaseGcStopWorld(owner);return false;}
            expected++;
        }
        NativeAotExceptionRuntime.TraceValue(0x15AUL,expected);
        UInt64 start=KernelTime.GetMonotonicNanoseconds();
        for(;;)
        {
            UInt64 parked=0UL;fixed(UInt64* p=&_gcParkedProcessors)Native.AtomicLoad64(p,&parked);
            if(parked>=expected){NativeAotExceptionRuntime.TraceStage(0x15BUL);NativeAotExceptionRuntime.TraceValue(0x15CUL,parked);break;}
            if(KernelTime.GetMonotonicNanoseconds()-start>2000000000UL){NativeAotExceptionRuntime.TraceStage(0x15DUL);NativeAotExceptionRuntime.TraceValue(0x15EUL,parked);NativeAotExceptionRuntime.TraceValue(0x15FUL,expected);ReleaseGcStopWorld(owner);return false;}
            Native.Pause();
        }

        // Every remote processor is now physically parked. Reconcile the collector's
        // thread table against that stronger fact before mark/sweep. CurrentSlot can
        // legitimately change in the narrow interval before a GC IPI arrives; once the
        // world is stopped, every registered stack range is immutable and therefore a
        // valid conservative root source.
        if(!NativeAotGarbageCollector.PrepareRegisteredStacksForWorldStoppedScan())
        {
            NativeAotExceptionRuntime.TraceStage(0x168UL);
            ReleaseGcStopWorld(owner);
            return false;
        }
        NativeAotExceptionRuntime.TraceStage(0x169UL);

        NativeAotExceptionRuntime.TraceStage(0x160UL);
        Boolean collected=NativeAotGarbageCollector.CollectAtSafepoint();
        NativeAotExceptionRuntime.TraceValue(0x161UL,collected?1UL:0UL);
        NativeAotExceptionRuntime.TraceValue(0x162UL,(UInt64)NativeAotGarbageCollector.GetStatistics().LastFailure);
        ReleaseGcStopWorld(owner);
        NativeAotExceptionRuntime.TraceStage(0x163UL);
        // Finalizers are arbitrary managed code. Never run them while remote CPUs are
        // parked in the stop-the-world rendezvous: they may allocate, take locks, throw,
        // resurrect objects or otherwise require normal scheduler/runtime services.
        if(collected)NativeAotGarbageCollector.DrainFinalizerQueue();
        return collected;
    }

    private static void ReleaseGcStopWorld(UInt32 owner)
    {
        fixed(UInt64* request=&_gcStopRequested)Native.AtomicStore64(request,0UL);
        fixed(UInt64* deadline=&_gcCollectionDeadline)Native.AtomicStore64(deadline,0UL);
        fixed(UInt64* abort=&_gcAbortRequested)Native.AtomicStore64(abort,0UL);
        UInt32 slot=(_cpus!=null&&owner<_processorCount)?(_cpus+owner)->CurrentSlot:0xFFFFFFFFU;
        if(slot!=0xFFFFFFFFU&&slot<MaximumThreads){ThreadRecord* r=_threads+slot;if(r->Id!=0UL)NativeAotGarbageCollector.SetThreadAtSafepoint(r->Id,false);}
        fixed(UInt64* ownerPtr=&_gcOwnerProcessor)Native.AtomicStore64(ownerPtr,NoProcessor);
    }

    /// <summary>Called from the reschedule-IPI path. A remote CPU remains parked until the mark/sweep phase completes.</summary>
    public static Boolean EnterGarbageCollectionSafepoint(UInt32 processorIndex)
    {
        if(!_initialized||processorIndex>=_processorCount)return false;
        UInt64 requested=0UL;fixed(UInt64* request=&_gcStopRequested)Native.AtomicLoad64(request,&requested);if(requested==0UL)return true;
        UInt64 owner=NoProcessor;fixed(UInt64* ownerPtr=&_gcOwnerProcessor)Native.AtomicLoad64(ownerPtr,&owner);if(owner==processorIndex)return true;
        UInt32 slot=(_cpus+processorIndex)->CurrentSlot;UInt64 threadId=0UL;if(slot!=0xFFFFFFFFU&&slot<MaximumThreads)threadId=(_threads+slot)->Id;
        if(threadId!=0UL)NativeAotGarbageCollector.SetThreadAtSafepoint(threadId,true,(void*)(nuint)Native.GetCurrentStackPointer());
        UInt64 previous=0UL;fixed(UInt64* parked=&_gcParkedProcessors)Native.AtomicFetchAdd64(parked,1UL,&previous);
        for(;;){requested=0UL;fixed(UInt64* request=&_gcStopRequested)Native.AtomicLoad64(request,&requested);if(requested==0UL)break;Native.Pause();}
        if(threadId!=0UL)NativeAotGarbageCollector.SetThreadAtSafepoint(threadId,false);
        return true;
    }

    public static Boolean NotifyInterruptEnter(UInt32 processorIndex) => NotifyInterruptEnter(processorIndex,true);

    /// <summary>Marks interrupt entry and distinguishes configurable device/dispatch work from kernel-local scheduler IPIs/ticks.</summary>
    public static Boolean NotifyInterruptEnter(UInt32 processorIndex,Boolean accountAsInterruptRole)
    {
        if(!_initialized||_cpus==null||processorIndex>=_processorCount)return false;
        CpuScheduleState* state=_cpus+processorIndex;AccountUntil(state,KernelTime.GetMonotonicNanoseconds());
        if(state->InterruptDepth==0){state->PreInterruptExecutionClass=state->ExecutionClass;state->PreInterruptAccountingState=state->AccountingState;}
        if(state->InterruptDepth<255)state->InterruptDepth++;
        state->AccountingState=0;
        if(accountAsInterruptRole&&KernelSmp.ProcessorHasRole(processorIndex,KernelCpuRole.Interrupts))state->ExecutionClass=(Byte)KernelCpuExecutionClass.Interrupts;
        else state->ExecutionClass=state->PreInterruptAccountingState==1?(Byte)KernelCpuExecutionClass.Kernel:state->PreInterruptExecutionClass;
        return true;
    }

    /// <summary>Marks exit from an interrupt and restores the interrupted execution class.</summary>
    public static Boolean NotifyInterruptExit(UInt32 processorIndex)
    { if(!_initialized||_cpus==null||processorIndex>=_processorCount)return false;CpuScheduleState* state=_cpus+processorIndex;AccountUntil(state,KernelTime.GetMonotonicNanoseconds());if(state->InterruptDepth!=0)state->InterruptDepth--;if(state->InterruptDepth==0){state->AccountingState=state->PreInterruptAccountingState;state->ExecutionClass=state->PreInterruptAccountingState==1?(Byte)KernelCpuExecutionClass.Idle:state->PreInterruptExecutionClass;}return true; }

    private static void AccountAllProcessors(){if(_cpus==null)return;UInt64 now=KernelTime.GetMonotonicNanoseconds();for(UInt32 cpu=0U;cpu<_processorCount;cpu++)AccountUntil(_cpus+cpu,now);}
    private static void RebaseAccounting(){if(_cpus==null)return;UInt64 now=KernelTime.GetMonotonicNanoseconds();for(UInt32 cpu=0U;cpu<_processorCount;cpu++)(_cpus+cpu)->LastAccountingNanoseconds=now;}

    private static KernelCpuExecutionClass ExecutionClassForCurrentThread(CpuScheduleState* state)
    { if(state==null||state->CurrentSlot==0xFFFFFFFFU||state->CurrentSlot>=MaximumThreads)return KernelCpuExecutionClass.Kernel;return (KernelCpuExecutionClass)(_threads+state->CurrentSlot)->ExecutionClass; }
    private static KernelCpuExecutionClass ExecutionClassForRole(KernelCpuRole role)
    { if(role==KernelCpuRole.Userland)return KernelCpuExecutionClass.Userland;if(role==KernelCpuRole.Gui)return KernelCpuExecutionClass.Gui;if(role==KernelCpuRole.Drivers)return KernelCpuExecutionClass.Drivers;if(role==KernelCpuRole.Interrupts)return KernelCpuExecutionClass.Interrupts;if(role==KernelCpuRole.Networking)return KernelCpuExecutionClass.Networking;if(role==KernelCpuRole.Storage)return KernelCpuExecutionClass.Storage;if(role==KernelCpuRole.Realtime)return KernelCpuExecutionClass.Realtime;if(role==KernelCpuRole.Background)return KernelCpuExecutionClass.Background;return KernelCpuExecutionClass.Kernel; }
    private static void AddExecutionDelta(CpuScheduleState* state,KernelCpuExecutionClass executionClass,UInt64 delta)
    { if(executionClass==KernelCpuExecutionClass.Userland)state->UserlandNanoseconds+=delta;else if(executionClass==KernelCpuExecutionClass.Gui)state->GuiNanoseconds+=delta;else if(executionClass==KernelCpuExecutionClass.Drivers)state->DriverNanoseconds+=delta;else if(executionClass==KernelCpuExecutionClass.Interrupts)state->InterruptNanoseconds+=delta;else if(executionClass==KernelCpuExecutionClass.Networking)state->NetworkingNanoseconds+=delta;else if(executionClass==KernelCpuExecutionClass.Storage)state->StorageNanoseconds+=delta;else if(executionClass==KernelCpuExecutionClass.Realtime)state->RealtimeNanoseconds+=delta;else if(executionClass==KernelCpuExecutionClass.Background)state->BackgroundNanoseconds+=delta;else state->KernelNanoseconds+=delta; }
    private static void AddExecutionDelta(KernelCpuExecutionClass executionClass,UInt64 delta,ref UInt64 kernel,ref UInt64 userland,ref UInt64 gui,ref UInt64 drivers,ref UInt64 interrupts,ref UInt64 networking,ref UInt64 storage,ref UInt64 realtime,ref UInt64 background)
    { if(executionClass==KernelCpuExecutionClass.Userland)userland+=delta;else if(executionClass==KernelCpuExecutionClass.Gui)gui+=delta;else if(executionClass==KernelCpuExecutionClass.Drivers)drivers+=delta;else if(executionClass==KernelCpuExecutionClass.Interrupts)interrupts+=delta;else if(executionClass==KernelCpuExecutionClass.Networking)networking+=delta;else if(executionClass==KernelCpuExecutionClass.Storage)storage+=delta;else if(executionClass==KernelCpuExecutionClass.Realtime)realtime+=delta;else if(executionClass==KernelCpuExecutionClass.Background)background+=delta;else kernel+=delta; }

}
