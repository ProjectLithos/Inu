using Inu.Core;

namespace Inu.Architecture.X64;

/// <summary>Identifies x64 control registers exposed through the architecture boundary.</summary>
/// <nova.when>Use with <see cref="X64Operations.ReadControlRegister"/> and <see cref="X64Operations.WriteControlRegister"/>.</nova.when>
/// <nova.depends>Inu.Architecture.X64 native bindings</nova.depends>
public enum X64ControlRegister : uint
{
    /// <summary>System control flags.</summary>
    Cr0 = 0,
    /// <summary>Page-fault linear address.</summary>
    Cr2 = 2,
    /// <summary>Page-table root address.</summary>
    Cr3 = 3,
    /// <summary>Extended processor control flags.</summary>
    Cr4 = 4,
    /// <summary>Extended control register selected by XCR index zero.</summary>
    Xcr0 = 0x100
}

/// <summary>Identifies common x64 CPU features without exposing CPUID leaf mechanics to callers.</summary>
/// <nova.when>Use to select optional x64 facilities after early initialisation.</nova.when>
/// <nova.depends>CPUID native binding</nova.depends>
public enum X64CpuFeature : uint
{
    /// <summary>APIC support.</summary>
    Apic = 1,
    /// <summary>x2APIC support.</summary>
    X2Apic = 2,
    /// <summary>NX execute-disable support.</summary>
    ExecuteDisable = 3,
    /// <summary>SYSCALL and SYSRET support.</summary>
    Syscall = 4,
    /// <summary>FSGSBASE instruction support.</summary>
    FsGsBase = 5,
    /// <summary>XSAVE processor-state support.</summary>
    XSave = 6,
    /// <summary>Supervisor mode execution prevention.</summary>
    Smep = 7,
    /// <summary>Supervisor mode access prevention.</summary>
    Smap = 8,
    /// <summary>Invariant timestamp-counter support.</summary>
    InvariantTimestamp = 9
}

