using System;

namespace Inu.Kernel.Contracts;

public enum KernelLogLevel : Byte { Trace=0, Debug=1, Info=2, Warning=3, Error=4, Critical=5 }
public readonly struct KernelLogRecord
{
    public KernelLogRecord(KernelLogLevel level,String subsystem,UInt32 cpu,UInt64 threadId,UInt64 processId,UInt64 timestampNanoseconds,String source,String message)
    { Level=level;Subsystem=subsystem;Cpu=cpu;ThreadId=threadId;ProcessId=processId;TimestampNanoseconds=timestampNanoseconds;Source=source;Message=message; }
    public KernelLogLevel Level { get; } public String Subsystem { get; } public UInt32 Cpu { get; } public UInt64 ThreadId { get; } public UInt64 ProcessId { get; } public UInt64 TimestampNanoseconds { get; } public String Source { get; } public String Message { get; }
}
public interface IKernelLogSink { Boolean TryWrite(KernelLogRecord record); }
public readonly struct KernelLogStatistics
{
    public KernelLogStatistics(UInt64 trace,UInt64 debug,UInt64 info,UInt64 warning,UInt64 error,UInt64 critical,UInt64 dropped)
    { Trace=trace;Debug=debug;Info=info;Warning=warning;Error=error;Critical=critical;Dropped=dropped; }
    public UInt64 Trace{get;} public UInt64 Debug{get;} public UInt64 Info{get;} public UInt64 Warning{get;} public UInt64 Error{get;} public UInt64 Critical{get;} public UInt64 Dropped{get;}
}
public interface IKernelLogContextProvider { Boolean TryGetContext(out UInt32 cpu,out UInt64 threadId,out UInt64 processId,out UInt64 timestampNanoseconds); }

public enum KernelTelemetryKind : Byte { Trace=1, Profile=2, BootEvent=3, Counter=4, DiagnosticEvent=5 }
public enum KernelBootPhase : Byte { Instant=0, Begin=1, End=2, Warning=3, Failed=4 }
public readonly struct KernelTelemetryEvent
{
    public KernelTelemetryEvent(KernelTelemetryKind kind,String subsystem,String name,UInt64 timestampNanoseconds,UInt64 value0,UInt64 value1,UInt64 correlationId,String detail)
        : this(kind,subsystem,name,timestampNanoseconds,value0,value1,correlationId,detail,0U,0UL,0UL) { }
    public KernelTelemetryEvent(KernelTelemetryKind kind,String subsystem,String name,UInt64 timestampNanoseconds,UInt64 value0,UInt64 value1,UInt64 correlationId,String detail,UInt32 cpu,UInt64 threadId,UInt64 processId)
    { Kind=kind;Subsystem=subsystem;Name=name;TimestampNanoseconds=timestampNanoseconds;Value0=value0;Value1=value1;CorrelationId=correlationId;Detail=detail;Cpu=cpu;ThreadId=threadId;ProcessId=processId; }
    public KernelTelemetryKind Kind { get; } public String Subsystem { get; } public String Name { get; } public UInt64 TimestampNanoseconds { get; } public UInt64 Value0 { get; } public UInt64 Value1 { get; } public UInt64 CorrelationId { get; } public String Detail { get; } public UInt32 Cpu { get; } public UInt64 ThreadId { get; } public UInt64 ProcessId { get; }
}
public readonly struct KernelTelemetryStatistics
{
    public KernelTelemetryStatistics(UInt64 trace,UInt64 profile,UInt64 boot,UInt64 counter,UInt64 diagnostic,UInt64 dropped) { Trace=trace;Profile=profile;Boot=boot;Counter=counter;Diagnostic=diagnostic;Dropped=dropped; }
    public UInt64 Trace{get;} public UInt64 Profile{get;} public UInt64 Boot{get;} public UInt64 Counter{get;} public UInt64 Diagnostic{get;} public UInt64 Dropped{get;}
}
public interface IKernelTelemetrySink { Boolean TryEmit(KernelTelemetryEvent telemetryEvent); }

/// <summary>Defines the stable Inu Crash Dump (NOCD) SDK format.</summary>
/// <remarks>
/// Major-version changes are breaking. Minor-version changes may append optional fields or section kinds.
/// Readers must ignore unknown section kinds and unknown fields. Each section has an independent version.
/// </remarks>
public static class KernelCrashDumpFormat
{
    /// <summary>ASCII "NOCD" encoded as a little-endian UInt32.</summary>
    public const UInt32 Magic=0x4E4F4344U;
    public const UInt16 Major=1;
    public const UInt16 Minor=1;
    public const UInt16 HeaderVersion=1;
    public const UInt16 CpuStateVersion=1;
    public const UInt16 RegistersVersion=1;
    public const UInt16 StackVersion=1;
    public const UInt16 PageTablesVersion=1;
    public const UInt16 ProcessesVersion=1;
    public const UInt16 ModulesVersion=1;
    public const UInt16 HeapVersion=1;
    public const UInt16 MemoryRangesVersion=1;
    public const UInt16 PanicVersion=1;
    public const UInt16 DriversVersion=1;
    public const UInt16 TelemetryVersion=1;

