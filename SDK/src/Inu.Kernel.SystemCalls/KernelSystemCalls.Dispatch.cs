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
    public static Int64 Dispatch(UInt64 encoded, UInt64 a0, UInt64 a1, UInt64 a2, UInt64 a3, UInt64 a4, UInt64 a5)
    {
        if(KernelFaultInjection.ShouldInject(KernelFaultKind.SyscallDenied,"syscall",out _)) return (Int64)KernelSystemCallError.NotPermitted;
        if (!_initialized || !KernelSystemCallMath.TryDecodeAbi(encoded, out KernelSystemCallAbi abi)) return (Int64)KernelSystemCallError.NotImplemented;
        if(abi==KernelSystemCallAbi.GetSetEvent)
        {
            if(!KernelSystemCallMath.IsNativeEncoding(encoded))return (Int64)KernelSystemCallError.InvalidArgument;
            KernelSystemCallOperation operation=KernelSystemCallMath.GetOperation(encoded);
            if (!KernelSecurity.TryValidateCurrentSyscall((UInt32)abi,(UInt32)operation)) return (Int64)KernelSystemCallError.NotPermitted;
            Int64 validation=ReadAndValidateNativeMessage(a0,operation,out KernelSystemCallMessage message);if(validation!=0L)return validation;
            KernelSystemCallFrame frame=new(abi,operation,0U,a0,a1,a2,a3,a4,a5,message);
            UInt64 cancellationHandler=_nativeCancellationHandler;
            if(cancellationHandler!=0UL)
            {
                delegate*<KernelSystemCallFrame*, Boolean> cancel=(delegate*<KernelSystemCallFrame*, Boolean>)(void*)cancellationHandler;
                if(cancel(&frame))return 0L;
            }
            return DispatchNative(&frame);
        }
        UInt32 service=KernelSystemCallMath.GetServiceNumber(encoded);
        if (!KernelSecurity.TryValidateCurrentSyscall((UInt32)abi,service)) return (Int64)KernelSystemCallError.NotPermitted;
        KernelSystemCallFrame compatibilityFrame=new(abi,KernelSystemCallOperation.Get,service,a0,a1,a2,a3,a4,a5,default);
        if (abi==KernelSystemCallAbi.Linux) return DispatchLinux(&compatibilityFrame);
        return DispatchNt(&compatibilityFrame);
    }

    [RuntimeExport("InuManagedSyscallDispatch")]
    private static Int64 NativeDispatch(UInt64 encoded, UInt64 a0, UInt64 a1, UInt64 a2, UInt64 a3, UInt64 a4, UInt64 a5)
    {
#if DEBUG
        UInt64 sequence=NextDiagnosticSequence();
        Boolean trace=sequence<=DiagnosticTraceLimit;
        if(trace)TraceSyscallEnter(sequence,encoded,a0);
        Int64 result=Dispatch(encoded,a0,a1,a2,a3,a4,a5);
        if(trace)TraceSyscallExit(sequence,result);
        return result;
#else
        // Per-syscall serial tracing is intentionally absent from normal/Release
        // kernels. QEMU serial I/O is orders of magnitude slower than the syscall
        // itself and made ordinary shell input and commands appear unresponsive.
        return Dispatch(encoded,a0,a1,a2,a3,a4,a5);
#endif
    }

    private static UInt64 NextDiagnosticSequence()
    {
        UInt64 previous=0UL;fixed(UInt64* p=&_diagnosticSequence){if(!Native.AtomicFetchAdd64(p,1UL,&previous))return DiagnosticTraceLimit+1UL;}return previous+1UL;
    }

    private static void TraceSyscallEnter(UInt64 sequence,UInt64 encoded,UInt64 a0)
    {
        if(!Native.BeginSerialRecord())return;
        TraceText("[SC ");TraceHex(sequence);TraceText("] > ");if(KernelSystemCallMath.TryDecodeAbi(encoded,out KernelSystemCallAbi abi)&&abi==KernelSystemCallAbi.GetSetEvent){KernelSystemCallOperation op=KernelSystemCallMath.GetOperation(encoded);Native.WriteSerial((Byte)(op==KernelSystemCallOperation.Get?'G':op==KernelSystemCallOperation.Set?'S':'E'));TraceText(" msg@");TraceHex(a0);}else{TraceText("compat svc=");TraceHex(KernelSystemCallMath.GetServiceNumber(encoded));}TraceNewLine();
        Native.EndSerialRecord();
    }

    private static void TraceSyscallExit(UInt64 sequence,Int64 result)
    { if(!Native.BeginSerialRecord())return;TraceText("[SC ");TraceHex(sequence);TraceText("] < rc=");TraceHex(unchecked((UInt64)result));TraceNewLine();Native.EndSerialRecord(); }
    private static void TraceText(String text){if(text==null)return;for(Int32 i=0;i<text.Length;i++)Native.WriteSerial((Byte)text[i]);}
    private static void TraceHex(UInt64 value){const String digits="0123456789ABCDEF";Native.WriteSerial((Byte)'0');Native.WriteSerial((Byte)'x');Boolean started=false;for(Int32 shift=60;shift>=0;shift-=4){UInt32 nibble=(UInt32)((value>>shift)&0xFUL);if(!started&&nibble==0U&&shift!=0)continue;started=true;Native.WriteSerial((Byte)digits[(Int32)nibble]);}}
    private static void TraceNewLine(){Native.WriteSerial((Byte)'\r');Native.WriteSerial((Byte)'\n');}

    private static Int64 DispatchNative(KernelSystemCallFrame* frame)=>DispatchRegisteredNative(frame->Operation,frame);

    private static Int64 DispatchLinux(KernelSystemCallFrame* frame)
    {
        if (frame->ServiceNumber==24U && KernelSmp.TryGetCurrentProcessor(out KernelProcessorState cpu))
            return KernelScheduler.Yield(cpu.Index,out UInt64 _) ? 0L : (Int64)KernelSystemCallError.Busy;
        return DispatchRegisteredAbi(KernelSystemCallAbi.Linux,frame);
    }

    private static Int64 DispatchNt(KernelSystemCallFrame* frame)
    {
        Int64 custom=DispatchRegisteredAbi(KernelSystemCallAbi.Nt,frame);
        return custom==(Int64)KernelSystemCallError.NotImplemented ? (Int64)(UInt64)KernelNtStatus.NotImplemented : custom;
    }

}