/// <summary>Contains the native function pointers required by the x64 static operation API.</summary>
/// <nova.when>Create once during native-to-managed bootstrap and bind before any x64 operation is called.</nova.when>
/// <nova.depends>Inu x64 assembly implementation</nova.depends>
public unsafe readonly struct X64NativeBindings
{
    /// <summary>Creates a complete x64 native binding table.</summary>
    /// <nova.when>Use at bootstrap after native symbols have been resolved.</nova.when>
    /// <nova.depends>Inu x64 assembly implementation</nova.depends>
    /// <returns>A binding table suitable for <see cref="X64Operations.Bind"/>.</returns>
    /// <example><code>X64NativeBindings bindings = new(enable, disable, halt, pause, timestamp, readCr, writeCr, readMsr, writeMsr, in8, in16, in32, out8, out16, out32, barrier, atomicCompareExchange, encodePage, switchContext, detectFeature);</code></example>
    public X64NativeBindings(
        delegate* unmanaged<byte> enableInterrupts,
        delegate* unmanaged<byte> disableInterrupts,
        delegate* unmanaged<byte> halt,
        delegate* unmanaged<byte> pause,
        delegate* unmanaged<ulong> readTimestamp,
        delegate* unmanaged<uint, ulong*, byte> readControlRegister,
        delegate* unmanaged<uint, ulong, byte> writeControlRegister,
        delegate* unmanaged<uint, ulong*, byte> readModelSpecificRegister,
        delegate* unmanaged<uint, ulong, byte> writeModelSpecificRegister,
        delegate* unmanaged<ushort, byte*, byte> input8,
        delegate* unmanaged<ushort, ushort*, byte> input16,
        delegate* unmanaged<ushort, uint*, byte> input32,
        delegate* unmanaged<ushort, byte, byte> output8,
        delegate* unmanaged<ushort, ushort, byte> output16,
        delegate* unmanaged<ushort, uint, byte> output32,
        delegate* unmanaged<uint, byte> memoryBarrier,
        delegate* unmanaged<ulong*, ulong, ulong, ulong*, byte> atomicCompareExchange,
        delegate* unmanaged<ulong, ulong, ulong*, byte> encodePageTableEntry,
        delegate* unmanaged<nuint, nuint, byte> switchContext,
        delegate* unmanaged<uint, ulong*, byte> detectFeature)
    {
        EnableInterrupts = enableInterrupts;
        DisableInterrupts = disableInterrupts;
        Halt = halt;
        Pause = pause;
        ReadTimestamp = readTimestamp;
        ReadControlRegister = readControlRegister;
        WriteControlRegister = writeControlRegister;
        ReadModelSpecificRegister = readModelSpecificRegister;
        WriteModelSpecificRegister = writeModelSpecificRegister;
        Input8 = input8;
        Input16 = input16;
        Input32 = input32;
        Output8 = output8;
        Output16 = output16;
        Output32 = output32;
        MemoryBarrier = memoryBarrier;
        AtomicCompareExchange = atomicCompareExchange;
        EncodePageTableEntry = encodePageTableEntry;
        SwitchContext = switchContext;
        DetectFeature = detectFeature;
    }

    internal delegate* unmanaged<byte> EnableInterrupts { get; }
    internal delegate* unmanaged<byte> DisableInterrupts { get; }
    internal delegate* unmanaged<byte> Halt { get; }
    internal delegate* unmanaged<byte> Pause { get; }
    internal delegate* unmanaged<ulong> ReadTimestamp { get; }
    internal delegate* unmanaged<uint, ulong*, byte> ReadControlRegister { get; }
    internal delegate* unmanaged<uint, ulong, byte> WriteControlRegister { get; }
    internal delegate* unmanaged<uint, ulong*, byte> ReadModelSpecificRegister { get; }
    internal delegate* unmanaged<uint, ulong, byte> WriteModelSpecificRegister { get; }
    internal delegate* unmanaged<ushort, byte*, byte> Input8 { get; }
    internal delegate* unmanaged<ushort, ushort*, byte> Input16 { get; }
    internal delegate* unmanaged<ushort, uint*, byte> Input32 { get; }
    internal delegate* unmanaged<ushort, byte, byte> Output8 { get; }
    internal delegate* unmanaged<ushort, ushort, byte> Output16 { get; }
    internal delegate* unmanaged<ushort, uint, byte> Output32 { get; }
    internal delegate* unmanaged<uint, byte> MemoryBarrier { get; }
    internal delegate* unmanaged<ulong*, ulong, ulong, ulong*, byte> AtomicCompareExchange { get; }
    internal delegate* unmanaged<ulong, ulong, ulong*, byte> EncodePageTableEntry { get; }
    internal delegate* unmanaged<nuint, nuint, byte> SwitchContext { get; }
    internal delegate* unmanaged<uint, ulong*, byte> DetectFeature { get; }

    internal bool IsComplete => EnableInterrupts != null && DisableInterrupts != null && Halt != null && Pause != null &&
        ReadTimestamp != null && ReadControlRegister != null && WriteControlRegister != null &&
        ReadModelSpecificRegister != null && WriteModelSpecificRegister != null && Input8 != null && Input16 != null &&
        Input32 != null && Output8 != null && Output16 != null && Output32 != null && MemoryBarrier != null &&
        AtomicCompareExchange != null && EncodePageTableEntry != null && SwitchContext != null && DetectFeature != null;
}

/// <summary>Provides zero-allocation static x64 operations backed by native assembly function pointers.</summary>
/// <nova.when>Use in performance-critical kernel paths after <see cref="Bind"/> succeeds.</nova.when>
/// <nova.depends>X64NativeBindings</nova.depends>
[SupportedArchitecture(SupportedArchitecture.X64)]
[BootStage(BootStage.ManagedBootstrap)]
public static unsafe class X64Operations
{
    private static X64NativeBindings _bindings;
    private static bool _isBound;

