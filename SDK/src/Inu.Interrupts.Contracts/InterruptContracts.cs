using Inu.Architecture;
using Inu.Primitives;
using System.Runtime.InteropServices;

namespace Inu.Interrupts;


/// <summary>Defines the operational state of Inu interrupt delivery and dispatch.</summary>
public enum InterruptLifecycleState : byte
{
    /// <summary>The interrupt runtime has not been started.</summary>
    Stopped = 0,
    /// <summary>The runtime is initialized and may be started.</summary>
    Ready = 1,
    /// <summary>Normal maskable interrupt dispatch is active.</summary>
    Running = 2,
    /// <summary>Normal maskable interrupt dispatch is suspended while exception handling remains defined.</summary>
    Paused = 3
}

/// <summary>Identifies the semantic source/type of an interrupt independently of its physical controller.</summary>
public enum InterruptType : byte
{
    /// <summary>An architectural processor exception such as #PF or #GP.</summary>
    CpuException = 0,
    /// <summary>A non-maskable interrupt.</summary>
    NonMaskable = 1,
    /// <summary>A legacy external IRQ routed through an interrupt controller.</summary>
    LegacyIrq = 2,
    /// <summary>A global-system interrupt routed through an I/O APIC.</summary>
    IoApic = 3,
    /// <summary>A PCI/PCIe Message Signalled Interrupt.</summary>
    Msi = 4,
    /// <summary>A PCI/PCIe MSI-X interrupt.</summary>
    MsiX = 5,
    /// <summary>A local-APIC timer interrupt.</summary>
    LocalApicTimer = 6,
    /// <summary>An inter-processor interrupt.</summary>
    InterProcessor = 7,
    /// <summary>A software-generated INT/trap interrupt.</summary>
    Software = 8,
    /// <summary>The local APIC spurious interrupt vector.</summary>
    Spurious = 9,
    /// <summary>A machine-check interrupt/exception.</summary>
    MachineCheck = 10,
    /// <summary>A hypervisor/virtualization-originated interrupt or exception.</summary>
    Virtualization = 11,
    /// <summary>An implementation-reserved platform interrupt.</summary>
    Platform = 12
}

/// <summary>Classifies every x64 interrupt vector according to Inu's stable vector layout.</summary>
public enum InterruptVectorClass : byte
{
    /// <summary>Architectural processor exceptions (vectors 0x00 through 0x1F).</summary>
    CpuException = 0,
    /// <summary>Platform interrupts (vectors 0x20 through 0x2F).</summary>
    Platform = 1,
    /// <summary>Kernel-reserved interrupts (vectors 0x30 through 0x3F).</summary>
    KernelReserved = 2,
    /// <summary>Dynamically allocated device interrupts (vectors 0x40 through 0xDF).</summary>
    DynamicDevice = 3,
    /// <summary>Kernel inter-processor interrupts (vectors 0xE0 through 0xEF).</summary>
    KernelIpi = 4,
    /// <summary>Local APIC interrupts (vectors 0xF0 through 0xF7).</summary>
    LocalApic = 5,
    /// <summary>Architecture-reserved interrupts (vectors 0xF8 through 0xFE).</summary>
    ArchitectureReserved = 6,
    /// <summary>The spurious interrupt (vector 0xFF).</summary>
    Spurious = 7
}

/// <summary>Defines Inu's formal x64 interrupt-vector ABI.</summary>
public static class InterruptVectorLayout
{
    /// <summary>The first vector in the CPU exception range (inclusive). Vector 0x00.</summary>
    public const byte FirstCpuExceptionVector = 0x00;
    /// <summary>The last vector in the CPU exception range (inclusive). Vector 0x1F.</summary>
    public const byte LastCpuExceptionVector = 0x1F;
    /// <summary>The first vector in the platform range (inclusive). Vector 0x20.</summary>
    public const byte FirstPlatformVector = 0x20;
    /// <summary>The last vector in the platform range (inclusive). Vector 0x2F.</summary>
    public const byte LastPlatformVector = 0x2F;
    /// <summary>The first vector in the kernel reserved range (inclusive). Vector 0x30.</summary>
    public const byte FirstKernelReservedVector = 0x30;
    /// <summary>The last vector in the kernel reserved range (inclusive). Vector 0x3F.</summary>
    public const byte LastKernelReservedVector = 0x3F;
    /// <summary>The first vector in the dynamic device range (inclusive). Vector 0x40.</summary>
    public const byte FirstDynamicDeviceVector = 0x40;
    /// <summary>The last vector in the dynamic device range (inclusive). Vector 0xDF.</summary>
    public const byte LastDynamicDeviceVector = 0xDF;
    /// <summary>The first vector in the kernel IPI range (inclusive). Vector 0xE0.</summary>
    public const byte FirstKernelIpiVector = 0xE0;
    /// <summary>The last vector in the kernel IPI range (inclusive). Vector 0xEF.</summary>
    public const byte LastKernelIpiVector = 0xEF;
    /// <summary>The first vector in the local APIC range (inclusive). Vector 0xF0.</summary>
    public const byte FirstLocalApicVector = 0xF0;
    /// <summary>The last vector in the local APIC range (inclusive). Vector 0xF7.</summary>
    public const byte LastLocalApicVector = 0xF7;
    /// <summary>The first vector in the architecture reserved range (inclusive). Vector 0xF8.</summary>
    public const byte FirstArchitectureReservedVector = 0xF8;
    /// <summary>The last vector in the architecture reserved range (inclusive). Vector 0xFE.</summary>
    public const byte LastArchitectureReservedVector = 0xFE;
    /// <summary>Identifies the spurious interrupt vector. Vector 0xFF.</summary>
    public const byte SpuriousVector = 0xFF;

