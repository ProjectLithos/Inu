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
    private const Byte SchedulerRescheduleIpiVector=0xF0;
    private const Byte FirstDynamicVector=0x40,LastDynamicVector=0xDF; private const UInt64 LocalApicEoi=0xB0UL; private const UInt32 MaximumProcessors=256U;
    private const Byte StateStopped=0,StateReady=1,StateRunning=2,StatePaused=3;
    [StructLayout(LayoutKind.Sequential,Pack=8)] private struct NativeInterruptContext { internal UInt64 Vector,ErrorCode,Rip,Cs,Rflags,Rsp,Ss,Cr0,Cr2,Cr3,Cr4,ProcessorId,PrivilegeTransition,Rax,Rbx,Rcx,Rdx,Rsi,Rdi,Rbp,R8,R9,R10,R11,R12,R13,R14,R15; }
    [StructLayout(LayoutKind.Sequential,Pack=8)] private struct ProcessorRuntime { internal UInt32 ProcessorId,ApicId; internal Byte Lifecycle,CurrentVector,Nesting,Reserved; internal UInt64 TotalInterrupts,HandledInterrupts,UnhandledInterrupts,ExceptionCount,LastVector; }
    /// <summary>Provides a stable snapshot of one processor's interrupt runtime state.</summary>
    public readonly struct ProcessorInterruptState
    {
        public readonly UInt32 ProcessorId,ApicId; public readonly Byte Lifecycle,CurrentVector,Nesting; public readonly UInt64 TotalInterrupts,HandledInterrupts,UnhandledInterrupts,ExceptionCount,LastVector;
        internal ProcessorInterruptState(UInt32 processorId,UInt32 apicId,Byte lifecycle,Byte currentVector,Byte nesting,UInt64 total,UInt64 handled,UInt64 unhandled,UInt64 exceptions,UInt64 lastVector){ProcessorId=processorId;ApicId=apicId;Lifecycle=lifecycle;CurrentVector=currentVector;Nesting=nesting;TotalInterrupts=total;HandledInterrupts=handled;UnhandledInterrupts=unhandled;ExceptionCount=exceptions;LastVector=lastVector;}
    }

    /// <summary>Describes the most recent unhandled CPL3 CPU exception contained by the interrupt boundary.</summary>
    public readonly struct UserFaultInfo
    {
        public readonly Byte Vector; public readonly UInt64 ErrorCode,Rip,Cs,Rflags,Rsp,Ss,Cr2,Cr3;
        internal UserFaultInfo(Byte vector,UInt64 errorCode,UInt64 rip,UInt64 cs,UInt64 rflags,UInt64 rsp,UInt64 ss,UInt64 cr2,UInt64 cr3)
        {Vector=vector;ErrorCode=errorCode;Rip=rip;Cs=cs;Rflags=rflags;Rsp=rsp;Ss=ss;Cr2=cr2;Cr3=cr3;}
    }
    private static UInt64* _callbacks; private static UInt64* _cookies; private static Byte* _allocated; private static ProcessorRuntime* _processors; private static KernelHeapAllocation _tables,_processorTable; private static Boolean _initialized; private static UInt64 _localApicBase; private static Byte _lifecycle;
    private static UInt64 _deferredPendingInterrupts,_deferredServicedInterrupts,_userCancellationHandler;
    private static UInt64 _lastUserFaultSequence,_lastUserFaultError,_lastUserFaultRip,_lastUserFaultCs,_lastUserFaultRflags,_lastUserFaultRsp,_lastUserFaultSs,_lastUserFaultCr2,_lastUserFaultCr3;
    private static Byte _lastUserFaultVector;
    /// <summary>Installs the managed native interrupt dispatcher without exposing IDT mechanics to drivers.</summary>
    public static Boolean Initialize(){if(_initialized)return true;if(!KernelHeap.IsInitialized()||!KernelAcpi.TryGetLocalApicAddress(out _localApicBase)||_localApicBase==0UL)return false;if(!KernelHeap.TryAllocate(4352UL,64UL,true,out _tables))return false;if(!KernelHeap.TryAllocate((UInt64)MaximumProcessors*(UInt64)sizeof(ProcessorRuntime),64UL,true,out _processorTable))return false;_callbacks=(UInt64*)(nuint)_tables.Address;_cookies=(UInt64*)(nuint)(_tables.Address+2048UL);_allocated=(Byte*)(nuint)(_tables.Address+4096UL);_processors=(ProcessorRuntime*)(nuint)_processorTable.Address;for(UInt32 i=0;i<MaximumProcessors;i++){_processors[i].ProcessorId=i;_processors[i].Lifecycle=StateReady;}_lifecycle=StateReady;if(!Native.InstallManagedInterruptDispatcher())return false;_initialized=true;if(!Register(SchedulerRescheduleIpiVector,&HandleSchedulerRescheduleIpi,0UL))return false;if(KernelScheduler.IsInitialized()&&!KernelScheduler.RegisterRoleWorker(KernelCpuRole.Interrupts,&ServiceDeferredWork,KernelThreadPriority.High))return false;return true;}
    /// <summary>Registers the process-runtime hook used to convert Ctrl-C into a controlled exit on the next user-origin hardware interrupt.</summary>


}

/// <summary>Stable public interrupt lifecycle facade. Routes all operations through the managed interrupt-dispatch runtime.</summary>
public static class Interrupts
{
    public static Boolean Initialize()=>KernelInterruptDispatch.Initialize();
    public static Boolean IsInitialized()=>KernelInterruptDispatch.IsInitialized();
    public static Byte GetLifecycleState()=>KernelInterruptDispatch.GetLifecycleState();
    public static Boolean Start()=>KernelInterruptDispatch.Start();
    public static Boolean Run()=>KernelInterruptDispatch.Run();
    public static Boolean Pause()=>KernelInterruptDispatch.Pause();
    public static Boolean Resume()=>KernelInterruptDispatch.Resume();
    public static Boolean Stop()=>KernelInterruptDispatch.Stop();
    public static Boolean Wait()=>KernelInterruptDispatch.Wait();
}