    public static Boolean IsCompatible(UInt16 major,UInt16 minor) => major==Major;
    public static Boolean IsSectionVersionSupported(KernelCrashSectionKind kind,UInt16 version)
    {
        if(version==0)return false;
        return kind switch
        {
            KernelCrashSectionKind.CpuState=>version<=CpuStateVersion,
            KernelCrashSectionKind.Registers=>version<=RegistersVersion,
            KernelCrashSectionKind.Stack=>version<=StackVersion,
            KernelCrashSectionKind.PageTables=>version<=PageTablesVersion,
            KernelCrashSectionKind.Processes=>version<=ProcessesVersion,
            KernelCrashSectionKind.Modules=>version<=ModulesVersion,
            KernelCrashSectionKind.Heap=>version<=HeapVersion,
            KernelCrashSectionKind.MemoryRanges=>version<=MemoryRangesVersion,
            KernelCrashSectionKind.Panic=>version<=PanicVersion,
            KernelCrashSectionKind.Drivers=>version<=DriversVersion,
            KernelCrashSectionKind.Telemetry=>version<=TelemetryVersion,
            _=>true
        };
    }
}

public enum KernelCrashSectionKind : UInt16
{
    CpuState=1,
    Registers=2,
    Stack=3,
    PageTables=4,
    Processes=5,
    Modules=6,
    Heap=7,
    MemoryRanges=8,
    Panic=9,
    Drivers=10,
    Telemetry=11
}

public readonly struct KernelCrashDumpHeader
{
    public KernelCrashDumpHeader(UInt32 magic,UInt16 major,UInt16 minor,UInt64 totalBytes,UInt32 sectionCount,UInt64 timestampNanoseconds,UInt32 architecture)
    { Magic=magic;Major=major;Minor=minor;TotalBytes=totalBytes;SectionCount=sectionCount;TimestampNanoseconds=timestampNanoseconds;Architecture=architecture; }
    public UInt32 Magic { get; }
    public UInt16 Major { get; }
    public UInt16 Minor { get; }
    public UInt64 TotalBytes { get; }
    public UInt32 SectionCount { get; }
    public UInt64 TimestampNanoseconds { get; }
    public UInt32 Architecture { get; }
    public Boolean IsCompatible()=>Magic==KernelCrashDumpFormat.Magic&&KernelCrashDumpFormat.IsCompatible(Major,Minor);
}

public readonly struct KernelCrashDumpSection
{
    public KernelCrashDumpSection(KernelCrashSectionKind kind,UInt16 version,UInt64 offset,UInt64 length)
    {Kind=kind;Version=version;Offset=offset;Length=length;}
    public KernelCrashSectionKind Kind{get;}
    public UInt16 Version{get;}
    public UInt64 Offset{get;}
    public UInt64 Length{get;}
    public Boolean IsSupported()=>KernelCrashDumpFormat.IsSectionVersionSupported(Kind,Version);
}

/// <summary>Common CPU context stored in the CpuState section.</summary>
public readonly struct KernelCrashCpuState
{
    public KernelCrashCpuState(UInt32 cpu,UInt64 threadId,UInt64 processId,UInt64 instructionPointer,UInt64 stackPointer,UInt64 framePointer,UInt64 flags,UInt64 pageTableRoot)
    {Cpu=cpu;ThreadId=threadId;ProcessId=processId;InstructionPointer=instructionPointer;StackPointer=stackPointer;FramePointer=framePointer;Flags=flags;PageTableRoot=pageTableRoot;}
    public UInt32 Cpu{get;} public UInt64 ThreadId{get;} public UInt64 ProcessId{get;}
    public UInt64 InstructionPointer{get;} public UInt64 StackPointer{get;} public UInt64 FramePointer{get;}
    public UInt64 Flags{get;} public UInt64 PageTableRoot{get;}
}

public readonly struct KernelCrashRegister
{
    public KernelCrashRegister(String name,UInt64 value){Name=name;Value=value;}
    public String Name{get;} public UInt64 Value{get;}
}

public readonly struct KernelCrashStackFrame
{
    public KernelCrashStackFrame(UInt32 index,UInt64 instructionPointer,UInt64 stackPointer,UInt64 framePointer,String symbol,String sourcePath,UInt32 sourceLine)
    {Index=index;InstructionPointer=instructionPointer;StackPointer=stackPointer;FramePointer=framePointer;Symbol=symbol;SourcePath=sourcePath;SourceLine=sourceLine;}
    public UInt32 Index{get;} public UInt64 InstructionPointer{get;} public UInt64 StackPointer{get;} public UInt64 FramePointer{get;}
    public String Symbol{get;} public String SourcePath{get;} public UInt32 SourceLine{get;}
}

public readonly struct KernelCrashMemoryRange
{
    public KernelCrashMemoryRange(UInt64 address,UInt64 length,UInt32 protection,UInt32 kind){Address=address;Length=length;Protection=protection;Kind=kind;}
    public UInt64 Address{get;} public UInt64 Length{get;} public UInt32 Protection{get;} public UInt32 Kind{get;}
}

public readonly struct KernelCrashProcess
{
    public KernelCrashProcess(UInt64 processId,UInt64 parentProcessId,String name,UInt32 state){ProcessId=processId;ParentProcessId=parentProcessId;Name=name;State=state;}
    public UInt64 ProcessId{get;} public UInt64 ParentProcessId{get;} public String Name{get;} public UInt32 State{get;}
}