    /// <summary>Gets whether a complete native x64 operation table has been bound.</summary>
    /// <nova.when>Check before invoking x64 static operations during bootstrap.</nova.when>
    /// <nova.depends>X64NativeBindings</nova.depends>
    public static bool IsBound => _isBound;

    /// <summary>Binds the static API to architecture assembly entry points.</summary>
    /// <nova.when>Call once during bootstrap before architecture services are exposed.</nova.when>
    /// <nova.depends>X64NativeBindings</nova.depends>
    /// <returns><see langword="true"/> when every required function pointer is present.</returns>
    /// <example><code>bool bound = X64Operations.Bind(bindings);</code></example>
    public static bool Bind(X64NativeBindings bindings)
    {
        if (!bindings.IsComplete) return false;
        _bindings = bindings;
        _isBound = true;
        return true;
    }

    /// <summary>Enables maskable interrupts.</summary>
    /// <nova.when>Use after IDT and interrupt-controller initialisation.</nova.when>
    /// <nova.depends>Bound x64 STI wrapper</nova.depends>
    /// <returns><see langword="true"/> when the native operation succeeds.</returns>
    /// <example><code>bool enabled = X64Operations.EnableInterrupts();</code></example>
    public static bool EnableInterrupts() => _isBound && _bindings.EnableInterrupts() != 0;

    /// <summary>Disables maskable interrupts.</summary>
    /// <nova.when>Use for short critical regions and terminal processor shutdown.</nova.when>
    /// <nova.depends>Bound x64 CLI wrapper</nova.depends>
    /// <returns><see langword="true"/> when the native operation succeeds.</returns>
    /// <example><code>bool disabled = X64Operations.DisableInterrupts();</code></example>
    public static bool DisableInterrupts() => _isBound && _bindings.DisableInterrupts() != 0;

    /// <summary>Executes the processor halt operation.</summary>
    /// <nova.when>Use in idle or terminal loops according to interrupt policy.</nova.when>
    /// <nova.depends>Bound x64 HLT wrapper</nova.depends>
    /// <returns><see langword="true"/> if the call returns successfully.</returns>
    /// <example><code>bool resumed = X64Operations.Halt();</code></example>
    public static bool Halt() => _isBound && _bindings.Halt() != 0;

    /// <summary>Executes the processor pause hint.</summary>
    /// <nova.when>Use inside bounded spin-wait loops.</nova.when>
    /// <nova.depends>Bound x64 PAUSE wrapper</nova.depends>
    /// <returns><see langword="true"/> when the hint was issued.</returns>
    /// <example><code>bool paused = X64Operations.Pause();</code></example>
    public static bool Pause() => _isBound && _bindings.Pause() != 0;

    /// <summary>Reads the x64 timestamp counter.</summary>
    /// <nova.when>Use after invariant-counter capability and synchronisation policy are established.</nova.when>
    /// <nova.depends>Bound RDTSC or RDTSCP wrapper</nova.depends>
    /// <returns>The timestamp value, or zero when unbound.</returns>
    /// <example><code>ulong ticks = X64Operations.ReadTimestamp();</code></example>
    public static ulong ReadTimestamp() => _isBound ? _bindings.ReadTimestamp() : 0;

    /// <summary>Reads an x64 control register.</summary>
    /// <nova.when>Use only inside privileged architecture and memory-management code.</nova.when>
    /// <nova.depends>Bound control-register wrapper</nova.depends>
    /// <returns>The operation result and register value.</returns>
    /// <example><code>ArchitectureValueResult cr3 = X64Operations.ReadControlRegister(X64ControlRegister.Cr3);</code></example>
    public static ArchitectureValueResult ReadControlRegister(X64ControlRegister register)
    {
        ulong value = 0;
        bool ok = _isBound && _bindings.ReadControlRegister((uint)register, &value) != 0;
        return new(ok, value);
    }

