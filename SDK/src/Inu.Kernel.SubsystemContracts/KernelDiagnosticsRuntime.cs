using System;

namespace Inu.Kernel.Contracts;

public static class KernelLog
{
    private const Int32 MaximumSinks=4;
    // Fixed reference slots deliberately avoid managed reference arrays during the no-GC bootstrap.
    // Reference-array stores require NativeAOT TypeCast/StelemRef helpers, which belong to the later managed runtime.
    private static IKernelLogSink _sink0,_sink1,_sink2,_sink3;
    private static IKernelLogContextProvider _context;
    private static KernelLogLevel _minimum=KernelLogLevel.Info;
    private static UInt64 _trace,_debug,_info,_warning,_error,_critical,_dropped;

    public static Boolean Configure(IKernelLogSink sink,IKernelLogContextProvider context,KernelLogLevel minimum)
    {
        if(sink==null)return false;
        RemoveAllSinks();
        _sink0=sink;
        _context=context;
        _minimum=minimum;
        return true;
    }
    public static Boolean AddSink(IKernelLogSink sink)
    {
        if(sink==null)return false;
        if(_sink0==sink||_sink1==sink||_sink2==sink||_sink3==sink)return true;
        if(_sink0==null){_sink0=sink;return true;}
        if(_sink1==null){_sink1=sink;return true;}
        if(_sink2==null){_sink2=sink;return true;}
        if(_sink3==null){_sink3=sink;return true;}
        return false;
    }
    public static Boolean RemoveAllSinks(){_sink0=_sink1=_sink2=_sink3=null;return true;}
    public static Boolean SetMinimumLevel(KernelLogLevel minimum){_minimum=minimum;return true;}
    public static KernelLogLevel GetMinimumLevel()=>_minimum;
    public static Boolean IsConfigured()=>_sink0!=null||_sink1!=null||_sink2!=null||_sink3!=null;
    public static KernelLogStatistics GetStatistics()=>new KernelLogStatistics(_trace,_debug,_info,_warning,_error,_critical,_dropped);
    public static Boolean ResetStatistics(){_trace=_debug=_info=_warning=_error=_critical=_dropped=0UL;return true;}

    public static Boolean Write(KernelLogLevel level,String subsystem,String source,String message)
    {
        if(level<_minimum)return true;
        UInt32 cpu=0;UInt64 thread=0,process=0,time=0;
        if(_context!=null)_context.TryGetContext(out cpu,out thread,out process,out time);
        KernelLogRecord record=new KernelLogRecord(level,subsystem??String.Empty,cpu,thread,process,time,source??String.Empty,message??String.Empty);
        Boolean any=false,ok=true;
        if(_sink0!=null){any=true;if(!_sink0.TryWrite(record))ok=false;}
        if(_sink1!=null){any=true;if(!_sink1.TryWrite(record))ok=false;}
        if(_sink2!=null){any=true;if(!_sink2.TryWrite(record))ok=false;}
        if(_sink3!=null){any=true;if(!_sink3.TryWrite(record))ok=false;}
        Count(level);
        if(!any||!ok)_dropped++;
        return any&&ok;
    }
    private static Boolean Count(KernelLogLevel level)
    {
        switch(level){case KernelLogLevel.Trace:_trace++;break;case KernelLogLevel.Debug:_debug++;break;case KernelLogLevel.Info:_info++;break;case KernelLogLevel.Warning:_warning++;break;case KernelLogLevel.Error:_error++;break;case KernelLogLevel.Critical:_critical++;break;default:return false;}return true;
    }
    public static Boolean Trace(String subsystem,String source,String message)=>Write(KernelLogLevel.Trace,subsystem,source,message);
    public static Boolean Debug(String subsystem,String source,String message)=>Write(KernelLogLevel.Debug,subsystem,source,message);
    public static Boolean Info(String subsystem,String source,String message)=>Write(KernelLogLevel.Info,subsystem,source,message);
    public static Boolean Warning(String subsystem,String source,String message)=>Write(KernelLogLevel.Warning,subsystem,source,message);
    public static Boolean Error(String subsystem,String source,String message)=>Write(KernelLogLevel.Error,subsystem,source,message);
    public static Boolean Critical(String subsystem,String source,String message)=>Write(KernelLogLevel.Critical,subsystem,source,message);
}

public static unsafe class KernelTelemetry
{
    // Inu.Runtime.NativeAot now provides managed allocation and reference barriers.
    // Dynamic interface dispatch remains intentionally constrained, so keep the official telemetry
    // API allocation-free by routing records through a raw managed function pointer.
    // The richer IKernelTelemetrySink/KernelTelemetryEvent contracts remain available to
    // hosted tooling, but the boot kernel never stores managed sink objects or dispatches
    // interfaces.
    private static delegate* managed<KernelTelemetryKind,String,String,UInt64,UInt64,UInt64,UInt64,String,UInt32,UInt64,UInt64,Boolean> _emit;
    private static delegate* managed<UInt32*,UInt64*,UInt64*,UInt64*,Boolean> _context;
    private static UInt64 _sequence=1,_trace,_profile,_boot,_counter,_diagnostic,_dropped;

