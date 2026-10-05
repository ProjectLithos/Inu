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
    private static Boolean CreateBootstrapThread()
    {
        if (!KernelSmp.TryGetCurrentProcessor(out KernelProcessorState cpu)) return false;
        if (!KernelHeap.TryAllocate(ContextBytes,16UL,true,out KernelHeapAllocation context)) return false;
        UInt64 stackBase=Native.GetBootstrapStackBase(),stackTop=Native.GetBootstrapStackTop();
        if(stackBase==0UL||stackTop<=stackBase){KernelHeap.TryRelease(context);return false;}
        ThreadRecord* r=_threads; r->Id=_nextThreadId++; r->StackBase=stackBase;r->StackTop=stackTop;r->State=(UInt32)KernelThreadState.Running; r->Priority=(UInt32)KernelThreadPriority.Critical; r->ProcessorIndex=cpu.Index; r->AffinityMask=cpu.Index<64U ? 1UL<<(Int32)cpu.Index : 0xFFFFFFFFFFFFFFFFUL; r->ContextAddress=context.Address; r->ExecutionClass=(Byte)KernelCpuExecutionClass.Kernel; r->LastMigrationNanoseconds=KernelTime.GetMonotonicNanoseconds();
        if(!NativeAotGarbageCollector.RegisterThreadStack(r->Id,(void*)(nuint)stackBase,(void*)(nuint)stackTop)){KernelHeap.TryRelease(context);return false;}
        NativeAotGarbageCollector.SetThreadAtSafepoint(r->Id,false);
        _cpus[cpu.Index].CurrentSlot=0U; _activeThreads=1U; return true;
    }
    private static Boolean FindUnusedSlot(out UInt32 slot)
    { slot=0U; for(UInt32 i=1U;i<MaximumThreads;i++) if((_threads+i)->State==(UInt32)KernelThreadState.Unused){slot=i;return true;} return false; }
    private static Boolean FindThread(UInt64 id,out UInt32 slot)
    { slot=0U; if(id==0UL||_threads==null)return false; for(UInt32 i=0U;i<MaximumThreads;i++) if((_threads+i)->Id==id && (_threads+i)->State!=(UInt32)KernelThreadState.Unused){slot=i;return true;} return false; }
}
