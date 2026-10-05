using System;
using System.Runtime;
using System.Runtime.InteropServices;
using Inu.Kernel.Acpi;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Heap;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Smp;

namespace Inu.Kernel.InterruptDispatch;

/// <summary>Owns freestanding managed interrupt dispatch, formal vector allocation and per-CPU interrupt runtime state.</summary>
public static unsafe partial class KernelInterruptDispatch
{
    private static Boolean ServiceDeferredWork()
    {
        UInt64 pending=0UL;fixed(UInt64* p=&_deferredPendingInterrupts)if(!Native.AtomicExchange64(p,0UL,&pending))return false;if(pending==0UL)return false;
        UInt64 previous=0UL;fixed(UInt64* p=&_deferredServicedInterrupts)if(!Native.AtomicFetchAdd64(p,pending,&previous))return false;return false;
    }
    private static void QueueDeferredInterruptObservation()
    { UInt64 previous=0UL;fixed(UInt64* p=&_deferredPendingInterrupts)Native.AtomicFetchAdd64(p,1UL,&previous); }
    /// <summary>Gets interrupt observations waiting for the scheduler-managed Interrupts-role worker.</summary>
    public static UInt64 GetDeferredPendingInterruptCount(){UInt64 value=0UL;fixed(UInt64* p=&_deferredPendingInterrupts)Native.AtomicLoad64(p,&value);return value;}
    /// <summary>Gets interrupt observations already consumed by the scheduler-managed Interrupts-role worker.</summary>
    public static UInt64 GetDeferredServicedInterruptCount(){UInt64 value=0UL;fixed(UInt64* p=&_deferredServicedInterrupts)Native.AtomicLoad64(p,&value);return value;}
    /// <summary>Gets the scheduler thread assigned to the Interrupts CPU role.</summary>
    public static UInt64 GetDeferredWorkerThreadId()=>KernelScheduler.GetRoleWorkerThreadId(KernelCpuRole.Interrupts);
}
