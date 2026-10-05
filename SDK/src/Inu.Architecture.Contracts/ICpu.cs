using Inu.Primitives;

namespace Inu.Architecture;

/// <summary>Defines the architecture-neutral processor operations exposed to kernel services.</summary>
/// <nova.when>Use this contract when code needs processor control without depending on x64 or ARM64 implementation details.</nova.when>
/// <nova.depends>Inu.Primitives.ProcessorId</nova.depends>
public interface ICpu
{
    /// <summary>Enables maskable interrupts on the current processor.</summary>
    /// <returns><see langword="true"/> when interrupts are enabled.</returns>
    /// <example><code>bool enabled = cpu.EnableInterrupts();</code></example>
    bool EnableInterrupts();

    /// <summary>Disables maskable interrupts on the current processor.</summary>
    /// <returns><see langword="true"/> when interrupts are disabled.</returns>
    /// <example><code>bool disabled = cpu.DisableInterrupts();</code></example>
    bool DisableInterrupts();

    /// <summary>Determines whether maskable interrupts are enabled on the current processor.</summary>
    /// <returns><see langword="true"/> when interrupts are enabled; otherwise, <see langword="false"/>.</returns>
    /// <example><code>bool enabled = cpu.AreInterruptsEnabled();</code></example>
    bool AreInterruptsEnabled();

    /// <summary>Gets the stable Inu identifier for the current logical processor.</summary>
    /// <returns>The current logical processor identifier.</returns>
    /// <example><code>ProcessorId processor = cpu.GetProcessorId();</code></example>
    ProcessorId GetProcessorId();

    /// <summary>Halts the current processor until an interrupt or architecture-defined wake event occurs.</summary>
    /// <returns><see langword="true"/> if execution resumes after the halt operation.</returns>
    /// <example><code>bool resumed = cpu.Halt();</code></example>
    bool Halt();
}