    public static Boolean ConfigureFreestanding(
        delegate* managed<KernelTelemetryKind,String,String,UInt64,UInt64,UInt64,UInt64,String,UInt32,UInt64,UInt64,Boolean> emit,
        delegate* managed<UInt32*,UInt64*,UInt64*,UInt64*,Boolean> context)
    {
        if(emit==null)return false;
        _emit=emit;_context=context;return true;
    }
    public static Boolean IsConfigured()=>_emit!=null;
    public static Boolean RemoveAllSinks(){_emit=null;_context=null;return true;}
    public static KernelTelemetryStatistics GetStatistics()=>new KernelTelemetryStatistics(_trace,_profile,_boot,_counter,_diagnostic,_dropped);
    public static Boolean ResetStatistics(){_trace=_profile=_boot=_counter=_diagnostic=_dropped=0;return true;}

    public static Boolean Emit(KernelTelemetryKind kind,String subsystem,String name,UInt64 timestamp,UInt64 value0=0,UInt64 value1=0,String detail="")
    {
        UInt32 cpu=0;UInt64 thread=0,process=0,contextTime=0;
        if(_context!=null)_context(&cpu,&thread,&process,&contextTime);
        if(timestamp==0)timestamp=contextTime;
        UInt64 sequence=_sequence++;
        Boolean ok=_emit!=null&&_emit(kind,subsystem??String.Empty,name??String.Empty,timestamp,value0,value1,sequence,detail??String.Empty,cpu,thread,process);
        Count(kind);if(!ok)_dropped++;return ok;
    }
    private static Boolean Count(KernelTelemetryKind kind)
    {
        switch(kind){case KernelTelemetryKind.Trace:_trace++;break;case KernelTelemetryKind.Profile:_profile++;break;case KernelTelemetryKind.BootEvent:_boot++;break;case KernelTelemetryKind.Counter:_counter++;break;case KernelTelemetryKind.DiagnosticEvent:_diagnostic++;break;default:return false;}return true;
    }
    public static Boolean KernelTrace(String subsystem,String name,String detail="")=>Emit(KernelTelemetryKind.Trace,subsystem,name,0,0,0,detail);
    public static Boolean KernelTrace(String subsystem,String name,UInt64 timestamp,String detail="")=>Emit(KernelTelemetryKind.Trace,subsystem,name,timestamp,0,0,detail);
    public static Boolean KernelProfile(String subsystem,String name,UInt64 samples,UInt64 durationNanoseconds)=>Emit(KernelTelemetryKind.Profile,subsystem,name,0,samples,durationNanoseconds,String.Empty);
    public static Boolean KernelProfile(String subsystem,String name,UInt64 timestamp,UInt64 samples,UInt64 duration)=>Emit(KernelTelemetryKind.Profile,subsystem,name,timestamp,samples,duration,String.Empty);
    public static Boolean KernelBootEvent(String name,UInt64 stage,KernelBootPhase phase,String detail="")=>Emit(KernelTelemetryKind.BootEvent,"boot",name,0,stage,(UInt64)phase,detail);
    public static Boolean KernelBootEvent(String name,UInt64 timestamp,UInt64 stage)=>Emit(KernelTelemetryKind.BootEvent,"boot",name,timestamp,stage,(UInt64)KernelBootPhase.End,String.Empty);
    public static Boolean KernelCounter(String subsystem,String name,UInt64 value)=>Emit(KernelTelemetryKind.Counter,subsystem,name,0,value,0,String.Empty);
    public static Boolean KernelCounter(String subsystem,String name,UInt64 timestamp,UInt64 value)=>Emit(KernelTelemetryKind.Counter,subsystem,name,timestamp,value,0,String.Empty);
    public static Boolean KernelDiagnosticEvent(String subsystem,String name,UInt64 code,String detail)=>Emit(KernelTelemetryKind.DiagnosticEvent,subsystem,name,0,code,0,detail);
    public static Boolean KernelDiagnosticEvent(String subsystem,String name,UInt64 timestamp,UInt64 code,String detail)=>Emit(KernelTelemetryKind.DiagnosticEvent,subsystem,name,timestamp,code,0,detail);
}