public readonly struct KernelCrashModule
{
    public KernelCrashModule(String name,UInt64 baseAddress,UInt64 size,String buildId){Name=name;BaseAddress=baseAddress;Size=size;BuildId=buildId;}
    public String Name{get;} public UInt64 BaseAddress{get;} public UInt64 Size{get;} public String BuildId{get;}
}

public readonly struct KernelCrashDriverState
{
    public KernelCrashDriverState(String id,String name,UInt32 state,UInt64 deviceId,String detail){Id=id;Name=name;State=state;DeviceId=deviceId;Detail=detail;}
    public String Id{get;} public String Name{get;} public UInt32 State{get;} public UInt64 DeviceId{get;} public String Detail{get;}
}

/// <summary>Defines the terminal action taken after a kernel panic has been recorded.</summary>
public enum KernelPanicPolicy : Byte
{
    Halt=0,
    Reboot=1,
    DebuggerThenHalt=2,
    DebuggerThenReboot=3
}

/// <summary>Stable panic reason codes used by the kernel and SDK.</summary>
public enum KernelPanicCode : UInt32
{
    Unknown=0,
    AssertionFailed=1,
    UnhandledException=2,
    DoubleFault=3,
    MachineCheck=4,
    PageFault=5,
    GeneralProtectionFault=6,
    OutOfMemory=7,
    HeapCorruption=8,
    SchedulerFailure=9,
    DriverFailure=10,
    FilesystemFailure=11,
    SecurityViolation=12,
    WatchdogExpired=13,
    UserRequested=14
}

/// <summary>Architecture-neutral register snapshot attached to a panic.</summary>
public readonly struct KernelPanicRegisters
{
    public KernelPanicRegisters(
        UInt64 rax,UInt64 rbx,UInt64 rcx,UInt64 rdx,UInt64 rsi,UInt64 rdi,
        UInt64 rbp,UInt64 rsp,UInt64 r8,UInt64 r9,UInt64 r10,UInt64 r11,
        UInt64 r12,UInt64 r13,UInt64 r14,UInt64 r15,UInt64 rip,UInt64 flags,UInt64 cr3)
    { Rax=rax;Rbx=rbx;Rcx=rcx;Rdx=rdx;Rsi=rsi;Rdi=rdi;Rbp=rbp;Rsp=rsp;R8=r8;R9=r9;R10=r10;R11=r11;R12=r12;R13=r13;R14=r14;R15=r15;Rip=rip;Flags=flags;Cr3=cr3; }
    public UInt64 Rax{get;} public UInt64 Rbx{get;} public UInt64 Rcx{get;} public UInt64 Rdx{get;}
    public UInt64 Rsi{get;} public UInt64 Rdi{get;} public UInt64 Rbp{get;} public UInt64 Rsp{get;}
    public UInt64 R8{get;} public UInt64 R9{get;} public UInt64 R10{get;} public UInt64 R11{get;}
    public UInt64 R12{get;} public UInt64 R13{get;} public UInt64 R14{get;} public UInt64 R15{get;}
    public UInt64 Rip{get;} public UInt64 Flags{get;} public UInt64 Cr3{get;}
}

/// <summary>Allocation-free top-of-stack snapshot. Zero values mean no frame was captured.</summary>
public readonly struct KernelPanicCallStack
{
    public KernelPanicCallStack(UInt32 count,UInt64 frame0,UInt64 frame1,UInt64 frame2,UInt64 frame3,UInt64 frame4,UInt64 frame5,UInt64 frame6,UInt64 frame7)
    {Count=count;Frame0=frame0;Frame1=frame1;Frame2=frame2;Frame3=frame3;Frame4=frame4;Frame5=frame5;Frame6=frame6;Frame7=frame7;}
    public UInt32 Count{get;}
    public UInt64 Frame0{get;} public UInt64 Frame1{get;} public UInt64 Frame2{get;} public UInt64 Frame3{get;}
    public UInt64 Frame4{get;} public UInt64 Frame5{get;} public UInt64 Frame6{get;} public UInt64 Frame7{get;}
}

/// <summary>Complete structured kernel-panic description.</summary>
public readonly struct KernelPanicInfo
{
    public KernelPanicInfo(UInt32 code,String reason,String message,UInt32 cpu,UInt64 threadId,UInt64 processId,UInt64 instructionPointer,UInt64 stackPointer,Boolean writeCrashDump,Boolean breakDebugger,KernelPanicPolicy policy)
    { Code=code;Reason=reason;Message=message;Cpu=cpu;ThreadId=threadId;ProcessId=processId;InstructionPointer=instructionPointer;StackPointer=stackPointer;WriteCrashDump=writeCrashDump;BreakDebugger=breakDebugger;Policy=policy; }
    public UInt32 Code{get;} public String Reason{get;} public String Message{get;} public UInt32 Cpu{get;}
    public UInt64 ThreadId{get;} public UInt64 ProcessId{get;} public UInt64 InstructionPointer{get;} public UInt64 StackPointer{get;}
    public Boolean WriteCrashDump{get;} public Boolean BreakDebugger{get;} public KernelPanicPolicy Policy{get;}
}

