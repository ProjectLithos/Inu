using System;
using System.Runtime.InteropServices;

namespace Inu.Kernel.Internal.X64;

/// <summary>Contains the private x64 native ABI used by managed kernel services.</summary>
public static class Native
{
    /// <summary>Initializes the x64 COM1 serial device used by the managed console.</summary>
    public static Boolean InitializeSerial()
    {
        if (!WritePort8(0x3F9, 0x00)) return false;
        if (!WritePort8(0x3FB, 0x80)) return false;
        if (!WritePort8(0x3F8, 0x01)) return false;
        if (!WritePort8(0x3F9, 0x00)) return false;
        if (!WritePort8(0x3FB, 0x03)) return false;
        if (!WritePort8(0x3FA, 0xC7)) return false;
        return WritePort8(0x3FC, 0x0B);
    }

    /// <summary>Writes one byte to the initialized x64 COM1 serial device. Record-level serialization is owned by Begin/EndSerialRecord.</summary>
    public static Boolean WriteSerial(Byte value) => WritePort8(0x3F8, value);

    /// <summary>Acquires the cross-CPU COM1 record gate. Recursive acquisition by the current CPU is supported.</summary>
    [DllImport("*", EntryPoint = "InuX64BeginSerialRecord", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean BeginSerialRecord();

    /// <summary>Attempts to acquire the cross-CPU COM1 record gate without waiting; intended for interrupt-context presentation.</summary>
    [DllImport("*", EntryPoint = "InuX64TryBeginSerialRecord", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean TryBeginSerialRecord();

    /// <summary>Releases one recursive level of the current CPU's COM1 record gate.</summary>
    [DllImport("*", EntryPoint = "InuX64EndSerialRecord", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean EndSerialRecord();

    /// <summary>Gets the address of the build-selected free TrueType console font embedded in the EFI image, or zero when unavailable.</summary>
    [DllImport("*", EntryPoint = "InuX64GetConsoleTrueTypeFontAddress", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetConsoleTrueTypeFontAddress();

    /// <summary>Gets the byte length of the embedded TrueType console font.</summary>
    [DllImport("*", EntryPoint = "InuX64GetConsoleTrueTypeFontLength", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetConsoleTrueTypeFontLength();

    /// <summary>Gets the native scratch coverage buffer reserved for TrueType console rasterisation.</summary>
    [DllImport("*", EntryPoint = "InuX64GetConsoleTrueTypeCoverageAddress", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetConsoleTrueTypeCoverageAddress();
    [DllImport("*", EntryPoint = "InuX64GetConsoleTrueTypeCoverageLength", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetConsoleTrueTypeCoverageLength();
    /// <summary>Gets the native outline workspace reserved for TrueType console rasterisation.</summary>
    [DllImport("*", EntryPoint = "InuX64GetConsoleTrueTypeWorkspaceAddress", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetConsoleTrueTypeWorkspaceAddress();
    [DllImport("*", EntryPoint = "InuX64GetConsoleTrueTypeWorkspaceLength", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetConsoleTrueTypeWorkspaceLength();

    [DllImport("*", EntryPoint = "InuX64GetConsoleTrueTypeCacheAddress", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetConsoleTrueTypeCacheAddress();

    [DllImport("*", EntryPoint = "InuX64GetConsoleTrueTypeCacheLength", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetConsoleTrueTypeCacheLength();

    /// <summary>Installs the bootstrap processor GDT and TSS.</summary>
    [DllImport("*", EntryPoint = "InuX64InitializeBootstrapDescriptors", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean InitializeBootstrapDescriptors();

    /// <summary>Installs the bootstrap processor IDT.</summary>
    [DllImport("*", EntryPoint = "InuX64InitializeBootstrapInterrupts", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean InitializeBootstrapInterrupts();

    /// <summary>Masks the two legacy 8259 PIC controllers.</summary>
    [DllImport("*", EntryPoint = "InuX64DisableLegacyPic", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean DisableLegacyPic();



    /// <summary>Captures the caller-visible RIP, RSP, RBP, RFLAGS and CR3 for panic diagnostics.</summary>
    [DllImport("*", EntryPoint = "InuX64CapturePanicContext", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean CapturePanicContext(out UInt64 instructionPointer, out UInt64 stackPointer, out UInt64 framePointer, out UInt64 flags, out UInt64 pageTableRoot);

    /// <summary>Raises x64 INT3 so an attached debugger can stop on a kernel panic and then continue.</summary>
    [DllImport("*", EntryPoint = "InuX64PanicDebuggerBreak", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean PanicDebuggerBreak();

    /// <summary>Reads the active x64 CR3 page-table root physical address.</summary>
    [DllImport("*", EntryPoint = "InuX64ReadPageTableRoot", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 ReadPageTableRoot();

    /// <summary>Loads one 4 KiB-aligned physical page-table root into x64 CR3.</summary>
    [DllImport("*", EntryPoint = "InuX64WritePageTableRoot", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean WritePageTableRoot(UInt64 physicalAddress);

    /// <summary>Invalidates the current processor translation for one virtual address.</summary>
    [DllImport("*", EntryPoint = "InuX64InvalidatePage", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean InvalidatePage(UInt64 virtualAddress);

    /// <summary>Enables the x64 EFER.NXE facility when the processor reports execute-disable support.</summary>
    [DllImport("*", EntryPoint = "InuX64EnableExecuteDisable", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean EnableExecuteDisable();

    /// <summary>Determines whether the processor supports 1 GiB x64 page-table leaves.</summary>
    [DllImport("*", EntryPoint = "InuX64Supports1GiBPages", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean Supports1GiBPages();



    /// <summary>Reads one x64 model-specific register.</summary>
    [DllImport("*", EntryPoint = "InuX64ReadMsr", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 ReadModelSpecificRegister(UInt32 register);

    /// <summary>Writes one x64 model-specific register.</summary>
    [DllImport("*", EntryPoint = "InuX64WriteMsr", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean WriteModelSpecificRegister(UInt32 register, UInt64 value);

    /// <summary>Gets the APIC identifier of the processor executing the current code.</summary>
    [DllImport("*", EntryPoint = "InuX64GetCurrentApicId", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt32 GetCurrentApicId();

    /// <summary>Builds one AP's permanent GDT/TSS state and emergency IST stack pointers before SIPI.</summary>
    [DllImport("*", EntryPoint = "InuX64PrepareApplicationProcessorDescriptorState", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean PrepareApplicationProcessorDescriptorState(UInt64 descriptorStateAddress, UInt64 kernelStackTop, UInt64 doubleFaultStackTop, UInt64 nmiStackTop, UInt64 machineCheckStackTop);

    /// <summary>Copies and patches the relocatable x64 application-processor startup trampoline using the legacy three-argument ABI.</summary>
    [DllImport("*", EntryPoint = "InuX64PrepareApplicationProcessorTrampoline", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean PrepareApplicationProcessorTrampoline(UInt64 trampolineAddress, UInt64 pageTableRoot, UInt64 stackTop);

    /// <summary>Copies and patches the AP trampoline and installs a permanent per-processor descriptor/TSS state pointer.</summary>
    [DllImport("*", EntryPoint = "InuX64PrepareApplicationProcessorTrampolineWithDescriptorState", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean PrepareApplicationProcessorTrampolineWithDescriptorState(UInt64 trampolineAddress, UInt64 pageTableRoot, UInt64 stackTop, UInt64 descriptorStateAddress);

    /// <summary>Gets the AP startup handshake state stored by the low-memory trampoline.</summary>
    [DllImport("*", EntryPoint = "InuX64GetApplicationProcessorStartupStatus", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt32 GetApplicationProcessorStartupStatus(UInt64 trampolineAddress);

    /// <summary>Gets the APIC identifier observed by the application processor in the startup trampoline.</summary>
    [DllImport("*", EntryPoint = "InuX64GetApplicationProcessorObservedApicId", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt32 GetApplicationProcessorObservedApicId(UInt64 trampolineAddress);

    /// <summary>Reads the invariant-capable x64 timestamp counter.</summary>
    [DllImport("*", EntryPoint = "InuX64ReadTimestampCounter", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 ReadTimestampCounter();

    /// <summary>Determines whether CPUID advertises a timestamp counter.</summary>
    [DllImport("*", EntryPoint = "InuX64SupportsTsc", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean SupportsTsc();

    /// <summary>Gets whether CPUID advertises an invariant timestamp counter.</summary>
    [DllImport("*", EntryPoint = "InuX64SupportsInvariantTsc", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean SupportsInvariantTsc();

    /// <summary>Reads one 32-bit memory-mapped I/O register.</summary>
    [DllImport("*", EntryPoint = "InuX64ReadMmio32", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt32 ReadMmio32(UInt64 address);

    /// <summary>Writes one 32-bit memory-mapped I/O register.</summary>
    [DllImport("*", EntryPoint = "InuX64WriteMmio32", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean WriteMmio32(UInt64 address, UInt32 value);

    /// <summary>Reads one 64-bit memory-mapped I/O register.</summary>
    [DllImport("*", EntryPoint = "InuX64ReadMmio64", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 ReadMmio64(UInt64 address);

    /// <summary>Writes one 64-bit memory-mapped I/O register.</summary>
    [DllImport("*", EntryPoint = "InuX64WriteMmio64", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean WriteMmio64(UInt64 address, UInt64 value);

    /// <summary>Initializes a fresh x64 kernel-thread context.</summary>
    [DllImport("*", EntryPoint = "InuX64InitializeThreadContext", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean InitializeThreadContext(UInt64 contextAddress, UInt64 stackTop, UInt64 entryPoint, UInt64 argument);

    /// <summary>Returns the current x64 stack pointer at the call boundary for conservative GC root scanning.</summary>
    [DllImport("*", EntryPoint = "InuX64GetCurrentStackPointer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetCurrentStackPointer();

    /// <summary>Saves the current x64 thread context and transfers execution to another saved context.</summary>
    [DllImport("*", EntryPoint = "InuX64SwitchThreadContext", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean SwitchThreadContext(UInt64 currentContextAddress, UInt64 nextContextAddress);

    [DllImport("*", EntryPoint = "InuX64GetBootstrapStackBase", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetBootstrapStackBase();

    [DllImport("*", EntryPoint = "InuX64GetBootstrapStackTop", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetBootstrapStackTop();

    /// <summary>Returns the first NativeAOT module-table sentinel in the linked image.</summary>
    [DllImport("*", EntryPoint = "InuX64GetManagedModuleTableStart", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetManagedModuleTableStart();

    /// <summary>Returns the final NativeAOT module-table sentinel in the linked image.</summary>
    [DllImport("*", EntryPoint = "InuX64GetManagedModuleTableEnd", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetManagedModuleTableEnd();

    [DllImport("*", EntryPoint = "InuX64GetManagedReadyToRunHeader", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetManagedReadyToRunHeader();

    /// <summary>Gets the native early-boot NMI takeover/diagnostic state block.</summary>
    [DllImport("*", EntryPoint = "InuX64GetNmiDiagnosticStateAddress", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetNmiDiagnosticStateAddress();

    [DllImport("*", EntryPoint = "InuX64GetSchedulerIdleThreadEntryPoint", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetSchedulerIdleThreadEntryPoint();

    [DllImport("*", EntryPoint = "InuX64GetSchedulerRoleWorkerEntryPoint", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetSchedulerRoleWorkerEntryPoint();

    [DllImport("*", EntryPoint = "InuX64GetSchedulerOneShotThreadEntryPoint", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern UInt64 GetSchedulerOneShotThreadEntryPoint();

    /// <summary>Gets whether EFER.NXE is enabled for execute-disable page protections.</summary>
    [DllImport("*", EntryPoint = "InuX64IsExecuteDisableEnabled", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean IsExecuteDisableEnabled();

    /// <summary>Enables CR0.WP so supervisor writes obey read-only page protections.</summary>
    [DllImport("*", EntryPoint = "InuX64EnableKernelWriteProtect", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean EnableKernelWriteProtect();

    /// <summary>Gets whether CR0.WP is enabled.</summary>
    [DllImport("*", EntryPoint = "InuX64IsKernelWriteProtectEnabled", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean IsKernelWriteProtectEnabled();

    /// <summary>Gets whether CPUID reports supervisor-mode execution prevention.</summary>
    [DllImport("*", EntryPoint = "InuX64SupportsSmep", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean SupportsSmep();

    /// <summary>Enables CR4.SMEP when supported.</summary>
    [DllImport("*", EntryPoint = "InuX64EnableSmep", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean EnableSmep();

    /// <summary>Gets whether CPUID reports supervisor-mode access prevention.</summary>
    [DllImport("*", EntryPoint = "InuX64SupportsSmap", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean SupportsSmap();

    /// <summary>Configures x64 SYSCALL/SYSRET and the current processor syscall stack state.</summary>
    [DllImport("*", EntryPoint = "InuX64ConfigureSystemCalls", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean ConfigureSystemCalls(UInt64 stateAddress, UInt64 kernelStackTop);

    /// <summary>Enables CR4.SMAP when the processor supports supervisor-mode access prevention.</summary>
    [DllImport("*", EntryPoint = "InuX64EnableSmap", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean EnableSmap();

    /// <summary>Gets whether CR4.SMAP is enabled.</summary>
    [DllImport("*", EntryPoint = "InuX64IsSmapEnabled", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean IsSmapEnabled();

    /// <summary>Temporarily permits supervisor access to validated user pages when SMAP is active.</summary>
    [DllImport("*", EntryPoint = "InuX64BeginUserMemoryAccess", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean BeginUserMemoryAccess();

    /// <summary>Restores SMAP protection after a guarded user-memory copy.</summary>
    [DllImport("*", EntryPoint = "InuX64EndUserMemoryAccess", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean EndUserMemoryAccess();

    /// <summary>Enters x64 ring 3 using a validated user RIP, stack pointer, and first argument.</summary>
    [DllImport("*", EntryPoint = "InuX64EnterUserMode", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Int32 EnterUserMode(UInt64 entryPoint, UInt64 stackTop, UInt64 argument);

    /// <summary>Requests a controlled return from the active ring-3 process to its saved kernel continuation.</summary>
    [DllImport("*", EntryPoint = "InuX64RequestUserModeExit", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean RequestUserModeExit(Int64 exitCode);


    /// <summary>Installs Inu's freestanding managed interrupt dispatcher.</summary>
    [DllImport("*", EntryPoint = "InuX64InstallManagedInterruptDispatcher", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean InstallManagedInterruptDispatcher();

    /// <summary>Disables maskable x64 interrupts on the current processor.</summary>
    [DllImport("*", EntryPoint = "InuX64DisableInterrupts", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean DisableInterrupts();

    /// <summary>Enables maskable x64 interrupts.</summary>
    [DllImport("*", EntryPoint = "InuX64EnableInterrupts", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean EnableInterrupts();

    /// <summary>Halts until one interrupt arrives, then returns to managed code.</summary>
    [DllImport("*", EntryPoint = "InuX64WaitForInterrupt", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean WaitForInterrupt();

    /// <summary>Executes one x64 PAUSE hint and returns to managed code.</summary>
    [DllImport("*", EntryPoint = "InuX64Pause", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean Pause();

    [DllImport("*", EntryPoint = "InuX64AtomicCompareExchange64", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern unsafe Boolean AtomicCompareExchange64(UInt64* location, UInt64 expected, UInt64 replacement, UInt64* previous);
    [DllImport("*", EntryPoint = "InuX64AtomicExchange64", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern unsafe Boolean AtomicExchange64(UInt64* location, UInt64 value, UInt64* previous);
    [DllImport("*", EntryPoint = "InuX64AtomicFetchAdd64", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern unsafe Boolean AtomicFetchAdd64(UInt64* location, UInt64 delta, UInt64* previous);
    [DllImport("*", EntryPoint = "InuX64AtomicLoad64", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern unsafe Boolean AtomicLoad64(UInt64* location, UInt64* value);
    [DllImport("*", EntryPoint = "InuX64AtomicStore64", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern unsafe Boolean AtomicStore64(UInt64* location, UInt64 value);
    [DllImport("*", EntryPoint = "InuX64MemoryBarrier", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean MemoryBarrier();

    /// <summary>Stops the current processor permanently.</summary>
    [DllImport("*", EntryPoint = "InuX64Halt", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean Halt();

    /// <summary>Writes one byte to an x64 I/O port.</summary>
    [DllImport("*", EntryPoint = "InuX64WritePort8", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean WritePort8(UInt16 port, Byte value);

    /// <summary>Reads one byte from an x64 I/O port.</summary>
    [DllImport("*", EntryPoint = "InuX64ReadPort8", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean ReadPort8(UInt16 port, out Byte value);

    /// <summary>Writes one 16-bit value to an x64 I/O port.</summary>
    [DllImport("*", EntryPoint = "InuX64WritePort16", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean WritePort16(UInt16 port, UInt16 value);

    /// <summary>Reads one 16-bit value from an x64 I/O port.</summary>
    [DllImport("*", EntryPoint = "InuX64ReadPort16", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean ReadPort16(UInt16 port, out UInt16 value);

    /// <summary>Writes one 32-bit value to an x64 I/O port.</summary>
    [DllImport("*", EntryPoint = "InuX64WritePort32", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean WritePort32(UInt16 port, UInt32 value);

    /// <summary>Reads one 32-bit value from an x64 I/O port.</summary>
    [DllImport("*", EntryPoint = "InuX64ReadPort32", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern Boolean ReadPort32(UInt16 port, out UInt32 value);
}
