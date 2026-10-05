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
    private static Boolean CreateApplicationProcessorIdleThreads()
    {
        UInt64 idleEntry=Native.GetSchedulerIdleThreadEntryPoint();
#if DEBUG
        DebugApSchedulerTrace("scheduler idle managed entrypoint = ", idleEntry);
#endif
        if(idleEntry==0UL)return false;
        UInt32 bootstrap=KernelSmp.GetBootstrapProcessorIndex();
        for(UInt32 cpu=0U;cpu<_processorCount;cpu++)
        {
            if(cpu==bootstrap)continue;if(cpu>=64U)return false;if(!FindUnusedSlot(out UInt32 slot))return false;
            if(!KernelHeap.TryAllocate(DefaultStackBytes,4096UL,true,out KernelHeapAllocation stack))return false;
            if(!KernelHeap.TryAllocate(ContextBytes,16UL,true,out KernelHeapAllocation context)){KernelHeap.TryRelease(stack);return false;}
            if(!Native.InitializeThreadContext(context.Address,stack.Address+stack.ByteCount,idleEntry,(UInt64)cpu)){KernelHeap.TryRelease(context);KernelHeap.TryRelease(stack);return false;}
            ThreadRecord* r=_threads+slot;r->Id=_nextThreadId++;r->StackBase=stack.Address;r->StackTop=stack.Address+stack.ByteCount;r->EntryPoint=idleEntry;r->Argument=cpu;r->AffinityMask=1UL<<(Int32)cpu;r->ContextAddress=context.Address;r->State=(UInt32)KernelThreadState.Ready;r->Priority=(UInt32)KernelThreadPriority.Low;r->ProcessorIndex=cpu;r->NextReady=0xFFFFFFFFU;r->ExecutionClass=(Byte)KernelCpuExecutionClass.Idle;r->LastMigrationNanoseconds=KernelTime.GetMonotonicNanoseconds();
            if(!NativeAotGarbageCollector.RegisterThreadStack(r->Id,(void*)(nuint)r->StackBase,(void*)(nuint)r->StackTop))return false;
            (_cpus+cpu)->IdleSlot=slot;_activeThreads++;
#if DEBUG
            DebugApSchedulerTrace("created AP idle thread for CPU = ", cpu);
            DebugApSchedulerTrace("AP idle thread id = ", r->Id);
            DebugApSchedulerTrace("AP idle thread context = ", r->ContextAddress);
            DebugApSchedulerTrace("AP idle thread stack top = ", r->StackTop);
#endif
        }
        return true;
    }

    private static UInt64 GetRoleWorkerCallback(KernelCpuRole role)
    {
        UInt64 value=0UL;
        if(role==KernelCpuRole.Kernel){fixed(UInt64* p=&_kernelWorkerCallback)Native.AtomicLoad64(p,&value);}
        else if(role==KernelCpuRole.Userland){fixed(UInt64* p=&_userlandWorkerCallback)Native.AtomicLoad64(p,&value);}
        else if(role==KernelCpuRole.Gui){fixed(UInt64* p=&_guiWorkerCallback)Native.AtomicLoad64(p,&value);}
        else if(role==KernelCpuRole.Drivers){fixed(UInt64* p=&_driverWorkerCallback)Native.AtomicLoad64(p,&value);}
        else if(role==KernelCpuRole.Interrupts){fixed(UInt64* p=&_interruptWorkerCallback)Native.AtomicLoad64(p,&value);}
        else if(role==KernelCpuRole.Networking){fixed(UInt64* p=&_networkingWorkerCallback)Native.AtomicLoad64(p,&value);}
        else if(role==KernelCpuRole.Storage){fixed(UInt64* p=&_storageWorkerCallback)Native.AtomicLoad64(p,&value);}
        else if(role==KernelCpuRole.Realtime){fixed(UInt64* p=&_realtimeWorkerCallback)Native.AtomicLoad64(p,&value);}
        else if(role==KernelCpuRole.Background){fixed(UInt64* p=&_backgroundWorkerCallback)Native.AtomicLoad64(p,&value);}
        return value;
    }
    private static Boolean SetRoleWorkerCallback(KernelCpuRole role,UInt64 value)
    {
        if(role==KernelCpuRole.Kernel){fixed(UInt64* p=&_kernelWorkerCallback)return Native.AtomicStore64(p,value);}
        if(role==KernelCpuRole.Userland){fixed(UInt64* p=&_userlandWorkerCallback)return Native.AtomicStore64(p,value);}
        if(role==KernelCpuRole.Gui){fixed(UInt64* p=&_guiWorkerCallback)return Native.AtomicStore64(p,value);}
        if(role==KernelCpuRole.Drivers){fixed(UInt64* p=&_driverWorkerCallback)return Native.AtomicStore64(p,value);}
        if(role==KernelCpuRole.Interrupts){fixed(UInt64* p=&_interruptWorkerCallback)return Native.AtomicStore64(p,value);}
        if(role==KernelCpuRole.Networking){fixed(UInt64* p=&_networkingWorkerCallback)return Native.AtomicStore64(p,value);}
        if(role==KernelCpuRole.Storage){fixed(UInt64* p=&_storageWorkerCallback)return Native.AtomicStore64(p,value);}
        if(role==KernelCpuRole.Realtime){fixed(UInt64* p=&_realtimeWorkerCallback)return Native.AtomicStore64(p,value);}
        if(role==KernelCpuRole.Background){fixed(UInt64* p=&_backgroundWorkerCallback)return Native.AtomicStore64(p,value);}
        return false;
    }

    private static Boolean SetRoleWorkerPending(KernelCpuRole role)
    {
        if(role==KernelCpuRole.Kernel){fixed(UInt64* p=&_kernelWorkerPending)return Native.AtomicStore64(p,1UL);}
        if(role==KernelCpuRole.Userland){fixed(UInt64* p=&_userlandWorkerPending)return Native.AtomicStore64(p,1UL);}
        if(role==KernelCpuRole.Gui){fixed(UInt64* p=&_guiWorkerPending)return Native.AtomicStore64(p,1UL);}
        if(role==KernelCpuRole.Drivers){fixed(UInt64* p=&_driverWorkerPending)return Native.AtomicStore64(p,1UL);}
        if(role==KernelCpuRole.Interrupts){fixed(UInt64* p=&_interruptWorkerPending)return Native.AtomicStore64(p,1UL);}
        if(role==KernelCpuRole.Networking){fixed(UInt64* p=&_networkingWorkerPending)return Native.AtomicStore64(p,1UL);}
        if(role==KernelCpuRole.Storage){fixed(UInt64* p=&_storageWorkerPending)return Native.AtomicStore64(p,1UL);}
        if(role==KernelCpuRole.Realtime){fixed(UInt64* p=&_realtimeWorkerPending)return Native.AtomicStore64(p,1UL);}
        if(role==KernelCpuRole.Background){fixed(UInt64* p=&_backgroundWorkerPending)return Native.AtomicStore64(p,1UL);}
        return false;
    }

    private static Boolean ConsumeRoleWorkerPending(KernelCpuRole role)
    {
        UInt64 previous=0UL;
        if(role==KernelCpuRole.Kernel){fixed(UInt64* p=&_kernelWorkerPending)return Native.AtomicExchange64(p,0UL,&previous)&&previous!=0UL;}
        if(role==KernelCpuRole.Userland){fixed(UInt64* p=&_userlandWorkerPending)return Native.AtomicExchange64(p,0UL,&previous)&&previous!=0UL;}
        if(role==KernelCpuRole.Gui){fixed(UInt64* p=&_guiWorkerPending)return Native.AtomicExchange64(p,0UL,&previous)&&previous!=0UL;}
        if(role==KernelCpuRole.Drivers){fixed(UInt64* p=&_driverWorkerPending)return Native.AtomicExchange64(p,0UL,&previous)&&previous!=0UL;}
        if(role==KernelCpuRole.Interrupts){fixed(UInt64* p=&_interruptWorkerPending)return Native.AtomicExchange64(p,0UL,&previous)&&previous!=0UL;}
        if(role==KernelCpuRole.Networking){fixed(UInt64* p=&_networkingWorkerPending)return Native.AtomicExchange64(p,0UL,&previous)&&previous!=0UL;}
        if(role==KernelCpuRole.Storage){fixed(UInt64* p=&_storageWorkerPending)return Native.AtomicExchange64(p,0UL,&previous)&&previous!=0UL;}
        if(role==KernelCpuRole.Realtime){fixed(UInt64* p=&_realtimeWorkerPending)return Native.AtomicExchange64(p,0UL,&previous)&&previous!=0UL;}
        if(role==KernelCpuRole.Background){fixed(UInt64* p=&_backgroundWorkerPending)return Native.AtomicExchange64(p,0UL,&previous)&&previous!=0UL;}
        return false;
    }
    private static UInt64 GetRoleWorkerThread(KernelCpuRole role)
    { if(role==KernelCpuRole.Kernel)return _kernelWorkerThread;if(role==KernelCpuRole.Userland)return _userlandWorkerThread;if(role==KernelCpuRole.Gui)return _guiWorkerThread;if(role==KernelCpuRole.Drivers)return _driverWorkerThread;if(role==KernelCpuRole.Interrupts)return _interruptWorkerThread;if(role==KernelCpuRole.Networking)return _networkingWorkerThread;if(role==KernelCpuRole.Storage)return _storageWorkerThread;if(role==KernelCpuRole.Realtime)return _realtimeWorkerThread;if(role==KernelCpuRole.Background)return _backgroundWorkerThread;return 0UL; }
    private static void SetRoleWorkerThread(KernelCpuRole role,UInt64 value)
    { if(role==KernelCpuRole.Kernel)_kernelWorkerThread=value;else if(role==KernelCpuRole.Userland)_userlandWorkerThread=value;else if(role==KernelCpuRole.Gui)_guiWorkerThread=value;else if(role==KernelCpuRole.Drivers)_driverWorkerThread=value;else if(role==KernelCpuRole.Interrupts)_interruptWorkerThread=value;else if(role==KernelCpuRole.Networking)_networkingWorkerThread=value;else if(role==KernelCpuRole.Storage)_storageWorkerThread=value;else if(role==KernelCpuRole.Realtime)_realtimeWorkerThread=value;else if(role==KernelCpuRole.Background)_backgroundWorkerThread=value; }

}