    /// <summary>Writes an x64 control register.</summary>
    /// <nova.when>Use only after validating reserved bits and required serialisation.</nova.when>
    /// <nova.depends>Bound control-register wrapper</nova.depends>
    /// <returns><see langword="true"/> when the register was written.</returns>
    /// <example><code>bool written = X64Operations.WriteControlRegister(X64ControlRegister.Cr3, root);</code></example>
    public static bool WriteControlRegister(X64ControlRegister register, ulong value) =>
        _isBound && _bindings.WriteControlRegister((uint)register, value) != 0;

    /// <summary>Reads a model-specific register.</summary>
    /// <nova.when>Use after feature detection confirms that the selected MSR exists.</nova.when>
    /// <nova.depends>Bound RDMSR wrapper</nova.depends>
    /// <returns>The operation result and MSR value.</returns>
    /// <example><code>ArchitectureValueResult efer = X64Operations.ReadModelSpecificRegister(0xC0000080);</code></example>
    public static ArchitectureValueResult ReadModelSpecificRegister(uint register)
    {
        ulong value = 0;
        bool ok = _isBound && _bindings.ReadModelSpecificRegister(register, &value) != 0;
        return new(ok, value);
    }

    /// <summary>Writes a model-specific register.</summary>
    /// <nova.when>Use after validating feature support, reserved bits and processor scope.</nova.when>
    /// <nova.depends>Bound WRMSR wrapper</nova.depends>
    /// <returns><see langword="true"/> when the MSR was written.</returns>
    /// <example><code>bool written = X64Operations.WriteModelSpecificRegister(0xC0000080, value);</code></example>
    public static bool WriteModelSpecificRegister(uint register, ulong value) =>
        _isBound && _bindings.WriteModelSpecificRegister(register, value) != 0;

    /// <summary>Reads an 8-bit x64 I/O port.</summary>
    /// <nova.when>Use for legacy port-mapped device access.</nova.when>
    /// <nova.depends>Bound IN instruction wrapper</nova.depends>
    /// <returns>The operation result and byte value.</returns>
    /// <example><code>ArchitectureValueResult status = X64Operations.Input8(0x3FD);</code></example>
    public static ArchitectureValueResult Input8(ushort port)
    {
        byte value = 0;
        bool ok = _isBound && _bindings.Input8(port, &value) != 0;
        return new(ok, value);
    }

    /// <summary>Reads a 16-bit x64 I/O port.</summary>
    /// <nova.when>Use for legacy port-mapped device access requiring a word transfer.</nova.when>
    /// <nova.depends>Bound IN instruction wrapper</nova.depends>
    /// <returns>The operation result and word value.</returns>
    /// <example><code>ArchitectureValueResult value = X64Operations.Input16(port);</code></example>
    public static ArchitectureValueResult Input16(ushort port)
    {
        ushort value = 0;
        bool ok = _isBound && _bindings.Input16(port, &value) != 0;
        return new(ok, value);
    }

    /// <summary>Reads a 32-bit x64 I/O port.</summary>
    /// <nova.when>Use for legacy port-mapped device access requiring a double-word transfer.</nova.when>
    /// <nova.depends>Bound IN instruction wrapper</nova.depends>
    /// <returns>The operation result and double-word value.</returns>
    /// <example><code>ArchitectureValueResult value = X64Operations.Input32(port);</code></example>
    public static ArchitectureValueResult Input32(ushort port)
    {
        uint value = 0;
        bool ok = _isBound && _bindings.Input32(port, &value) != 0;
        return new(ok, value);
    }

    /// <summary>Writes an 8-bit x64 I/O port.</summary>
    /// <nova.when>Use for legacy port-mapped device control.</nova.when>
    /// <nova.depends>Bound OUT instruction wrapper</nova.depends>
    /// <returns><see langword="true"/> when the byte was written.</returns>
    /// <example><code>bool written = X64Operations.Output8(0x3F8, value);</code></example>
    public static bool Output8(ushort port, byte value) => _isBound && _bindings.Output8(port, value) != 0;

