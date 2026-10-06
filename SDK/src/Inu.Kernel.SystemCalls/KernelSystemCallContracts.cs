using System;

namespace Inu.Kernel.SystemCalls;

/// <summary>Identifies the syscall convention selected for one protected kernel entry.</summary>
public enum KernelSystemCallAbi : Byte
{
    Unknown = 0,
    GetSetEvent = 1,
    Linux = 2,
    Nt = 3
}

/// <summary>Identifies the only three native Inu syscall classes.</summary>
public enum KernelSystemCallOperation : Byte
{
    Get = 0,
    Set = 1,
    Event = 2
}

/// <summary>Canonical semantic messages carried by native Inu syscall envelopes.</summary>
public static class KernelSystemCallMessages
{
    public const String SystemVersion = "system.version";
    public const String TimeMonotonic = "time.monotonic";
    public const String CpuOnlineCount = "cpu.online.count";
    public const String SchedulerQuantum = "scheduler.quantum";
    public const String SchedulerYield = "scheduler.yield";
    public const String ProcessIdCurrent = "process.id.current";
    public const String ProcessCommandComplete = "process.command.complete"; // legacy alias
    public const String ProcessExit = "process.exit";
    public const String ProcessSpawn = "process.spawn";
    public const String ProcessWait = "process.wait";
    public const String ProcessArguments = "process.arguments";
    public const String ProcessEnvironment = "process.environment";
    public const String ProcessCurrentDirectory = "process.current-directory";
    public const String ProcessControl = "process.control";
    public const String ConsoleOutput = "console.output";
    public const String ConsoleInput = "console.input";
    public const String ConsoleClear = "console.clear";
    public const String StressControl = "runtime.stress.control";
    public const String StressStatus = "runtime.stress.status";
    public const String KeyboardLayout = "input.keyboard.layout";
    public const String ConsoleFont = "console.font";
    public const String ConsoleBuffering = "console.buffering";
    public const String DeviceStateNext = "device.state.next";
    public const String GuiCapabilities = "gui.capabilities";
    public const String GuiSurfaceCreate = "gui.surface.create";
    public const String GuiSurfaceDestroy = "gui.surface.destroy";
    public const String GuiSurfaceGeometry = "gui.surface.geometry";
    public const String GuiSurfaceVisibility = "gui.surface.visibility";
    public const String GuiSurfacePresent = "gui.surface.present";
    public const String GuiFocus = "gui.focus";
    public const String GuiEventNext = "gui.event.next";
    public const String GuiWindowClose = "gui.window.close";
    public const String SessionLogin = "session.login";
    public const String FileOpen = "file.open";
    public const String FileRead = "file.read";
    public const String FileWrite = "file.write";
    public const String FileCreate = "file.create";
    public const String FileDelete = "file.delete";
    public const String FileClose = "file.close";
    public const String DirectoryOpen = "directory.open";
    public const String DirectoryCreate = "directory.create";
    public const String DirectoryDelete = "directory.delete";
    public const String DirectoryClose = "directory.close";
}

/// <summary>
/// User-visible native Inu syscall envelope. AppName, PID and Message are the common identity/routing
/// fields; Data, Output, typed values, flags, correlation and capability fields carry operation-specific
/// information without inventing another syscall number.
/// </summary>
public struct KernelSystemCallMessage
{
    public const UInt64 CurrentVersion = 1UL;
    public const UInt64 SerializedBytes = 144UL;
    public const UInt64 MaximumAppNameBytes = 64UL;
    public const UInt64 MaximumMessageBytes = 96UL;
    public const UInt64 MaximumPayloadBytes = 1048576UL;

    public UInt64 Version;
    public UInt64 ByteSize;
    public UInt64 AppNameAddress;
    public UInt64 AppNameLength;
    public UInt64 ProcessId;
    public UInt64 MessageAddress;
    public UInt64 MessageLength;
    public UInt64 DataAddress;
    public UInt64 DataLength;
    public UInt64 OutputAddress;
    public UInt64 OutputCapacity;
    public UInt64 CorrelationId;
    public UInt64 Flags;
    public UInt64 Value0;
    public UInt64 Value1;
    public UInt64 Value2;
    public UInt64 Value3;
    public UInt64 Capability;
}

public enum KernelSystemCallError : Int64
{
    Success = 0,
    NotPermitted = -1,
    NotFound = -2,
    InvalidArgument = -22,
    NotImplemented = -38,
    Fault = -14,
    Busy = -16
}

/// <summary>Defines common NTSTATUS values used by the NT-style compatibility dispatcher.</summary>
public enum KernelNtStatus : UInt32
{
    Success = 0x00000000U,
    NotImplemented = 0xC0000002U,
    InvalidParameter = 0xC000000DU,
    AccessViolation = 0xC0000005U
}

/// <summary>Contains the syscall arguments and, for native Inu calls, the validated message envelope.</summary>
public unsafe struct KernelSystemCallFrame
{
    internal KernelSystemCallFrame(KernelSystemCallAbi abi, KernelSystemCallOperation operation, UInt32 service, UInt64 a0, UInt64 a1, UInt64 a2, UInt64 a3, UInt64 a4, UInt64 a5, KernelSystemCallMessage message)
    { Abi=abi; Operation=operation; ServiceNumber=service; Argument0=a0; Argument1=a1; Argument2=a2; Argument3=a3; Argument4=a4; Argument5=a5; NativeMessage=message; }
    public KernelSystemCallAbi Abi { get; }
    public KernelSystemCallOperation Operation { get; }
    /// <summary>Numeric service identifier for Linux/NT compatibility. Always zero for native Inu.</summary>
    public UInt32 ServiceNumber { get; }
    public UInt64 Argument0 { get; }
    public UInt64 Argument1 { get; }
    public UInt64 Argument2 { get; }
    public UInt64 Argument3 { get; }
    public UInt64 Argument4 { get; }
    public UInt64 Argument5 { get; }
    public KernelSystemCallMessage NativeMessage { get; }
}

/// <summary>Reports the active protected syscall environment.</summary>
public readonly struct KernelSystemCallCapabilities
{
    internal KernelSystemCallCapabilities(Boolean syscall, Boolean smap, UInt32 configured, UInt32 processors, UInt64 stackBytes)
    { HasX64Syscall=syscall; SmapEnabled=smap; ConfiguredProcessors=configured; ProcessorCount=processors; SyscallStackBytes=stackBytes; }
    public Boolean HasX64Syscall { get; }
    public Boolean SmapEnabled { get; }
    public UInt32 ConfiguredProcessors { get; }
    public UInt32 ProcessorCount { get; }
    public UInt64 SyscallStackBytes { get; }
    public Boolean SupportsGetSetEvent => true;
    public Boolean SupportsStructuredMessages => true;
    public Boolean SupportsLinuxStyle => true;
    public Boolean SupportsNtStyle => true;
}
