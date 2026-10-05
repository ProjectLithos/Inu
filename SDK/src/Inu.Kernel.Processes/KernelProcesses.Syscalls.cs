using System;
using System.Runtime;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Heap;
using Inu.Kernel.Gui;
using KernelFaultInjection = Inu.Kernel.Contracts.KernelFaultInjection;
using Inu.Kernel.InterruptDispatch;
using Inu.Kernel.Storage;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Memory;
using Inu.Kernel.Protection;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Security;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Smp;
using Inu.Kernel.VirtualMemory;
using Inu.ApplicationFormat;

namespace Inu.Kernel.Processes;

public static unsafe partial class KernelProcesses
{
    private const UInt32 LinuxGetPidService=39U, LinuxExitService=60U;

private static Int64 GetCurrentProcessIdSyscall(KernelSystemCallFrame* frame)=>TryGetCurrentProcessId(out UInt64 id)?unchecked((Int64)id):(Int64)KernelSystemCallError.NotFound;


private static Int64 CommandCompleteEventSyscall(KernelSystemCallFrame* frame)
    {
        if(frame==null||!TryGetCurrentProcessId(out UInt64 id)||id==0UL||GetRunningProcessId()!=id)return (Int64)KernelSystemCallError.NotPermitted;Int64 code=frame->Abi==KernelSystemCallAbi.GetSetEvent?unchecked((Int64)frame->NativeMessage.Value0):unchecked((Int64)frame->Argument0);if(!SetPendingExitForCurrentProcessor(id,code,false))return (Int64)KernelSystemCallError.Fault;return Native.RequestUserModeExit(code)?0L:(Int64)KernelSystemCallError.Fault;
    }

private static Int64 SetProcessControlSyscall(KernelSystemCallFrame* frame)
    {
        if(frame==null)return (Int64)KernelSystemCallError.InvalidArgument;UInt64 target=frame->Abi==KernelSystemCallAbi.GetSetEvent?frame->NativeMessage.Value0:frame->Argument0;UInt64 rawControl=frame->Abi==KernelSystemCallAbi.GetSetEvent?frame->NativeMessage.Value1:frame->Argument1;if((KernelProcessControl)(Byte)rawControl!=KernelProcessControl.Kill)return (Int64)KernelSystemCallError.InvalidArgument;if(!TryGetCurrentProcessId(out UInt64 id)||id==0UL||GetRunningProcessId()!=id||target!=id)return (Int64)KernelSystemCallError.NotPermitted;const Int64 code=-1L;if(!SetPendingExitForCurrentProcessor(id,code,true))return (Int64)KernelSystemCallError.Fault;return Native.RequestUserModeExit(code)?0L:(Int64)KernelSystemCallError.Fault;
    }

private static Int64 LinuxExitCompatibilitySyscall(KernelSystemCallFrame* frame)
    {
        if(frame==null||!TryGetCurrentProcessId(out UInt64 id)||id==0UL||GetRunningProcessId()!=id)return (Int64)KernelSystemCallError.NotPermitted;Int64 code=frame->Abi==KernelSystemCallAbi.GetSetEvent?unchecked((Int64)frame->NativeMessage.Value0):unchecked((Int64)frame->Argument0);if(!SetPendingExitForCurrentProcessor(id,code,true))return (Int64)KernelSystemCallError.Fault;return Native.RequestUserModeExit(code)?0L:(Int64)KernelSystemCallError.Fault;
    }
public static Boolean RequestCurrentProcessExit(Int64 code,Boolean kill)
    {
        if(!TryGetCurrentProcessId(out UInt64 id)||id==0UL||GetRunningProcessId()!=id)return false;
        return SetPendingExitForCurrentProcessor(id,code,kill)&&Native.RequestUserModeExit(code);
    }

}