    /// <summary>Writes a 16-bit x64 I/O port.</summary>
    /// <nova.when>Use for legacy port-mapped device control requiring a word transfer.</nova.when>
    /// <nova.depends>Bound OUT instruction wrapper</nova.depends>
    /// <returns><see langword="true"/> when the word was written.</returns>
    /// <example><code>bool written = X64Operations.Output16(port, value);</code></example>
    public static bool Output16(ushort port, ushort value) => _isBound && _bindings.Output16(port, value) != 0;

    /// <summary>Writes a 32-bit x64 I/O port.</summary>
    /// <nova.when>Use for legacy port-mapped device control requiring a double-word transfer.</nova.when>
    /// <nova.depends>Bound OUT instruction wrapper</nova.depends>
    /// <returns><see langword="true"/> when the double word was written.</returns>
    /// <example><code>bool written = X64Operations.Output32(port, value);</code></example>
    public static bool Output32(ushort port, uint value) => _isBound && _bindings.Output32(port, value) != 0;

    /// <summary>Issues an architecture memory barrier.</summary>
    /// <nova.when>Use to enforce processor or device memory ordering.</nova.when>
    /// <nova.depends>Bound x64 fence wrapper</nova.depends>
    /// <returns><see langword="true"/> when the barrier was issued.</returns>
    /// <example><code>bool ordered = X64Operations.MemoryBarrier(MemoryBarrierKind.Full);</code></example>
    public static bool MemoryBarrier(MemoryBarrierKind kind) => _isBound && _bindings.MemoryBarrier((uint)kind) != 0;

    /// <summary>Atomically compares and exchanges a 64-bit value.</summary>
    /// <nova.when>Use to build lock-free architecture services and synchronisation primitives.</nova.when>
    /// <nova.depends>Bound LOCK CMPXCHG wrapper</nova.depends>
    /// <returns>The operation result and value observed before exchange.</returns>
    /// <example><code>ArchitectureValueResult observed = X64Operations.CompareExchange64(location, expected, replacement);</code></example>
    public static ArchitectureValueResult CompareExchange64(ulong* location, ulong expected, ulong replacement)
    {
        ulong observed = 0;
        bool ok = _isBound && location != null && _bindings.AtomicCompareExchange(location, expected, replacement, &observed) != 0;
        return new(ok, observed);
    }

    /// <summary>Encodes an architecture-neutral descriptor as an x64 page-table entry.</summary>
    /// <nova.when>Use in the x64 page-table builder after physical-address validation.</nova.when>
    /// <nova.depends>Bound x64 page-table encoder</nova.depends>
    /// <returns>The operation result and encoded entry.</returns>
    /// <example><code>ArchitectureValueResult entry = X64Operations.EncodePageTableEntry(descriptor);</code></example>
    public static ArchitectureValueResult EncodePageTableEntry(PageTableEntryDescriptor descriptor)
    {
        ulong encoded = 0;
        bool ok = _isBound && _bindings.EncodePageTableEntry(descriptor.PhysicalAddress, (ulong)descriptor.Flags, &encoded) != 0;
        return new(ok, encoded);
    }

    /// <summary>Switches from one architecture context frame to another.</summary>
    /// <nova.when>Use only from the scheduler with architecture-owned context frame addresses.</nova.when>
    /// <nova.depends>Bound x64 context-switch assembly</nova.depends>
    /// <returns><see langword="true"/> when control later returns to the outgoing context.</returns>
    /// <example><code>bool switched = X64Operations.SwitchContext(oldFrame, newFrame);</code></example>
    public static bool SwitchContext(nuint outgoingContext, nuint incomingContext) =>
        _isBound && outgoingContext != 0 && incomingContext != 0 && _bindings.SwitchContext(outgoingContext, incomingContext) != 0;

