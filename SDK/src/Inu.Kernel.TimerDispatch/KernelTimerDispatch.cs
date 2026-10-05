using System;
using Inu.Kernel.Heap;
using Inu.Kernel.InterruptDispatch;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Smp;
using Inu.Kernel.Time;

namespace Inu.Kernel.TimerDispatch;

/// <summary>Dispatches periodic kernel work from the Local APIC timer instead of busy polling loops.</summary>
public static unsafe class KernelTimerDispatch
{
    private struct Entry { internal Byte Used; internal UInt64 Callback,Cookie,PeriodTicks,NextTick; }
    private static Entry* _entries; private static KernelHeapAllocation _allocation; private static UInt32 _capacity,_count; private static UInt64 _tickNanoseconds,_tick,_roleServicePeriodTicks; private static Byte _vector; private static Boolean _initialized;
    /// <summary>Initializes periodic timer dispatch. One millisecond is the default kernel service quantum.</summary>
    public static Boolean Initialize()=>Initialize(1000000UL);
    /// <summary>Initializes periodic timer dispatch with an explicit tick duration.</summary>
    public static Boolean Initialize(UInt64 tickNanoseconds){if(_initialized)return true;if(tickNanoseconds==0UL||!KernelHeap.IsInitialized()||!KernelTime.IsInitialized||!KernelInterruptDispatch.IsInitialized())return false;if(!Allocate(16U,out _allocation,out _entries))return false;_capacity=16U;_tickNanoseconds=tickNanoseconds;_roleServicePeriodTicks=(10000000UL+tickNanoseconds-1UL)/tickNanoseconds;if(_roleServicePeriodTicks==0UL)_roleServicePeriodTicks=1UL;if(!KernelSchedulerTick.Configure(tickNanoseconds))return false;_vector=KernelInterruptDispatch.AllocateVector();if(_vector==0||!KernelInterruptDispatch.Register(_vector,&OnTimer,0UL)||!KernelTime.TryArmPeriodic(_vector,tickNanoseconds))return false;_initialized=true;return true;}
    /// <summary>Registers periodic work driven by timer interrupts.</summary>
    public static Boolean Register(UInt64 periodNanoseconds,delegate*<UInt64,Boolean> callback,UInt64 cookie,out UInt32 handle){handle=0;if(!_initialized||periodNanoseconds==0||callback==null)return false;UInt64 ticks=(periodNanoseconds+_tickNanoseconds-1UL)/_tickNanoseconds;if(ticks==0)ticks=1;Int32 slot=Free();if(slot<0){if(!Grow())return false;slot=Free();if(slot<0)return false;}Entry* e=_entries+slot;e->Used=1;e->Callback=(UInt64)(void*)callback;e->Cookie=cookie;e->PeriodTicks=ticks;e->NextTick=_tick+ticks;_count++;handle=(UInt32)slot+1U;return true;}
    /// <summary>Unregisters periodic timer-driven work.</summary>
    public static Boolean Unregister(UInt32 handle){Int32 i=(Int32)handle-1;if(!_initialized||i<0||(UInt32)i>=_capacity||(_entries+i)->Used==0)return false;Clear((Byte*)(_entries+i),(UInt64)sizeof(Entry));_count--;return true;}
    /// <summary>Gets the number of timer-dispatched callbacks.</summary>
    public static UInt32 GetCallbackCount()=>_count;
    /// <summary>Gets the hardware interrupt vector used by timer dispatch.</summary>
    public static Byte GetVector()=>_vector;
    private static Boolean OnTimer(Byte vector,UInt64 cookie){_tick++;KernelSchedulerTick.Advance();Boolean ok=true;for(UInt32 i=0;i<_capacity;i++){Entry* e=_entries+i;if(e->Used==0||_tick<e->NextTick)continue;e->NextTick=_tick+e->PeriodTicks;delegate*<UInt64,Boolean> cb=(delegate*<UInt64,Boolean>)(void*)e->Callback;ok=cb(e->Cookie)&ok;}if(_roleServicePeriodTicks!=0UL&&(_tick%_roleServicePeriodTicks)==0UL){KernelScheduler.NotifyRoleWork(KernelCpuRole.Userland);KernelScheduler.NotifyRoleWork(KernelCpuRole.Interrupts);}return ok;}
    private static Int32 Free(){for(UInt32 i=0;i<_capacity;i++)if((_entries+i)->Used==0)return (Int32)i;return -1;}
    private static Boolean Grow(){UInt32 next=_capacity>0x7FFFFFFFU?UInt32.MaxValue:_capacity*2U;if(next<=_capacity)return false;if(!Allocate(next,out KernelHeapAllocation a,out Entry* n))return false;for(UInt32 i=0;i<_capacity;i++)n[i]=_entries[i];if(!KernelHeap.TryRelease(_allocation)){KernelHeap.TryRelease(a);return false;}_allocation=a;_entries=n;_capacity=next;return true;}
    private static Boolean Allocate(UInt32 capacity,out KernelHeapAllocation allocation,out Entry* entries){allocation=default;entries=null;if(!KernelHeap.TryAllocate((UInt64)capacity*(UInt64)sizeof(Entry),64UL,true,out allocation))return false;entries=(Entry*)(nuint)allocation.Address;return true;}
    private static Boolean Clear(Byte* p,UInt64 n){for(UInt64 i=0;i<n;i++)p[i]=0;return true;}
}
