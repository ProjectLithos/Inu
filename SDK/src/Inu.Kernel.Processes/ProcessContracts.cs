using System;

namespace Inu.Kernel.Processes;

/// <summary>Identifies an executable image format understood by the Inu process loader.</summary>
public enum ProcessExecutableFormat : Byte
{
    /// <summary>The image format is not recognized.</summary>
    Unknown = 0,
    /// <summary>System V ELF64 for x86-64.</summary>
    Elf64 = 1,
    /// <summary>Microsoft PE32+ for x86-64.</summary>
    PortableExecutable64 = 2
}

/// <summary>Identifies the lifecycle state of a user process.</summary>
public enum ProcessSegmentProtection : Byte
{
    /// <summary>The segment can be read.</summary>
    Read = 1,
    /// <summary>The segment can be written.</summary>
    Write = 2,
    /// <summary>The segment can be executed.</summary>
    Execute = 4
}

/// <summary>Identifies the lifecycle state of a user process.</summary>
public enum KernelProcessState : Byte
{
    /// <summary>The process-table slot is unused.</summary>
    Unused = 0,
    /// <summary>The image and address space are constructed but have not entered user mode.</summary>
    Ready = 1,
    /// <summary>The process has entered ring 3.</summary>
    Running = 2,
    /// <summary>The process was terminated and its owned image pages were released.</summary>
    Terminated = 3,
    /// <summary>Process startup failed after construction.</summary>
    Faulted = 4,
    /// <summary>The command reported completion and is quiescent pending Set(Process, PID, Kill).</summary>
    Completed = 5,
    /// <summary>The process-table slot is reserved while image/address-space construction is in progress.</summary>
    Loading = 6,
    /// <summary>The process has been claimed for teardown and cannot be started or released twice.</summary>
    Terminating = 7
}

/// <summary>Describes which interactive ownership rules apply to a user process.</summary>
public enum KernelProcessOwnership : Byte
{
    /// <summary>The process belongs to the active interactive command and may be cancelled by Ctrl-C.</summary>
    Foreground = 1,
    /// <summary>The process is independent of the interactive command and is not cancelled by foreground Ctrl-C.</summary>
    Background = 2
}

/// <summary>Identifies state-changing process controls carried by Set(ProcessControl).</summary>
public enum KernelProcessControl : Byte
{
    /// <summary>Release the completed process and all resources owned by it.</summary>
    Kill = 1
}

/// <summary>Stable reason codes returned by the Inu application/package loader.</summary>
public enum InuApplicationLoadError : Byte
{
    None = 0,
    InvalidArgument = 1,
    MalformedPackage = 2,
    UnsupportedArchitecture = 3,
    UnsupportedAbi = 4,
    UnsupportedFlags = 5,
    SignatureRequired = 6,
    DependenciesUnavailable = 7,
    CapabilitiesUnavailable = 8,
    NativeImageUnavailable = 9,
    MalformedExecutable = 10,
    EntryPointMismatch = 11,
    AddressSpaceUnavailable = 12,
    MappingFailed = 13,
    SecurityPolicyRejected = 14,
    ProcessTableFull = 15
}

/// <summary>Describes one loadable executable segment using ordinary .NET value semantics.</summary>
public readonly struct ProcessImageSegment
{
    /// <summary>Creates an immutable load-segment description.</summary>
    public ProcessImageSegment(UInt64 virtualAddress, UInt64 memorySize, UInt64 fileSize, UInt64 fileOffset, ProcessSegmentProtection protection)
    { VirtualAddress=virtualAddress; MemorySize=memorySize; FileSize=fileSize; FileOffset=fileOffset; Protection=protection; }
    public UInt64 VirtualAddress { get; }
    public UInt64 MemorySize { get; }
    public UInt64 FileSize { get; }
    public UInt64 FileOffset { get; }
    public ProcessSegmentProtection Protection { get; }
}

/// <summary>Reports validated executable metadata before any pages are allocated.</summary>
public readonly struct ProcessExecutableInfo
{
    /// <summary>Creates an executable-information snapshot.</summary>
    public ProcessExecutableInfo(ProcessExecutableFormat format, UInt64 entryPoint, UInt64 imageBase, UInt32 segmentCount, Boolean positionIndependent)
    { Format=format; EntryPoint=entryPoint; ImageBase=imageBase; SegmentCount=segmentCount; IsPositionIndependent=positionIndependent; }
    public ProcessExecutableFormat Format { get; }
    public UInt64 EntryPoint { get; }
    public UInt64 ImageBase { get; }
    public UInt32 SegmentCount { get; }
    public Boolean IsPositionIndependent { get; }
}