    /// <summary>Queries a named x64 CPU feature.</summary>
    /// <nova.when>Use before enabling optional processor facilities.</nova.when>
    /// <nova.depends>Bound CPUID feature detector</nova.depends>
    /// <returns>A feature result containing support and architecture-specific detail.</returns>
    /// <example><code>CpuFeatureResult nx = X64Operations.DetectFeature(X64CpuFeature.ExecuteDisable);</code></example>
    public static CpuFeatureResult DetectFeature(X64CpuFeature feature)
    {
        ulong value = 0;
        bool supported = _isBound && _bindings.DetectFeature((uint)feature, &value) != 0;
        return new((uint)feature, supported, value);
    }
}

/// <summary>Implements the architecture lifecycle contract for x64 while delegating hot operations to static bindings.</summary>
/// <nova.when>Use as the selected architecture service on an x64 kernel.</nova.when>
/// <nova.depends>X64Operations</nova.depends>
[SupportedArchitecture(SupportedArchitecture.X64)]
public sealed class X64CpuArchitecture : ICpuArchitecture
{
    /// <summary>Gets the x64 architecture identifier.</summary>
    /// <nova.when>Use to validate architecture selection.</nova.when>
    /// <nova.depends>Inu.Core</nova.depends>
    public SupportedArchitecture Architecture => SupportedArchitecture.X64;

    /// <summary>Validates that the x64 operation table is ready.</summary>
    /// <nova.when>Call during bootstrap after binding native operations.</nova.when>
    /// <nova.depends>X64Operations.Bind</nova.depends>
    /// <returns><see langword="true"/> when x64 operations are bound.</returns>
    /// <example><code>bool ready = architecture.InitialiseEarly();</code></example>
    public bool InitialiseEarly() => X64Operations.IsBound;

    /// <summary>Initialises one x64 processor at the contract boundary.</summary>
    /// <nova.when>Call on each logical processor after its native entry path is established.</nova.when>
    /// <nova.depends>Native per-CPU GDT, TSS, IDT and APIC setup</nova.depends>
    /// <returns><see langword="true"/> when the operation table is available for the processor.</returns>
    /// <example><code>bool ready = architecture.InitialiseProcessor(processorId);</code></example>
    public bool InitialiseProcessor(uint processorId) => X64Operations.IsBound;

    /// <summary>Enables maskable interrupts through the static x64 operation API.</summary>
    /// <nova.when>Call after the IDT and interrupt controller are initialised.</nova.when>
    /// <nova.depends>X64Operations.EnableInterrupts</nova.depends>
    /// <returns><see langword="true"/> when interrupts are enabled.</returns>
    /// <example><code>bool enabled = architecture.EnableInterrupts();</code></example>
    public bool EnableInterrupts() => X64Operations.EnableInterrupts();

    /// <summary>Disables maskable interrupts through the static x64 operation API.</summary>
    /// <nova.when>Use for critical regions or terminal processor shutdown.</nova.when>
    /// <nova.depends>X64Operations.DisableInterrupts</nova.depends>
    /// <returns><see langword="true"/> when interrupts are disabled.</returns>
    /// <example><code>bool disabled = architecture.DisableInterrupts();</code></example>
    public bool DisableInterrupts() => X64Operations.DisableInterrupts();

    /// <summary>Halts the current processor through the static x64 operation API.</summary>
    /// <nova.when>Use in idle or terminal halt loops.</nova.when>
    /// <nova.depends>X64Operations.Halt</nova.depends>
    /// <returns><see langword="true"/> if execution resumes and the wrapper succeeds.</returns>
    /// <example><code>bool resumed = architecture.Halt();</code></example>
    public bool Halt() => X64Operations.Halt();

    /// <summary>Reads the x64 timestamp counter through the static operation API.</summary>
    /// <nova.when>Use after timestamp feature and synchronisation checks.</nova.when>
    /// <nova.depends>X64Operations.ReadTimestamp</nova.depends>
    /// <returns>The current timestamp, or zero when operations are unbound.</returns>
    /// <example><code>ulong ticks = architecture.ReadTimestamp();</code></example>
    public ulong ReadTimestamp() => X64Operations.ReadTimestamp();
}