    /// <summary>Requests CPU rescheduling. Vector 0xE0.</summary>
    public const byte IpiRescheduleVector = 0xE0;
    /// <summary>Requests invalidation of cached address translations. Vector 0xE1.</summary>
    public const byte IpiTlbShootdownVector = 0xE1;
    /// <summary>Requests execution of a function on another CPU. Vector 0xE2.</summary>
    public const byte IpiCallFunctionVector = 0xE2;
    /// <summary>Requests scheduler load rebalancing. Vector 0xE3.</summary>
    public const byte IpiSchedulerRebalanceVector = 0xE3;
    /// <summary>Requests a debugger break on another CPU. Vector 0xEC.</summary>
    public const byte IpiDebugBreakVector = 0xEC;
    /// <summary>Requests that another CPU stop. Vector 0xED.</summary>
    public const byte IpiCpuStopVector = 0xED;
    /// <summary>Requests that another CPU wake. Vector 0xEE.</summary>
    public const byte IpiCpuWakeVector = 0xEE;
    /// <summary>Requests that other CPUs stop during a kernel panic. Vector 0xEF.</summary>
    public const byte IpiPanicStopVector = 0xEF;
    /// <summary>Delivers local APIC timer interrupts. Vector 0xF0.</summary>
    public const byte LocalApicTimerVector = 0xF0;
    /// <summary>Delivers local APIC error interrupts. Vector 0xF1.</summary>
    public const byte LocalApicErrorVector = 0xF1;
    /// <summary>Delivers local APIC performance-monitoring interrupts. Vector 0xF2.</summary>
    public const byte LocalApicPerformanceVector = 0xF2;

    /// <summary>Returns true when the vector can be dynamically allocated to a device.</summary>
    public static bool IsDynamicDeviceVector(byte vector) => vector >= FirstDynamicDeviceVector && vector <= LastDynamicDeviceVector;

    /// <summary>Gets the stable Inu vector class for a vector.</summary>
    public static InterruptVectorClass GetClass(byte vector)
    {
        if (vector <= LastCpuExceptionVector) return InterruptVectorClass.CpuException;
        if (vector <= LastPlatformVector) return InterruptVectorClass.Platform;
        if (vector <= LastKernelReservedVector) return InterruptVectorClass.KernelReserved;
        if (vector <= LastDynamicDeviceVector) return InterruptVectorClass.DynamicDevice;
        if (vector <= LastKernelIpiVector) return InterruptVectorClass.KernelIpi;
        if (vector <= LastLocalApicVector) return InterruptVectorClass.LocalApic;
        if (vector <= LastArchitectureReservedVector) return InterruptVectorClass.ArchitectureReserved;
        return InterruptVectorClass.Spurious;
    }
}

/// <summary>Defines the result returned by an interrupt handler.</summary>
public enum InterruptResult : byte
{
    /// <summary>The handler did not claim the vector.</summary>
    Unhandled = 0,
    /// <summary>The interrupt was handled and execution may resume.</summary>
    Handled = 1,
    /// <summary>The interrupt was handled and scheduling should occur.</summary>
    Reschedule = 2,
    /// <summary>The processor must stop because execution cannot safely resume.</summary>
    Fatal = 3
}

/// <summary>Defines the x64 gate type installed in an IDT entry.</summary>
public enum InterruptGateType : byte
{
    /// <summary>Clears IF while the handler runs.</summary>
    Interrupt = 0xE,
    /// <summary>Preserves IF while the handler runs.</summary>
    Trap = 0xF
}