/// <summary>Provides an immutable public snapshot of one Inu user process.</summary>
public readonly struct KernelProcessInfo
{
    /// <summary>Creates a process-information snapshot.</summary>
    public KernelProcessInfo(UInt64 id, KernelProcessState state, KernelProcessOwnership ownership, ProcessExecutableFormat format, Inu.ApplicationFormat.InuApplicationAbi syscallAbi, UInt64 pageTableRoot, UInt64 entryPoint, UInt64 stackBase, UInt64 stackTop, UInt64 stackGuardBase, UInt64 applicationIdHash, UInt64 applicationNameHash, UInt64 applicationVersionHash, Int64 exitCode)
    { Id=id; State=state; Ownership=ownership; ExecutableFormat=format; SyscallAbi=syscallAbi; PageTableRoot=pageTableRoot; EntryPoint=entryPoint; StackBase=stackBase; StackTop=stackTop; StackGuardBase=stackGuardBase; ApplicationIdHash=applicationIdHash; ApplicationNameHash=applicationNameHash; ApplicationVersionHash=applicationVersionHash; ExitCode=exitCode; }
    public UInt64 Id { get; }
    /// <summary>Compatibility/readability alias for the stable process identifier.</summary>
    public UInt64 ProcessId => Id;
    public KernelProcessState State { get; }
    public KernelProcessOwnership Ownership { get; }
    public ProcessExecutableFormat ExecutableFormat { get; }
    public Inu.ApplicationFormat.InuApplicationAbi SyscallAbi { get; }
    public UInt64 PageTableRoot { get; }
    public UInt64 EntryPoint { get; }
    public UInt64 StackBase { get; }
    public UInt64 StackTop { get; }
    public UInt64 StackGuardBase { get; }
    public UInt64 StackGuardBytes => 4096UL;
    /// <summary>Stable FNV-1a hash of the packaged application id, or zero for a raw native image.</summary>
    public UInt64 ApplicationIdHash { get; }
    /// <summary>Stable FNV-1a hash of the packaged display name, or zero for a raw native image.</summary>
    public UInt64 ApplicationNameHash { get; }
    /// <summary>Stable FNV-1a hash of the packaged application version, or zero for a raw native image.</summary>
    public UInt64 ApplicationVersionHash { get; }
    public Int64 ExitCode { get; }
}

/// <summary>Describes the bounded bootstrap process facility.</summary>
public readonly struct KernelProcessCapabilities
{
    /// <summary>Creates a process-capability snapshot.</summary>
    public KernelProcessCapabilities(UInt32 maximumProcesses, UInt32 activeProcesses, UInt32 maximumSegments, UInt64 defaultStackBytes, Boolean elf64, Boolean pe64, Boolean concurrentExecution, UInt32 maximumConcurrentProcessors)
    { MaximumProcesses=maximumProcesses; ActiveProcessCount=activeProcesses; MaximumSegments=maximumSegments; DefaultStackBytes=defaultStackBytes; SupportsElf64=elf64; SupportsPortableExecutable64=pe64; SupportsConcurrentExecution=concurrentExecution; MaximumConcurrentProcessors=maximumConcurrentProcessors; }
    public UInt32 MaximumProcesses { get; }
    public UInt32 ActiveProcessCount { get; }
    public UInt32 MaximumSegments { get; }
    public UInt64 DefaultStackBytes { get; }
    public Boolean SupportsElf64 { get; }
    public Boolean SupportsPortableExecutable64 { get; }
    public Boolean SupportsConcurrentExecution { get; }
    public UInt32 MaximumConcurrentProcessors { get; }
}

/// <summary>Reports one end-to-end packaged application/ring-3/syscall/exit self-test.</summary>
public readonly struct KernelUserModeSelfTestResult
{
    internal KernelUserModeSelfTestResult(Boolean packageValidated,Boolean processCreated,Boolean enteredRing3,UInt64 processId,Int64 exitCode)
    {PackageValidated=packageValidated;ProcessCreated=processCreated;EnteredRing3=enteredRing3;ProcessId=processId;ExitCode=exitCode;}
    public Boolean PackageValidated{get;}
    public Boolean ProcessCreated{get;}
    public Boolean EnteredRing3{get;}
    public UInt64 ProcessId{get;}
    public Int64 ExitCode{get;}
    public Boolean Passed=>PackageValidated&&ProcessCreated&&EnteredRing3&&ExitCode==0L;
}
