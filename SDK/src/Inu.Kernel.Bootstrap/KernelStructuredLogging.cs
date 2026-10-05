using System;
using Inu.Kernel.Console;
using Inu.Kernel.Contracts;
using Inu.Kernel.Smp;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Processes;
using Inu.Kernel.Time;

namespace Inu.Kernel.Bootstrap;

/// <summary>Allocation-free structured logging for the no-GC bootstrap.</summary>
public static class KernelStructuredLogging
{
    private static KernelLogLevel _minimum=KernelLogLevel.Trace;
    public static Boolean Initialize(){_minimum=KernelLogLevel.Trace;return true;}
    public static Boolean SetMinimumLevel(KernelLogLevel level){_minimum=level;return true;}
    public static Boolean Begin(KernelLogLevel level,String subsystem,String source)
    {
        if(level<_minimum)return true;
        UInt32 cpu=0U; UInt64 thread=0UL,process=0UL,time=0UL;
        time=KernelTime.IsInitialized?KernelTime.GetMonotonicNanoseconds():0UL;
        if(KernelSmp.IsInitialized()&&KernelSmp.TryGetCurrentProcessor(out KernelProcessorState processor))cpu=processor.Index;
        if(KernelScheduler.IsInitialized())KernelScheduler.TryGetCurrentThreadId(out thread);
        if(KernelProcesses.IsInitialized())KernelProcesses.TryGetCurrentProcessId(out process);
        if(!KernelConsole.Write("["))return false;
        if(!KernelConsole.Write(LevelName(level)))return false;
        if(!KernelConsole.Write("][t="))return false;
        if(!KernelConsole.WriteUInt64(time))return false;
        if(!KernelConsole.Write("][cpu="))return false;
        if(!KernelConsole.WriteUInt64(cpu))return false;
        if(!KernelConsole.Write("][thread="))return false;
        if(!KernelConsole.WriteUInt64(thread))return false;
        if(!KernelConsole.Write("][process="))return false;
        if(!KernelConsole.WriteUInt64(process))return false;
        if(!KernelConsole.Write("]["))return false;
        if(!KernelConsole.Write(subsystem??String.Empty))return false;
        if(!KernelConsole.Write("]["))return false;
        if(!KernelConsole.Write(source??String.Empty))return false;
        return KernelConsole.Write("] ");
    }
    private static Boolean Line(KernelLogLevel level,String subsystem,String source,String message)
    {
        if(level<_minimum)return true;
        if(!Begin(level,subsystem,source))return false;
        return KernelConsole.WriteLine(message??String.Empty);
    }
    public static Boolean TraceLine(String subsystem,String source,String message)=>Line(KernelLogLevel.Trace,subsystem,source,message);
    public static Boolean DebugLine(String subsystem,String source,String message)=>Line(KernelLogLevel.Debug,subsystem,source,message);
    public static Boolean InfoLine(String subsystem,String source,String message)=>Line(KernelLogLevel.Info,subsystem,source,message);
    public static Boolean WarningLine(String subsystem,String source,String message)=>Line(KernelLogLevel.Warning,subsystem,source,message);
    public static Boolean ErrorLine(String subsystem,String source,String message)=>Line(KernelLogLevel.Error,subsystem,source,message);
    public static Boolean CriticalLine(String subsystem,String source,String message)=>Line(KernelLogLevel.Critical,subsystem,source,message);
    private static String LevelName(KernelLogLevel level)
    {
        switch(level){case KernelLogLevel.Trace:return "TRACE";case KernelLogLevel.Debug:return "DEBUG";case KernelLogLevel.Info:return "INFO";case KernelLogLevel.Warning:return "WARN";case KernelLogLevel.Error:return "ERROR";case KernelLogLevel.Critical:return "CRITICAL";default:return "UNKNOWN";}
    }
}
