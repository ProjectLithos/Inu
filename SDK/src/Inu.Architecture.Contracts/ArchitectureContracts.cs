using Inu.Core;

namespace Inu.Architecture;

/// <summary>Defines lifecycle and discovery services implemented by a processor architecture.</summary>
/// <nova.when>Use this contract when kernel code selects or initialises an architecture implementation.</nova.when>
/// <nova.depends>Inu.Core</nova.depends>
[SupportedArchitecture(SupportedArchitecture.All)]
[BootStage(BootStage.ManagedBootstrap)]
public interface ICpuArchitecture
{
    /// <summary>Gets the architecture implemented by this instance.</summary>
    /// <nova.when>Use this value to validate architecture selection before initialisation.</nova.when>
    /// <nova.depends>Inu.Core.SupportedArchitecture</nova.depends>
    SupportedArchitecture Architecture { get; }

    /// <summary>Initialises architecture state required before memory and interrupt services.</summary>
    /// <nova.when>Call once on the bootstrap processor during managed bootstrap.</nova.when>
    /// <nova.depends>Architecture-specific native entry support</nova.depends>
    /// <returns><see langword="true"/> when early initialisation completed.</returns>
    /// <example><code>bool ready = architecture.InitialiseEarly();</code></example>
    bool InitialiseEarly();

    /// <summary>Initialises architecture state for one logical processor.</summary>
    /// <nova.when>Call on each processor before enabling interrupts or scheduling work.</nova.when>
    /// <nova.depends>InitialiseEarly</nova.depends>
    /// <returns><see langword="true"/> when the processor was initialised.</returns>
    /// <example><code>bool ready = architecture.InitialiseProcessor(0);</code></example>
    bool InitialiseProcessor(uint processorId);

    /// <summary>Enables maskable interrupts on the current processor.</summary>
    /// <nova.when>Call only after the interrupt tables and controller are ready.</nova.when>
    /// <nova.depends>Architecture interrupt-entry stubs</nova.depends>
    /// <returns><see langword="true"/> when interrupts are enabled.</returns>
    /// <example><code>bool enabled = architecture.EnableInterrupts();</code></example>
    bool EnableInterrupts();

    /// <summary>Disables maskable interrupts on the current processor.</summary>
    /// <nova.when>Use around short critical regions or before terminal halt.</nova.when>
    /// <nova.depends>Architecture instruction binding</nova.depends>
    /// <returns><see langword="true"/> when interrupts are disabled.</returns>
    /// <example><code>bool disabled = architecture.DisableInterrupts();</code></example>
    bool DisableInterrupts();

    /// <summary>Halts the current processor according to architecture policy.</summary>
    /// <nova.when>Use when the current processor has no executable work or has reached a terminal state.</nova.when>
    /// <nova.depends>Architecture instruction binding</nova.depends>
    /// <returns><see langword="true"/> if control resumes after a non-terminal halt.</returns>
    /// <example><code>bool resumed = architecture.Halt();</code></example>
    bool Halt();

    /// <summary>Reads the architecture timestamp counter.</summary>
    /// <nova.when>Use for monotonic low-level timing after feature validation.</nova.when>
    /// <nova.depends>Architecture timestamp source</nova.depends>
    /// <returns>The current architecture timestamp value.</returns>
    /// <example><code>ulong ticks = architecture.ReadTimestamp();</code></example>
    ulong ReadTimestamp();
}

/// <summary>Identifies architecture-independent memory barrier strengths.</summary>
/// <nova.when>Use when ordering memory operations shared with processors or devices.</nova.when>
/// <nova.depends>Architecture-specific barrier implementation</nova.depends>
public enum MemoryBarrierKind
{
    /// <summary>Orders preceding loads before subsequent loads.</summary>
    Load = 0,
    /// <summary>Orders preceding stores before subsequent stores.</summary>
    Store = 1,
    /// <summary>Orders all preceding memory operations before subsequent operations.</summary>
    Full = 2,
    /// <summary>Orders instruction fetching after code or translation changes.</summary>
    Instruction = 3
}

/// <summary>Describes an architecture-neutral CPU feature query result.</summary>
/// <nova.when>Use when optional architecture capabilities affect kernel policy.</nova.when>
/// <nova.depends>Architecture-specific feature detector</nova.depends>
public readonly record struct CpuFeatureResult(uint FeatureId, bool IsSupported, ulong Value);

/// <summary>Describes a page-table entry without exposing an architecture encoding.</summary>
/// <nova.when>Use in architecture-neutral memory-management code before requesting native encoding.</nova.when>
/// <nova.depends>Architecture-specific page-table encoder</nova.depends>
public readonly record struct PageTableEntryDescriptor(ulong PhysicalAddress, PageTableEntryFlags Flags);

/// <summary>Defines architecture-neutral page-table permissions and attributes.</summary>
/// <nova.when>Use to describe mappings independently of x64 or ARM64 bit layouts.</nova.when>
/// <nova.depends>Architecture-specific page-table encoder</nova.depends>
[Flags]
public enum PageTableEntryFlags : ulong
{
    /// <summary>No mapping attributes are selected.</summary>
    None = 0,
    /// <summary>The mapping is present and valid.</summary>
    Present = 1UL << 0,
    /// <summary>The mapping may be written.</summary>
    Writable = 1UL << 1,
    /// <summary>The mapping may be accessed from user mode.</summary>
    User = 1UL << 2,
    /// <summary>The mapping represents a large page.</summary>
    LargePage = 1UL << 3,
    /// <summary>The mapping is global across address spaces.</summary>
    Global = 1UL << 4,
    /// <summary>Instruction execution is prohibited.</summary>
    NoExecute = 1UL << 5,
    /// <summary>Device or strongly ordered memory semantics are requested.</summary>
    Device = 1UL << 6,
    /// <summary>Write-through caching is requested.</summary>
    WriteThrough = 1UL << 7,
    /// <summary>Caching is disabled.</summary>
    CacheDisable = 1UL << 8
}

/// <summary>Provides a common result for architecture operations that also return a machine value.</summary>
/// <nova.when>Use when an operation must report success separately from a zero-valued result.</nova.when>
/// <nova.depends>None</nova.depends>
public readonly record struct ArchitectureValueResult(bool Succeeded, ulong Value);
