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
    /// <summary>Creates a ready kernel thread with a page-aligned kernel stack and initial x64 entry context.</summary>
    public static Boolean TryCreateThread(UInt64 entryPoint, UInt64 argument, KernelThreadPriority priority, UInt64 affinityMask, UInt64 stackBytes, out UInt64 threadId)
    {
        threadId=0UL; if (!_initialized || entryPoint==0UL || affinityMask==0UL) return false;
        if (stackBytes==0UL) stackBytes=DefaultStackBytes;
        if (!KernelSchedulerMath.IsValidStackSize(stackBytes)) return false;
        if (!FindUnusedSlot(out UInt32 slot)) return false;
        if (!KernelHeap.TryAllocate(stackBytes, 4096UL, true, out KernelHeapAllocation stack)) return false;
        if (!KernelHeap.TryAllocate(ContextBytes, 16UL, true, out KernelHeapAllocation context)) { KernelHeap.TryRelease(stack); return false; }
        if (!Native.InitializeThreadContext(context.Address, stack.Address+stack.ByteCount, entryPoint, argument)) { KernelHeap.TryRelease(context); KernelHeap.TryRelease(stack); return false; }
        if(!KernelThreadPlacementServices.TrySelectInitial(affinityMask,out UInt32 ownerCpu)){KernelHeap.TryRelease(context);KernelHeap.TryRelease(stack);return false;}
        ThreadRecord* r=_threads+slot; r->Id=_nextThreadId++; r->StackBase=stack.Address; r->StackTop=stack.Address+stack.ByteCount; r->EntryPoint=entryPoint; r->Argument=argument; r->AffinityMask=affinityMask; r->ContextAddress=context.Address; r->StackAllocation=stack; r->ContextAllocation=context; r->State=(UInt32)KernelThreadState.Ready; r->Priority=(UInt32)priority; r->ProcessorIndex=ownerCpu; r->NextReady=0xFFFFFFFFU; r->ExecutionClass=(Byte)KernelCpuExecutionClass.Kernel; r->OneShot=0; r->RetireReady=0; r->LastMigrationNanoseconds=KernelTime.GetMonotonicNanoseconds();
        if(!NativeAotGarbageCollector.RegisterThreadStack(r->Id,(void*)(nuint)r->StackBase,(void*)(nuint)r->StackTop)){r->Id=0UL;r->State=(UInt32)KernelThreadState.Unused;KernelHeap.TryRelease(context);KernelHeap.TryRelease(stack);return false;}
        _activeThreads++; threadId=r->Id; WakeProcessor(ownerCpu); return true;
    }

    /// <summary>Creates a short-lived scheduler thread that invokes one managed callback and then parks itself.</summary>
    public static Boolean TryCreateOneShotThread(delegate*<void> callback, KernelThreadPriority priority, UInt64 affinityMask, UInt64 stackBytes, out UInt64 threadId)
    {
        threadId=0UL;if(callback==null)return false;UInt64 entry=Native.GetSchedulerOneShotThreadEntryPoint();if(entry==0UL)return false;
        if(!TryCreateThread(entry,(UInt64)(void*)callback,priority,affinityMask,stackBytes,out threadId))return false;
        EnterSchedulerLock();
        if(FindThread(threadId,out UInt32 slot)){ThreadRecord* r=_threads+slot;r->OneShot=1;r->RetireReady=0;}
        ExitSchedulerLock();
        return true;
    }

    /// <summary>Creates a thread using the OS-author-selected CPU role as its affinity policy.</summary>
    public static Boolean TryCreateThreadForRole(UInt64 entryPoint,UInt64 argument,KernelThreadPriority priority,KernelCpuRole role,UInt64 stackBytes,out UInt64 threadId)
    { KernelCpuSet set=KernelSmp.GetRoleCpuSet(role);if(!TryCreateThread(entryPoint,argument,priority,set.Cpu0To63,stackBytes,out threadId))return false;if(!FindThread(threadId,out UInt32 slot))return false;(_threads+slot)->ExecutionClass=(Byte)ExecutionClassForRole(role);return true; }

    /// <summary>Registers one persistent scheduler-managed worker for a CPU role. The worker blocks when the callback reports no work and is woken by NotifyRoleWork.</summary>
    public static Boolean RegisterRoleWorker(KernelCpuRole role,delegate*<Boolean> service,KernelThreadPriority priority)
    {
        if(!_initialized||service==null)return false;
        UInt64 existing=GetRoleWorkerThread(role);
        if(!SetRoleWorkerCallback(role,(UInt64)(void*)service))return false;
        if(existing!=0UL)return true;
        UInt64 entry=Native.GetSchedulerRoleWorkerEntryPoint();if(entry==0UL)return false;
        if(!TryCreateThreadForRole(entry,(UInt64)(Byte)role,priority,role,DefaultStackBytes,out UInt64 threadId))return false;
        SetRoleWorkerThread(role,threadId);return true;
    }

    /// <summary>Wakes the scheduler-managed worker assigned to a role after new work has been queued.</summary>
    public static Boolean NotifyRoleWork(KernelCpuRole role)
    {
        // Publish a durable work indication before looking at the worker state.  If the worker
        // is Running it cannot be made Ready yet, but it will consume this latch before it
        // commits to Blocked.  If it is already Blocked, NotifyThreadWork wakes it normally.
        if(!SetRoleWorkerPending(role))return false;
        UInt64 threadId=GetRoleWorkerThread(role);
        return threadId!=0UL&&NotifyThreadWork(threadId);
    }

    /// <summary>Gets the worker thread id registered for a role, or zero when that role has no persistent worker.</summary>
    public static UInt64 GetRoleWorkerThreadId(KernelCpuRole role)=>GetRoleWorkerThread(role);
}