/// <summary>
/// Unmanaged panic context used by the freestanding function-pointer ABI.
/// It intentionally contains no String or object references.
/// </summary>
public readonly struct KernelPanicNativeInfo
{
    public KernelPanicNativeInfo(UInt32 code,UInt32 cpu,UInt64 threadId,UInt64 processId,UInt64 instructionPointer,UInt64 stackPointer,Boolean writeCrashDump,Boolean breakDebugger,KernelPanicPolicy policy)
    {Code=code;Cpu=cpu;ThreadId=threadId;ProcessId=processId;InstructionPointer=instructionPointer;StackPointer=stackPointer;WriteCrashDump=writeCrashDump;BreakDebugger=breakDebugger;Policy=policy;}
    public UInt32 Code{get;} public UInt32 Cpu{get;} public UInt64 ThreadId{get;} public UInt64 ProcessId{get;}
    public UInt64 InstructionPointer{get;} public UInt64 StackPointer{get;}
    public Boolean WriteCrashDump{get;} public Boolean BreakDebugger{get;} public KernelPanicPolicy Policy{get;}
}

/// <summary>
/// Last allocation-free panic snapshot retained in static kernel memory for debugger/offline inspection.
/// Human-readable reason/message remain in KernelPanicInfo and structured telemetry; they are not stored as
/// static managed references in the terminal panic path.
/// </summary>
public readonly struct KernelPanicSnapshot
{
    public KernelPanicSnapshot(KernelPanicNativeInfo info,KernelPanicRegisters registers,KernelPanicCallStack callStack,UInt64 timestampNanoseconds)
    {Info=info;Registers=registers;CallStack=callStack;TimestampNanoseconds=timestampNanoseconds;}
    public KernelPanicNativeInfo Info{get;} public KernelPanicRegisters Registers{get;} public KernelPanicCallStack CallStack{get;} public UInt64 TimestampNanoseconds{get;}
}

/// <summary>
/// Hosted panic backend contract. The freestanding kernel uses KernelPanic.ConfigureFreestanding
/// so the panic path never depends on managed-object allocation or interface dispatch.
/// </summary>
public interface IKernelPanicBackend
{
    Boolean TryCaptureCallStack(KernelPanicInfo info);
    Boolean TryCaptureRegisters(KernelPanicInfo info);
    Boolean TryWriteCrashDump(KernelPanicInfo info);
    Boolean TryBreakDebugger(KernelPanicInfo info);
    Boolean TryHaltOrReboot(KernelPanicInfo info);
}

/// <summary>Stable SDK test categories. Every Inu test is one of these kinds.</summary>
public enum KernelTestKind : Byte { Kernel=1,Unit=2,Integration=3,Boot=4,Driver=5,Stress=6,FaultInjection=7,HardwareSimulation=8 }
public enum KernelTestResult : Byte { NotRun=0,Passed=1,Failed=2,Skipped=3,Timeout=4 }
public readonly struct KernelTestDescriptor { public KernelTestDescriptor(String id,String name,KernelTestKind kind,UInt64 timeoutMilliseconds,String[] tags){Id=id;Name=name;Kind=kind;TimeoutMilliseconds=timeoutMilliseconds;Tags=tags;} public String Id{get;} public String Name{get;} public KernelTestKind Kind{get;} public UInt64 TimeoutMilliseconds{get;} public String[] Tags{get;} }
public interface IKernelTestCase { KernelTestDescriptor Descriptor{get;} KernelTestResult Run(); }

/// <summary>Value-only execution record passed into the freestanding test runtime.</summary>
public readonly struct KernelTestExecution { public KernelTestExecution(UInt64 testId,KernelTestKind kind,UInt64 timeoutMilliseconds,UInt64 startedMilliseconds){TestId=testId;Kind=kind;TimeoutMilliseconds=timeoutMilliseconds;StartedMilliseconds=startedMilliseconds;} public UInt64 TestId{get;} public KernelTestKind Kind{get;} public UInt64 TimeoutMilliseconds{get;} public UInt64 StartedMilliseconds{get;} }
public readonly struct KernelTestReport { public KernelTestReport(UInt64 testId,KernelTestKind kind,KernelTestResult result,UInt64 durationMilliseconds,UInt64 assertions,UInt64 assertionFailures,UInt64 faultsInjected){TestId=testId;Kind=kind;Result=result;DurationMilliseconds=durationMilliseconds;Assertions=assertions;AssertionFailures=assertionFailures;FaultsInjected=faultsInjected;} public UInt64 TestId{get;} public KernelTestKind Kind{get;} public KernelTestResult Result{get;} public UInt64 DurationMilliseconds{get;} public UInt64 Assertions{get;} public UInt64 AssertionFailures{get;} public UInt64 FaultsInjected{get;} }
public readonly struct KernelTestStatistics { public KernelTestStatistics(UInt64 run,UInt64 passed,UInt64 failed,UInt64 skipped,UInt64 timedOut,UInt64 assertions,UInt64 assertionFailures,UInt64 faultsInjected){Run=run;Passed=passed;Failed=failed;Skipped=skipped;TimedOut=timedOut;Assertions=assertions;AssertionFailures=assertionFailures;FaultsInjected=faultsInjected;} public UInt64 Run{get;} public UInt64 Passed{get;} public UInt64 Failed{get;} public UInt64 Skipped{get;} public UInt64 TimedOut{get;} public UInt64 Assertions{get;} public UInt64 AssertionFailures{get;} public UInt64 FaultsInjected{get;} }

