using System;

namespace Inu.Kernel.Contracts;

/// <summary>Stable contract version shared by all first-generation Inu kernel subsystem boundaries.</summary>
public static class KernelSubsystemContractVersion
{
    /// <summary>Gets the subsystem contract major version.</summary>
    public const UInt16 Major = 1;
    /// <summary>Gets the subsystem contract minor version.</summary>
    public const UInt16 Minor = 0;
    /// <summary>Gets the printable subsystem contract version.</summary>
    public const String Current = "1.0";
}

/// <summary>Identifies each formal kernel subsystem boundary.</summary>
public enum KernelSubsystemId : Byte
{
    Memory=1, Interrupts=2, Scheduler=3, Processes=4, Syscalls=5, Drivers=6,
    Filesystem=7, Networking=8, Graphics=9, Input=10, Time=11, Power=12, Smp=13, Diagnostics=14, Security=15, Synchronization=16, Tests=17
}

/// <summary>Reports whether one subsystem implementation is ready for clients.</summary>
public enum KernelSubsystemState : Byte { Unavailable=0, Initializing=1, Ready=2, Degraded=3, Failed=4 }

/// <summary>Common immutable status exposed by every formal subsystem contract.</summary>
public readonly struct KernelSubsystemStatus
{
    /// <summary>Creates a subsystem status snapshot.</summary>
    public KernelSubsystemStatus(KernelSubsystemId id, KernelSubsystemState state, UInt16 major, UInt16 minor, UInt64 capabilities)
    { Id=id; State=state; ContractMajor=major; ContractMinor=minor; Capabilities=capabilities; }
    public KernelSubsystemId Id { get; }
    public KernelSubsystemState State { get; }
    public UInt16 ContractMajor { get; }
    public UInt16 ContractMinor { get; }
    public UInt64 Capabilities { get; }
    /// <summary>Returns true when this status is compatible with the requested major/minor contract.</summary>
    public Boolean IsCompatible(UInt16 requiredMajor, UInt16 requiredMinor) => ContractMajor==requiredMajor && ContractMinor>=requiredMinor;
}

/// <summary>Base boundary implemented by every Inu kernel subsystem service.</summary>
public interface IKernelSubsystemContract
{
    KernelSubsystemId Id { get; }
    UInt16 ContractMajor { get; }
    UInt16 ContractMinor { get; }
    Boolean TryGetStatus(out KernelSubsystemStatus status);
}

/// <summary>Formal memory boundary. Consumers request pages and translations through this contract rather than allocator internals.</summary>
public interface IKernelMemoryContract : IKernelSubsystemContract
{
    Boolean TryAllocatePages(UInt64 pageCount, UInt64 alignment, out UInt64 physicalAddress);
    Boolean TryReleasePages(UInt64 physicalAddress, UInt64 pageCount);
    Boolean TryTranslate(UInt64 virtualAddress, out UInt64 physicalAddress);
}

/// <summary>Formal interrupt boundary. Drivers request logical vectors without programming APIC/PIC/MSI machinery directly.</summary>
public interface IKernelInterruptContract : IKernelSubsystemContract
{
    Boolean TryAllocateVector(UInt32 source, out UInt32 vector);
    Boolean TryMask(UInt32 vector, Boolean masked);
    Boolean TrySendIpi(UInt32 processorIndex, UInt32 vector);
}

/// <summary>Formal scheduler boundary for thread creation, state changes and affinity.</summary>
public interface IKernelSchedulerContract : IKernelSubsystemContract
{
    Boolean TryCreateThread(UInt64 entryPoint, UInt64 argument, UInt64 affinityMask, out UInt64 threadId);
    Boolean TrySetRunnable(UInt64 threadId, Boolean runnable);
    Boolean TrySetAffinity(UInt64 threadId, UInt64 affinityMask);
}

/// <summary>Formal process boundary for executable creation, termination and process queries.</summary>
public interface IKernelProcessContract : IKernelSubsystemContract
{
    Boolean TryCreateProcess(UInt64 imageAddress, UInt64 imageBytes, out UInt64 processId);
    Boolean TryTerminateProcess(UInt64 processId, Int64 exitCode);
    Boolean TryGetCurrentProcess(out UInt64 processId);
}

/// <summary>Formal syscall boundary shared by Inu Get/Set/Event, Linux-style and NT-style dispatch.</summary>
public interface IKernelSyscallContract : IKernelSubsystemContract
{
    Boolean TryRegister(UInt32 abi, UInt32 service, UInt64 handlerAddress);
    Boolean TryUnregister(UInt32 abi, UInt32 service);
    Boolean TryDispatch(UInt32 abi, UInt32 service, UInt64 argument0, UInt64 argument1, out Int64 result);
}

