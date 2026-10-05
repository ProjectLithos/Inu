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
    /// <summary>Publishes work to one scheduler thread and kicks the CPU-local scheduler that owns it.</summary>
    public static Boolean NotifyThreadWork(UInt64 threadId)
    {
        if(threadId==0UL)return false;
        UInt32 ownerCpu=NoProcessor;Boolean accepted=false;
        EnterSchedulerLock();
        if(FindThread(threadId,out UInt32 slot))
        {
            ThreadRecord* r=_threads+slot;ownerCpu=r->ProcessorIndex;
            if(r->State==(UInt32)KernelThreadState.Blocked)
            {
                // Wakes are migration opportunities, not migration commands. Keep a worker on
                // its warm CPU unless the current CPU is no longer eligible or the load
                // advantage is large enough and the minimum residency interval has elapsed.
                if(!KernelThreadPlacementServices.TryRebalance(new KernelRunnableThreadHandle(slot+1U),ownerCpu,out ownerCpu)){ExitSchedulerLock();return false;}if(r->ProcessorIndex!=ownerCpu){r->ProcessorIndex=ownerCpu;r->LastMigrationNanoseconds=KernelTime.GetMonotonicNanoseconds();}
                r->State=(UInt32)KernelThreadState.Ready;accepted=true;
            }
            else if(r->State==(UInt32)KernelThreadState.Ready)
            {
                if(KernelThreadPlacementServices.TryRebalance(new KernelRunnableThreadHandle(slot+1U),ownerCpu,out UInt32 balanced)){if(r->ProcessorIndex!=balanced){r->ProcessorIndex=balanced;r->LastMigrationNanoseconds=KernelTime.GetMonotonicNanoseconds();}ownerCpu=balanced;}accepted=true;
            }
            else if(r->State==(UInt32)KernelThreadState.Running)accepted=true;
        }
        ExitSchedulerLock();
        // The Ready publication occurs before the IPI. Repeated notifications also kick a
        // thread that is already Ready, which recovers from an earlier consumed/early IPI.
        if(accepted&&ownerCpu!=NoProcessor)WakeProcessor(ownerCpu);
        return accepted;
    }

    /// <summary>Gets the thread currently executing on the calling processor.</summary>
    public static Boolean TryGetCurrentThreadId(out UInt64 threadId)
    {
        threadId=0UL;
        if (!_initialized || _cpus==null || !KernelSmp.TryGetCurrentProcessor(out KernelProcessorState cpu) || cpu.Index>=_processorCount) return false;
        UInt32 slot=(_cpus+cpu.Index)->CurrentSlot;
        if (slot==0xFFFFFFFFU || slot>=MaximumThreads) return false;
        ThreadRecord* record=_threads+slot;
        if (record->Id==0UL || record->State==(UInt32)KernelThreadState.Unused || record->State==(UInt32)KernelThreadState.Terminated) return false;
        threadId=record->Id;
        return true;
    }

    /// <summary>Gets a stable snapshot of a thread.</summary>
    public static Boolean TryGetThread(UInt64 threadId, out KernelThreadInfo info)
    {
        info=default; if (!FindThread(threadId,out UInt32 slot)) return false; ThreadRecord* r=_threads+slot;
        info=new KernelThreadInfo(r->Id,(KernelThreadState)r->State,(KernelThreadPriority)r->Priority,r->ProcessorIndex,r->AffinityMask,r->StackBase,r->StackTop,r->EntryPoint,r->Argument); return true;
    }

    /// <summary>Changes a thread affinity mask while preserving at least one allowed processor.</summary>
    public static Boolean SetAffinity(UInt64 threadId, UInt64 affinityMask)
    {
        if(affinityMask==0UL||!FindThread(threadId,out UInt32 slot))return false;ThreadRecord* r=_threads+slot;r->AffinityMask=affinityMask;
        if((r->State==(UInt32)KernelThreadState.Ready||r->State==(UInt32)KernelThreadState.Blocked)&&!KernelSchedulerMath.AllowsProcessor(affinityMask,r->ProcessorIndex))
        { if(!KernelThreadPlacementServices.TrySelectInitial(affinityMask,out UInt32 ownerCpu))return false;r->ProcessorIndex=ownerCpu;r->LastMigrationNanoseconds=KernelTime.GetMonotonicNanoseconds();if(r->State==(UInt32)KernelThreadState.Ready)WakeProcessor(ownerCpu); }
        return true;
    }

    /// <summary>Blocks a non-terminated thread until Wake is requested.</summary>
    public static Boolean Block(UInt64 threadId)
    {
        if(!_initialized)return false;Boolean blocked=false;EnterSchedulerLock();
        if(FindThread(threadId,out UInt32 slot)){ThreadRecord* r=_threads+slot;if(r->State!=(UInt32)KernelThreadState.Terminated){r->State=(UInt32)KernelThreadState.Blocked;NativeAotGarbageCollector.SetThreadAtSafepoint(r->Id,true);blocked=true;}}
        ExitSchedulerLock();return blocked;
    }

    /// <summary>Makes a blocked thread runnable again.</summary>
    public static Boolean Wake(UInt64 threadId)
    {
        if(!_initialized)return false;
        UInt32 ownerCpu=NoProcessor;Boolean wake=false;
        EnterSchedulerLock();
        if(FindThread(threadId,out UInt32 slot))
        {
            ThreadRecord* r=_threads+slot;
            if(r->State==(UInt32)KernelThreadState.Blocked)
            {
                // A generic wake uses the same residency/hysteresis policy as role-worker
                // notification; a wake should not make a warm thread bounce between CPUs.
                ownerCpu=r->ProcessorIndex;if(!KernelThreadPlacementServices.TryRebalance(new KernelRunnableThreadHandle(slot+1U),ownerCpu,out ownerCpu)){ExitSchedulerLock();return false;}if(r->ProcessorIndex!=ownerCpu){r->ProcessorIndex=ownerCpu;r->LastMigrationNanoseconds=KernelTime.GetMonotonicNanoseconds();}
                // Publish Ready while holding the same lock used by AP selection.  The IPI is
                // sent only after the lock is released, so the target CPU cannot wake and miss
                // the state transition because of an unsynchronised cross-CPU write.
                r->State=(UInt32)KernelThreadState.Ready;wake=true;
            }
        }
        ExitSchedulerLock();
        if(wake)WakeProcessor(ownerCpu);
        return wake;
    }

    /// <summary>Marks a thread terminated so it can no longer be selected.</summary>
    public static Boolean Terminate(UInt64 threadId)
    { if (!FindThread(threadId,out UInt32 slot)) return false; ThreadRecord* r=_threads+slot; r->State=(UInt32)KernelThreadState.Terminated; r->ProcessorIndex=NoProcessor; NativeAotGarbageCollector.SetThreadAtSafepoint(r->Id,true); return true; }

    /// <summary>Sets thread affinity using the formal CPU-affinity value object.</summary>
    public static Boolean SetAffinity(UInt64 threadId, KernelCpuAffinity affinity) => !affinity.IsEmpty() && SetAffinity(threadId, affinity.Mask);
}