public enum KernelFaultKind : Byte
{
    AllocationFailure=1,
    IoTimeout=2,
    DroppedInterrupt=3,
    DeviceReset=4,
    BadDma=5,
    CorruptPacket=6,
    PageFault=7,
    CpuOffline=8,
    FilesystemError=9,
    SyscallDenied=10,
    DelayedInterrupt=11,
    StorageError=12,
    NetworkPacketLoss=13,
    DriverInitializationFailure=14,
    DriverRuntimeFailure=15,
    UserProcessCrash=16
}
public readonly struct KernelFaultRule { public KernelFaultRule(KernelFaultKind kind,String subsystem,UInt64 triggerAfter,UInt32 repeatCount,UInt64 parameter){Kind=kind;Subsystem=subsystem;TriggerAfter=triggerAfter;RepeatCount=repeatCount;Parameter=parameter;} public KernelFaultKind Kind{get;} public String Subsystem{get;} public UInt64 TriggerAfter{get;} public UInt32 RepeatCount{get;} public UInt64 Parameter{get;} }
/// <summary>Value-only form used by the freestanding fault injector; SubsystemHash is FNV-1a over the subsystem name.</summary>
public readonly struct KernelFaultNativeRule { public KernelFaultNativeRule(KernelFaultKind kind,UInt64 subsystemHash,UInt64 triggerAfter,UInt32 repeatCount,UInt64 parameter){Kind=kind;SubsystemHash=subsystemHash;TriggerAfter=triggerAfter;RepeatCount=repeatCount;Parameter=parameter;} public KernelFaultKind Kind{get;} public UInt64 SubsystemHash{get;} public UInt64 TriggerAfter{get;} public UInt32 RepeatCount{get;} public UInt64 Parameter{get;} }
public interface IKernelFaultInjector { Boolean TryArm(KernelFaultRule rule,out UInt64 ruleId); Boolean TryDisarm(UInt64 ruleId); Boolean ShouldInject(KernelFaultKind kind,String subsystem,out UInt64 parameter); }

public enum KernelArchitecture : Byte { X64=1,Arm64=2 }
public interface IKernelArchitectureContract : IKernelSubsystemContract { KernelArchitecture Architecture{get;} Boolean TryMemoryBarrier(); Boolean TryInstructionBarrier(); Boolean TryGetCurrentCpu(out UInt32 cpu); Boolean TryHalt(); }
public interface IKernelPerCpuStorage { Boolean TryGet(UInt32 cpu,UInt32 key,out UInt64 value); Boolean TrySet(UInt32 cpu,UInt32 key,UInt64 value); }
public interface IKernelSmpProfessionalContract : IKernelSmpContract { Boolean TryEnumerate(UInt32 index,out UInt64 hardwareId,out Boolean online); Boolean TryGetCurrentCpu(out UInt32 processorIndex); Boolean TryGetPerCpu(UInt32 processorIndex,UInt32 key,out UInt64 value); Boolean TrySetPerCpu(UInt32 processorIndex,UInt32 key,UInt64 value); new Boolean TrySendIpi(UInt32 processorIndex,UInt32 vector); Boolean TrySetAffinity(UInt64 threadId,UInt64 cpuMask); Boolean TryStartProcessor(UInt32 processorIndex); Boolean TryShutdownProcessor(UInt32 processorIndex); Boolean TryGetLocalScheduler(UInt32 processorIndex,out UInt64 schedulerId); }

public enum KernelSynchronizationKind : Byte { SpinLock=1,Mutex=2,Semaphore=3,Event=4,ReaderWriterLock=5,Atomic=6,Barrier=7,LockFree=8 }
public interface IKernelSynchronizationContract : IKernelSubsystemContract { Boolean TryCreate(KernelSynchronizationKind kind,UInt32 initialValue,out UInt64 handle); Boolean TryAcquire(UInt64 handle,UInt64 timeoutNanoseconds); Boolean TryRelease(UInt64 handle); Boolean TryAtomicCompareExchange(UInt64 address,UInt64 expected,UInt64 replacement,out UInt64 previous); Boolean TryAtomicExchange(UInt64 address,UInt64 value,out UInt64 previous); Boolean TryAtomicFetchAdd(UInt64 address,UInt64 delta,out UInt64 previous); Boolean TryMemoryBarrier(); Boolean TrySpinWaitHint(); }

