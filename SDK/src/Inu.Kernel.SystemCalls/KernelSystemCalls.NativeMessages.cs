using System;
using System.Runtime;
using Inu.Kernel.Heap;
using Inu.Kernel.Contracts;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Protection;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Smp;
using Inu.Kernel.Security;
using Inu.Kernel.Time;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.SystemCalls;

public static unsafe partial class KernelSystemCalls
{
    private static Int64 ReadAndValidateNativeMessage(UInt64 userEnvelope,KernelSystemCallOperation operation,out KernelSystemCallMessage message)
    {
        message=default;if(userEnvelope==0UL)return (Int64)KernelSystemCallError.InvalidArgument;
        KernelSystemCallMessage local=default;
        if(!TryCopyFromUser(userEnvelope,(UInt64)(nuint)(&local),KernelSystemCallMessage.SerializedBytes))return (Int64)KernelSystemCallError.Fault;
        message=local;
        if(!KernelSystemCallMath.IsValidNativeEnvelopeShape(message))return (Int64)KernelSystemCallError.InvalidArgument;
        Byte* app=stackalloc Byte[(Int32)KernelSystemCallMessage.MaximumAppNameBytes];Byte* semantic=stackalloc Byte[(Int32)KernelSystemCallMessage.MaximumMessageBytes];
        if(!TryCopyFromUser(message.AppNameAddress,(UInt64)(nuint)app,message.AppNameLength)||!TryCopyFromUser(message.MessageAddress,(UInt64)(nuint)semantic,message.MessageLength))return (Int64)KernelSystemCallError.Fault;
        for(UInt64 i=0UL;i<message.AppNameLength;i++)if(app[i]<32U||app[i]>126U)return (Int64)KernelSystemCallError.InvalidArgument;
        for(UInt64 i=0UL;i<message.MessageLength;i++)if(semantic[i]<32U||semantic[i]>126U)return (Int64)KernelSystemCallError.InvalidArgument;
        if(!KernelSecurity.TryGetCurrentProcess(out UInt64 currentPid)||currentPid==0UL)return (Int64)KernelSystemCallError.NotPermitted;
        Boolean bootstrap=operation==KernelSystemCallOperation.Get&&message.ProcessId==0UL&&AsciiEquals(semantic,message.MessageLength,KernelSystemCallMessages.ProcessIdCurrent);
        if(!bootstrap&&message.ProcessId!=currentPid)return (Int64)KernelSystemCallError.NotPermitted;
        if(message.DataLength!=0UL&&!ValidateUserRange(message.DataAddress,message.DataLength,false))return (Int64)KernelSystemCallError.Fault;
        if(message.OutputCapacity!=0UL&&!ValidateUserRange(message.OutputAddress,message.OutputCapacity,true))return (Int64)KernelSystemCallError.Fault;
        return 0L;
    }

    private static Boolean AsciiEquals(Byte* value,UInt64 length,String text)
    {
        if(value==null||text==null||length!=(UInt64)text.Length)return false;for(UInt64 i=0UL;i<length;i++)if(value[i]!=(Byte)text[(Int32)i])return false;return true;
    }

    private static Int64 GetSystemVersionMessage(KernelSystemCallFrame* frame)=>1L;
    private static Int64 GetMonotonicTimeMessage(KernelSystemCallFrame* frame)=>unchecked((Int64)KernelTime.GetMonotonicNanoseconds());
    private static Int64 GetOnlineCpuCountMessage(KernelSystemCallFrame* frame)=>KernelSmp.GetOnlineProcessorCount();
    private static Int64 GetSchedulerQuantumMessage(KernelSystemCallFrame* frame)=>unchecked((Int64)KernelScheduler.GetQuantumNanoseconds());
    private static Int64 SetSchedulerQuantumMessage(KernelSystemCallFrame* frame)=>KernelScheduler.SetQuantumNanoseconds(frame->NativeMessage.Value0)?0L:(Int64)KernelSystemCallError.InvalidArgument;
    private static Int64 SchedulerYieldMessage(KernelSystemCallFrame* frame)=>0L;

    private static Boolean ValidateUserRange(UInt64 address, UInt64 byteCount, Boolean write)
    {
        if (byteCount==0UL)return true;if (!KernelSecurity.TryGetCurrentProcess(out UInt64 processId)) return false;
        KernelUserMemoryAccess access=KernelUserMemoryAccess.Read; if(write)access|=KernelUserMemoryAccess.Write;
        return KernelSecurity.TryValidateUserPointer(processId,address,byteCount,access);
    }
}