/// <summary>Formal driver boundary for binding, lifecycle and declared-capability enforcement.</summary>
public interface IKernelDriverContract : IKernelSubsystemContract
{
    Boolean TryBind(UInt64 deviceId, UInt64 driverId, out UInt64 bindingId);
    Boolean TryStart(UInt64 bindingId);
    Boolean TryStop(UInt64 bindingId);
    Boolean TryReset(UInt64 bindingId);
    Boolean TryGrantCapability(UInt64 bindingId, UInt64 capability, UInt64 start, UInt64 length, UInt32 access, out UInt64 grantToken);
    Boolean TryRevokeCapability(UInt64 bindingId, UInt64 grantToken);
}

/// <summary>Formal filesystem/VFS boundary. Filesystems remain providers below this interface.</summary>
public interface IKernelFilesystemContract : IKernelSubsystemContract
{
    Boolean TryMount(UInt64 volumeId, String path, out UInt64 mountId);
    Boolean TryOpen(String path, UInt32 access, out UInt64 fileHandle);
    Boolean TryRead(UInt64 fileHandle, UInt64 offset, UInt64 bufferAddress, UInt32 bufferBytes, out UInt32 bytesRead);
    Boolean TryClose(UInt64 fileHandle);
}

/// <summary>Formal network-stack boundary for interface-independent packet and socket operations.</summary>
public interface IKernelNetworkingContract : IKernelSubsystemContract
{
    Boolean TryOpenSocket(UInt32 family, UInt32 type, UInt32 protocol, out UInt64 socketHandle);
    Boolean TrySend(UInt64 socketHandle, UInt64 bufferAddress, UInt32 bufferBytes, out UInt32 bytesSent);
    Boolean TryReceive(UInt64 socketHandle, UInt64 bufferAddress, UInt32 bufferBytes, out UInt32 bytesReceived);
    Boolean TryCloseSocket(UInt64 socketHandle);
}

/// <summary>Formal graphics boundary independent of GOP, VirtIO GPU or future hardware drivers.</summary>
public interface IKernelGraphicsContract : IKernelSubsystemContract
{
    Boolean TryGetFramebuffer(out UInt64 address, out UInt32 width, out UInt32 height, out UInt32 strideBytes);
    Boolean TrySetMode(UInt32 width, UInt32 height, UInt32 bitsPerPixel);
    Boolean TryPresent(UInt64 sourceAddress, UInt32 sourceBytes);
}

/// <summary>Formal input boundary independent of PS/2, USB HID or future input buses.</summary>
public interface IKernelInputContract : IKernelSubsystemContract
{
    Boolean TryReadEvent(out UInt32 deviceId, out UInt32 eventType, out Int64 value0, out Int64 value1);
    Boolean TrySetKeyboardLayout(UInt32 layoutId);
}

/// <summary>Formal timekeeping boundary separating monotonic time, wall time and timeout scheduling.</summary>
public interface IKernelTimeContract : IKernelSubsystemContract
{
    Boolean TryGetMonotonicNanoseconds(out UInt64 nanoseconds);
    Boolean TryGetUnixNanoseconds(out Int64 nanoseconds);
    Boolean TryScheduleTimeout(UInt64 deadlineNanoseconds, UInt64 cookie, out UInt64 timeoutId);
    Boolean TryCancelTimeout(UInt64 timeoutId);
}

/// <summary>Formal power-management boundary above ACPI and architecture-specific reset machinery.</summary>
public interface IKernelPowerContract : IKernelSubsystemContract
{
    Boolean TryShutdown();
    Boolean TryReboot();
    Boolean TrySuspend(UInt32 state);
    Boolean TrySuspendDevice(UInt64 deviceId);
    Boolean TryResumeDevice(UInt64 deviceId);
}

/// <summary>Formal SMP boundary for CPU enumeration, affinity and inter-processor coordination.</summary>
public interface IKernelSmpContract : IKernelSubsystemContract
{
    Boolean TryGetProcessorCount(out UInt32 processorCount);
    Boolean TryGetCurrentProcessor(out UInt32 processorIndex);
    Boolean TrySetProcessorOnline(UInt32 processorIndex, Boolean online);
    Boolean TrySendIpi(UInt32 processorIndex, UInt32 vector);
}