public readonly struct KernelPhysicalAllocatorStatistics { public KernelPhysicalAllocatorStatistics(UInt64 totalPages,UInt64 freePages,UInt64 reservedPages,UInt64 allocatedPages){TotalPages=totalPages;FreePages=freePages;ReservedPages=reservedPages;AllocatedPages=allocatedPages;} public UInt64 TotalPages{get;} public UInt64 FreePages{get;} public UInt64 ReservedPages{get;} public UInt64 AllocatedPages{get;} }
public readonly struct KernelHeapDiagnostics { public KernelHeapDiagnostics(UInt64 committedBytes,UInt64 usedBytes,UInt64 allocationCount,UInt64 peakBytes,UInt64 leakCandidates,UInt64 guardFailures,UInt64 doubleFreeFailures){CommittedBytes=committedBytes;UsedBytes=usedBytes;AllocationCount=allocationCount;PeakBytes=peakBytes;LeakCandidates=leakCandidates;GuardFailures=guardFailures;DoubleFreeFailures=doubleFreeFailures;} public UInt64 CommittedBytes{get;} public UInt64 UsedBytes{get;} public UInt64 AllocationCount{get;} public UInt64 PeakBytes{get;} public UInt64 LeakCandidates{get;} public UInt64 GuardFailures{get;} public UInt64 DoubleFreeFailures{get;} }
public readonly struct KernelMemoryLeakCandidate { public KernelMemoryLeakCandidate(UInt64 token,UInt64 address,UInt64 byteCount,UInt64 tagHash,UInt64 allocationSequence,Boolean guarded){Token=token;Address=address;ByteCount=byteCount;TagHash=tagHash;AllocationSequence=allocationSequence;Guarded=guarded;} public UInt64 Token{get;} public UInt64 Address{get;} public UInt64 ByteCount{get;} public UInt64 TagHash{get;} public UInt64 AllocationSequence{get;} public Boolean Guarded{get;} }
public readonly struct KernelMemoryDiagnosticFailures { public KernelMemoryDiagnosticFailures(UInt64 guardFailures,UInt64 doubleFreeFailures){GuardFailures=guardFailures;DoubleFreeFailures=doubleFreeFailures;} public UInt64 GuardFailures{get;} public UInt64 DoubleFreeFailures{get;} }
public interface IKernelMemoryDiagnosticsContract : IKernelSubsystemContract { Boolean TryGetPhysicalStatistics(out KernelPhysicalAllocatorStatistics statistics); Boolean TryGetHeapDiagnostics(out KernelHeapDiagnostics diagnostics); Boolean TryInspectPageTable(UInt64 address,out UInt64 physicalAddress,out UInt64 flags); Boolean TryCreateLeakCheckpoint(out UInt64 checkpoint); Boolean TryGetLeakCandidate(UInt64 checkpoint,UInt64 candidateIndex,out KernelMemoryLeakCandidate candidate); Boolean TrySetAllocationTag(UInt64 allocation,String tag); Boolean TryGetAllocationTagHash(UInt64 allocation,out UInt64 tagHash); Boolean TryValidateGuards(out UInt64 failures); Boolean TryGetFailureCounters(out KernelMemoryDiagnosticFailures failures); }

[Flags] public enum KernelMemoryProtection : UInt32 { None=0,Read=1,Write=2,Execute=4,User=8,Guard=16 }
public interface IKernelSecurityContract : IKernelSubsystemContract { Boolean TryGetAddressSpace(UInt64 processId,out UInt64 rootPhysicalAddress,out Boolean userSpace); Boolean TryValidateUserPointer(UInt64 processId,UInt64 address,UInt64 length,KernelMemoryProtection access); Boolean TryProtect(UInt64 processId,UInt64 address,UInt64 length,KernelMemoryProtection protection); Boolean TryValidateExecutable(UInt64 processId,UInt64 address,UInt64 length); Boolean TryReserveGuard(UInt64 processId,UInt64 address,UInt64 length); Boolean TryValidatePrivilegeRing(UInt32 ring); Boolean TryValidateSyscall(UInt64 processId,UInt32 abi,UInt32 service); Boolean TrySetSyscallAbiPolicy(UInt64 processId,UInt32 abi,Boolean allowed); Boolean TryCreateCapability(UInt64 processId,UInt64 objectId,UInt64 rights,out UInt64 handle); Boolean TryResolveCapability(UInt64 processId,UInt64 handle,UInt64 requiredRights,out UInt64 objectId); Boolean TryCloseCapability(UInt64 processId,UInt64 handle); Boolean TryDuplicateCapability(UInt64 processId,UInt64 handle,UInt64 rights,out UInt64 duplicate); }

