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
    [RuntimeExport("InuManagedInterruptDispatch")]
    private static Int32 Dispatch(UInt64 contextAddress)
    {
        if(contextAddress==0UL)return 3;NativeInterruptContext* c=(NativeInterruptContext*)(nuint)contextAddress;UInt64 raw=c->Vector;if(raw>255UL)return 3;Byte vector=(Byte)raw;
        Boolean fromUser=(c->Cs&3UL)==3UL;UInt32 pid=c->ProcessorId<MaximumProcessors?(UInt32)c->ProcessorId:0U;ProcessorRuntime* p=_processors+pid;
        KernelScheduler.NotifyInterruptEnter(pid,vector!=SchedulerRescheduleIpiVector);if(vector==SchedulerRescheduleIpiVector)KernelScheduler.EnterGarbageCollectionSafepoint(pid);
        p->ProcessorId=pid;p->ApicId=Native.GetCurrentApicId();p->CurrentVector=vector;if(p->Nesting<255)p->Nesting++;p->TotalInterrupts++;p->LastVector=vector;if(vector<32)p->ExceptionCount++;
        Boolean handled=vector==0xFF||vector==2;Boolean dispatchAllowed=vector<32||(_lifecycle==StateRunning&&p->Lifecycle!=StatePaused&&p->Lifecycle!=StateStopped);
        if(dispatchAllowed&&!handled){UInt64 address=_callbacks[vector];if(address!=0UL){delegate*<Byte,UInt64,Boolean> cb=(delegate*<Byte,UInt64,Boolean>)(void*)address;handled=cb(vector,_cookies[vector]);}}
        if(handled)p->HandledInterrupts++;else p->UnhandledInterrupts++;if(vector>=32&&vector!=SchedulerRescheduleIpiVector&&vector!=0xFF)QueueDeferredInterruptObservation();if(vector>=32&&vector!=0xFF&&_localApicBase!=0UL)Native.WriteMmio32(_localApicBase+LocalApicEoi,0U);
        if(vector<32&&!handled&&fromUser)RecordUserFault(c,vector);
        Boolean cancelUser=false;if(fromUser&&vector>=32&&_userCancellationHandler!=0UL){delegate*<Boolean> cancel=(delegate*<Boolean>)(void*)_userCancellationHandler;cancelUser=cancel();}
        if(p->Nesting!=0)p->Nesting--;p->CurrentVector=0;KernelScheduler.NotifyInterruptExit(pid);
        if(vector<32&&!handled)return fromUser?4:3;if(cancelUser)return 5;if(!dispatchAllowed&&vector>=32)return 1;return handled?1:0;
    }
}