/// <summary>Identifies the architectural CPU exception vectors.</summary>
public enum CpuExceptionVector : byte
{
    /// <summary>Identifies the divide error vector.</summary>
    DivideError = 0,
    /// <summary>Identifies the debug vector.</summary>
    Debug = 1,
    /// <summary>Identifies the non-maskable interrupt vector.</summary>
    NonMaskableInterrupt = 2,
    /// <summary>Identifies the breakpoint vector.</summary>
    Breakpoint = 3,
    /// <summary>Identifies the overflow vector.</summary>
    Overflow = 4,
    /// <summary>Identifies the bound-range exceeded vector.</summary>
    BoundRangeExceeded = 5,
    /// <summary>Identifies the invalid opcode vector.</summary>
    InvalidOpcode = 6,
    /// <summary>Identifies the device not available vector.</summary>
    DeviceNotAvailable = 7,
    /// <summary>Identifies the double fault vector.</summary>
    DoubleFault = 8,
    /// <summary>Identifies the legacy coprocessor segment overrun vector.</summary>
    CoprocessorSegmentOverrun = 9,
    /// <summary>Identifies the invalid task-state segment vector.</summary>
    InvalidTaskStateSegment = 10,
    /// <summary>Identifies the segment not present vector.</summary>
    SegmentNotPresent = 11,
    /// <summary>Identifies the stack-segment fault vector.</summary>
    StackSegmentFault = 12,
    /// <summary>Identifies the general protection fault vector.</summary>
    GeneralProtectionFault = 13,
    /// <summary>Identifies the page fault vector.</summary>
    PageFault = 14,
    /// <summary>Identifies the reserved vector 15 vector.</summary>
    Reserved15 = 15,
    /// <summary>Identifies the x87 floating-point exception vector.</summary>
    X87FloatingPoint = 16,
    /// <summary>Identifies the alignment check vector.</summary>
    AlignmentCheck = 17,
    /// <summary>Identifies the machine check vector.</summary>
    MachineCheck = 18,
    /// <summary>Identifies the SIMD floating-point exception vector.</summary>
    SimdFloatingPoint = 19,
    /// <summary>Identifies the virtualization exception vector.</summary>
    Virtualization = 20,
    /// <summary>Identifies the control-protection exception vector.</summary>
    ControlProtection = 21,
    /// <summary>Identifies the reserved vector 22 vector.</summary>
    Reserved22 = 22,
    /// <summary>Identifies the reserved vector 23 vector.</summary>
    Reserved23 = 23,
    /// <summary>Identifies the reserved vector 24 vector.</summary>
    Reserved24 = 24,
    /// <summary>Identifies the reserved vector 25 vector.</summary>
    Reserved25 = 25,
    /// <summary>Identifies the reserved vector 26 vector.</summary>
    Reserved26 = 26,
    /// <summary>Identifies the reserved vector 27 vector.</summary>
    Reserved27 = 27,
    /// <summary>Identifies the hypervisor-injection exception vector.</summary>
    HypervisorInjection = 28,
    /// <summary>Identifies the VMM communication exception vector.</summary>
    VmmCommunication = 29,
    /// <summary>Identifies the security exception vector.</summary>
    SecurityException = 30,
    /// <summary>Identifies the reserved vector 31 vector.</summary>
    Reserved31 = 31
}