public static class InuExecutableFormat { public const UInt32 Magic=0x50414F4EU; public const UInt16 Major=1; public const UInt16 Minor=1; public const String ApplicationExtension=".exe"; public const String NativeExecutableExtension=".nexe"; public const String DynamicLibraryExtension=".dll"; public const String StaticLibraryExtension=".lib"; }
public enum InuApplicationArchitecture : UInt16 { X64=1,Arm64=2,RiscV64=3 }
public enum InuApplicationAbi : UInt16 { Inu=1,Linux=2,WindowsNt=3 }
public readonly struct InuExecutableMetadata { public InuExecutableMetadata(String id,String name,String version,String publisher,InuApplicationArchitecture architecture,UInt16 abiMajor,UInt16 abiMinor,InuApplicationAbi syscallAbi,UInt64 entryPointRva,String nativeImage,String[] dependencies,String[] requiredCapabilities,String[] resources){Id=id;Name=name;Version=version;Publisher=publisher;Architecture=architecture;AbiMajor=abiMajor;AbiMinor=abiMinor;SyscallAbi=syscallAbi;EntryPointRva=entryPointRva;NativeImage=nativeImage;Dependencies=dependencies;RequiredCapabilities=requiredCapabilities;Resources=resources;} public String Id{get;} public String Name{get;} public String Version{get;} public String Publisher{get;} public InuApplicationArchitecture Architecture{get;} public UInt16 AbiMajor{get;} public UInt16 AbiMinor{get;} public InuApplicationAbi SyscallAbi{get;} public UInt64 EntryPointRva{get;} public String NativeImage{get;} public String[] Dependencies{get;} public String[] RequiredCapabilities{get;} public String[] Resources{get;} }
public static class InuPackageContainer { public const String Extension=".zip"; public const String ManifestName="Inu.Package.json"; public const String Format="inu-package-v1"; public const UInt32 SchemaVersion=1; }
public enum InuPackageKind : Byte { Application=1,Driver=2,Library=3,Service=4,KernelExtension=5 }
public readonly struct InuPackageDependency { public InuPackageDependency(String id,String versionConstraint,Boolean optional){Id=id;VersionConstraint=versionConstraint;Optional=optional;} public String Id{get;} public String VersionConstraint{get;} public Boolean Optional{get;} }
public readonly struct InuPackageFile { public InuPackageFile(String path,UInt64 length,String sha256){Path=path;Length=length;Sha256=sha256;} public String Path{get;} public UInt64 Length{get;} public String Sha256{get;} }
public readonly struct InuPackageSignature { public InuPackageSignature(String state,String algorithm,String signerId,String manifestDigest,String value){State=state;Algorithm=algorithm;SignerId=signerId;ManifestDigest=manifestDigest;Value=value;} public String State{get;} public String Algorithm{get;} public String SignerId{get;} public String ManifestDigest{get;} public String Value{get;} }
public readonly struct InuPackageManifest { public InuPackageManifest(UInt32 schemaVersion,String id,String name,String version,InuPackageKind kind,String[] architectures,String minimumInuVersion,String sdkApiVersion,String abiVersion,InuPackageDependency[] dependencies,String[] capabilities,InuPackageFile[] files,InuPackageSignature signature){SchemaVersion=schemaVersion;Id=id;Name=name;Version=version;Kind=kind;Architectures=architectures;MinimumInuVersion=minimumInuVersion;SdkApiVersion=sdkApiVersion;AbiVersion=abiVersion;Dependencies=dependencies;Capabilities=capabilities;Files=files;Signature=signature;} public UInt32 SchemaVersion{get;} public String Id{get;} public String Name{get;} public String Version{get;} public InuPackageKind Kind{get;} public String[] Architectures{get;} public String MinimumInuVersion{get;} public String SdkApiVersion{get;} public String AbiVersion{get;} public InuPackageDependency[] Dependencies{get;} public String[] Capabilities{get;} public InuPackageFile[] Files{get;} public InuPackageSignature Signature{get;} }
public interface IInuPackageManagerContract { Boolean TryInspect(String zipPath,out InuPackageManifest manifest); Boolean TryVerify(String zipPath); Boolean TryInstall(String zipPath,String systemRoot); Boolean TryUninstall(String packageId,String systemRoot); Boolean TryEnumerateInstalled(UInt32 index,out String packageId,out String version,out InuPackageKind kind); }
public interface IKernelApplicationFormatContract : IKernelSubsystemContract { Boolean TryInspect(UInt64 packageAddress,UInt64 packageBytes,out InuExecutableMetadata metadata); Boolean TryValidateArchitecture(InuApplicationArchitecture architecture); Boolean TryValidateAbi(UInt16 major,UInt16 minor,InuApplicationAbi syscallAbi); Boolean TryEnumerateDependency(UInt64 packageAddress,UInt64 packageBytes,UInt32 index,out String id,out String versionConstraint); Boolean TryEnumerateRequiredCapability(UInt64 packageAddress,UInt64 packageBytes,UInt32 index,out String capability); Boolean TryEnumerateResource(UInt64 packageAddress,UInt64 packageBytes,UInt32 index,out String resourceName,out UInt64 offset,out UInt64 length); Boolean TryResolveNativeImage(UInt64 packageAddress,UInt64 packageBytes,out UInt64 nativeImageAddress,out UInt64 nativeImageBytes); }

