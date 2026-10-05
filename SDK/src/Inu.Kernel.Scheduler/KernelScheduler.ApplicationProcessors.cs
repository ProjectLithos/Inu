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
    [RuntimeExport("InuManagedSchedulerApplicationProcessorEntry")]
    private static void ApplicationProcessorEntry(UInt32 apicId)
    {
#if DEBUG
        DebugApSchedulerTrace("managed AP entry reached; argument APIC ID = ", apicId);
        DebugApSchedulerTrace("managed AP entry current APIC ID = ", Native.GetCurrentApicId());
        DebugApSchedulerTrace("managed AP EFER.NXE enabled = ", Native.IsExecuteDisableEnabled() ? 1UL : 0UL);
        DebugApSchedulerTrace("waiting for KernelSmp initialization");
#endif
        while(!KernelSmp.IsInitialized()) Native.Pause();
#if DEBUG
        DebugApSchedulerTrace("KernelSmp initialization visible to AP");
#endif
        // The x64 trampoline publishes its handoff marker only after consuming all
        // trampoline-specific data, so managed scheduler entry has no bootstrap handshake.
        // SMP brings APs into this managed entry before the BSP initializes the
        // scheduler. Never validate against _processorCount until _initialized
        // publishes the scheduler tables: before that point _processorCount is 0
        // and every non-bootstrap CPU would park forever with runnable work queued.
#if DEBUG
        DebugApSchedulerTrace("waiting for KernelScheduler initialization");
#endif
        while(!_initialized) Native.Pause();
#if DEBUG
        DebugApSchedulerTrace("KernelScheduler initialization visible to AP");
#endif
        // _initialized is a volatile publication barrier: all scheduler tables and
        // per-CPU contexts written before Initialize() publishes true are now visible.
        if(!KernelSmp.TryGetCurrentProcessorIndex(out UInt32 cpu) || cpu>=_processorCount)
        {
#if DEBUG
            DebugApSchedulerTrace("AP execution FAILED: cannot resolve current CPU index");
#endif
            for(;;) Native.Pause();
        }
#if DEBUG
        DebugApSchedulerTrace("AP resolved logical CPU index = ", cpu);
#endif
        if(_cpus==null)
        {
#if DEBUG
            DebugApSchedulerTrace("AP execution FAILED: scheduler CPU table is null");
#endif
            for(;;) Native.Pause();
        }
        CpuScheduleState* cs=_cpus+cpu;
        // INIT/SIPI resets processor-local APIC software state. The BSP's APIC enable done
        // during time calibration is not inherited by application processors. Enable this
        // AP's own Local APIC before relying on runtime reschedule IPIs or its scheduler tick.
        cs->RuntimeInterruptWake=KernelSmp.EnableCurrentProcessorRuntimeInterrupts()?(Byte)1:(Byte)0;
#if DEBUG
        DebugApSchedulerTrace(cs->RuntimeInterruptWake!=0 ? "AP Local APIC runtime interrupts ENABLED on CPU = " : "AP Local APIC runtime interrupts FAILED on CPU = ", cpu);
#endif
#if DEBUG
        DebugApSchedulerTrace("waiting for scheduler lifecycle Running, CPU = ", cpu);
#endif
        while(_lifecycle!=StateRunning) Native.Pause();
#if DEBUG
        DebugApSchedulerTrace("scheduler lifecycle Running observed, CPU = ", cpu);
#endif
        // Each application processor owns its own Local APIC scheduler tick.  The
        // BSP timer only drives global kernel services; an AP must not depend on
        // another CPU's timer or on a one-shot reschedule IPI to make forward
        // progress.  Programming vector 0xF0 on this CPU gives every local
        // scheduler an independent 1 ms wake source while it is in HLT.
        // The AP-local timer is an optimisation, not a prerequisite for scheduler entry.
        // Some firmware/QEMU Local APIC configurations expose a calibrated timer on the BSP
        // but reject/restrict reprogramming while an AP is entering its managed scheduler.
        // Parking the AP forever on that failure leaves perfectly runnable CPU-local threads
        // stranded.  Reschedule IPIs plus the idle thread's STI;HLT path are sufficient for
        // forward progress, so arm the local tick best-effort and always enter the scheduler.
        if(_timerPreemption)
        {
            Boolean timerArmed=KernelTime.TryArmPeriodic(RescheduleIpiVector,ApplicationProcessorSchedulerTickNanoseconds);
#if DEBUG
            DebugApSchedulerTrace(timerArmed ? "AP scheduler periodic timer armed on CPU = " : "AP scheduler periodic timer FAILED on CPU = ", cpu);
#endif
        }
        // The first AP dispatch is intentionally the CPU-local idle thread, even when a role
        // worker is already Ready. IdleThreadEntry publishes SchedulerReady only after the
        // C# / NativeAOTontext switch has actually succeeded. It immediately dispatches any queued
        // higher-priority role work, so this is a one-time proof step rather than a policy change.
#if DEBUG
        DebugApSchedulerTrace("starting first AP CPU-local scheduler context transfer, CPU = ", cpu);
#endif
        if(!RunApplicationProcessorDispatcher(cpu,cs))
        {
#if DEBUG
            DebugApSchedulerTrace("AP scheduler dispatcher FAILED, CPU = ", cpu);
#endif
            for(;;)Native.Pause();
        }
        for(;;) Native.Pause();
    }

    private static Boolean RunApplicationProcessorDispatcher(UInt32 cpu,CpuScheduleState* cs)
    {
        if(cs==null||cpu>=_processorCount)return false;
        Boolean first=true;
        for(;;)
        {
            EnterSchedulerLock();
            UInt32 slot=0xFFFFFFFFU;
            UInt64 nextThreadId=0UL;
            if(first)
            {
                slot=cs->IdleSlot;
                if(_lifecycle!=StateRunning||slot==0xFFFFFFFFU||slot>=MaximumThreads){ExitSchedulerLock();return false;}
                ThreadRecord* idle=_threads+slot;
                if(idle->Id==0UL||idle->ContextAddress==0UL||idle->ProcessorIndex!=cpu||(idle->State!=(UInt32)KernelThreadState.Ready&&idle->State!=(UInt32)KernelThreadState.Running)){ExitSchedulerLock();return false;}
                nextThreadId=idle->Id;
                first=false;
            }
            else
            {
                if(_lifecycle!=StateRunning){ExitSchedulerLock();return false;}
                if(!KernelSchedulingPolicyServices.TrySelectBalanced(cpu,out nextThreadId)||!FindThread(nextThreadId,out slot)){ExitSchedulerLock();Native.Pause();continue;}
            }
            ThreadRecord* next=_threads+slot;
            next->State=(UInt32)KernelThreadState.Running;next->ProcessorIndex=cpu;cs->CurrentSlot=slot;cs->SwitchCount++;cs->LastDispatchNanoseconds=KernelTime.GetMonotonicNanoseconds();cs->ExecutionClass=next->ExecutionClass;
            UInt64 nextContext=next->ContextAddress;
            ExitSchedulerLock();
            Boolean switched=Native.SwitchThreadContext(cs->DispatcherContextAddress,nextContext);
            // Returning here is a positive architectural acknowledgement that a one-shot
            // thread which requested dispatcher return has switched completely off its own
            // stack. The pending id is published by that thread before the native switch.
            EnterSchedulerLock();
            UInt64 retiredId=cs->PendingRetireThreadId;
            if(retiredId!=0UL&&FindThread(retiredId,out UInt32 retiredSlot))
            {
                ThreadRecord* returned=_threads+retiredSlot;
                if(returned->OneShot!=0&&(returned->State==(UInt32)KernelThreadState.Blocked||returned->State==(UInt32)KernelThreadState.Terminated))returned->RetireReady=1;
            }
            cs->PendingRetireThreadId=0UL;
            if(cs->CurrentSlot==slot)cs->CurrentSlot=0xFFFFFFFFU;
            ExitSchedulerLock();
            if(!switched)return false;
        }
    }

    private static Boolean ReturnOneShotToDispatcher(UInt32 cpu,UInt64 threadId)
    {
        if(cpu>=_processorCount||threadId==0UL)return false;
        UInt64 currentContext=0UL,dispatcherContext=0UL;
        EnterSchedulerLock();
        CpuScheduleState* cs=_cpus+cpu;UInt32 slot=cs->CurrentSlot;
        if(slot==0xFFFFFFFFU||slot>=MaximumThreads){ExitSchedulerLock();return false;}
        ThreadRecord* current=_threads+slot;
        if(current->Id!=threadId||current->OneShot==0||current->ContextAddress==0UL||cs->DispatcherContextAddress==0UL){ExitSchedulerLock();return false;}
        current->State=(UInt32)KernelThreadState.Blocked;current->ProcessorIndex=cpu;currentContext=current->ContextAddress;dispatcherContext=cs->DispatcherContextAddress;
        // Publish the exact thread that is about to leave its stack. The dispatcher clears this
        // only after its own context has actually resumed, which is the retirement acknowledgement.
        cs->PendingRetireThreadId=threadId;
        // CurrentSlot is intentionally cleared before switching; RetireReady remains false until
        // the dispatcher has actually resumed on its independent stack.
        cs->CurrentSlot=0xFFFFFFFFU;
        ExitSchedulerLock();
        return Native.SwitchThreadContext(currentContext,dispatcherContext);
    }

    /// <summary>One-shot managed worker used when a producer stack must be retired before a later GC root snapshot.</summary>
    [RuntimeExport("InuManagedSchedulerOneShotThread")]
    private static void OneShotThreadEntry(UInt64 callbackAddress)
    {
        if(callbackAddress!=0UL){delegate*<void> callback=(delegate*<void>)(void*)(nuint)callbackAddress;callback();}
        if(TryGetCurrentThreadId(out UInt64 threadId)&&KernelSmp.TryGetCurrentProcessorIndex(out UInt32 cpu))ReturnOneShotToDispatcher(cpu,threadId);
        for(;;)Native.Pause();
    }

    /// <summary>Persistent role worker. It performs bounded subsystem work, blocks when empty, and is woken by the owning queue/interrupt source.</summary>
    [RuntimeExport("InuManagedSchedulerRoleWorker")]
    private static void RoleWorkerEntry(UInt64 roleArgument)
    {
        KernelCpuRole role=(KernelCpuRole)(Byte)roleArgument;
        for(;;)
        {
            // Consume the wake that brought us here. New notifications arriving while the
            // callback runs remain latched for the next pass.
            ConsumeRoleWorkerPending(role);
            UInt64 callback=GetRoleWorkerCallback(role);Boolean didWork=false;
            if(_lifecycle==StateRunning&&callback!=0UL){delegate*<Boolean> service=(delegate*<Boolean>)(void*)callback;didWork=service();}
            if(didWork)
            {
                if(KernelSmp.TryGetCurrentProcessorIndex(out UInt32 busyCpu))Schedule(busyCpu,false,out _);
                continue;
            }
            // Close both halves of the lost-wakeup window. A notification before Block is
            // caught by the first consume. A notification after Block either makes the thread
            // Ready itself or is caught by the second consume and converted into a self-wake
            // before we leave this execution context.
            if(ConsumeRoleWorkerPending(role))continue;
            if(TryGetCurrentThreadId(out UInt64 threadId))
            {
                if(Block(threadId)&&ConsumeRoleWorkerPending(role))Wake(threadId);
            }
            if(KernelSmp.TryGetCurrentProcessorIndex(out UInt32 cpu))Schedule(cpu,false,out _);
            else Native.Pause();
        }
    }

    /// <summary>CPU-local idle scheduler thread. It cannot sleep while runnable local work exists and uses an atomic interrupt-enable/halt wait to avoid lost reschedule IPIs.</summary>
    [RuntimeExport("InuManagedSchedulerIdleThread")]
    private static void IdleThreadEntry(UInt64 processorArgument)
    {
        UInt32 cpu=(UInt32)processorArgument;
#if DEBUG
        DebugApSchedulerTrace("AP idle thread ENTRY reached on CPU = ", cpu);
        DebugApSchedulerTrace("AP idle entry current APIC ID = ", Native.GetCurrentApicId());
#endif
        // Reaching this point is the managed scheduler readiness handshake. The AP has left the
        // SIPI trampoline, entered NativeAOT, and successfully switched from its dispatcher
        // context onto an initialized scheduler thread stack/context.
#if DEBUG
        DebugApSchedulerTrace("publishing scheduler-ready from AP idle thread, CPU = ", cpu);
#endif
        if(!KernelSmp.NotifyCurrentProcessorSchedulerReady())
        {
#if DEBUG
            DebugApSchedulerTrace("scheduler-ready publication FAILED from AP idle thread, CPU = ", cpu);
#endif
            for(;;)Native.Pause();
        }
#if DEBUG
        DebugApSchedulerTrace("AP scheduler READY; entering idle/run-queue loop, CPU = ", cpu);
#endif
        for(;;)
        {
            CpuScheduleState* cs=(_cpus!=null&&cpu<_processorCount)?_cpus+cpu:null;
            if(cs!=null){AccountUntil(cs,KernelTime.GetMonotonicNanoseconds());cs->AccountingState=1;cs->ExecutionClass=(Byte)KernelCpuExecutionClass.Idle;}

            // Close the classic lost-wakeup window: no interrupt may be consumed between
            // observing an empty local run queue and entering HLT. If work is already
            // runnable, dispatch it immediately instead of sleeping.
            Native.DisableInterrupts();
            if(_lifecycle==StateRunning&&HasDispatchableWork(cpu))
            {
                Native.EnableInterrupts();
                if(cs!=null){AccountUntil(cs,KernelTime.GetMonotonicNanoseconds());cs->AccountingState=0;cs->ExecutionClass=(Byte)KernelCpuExecutionClass.Kernel;}
                Schedule(cpu,false,out _);
                continue;
            }

            // Correctness must not depend on an AP receiving a reschedule IPI or Local APIC
            // timer interrupt.  Several real/QEMU APIC states can report software-enable while
            // still failing to deliver the first post-idle scheduler wake.  Keep interrupts
            // enabled, account this CPU as idle, and poll the CPU-local ready queue at a bounded
            // cadence.  This guarantees forward progress for Userland/driver/service threads
            // even if the wake transport is lost.  HLT can be reintroduced only after Inu
            // has a separately verified per-CPU wake acknowledgement protocol.
            Native.EnableInterrupts();
            for(UInt32 spin=0U;spin<16384U;spin++)Native.Pause();
            // Polling for runnable work is the AP's scheduler-idle implementation. Keep the
            // entire no-work polling interval charged to idle; only transition to kernel-busy
            // accounting when a runnable thread is actually about to be dispatched.
            if(_lifecycle==StateRunning&&HasDispatchableWork(cpu))
            {
                if(cs!=null){AccountUntil(cs,KernelTime.GetMonotonicNanoseconds());cs->AccountingState=0;cs->ExecutionClass=(Byte)KernelCpuExecutionClass.Kernel;}
                Schedule(cpu,false,out _);
            }
        }
    }

}