public static unsafe class KernelPanic
{
    // Panic handling must continue to work when the heap, GC or interface dispatch is damaged.
    // The freestanding path therefore stores only raw managed function pointers and value types.
    private static delegate* managed<UInt32*,UInt64*,UInt64*,UInt64*,UInt64*,UInt64*,Boolean> _context;
    private static delegate* managed<KernelPanicRegisters*,Boolean> _registers;
    private static delegate* managed<KernelPanicCallStack*,Boolean> _callStack;
    private static delegate* managed<KernelPanicNativeInfo*,KernelPanicRegisters*,KernelPanicCallStack*,Boolean> _crashDump;
    private static delegate* managed<KernelPanicNativeInfo*,Boolean> _debugBreak;
    private static delegate* managed<Boolean> _halt;
    private static delegate* managed<Boolean> _reboot;
    private static Boolean _configured,_panicking,_hasSnapshot;
    private static KernelPanicSnapshot _last;

    public static Boolean ConfigureFreestanding(
        delegate* managed<UInt32*,UInt64*,UInt64*,UInt64*,UInt64*,UInt64*,Boolean> context,
        delegate* managed<KernelPanicRegisters*,Boolean> registers,
        delegate* managed<KernelPanicCallStack*,Boolean> callStack,
        delegate* managed<KernelPanicNativeInfo*,KernelPanicRegisters*,KernelPanicCallStack*,Boolean> crashDump,
        delegate* managed<KernelPanicNativeInfo*,Boolean> debugBreak,
        delegate* managed<Boolean> halt,
        delegate* managed<Boolean> reboot)
    {
        if(context==null||registers==null||callStack==null||halt==null)return false;
        _context=context;_registers=registers;_callStack=callStack;_crashDump=crashDump;_debugBreak=debugBreak;_halt=halt;_reboot=reboot;_configured=true;return true;
    }

    public static Boolean IsConfigured()=>_configured;
    public static Boolean IsPanicking()=>_panicking;
    public static Boolean TryGetLastSnapshot(out KernelPanicSnapshot snapshot){snapshot=_last;return _hasSnapshot;}
    public static Boolean ResetForTesting(){_panicking=false;_hasSnapshot=false;_last=default;return true;}

    public static Boolean Raise(KernelPanicCode code,String reason,String message,Boolean writeCrashDump=true,Boolean breakDebugger=true,KernelPanicPolicy policy=KernelPanicPolicy.DebuggerThenHalt)
    {
        UInt32 cpu=0;UInt64 thread=0,process=0,rip=0,rsp=0,time=0;
        if(_context!=null)_context(&cpu,&thread,&process,&rip,&rsp,&time);
        return Raise(new KernelPanicInfo((UInt32)code,reason??String.Empty,message??String.Empty,cpu,thread,process,rip,rsp,writeCrashDump,breakDebugger,policy),time);
    }

    public static Boolean Raise(KernelPanicInfo info)=>Raise(info,0UL);

    private static Boolean Raise(KernelPanicInfo info,UInt64 timestamp)
    {
        if(_panicking)return false;
        _panicking=true;

        KernelPanicRegisters registers=default;
        KernelPanicCallStack stack=default;
        Boolean registerOk=_registers!=null&&_registers(&registers);
        Boolean stackOk=_callStack!=null&&_callStack(&stack);
        if(timestamp==0UL)timestamp=1UL;

        KernelPanicNativeInfo nativeInfo=new(
            info.Code,info.Cpu,info.ThreadId,info.ProcessId,info.InstructionPointer,info.StackPointer,
            info.WriteCrashDump,info.BreakDebugger,info.Policy);
        _last=new KernelPanicSnapshot(nativeInfo,registers,stack,timestamp);
        _hasSnapshot=true;

        // Human-readable strings are emitted, not stored in static panic state.
        KernelTelemetry.KernelDiagnosticEvent("panic","raised",(UInt64)info.Code,info.Reason??String.Empty);
        KernelTelemetry.KernelTrace("panic","message",info.Message??String.Empty);

        Boolean ok=registerOk&&stackOk;
        if(info.WriteCrashDump&&_crashDump!=null)ok=_crashDump(&nativeInfo,&registers,&stack)&&ok;

        Boolean wantsDebugger=info.BreakDebugger||info.Policy==KernelPanicPolicy.DebuggerThenHalt||info.Policy==KernelPanicPolicy.DebuggerThenReboot;
        if(wantsDebugger&&_debugBreak!=null)ok=_debugBreak(&nativeInfo)&&ok;

        Boolean wantsReboot=info.Policy==KernelPanicPolicy.Reboot||info.Policy==KernelPanicPolicy.DebuggerThenReboot;
        if(wantsReboot&&_reboot!=null&&_reboot())return ok;

        // A failed/unsupported reboot always degrades safely to a terminal halt.
        if(_halt!=null)return _halt()&&ok;
        return false;
    }
}