public interface IKernelVfsContract : IKernelFilesystemContract
{
    Boolean TryUnmount(UInt64 mountId);
    Boolean TryWrite(UInt64 fileHandle,UInt64 offset,UInt64 bufferAddress,UInt32 bufferBytes,out UInt32 bytesWritten);
    Boolean TryOpenDirectory(String path,out UInt64 directoryHandle);
    Boolean TryReadDirectory(UInt64 directoryHandle,UInt64 entryIndex,UInt64 nameBuffer,UInt32 nameCapacityChars,out UInt32 nameLength,out UInt32 type,out UInt64 length,out UInt32 permissions);
    Boolean TryCloseDirectory(UInt64 directoryHandle);
    Boolean TryGetPermissions(String path,out UInt32 permissions);
    Boolean TrySetPermissions(String path,UInt32 permissions);
    Boolean TryRegisterFileSystemDriver(UInt32 fileSystemType,UInt64 callbackTable,UInt32 features);
    Boolean SupportsAsyncIo();
}
public enum KernelNetworkLayer : Byte { Nic=1,Ethernet=2,Arp=3,Ndp=4,Ipv4=5,Ipv6=6,Icmp=7,Udp=8,Tcp=9,Sockets=10,Dns=11 }
public interface IKernelNetworkStackContract : IKernelNetworkingContract { Boolean TryGetLayerState(KernelNetworkLayer layer,out KernelSubsystemState state); Boolean TryGetNic(UInt64 interfaceHandle,out UInt64 deviceHandle,out UInt32 mtu,out UInt32 state); Boolean TryReceiveEthernet(UInt64 interfaceHandle,UInt64 frameAddress,UInt32 frameBytes); Boolean TryResolveArp(UInt64 interfaceHandle,UInt32 ipv4Address,out UInt64 macAddress); Boolean TryProcessNdp(UInt64 interfaceHandle,UInt64 ipv6PacketAddress,UInt32 packetBytes); Boolean TryConfigureIpv4(UInt64 interfaceHandle,UInt32 address,UInt32 prefix,UInt32 gateway); Boolean TryConfigureIpv6(UInt64 interfaceHandle,UInt64 addressHigh,UInt64 addressLow,UInt32 prefix); Boolean TrySendIcmpEcho(UInt64 interfaceHandle,UInt32 destination,UInt16 identifier,UInt16 sequence,UInt64 payloadAddress,UInt32 payloadBytes); Boolean TryCreateSocket(UInt32 type,out UInt64 socketHandle); Boolean TryBind(UInt64 socketHandle,UInt64 address,UInt16 port); Boolean TryConnect(UInt64 socketHandle,UInt64 address,UInt16 port); Boolean TryListen(UInt64 socketHandle,UInt32 backlog); Boolean TryAccept(UInt64 socketHandle,out UInt64 clientSocket); Boolean TrySendUdp(UInt64 socketHandle,UInt64 destination,UInt16 port,UInt64 bufferAddress,UInt32 bufferBytes); new Boolean TryReceive(UInt64 socketHandle,UInt64 bufferAddress,UInt32 bufferBytes,out UInt32 received); Boolean TryResolveName(String host,out UInt64 address); Boolean TryResolveNameIpv6(String host,out UInt64 addressHigh,out UInt64 addressLow); }
public enum KernelPowerState : Byte { Working=0,Sleep1=1,Sleep2=2,Sleep3=3,Hibernate=4,Off=5 }
public enum KernelCpuIdleState : Byte { RunningC0=0,IdleC1=1,IdleC2=2,IdleC3=3 }
public interface IKernelPowerManagementContract : IKernelPowerContract { Boolean TryEnterSystemState(KernelPowerState state); Boolean TryGetSystemPowerState(out KernelPowerState state); Boolean TryGetPowerCapabilities(out UInt32 capabilities); Boolean TryConfigureResumeVector(UInt32 physicalAddress); Boolean TrySetCpuPowerState(UInt32 cpu,UInt32 state); Boolean TryGetCpuPowerState(UInt32 cpu,out UInt32 state); Boolean TryResumeDevicePower(UInt64 deviceId); Boolean TrySuspendStartedDevices(); Boolean TryResumeSuspendedDevices(); }
public interface IKernelTimekeepingContract : IKernelTimeContract { Boolean TryGetWallClockNanoseconds(out Int64 nanoseconds); Boolean TrySetWallClockNanoseconds(Int64 nanoseconds); Boolean TryGetHighResolutionCounter(out UInt64 ticks,out UInt64 frequency); Boolean TryGetSchedulerTick(out UInt64 tick,out UInt64 periodNanoseconds); Boolean TryScheduleTimeoutAfter(UInt64 delayNanoseconds,UInt64 cookie,out UInt64 timeoutId); Boolean TryDequeueExpiredTimeout(out UInt64 timeoutId,out UInt64 cookie); Boolean TryGetNextTimeoutDeadline(out UInt64 deadlineNanoseconds); }

public readonly struct InuQemuMatrixEntry { public InuQemuMatrixEntry(UInt32 cpus,UInt64 memoryMiB,String storage,String network,String graphics,String usb,String firmware){Cpus=cpus;MemoryMiB=memoryMiB;Storage=storage;Network=network;Graphics=graphics;Usb=usb;Firmware=firmware;} public UInt32 Cpus{get;} public UInt64 MemoryMiB{get;} public String Storage{get;} public String Network{get;} public String Graphics{get;} public String Usb{get;} public String Firmware{get;} }
public readonly struct InuBuildProvenance { public InuBuildProvenance(String sdkVersion,String compilerVersion,String linkerVersion,String configuration,String sourceHash,String dependencyHash){SdkVersion=sdkVersion;CompilerVersion=compilerVersion;LinkerVersion=linkerVersion;Configuration=configuration;SourceHash=sourceHash;DependencyHash=dependencyHash;} public String SdkVersion{get;} public String CompilerVersion{get;} public String LinkerVersion{get;} public String Configuration{get;} public String SourceHash{get;} public String DependencyHash{get;} }
public readonly struct InuSdkVersionManifest { public InuSdkVersionManifest(UInt32 schemaVersion,String sdkVersion,String apiVersion,String abiVersion,KernelArchitecture[] architectures,String dotnetVersion,String ilcVersion,String llvmVersion,String[] driverFormats,UInt32[] projectSchemas){SchemaVersion=schemaVersion;SdkVersion=sdkVersion;ApiVersion=apiVersion;AbiVersion=abiVersion;Architectures=architectures;DotnetVersion=dotnetVersion;IlcVersion=ilcVersion;LlvmVersion=llvmVersion;DriverFormats=driverFormats;ProjectSchemas=projectSchemas;} public UInt32 SchemaVersion{get;} public String SdkVersion{get;} public String ApiVersion{get;} public String AbiVersion{get;} public KernelArchitecture[] Architectures{get;} public String DotnetVersion{get;} public String IlcVersion{get;} public String LlvmVersion{get;} public String[] DriverFormats{get;} public UInt32[] ProjectSchemas{get;} }
