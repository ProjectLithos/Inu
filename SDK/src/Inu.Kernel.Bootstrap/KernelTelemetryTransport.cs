using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Console;
using Inu.Kernel.Time;
using Inu.Kernel.Smp;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Processes;

namespace Inu.Kernel.Bootstrap;

/// <summary>Allocation-free telemetry transport for the freestanding NativeAOT kernel.</summary>
public static unsafe class KernelTelemetryTransport
{
    public static Boolean TryGetContext(UInt32* cpu, UInt64* threadId, UInt64* processId, UInt64* timestampNanoseconds)
    {
        UInt32 currentCpu = 0U;
        UInt64 currentThreadId = 0UL;
        UInt64 currentProcessId = 0UL;

        if (KernelSmp.TryGetCurrentProcessor(out KernelProcessorState processor))
            currentCpu = processor.Index;

        KernelScheduler.TryGetCurrentThreadId(out currentThreadId);
        KernelProcesses.TryGetCurrentProcessId(out currentProcessId);

        if (cpu != null) *cpu = currentCpu;
        if (threadId != null) *threadId = currentThreadId;
        if (processId != null) *processId = currentProcessId;
        if (timestampNanoseconds != null) *timestampNanoseconds = KernelTime.GetMonotonicNanoseconds();
        return true;
    }

    public static Boolean TryEmit(KernelTelemetryKind kind,String subsystem,String name,UInt64 timestampNanoseconds,UInt64 value0,UInt64 value1,UInt64 sequence,String detail,UInt32 cpu,UInt64 threadId,UInt64 processId)
    {
        if(!KernelConsole.Write("[INU:"))return false;
        if(!KernelConsole.Write(Prefix(kind)))return false;
        if(!KernelConsole.Write("] v=1.1 seq="))return false;
        if(!KernelConsole.WriteUInt64(sequence))return false;
        if(!KernelConsole.Write(" ts="))return false;
        if(!KernelConsole.WriteUInt64(timestampNanoseconds))return false;
        if(!KernelConsole.Write(" cpu="))return false;
        if(!KernelConsole.WriteUInt64(cpu))return false;
        if(!KernelConsole.Write(" thread="))return false;
        if(!KernelConsole.WriteUInt64(threadId))return false;
        if(!KernelConsole.Write(" process="))return false;
        if(!KernelConsole.WriteUInt64(processId))return false;
        if(!KernelConsole.Write(" subsystem="))return false;
        if(!KernelConsole.Write(subsystem??String.Empty))return false;
        if(!KernelConsole.Write(" name="))return false;
        if(!KernelConsole.Write(name??String.Empty))return false;
        if(!WriteFields(kind,value0,value1))return false;
        if(detail!=null&&detail.Length!=0){if(!KernelConsole.Write(" detail="))return false;if(!KernelConsole.Write(detail))return false;}
        return KernelConsole.WriteLine("");
    }

    private static Boolean WriteFields(KernelTelemetryKind kind,UInt64 value0,UInt64 value1)
    {
        switch(kind)
        {
            case KernelTelemetryKind.Trace:return true;
            case KernelTelemetryKind.Profile:
                if(!KernelConsole.Write(" samples="))return false;if(!KernelConsole.WriteUInt64(value0))return false;
                if(!KernelConsole.Write(" duration_ns="))return false;return KernelConsole.WriteUInt64(value1);
            case KernelTelemetryKind.BootEvent:
                if(!KernelConsole.Write(" stage="))return false;if(!KernelConsole.WriteUInt64(value0))return false;
                if(!KernelConsole.Write(" phase="))return false;return KernelConsole.WriteUInt64(value1);
            case KernelTelemetryKind.Counter:
                if(!KernelConsole.Write(" value="))return false;return KernelConsole.WriteUInt64(value0);
            case KernelTelemetryKind.DiagnosticEvent:
                if(!KernelConsole.Write(" code="))return false;return KernelConsole.WriteUInt64(value0);
            default:return false;
        }
    }
    private static String Prefix(KernelTelemetryKind kind)
    {
        switch(kind){case KernelTelemetryKind.Trace:return "TRACE";case KernelTelemetryKind.Profile:return "PROFILE";case KernelTelemetryKind.BootEvent:return "BOOT";case KernelTelemetryKind.Counter:return "COUNTER";case KernelTelemetryKind.DiagnosticEvent:return "DIAGNOSTIC";default:return "UNKNOWN";}
    }
}