/// <summary>Provides the stable managed/native x64 interrupt frame.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct InterruptContext
{
    /// <summary>Gets or sets the vector number.</summary>
    public ulong Vector;
    /// <summary>Gets or sets the normalised architectural error code.</summary>
    public ulong ErrorCode;
    /// <summary>Gets or sets the interrupted RIP.</summary>
    public ulong InstructionPointer;
    /// <summary>Gets or sets the interrupted CS.</summary>
    public ulong CodeSegment;
    /// <summary>Gets or sets the interrupted RFLAGS.</summary>
    public ulong Flags;
    /// <summary>Gets or sets the interrupted RSP.</summary>
    public ulong StackPointer;
    /// <summary>Gets or sets the interrupted SS.</summary>
    public ulong StackSegment;
    /// <summary>Gets or sets the captured CR0.</summary>
    public ulong ControlRegister0;
    /// <summary>Gets or sets the captured CR2 fault address.</summary>
    public ulong ControlRegister2;
    /// <summary>Gets or sets the captured CR3.</summary>
    public ulong ControlRegister3;
    /// <summary>Gets or sets the captured CR4.</summary>
    public ulong ControlRegister4;
    /// <summary>Gets or sets the current processor identifier.</summary>
    public ulong ProcessorId;
    /// <summary>Gets or sets the one when hardware changed privilege levels.</summary>
    public ulong PrivilegeTransition;
    /// <summary>Gets or sets the RAX.</summary>
    public ulong Rax;
    /// <summary>Gets or sets the RBX.</summary>
    public ulong Rbx;
    /// <summary>Gets or sets the RCX.</summary>
    public ulong Rcx;
    /// <summary>Gets or sets the RDX.</summary>
    public ulong Rdx;
    /// <summary>Gets or sets the RSI.</summary>
    public ulong Rsi;
    /// <summary>Gets or sets the RDI.</summary>
    public ulong Rdi;
    /// <summary>Gets or sets the RBP.</summary>
    public ulong Rbp;
    /// <summary>Gets or sets the R8.</summary>
    public ulong R8;
    /// <summary>Gets or sets the R9.</summary>
    public ulong R9;
    /// <summary>Gets or sets the R10.</summary>
    public ulong R10;
    /// <summary>Gets or sets the R11.</summary>
    public ulong R11;
    /// <summary>Gets or sets the R12.</summary>
    public ulong R12;
    /// <summary>Gets or sets the R13.</summary>
    public ulong R13;
    /// <summary>Gets or sets the R14.</summary>
    public ulong R14;
    /// <summary>Gets or sets the R15.</summary>
    public ulong R15;

    /// <summary>Gets whether the interrupt crossed a privilege boundary.</summary>
    public readonly bool HasPrivilegeTransition() => (PrivilegeTransition & 1) != 0;

    /// <summary>Gets whether hardware switched stacks because of privilege or IST policy.</summary>
    public readonly bool HasStackSwitch() => (PrivilegeTransition & 2) != 0;

    /// <summary>Gets whether this frame describes a page fault.</summary>
    public readonly bool IsPageFault() => Vector == (byte)CpuExceptionVector.PageFault;
}

/// <summary>Handles one normalised interrupt frame.</summary>
/// <param name="context">The writable processor context.</param>
/// <returns>The action required after dispatch.</returns>
public delegate InterruptResult InterruptHandler(ref InterruptContext context);

/// <summary>Controls how one IDT gate and managed registration are installed.</summary>
public readonly record struct InterruptRegistrationOptions(
    DescriptorPrivilegeLevel PrivilegeLevel,
    InterruptGateType GateType,
    byte InterruptStackTable,
    bool ReplaceExisting);

/// <summary>Identifies a successful registration for later removal.</summary>
public readonly record struct InterruptRegistrationHandle(byte Vector, ulong Generation);

/// <summary>Reports the outcome of a registration request.</summary>
public readonly record struct InterruptRegistrationResult(
    bool Succeeded,
    InterruptRegistrationHandle Handle,
    string Error);

/// <summary>Describes caller-owned storage and selectors for one processor's IDT.</summary>
public readonly record struct InterruptDescriptorTableConfiguration(
    ProcessorId ProcessorId,
    Address TableAddress,
    uint TableCapacity,
    SegmentSelector KernelCodeSelector,
    byte DoubleFaultInterruptStack,
    byte NonMaskableInterruptStack,
    byte MachineCheckInterruptStack);

/// <summary>Allocates driver vectors without colliding with exceptions or reserved ranges.</summary>
public interface IInterruptVectorAllocator
{
    /// <summary>Allocates one vector from the configured driver range.</summary>
    byte Allocate();
    /// <summary>Releases a previously allocated driver vector.</summary>
    bool Release(byte vector);
    /// <summary>Gets whether a vector is currently allocated.</summary>
    bool IsAllocated(byte vector);
}

/// <summary>Builds, installs and dispatches one processor-local IDT.</summary>
public interface IInterruptDescriptorTable
{
    /// <summary>Builds all 256 entries in caller-owned memory.</summary>
    bool Configure(InterruptDescriptorTableConfiguration configuration);
    /// <summary>Loads the configured IDT and native dispatcher.</summary>
    bool Install();
    /// <summary>Registers a handler and applies its gate policy.</summary>
    InterruptRegistrationResult Register(byte vector, InterruptHandler handler, InterruptRegistrationOptions options);
    /// <summary>Removes a registration identified by its generation-safe handle.</summary>
    bool Remove(InterruptRegistrationHandle handle);
    /// <summary>Gets whether a vector currently has a managed handler.</summary>
    bool IsRegistered(byte vector);
    /// <summary>Dispatches a context through the registered/default policy.</summary>
    InterruptResult Dispatch(ref InterruptContext context);
}

/// <summary>Receives fatal exception diagnostics without prescribing a console implementation.</summary>
public interface IExceptionDiagnosticSink
{
    /// <summary>Writes one diagnostic line.</summary>
    bool WriteLine(string text);
    /// <summary>Stops the current processor safely and does not resume corrupted execution.</summary>
    bool StopProcessor();
}
