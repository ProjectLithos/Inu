using Inu.Core;

namespace Inu.Architecture.Arm64;

/// <summary>Defines the explicit readiness state of the ARM64 architecture implementation.</summary>
/// <nova.when>Use to distinguish the present contract scaffold from a bound native ARM64 implementation.</nova.when>
/// <nova.depends>Future Inu ARM64 native bindings</nova.depends>
public enum Arm64ArchitectureState
{
    /// <summary>No ARM64 native operation table is available.</summary>
    Unavailable = 0,
    /// <summary>The ARM64 native operation table is bound and ready.</summary>
    Ready = 1
}

/// <summary>Provides the ARM64 architecture lifecycle scaffold without pretending native operations exist.</summary>
/// <nova.when>Use for architecture selection and capability reporting while the ARM64 native backend is developed.</nova.when>
/// <nova.depends>Inu.Architecture.Contracts</nova.depends>
[SupportedArchitecture(SupportedArchitecture.Arm64)]
public sealed class Arm64CpuArchitecture : ICpuArchitecture
{
    /// <summary>Gets the current native backend readiness state.</summary>
    /// <nova.when>Check before attempting to initialise an ARM64 kernel.</nova.when>
    /// <nova.depends>Future ARM64 native binding table</nova.depends>
    public Arm64ArchitectureState State => Arm64ArchitectureState.Unavailable;

    /// <summary>Gets the ARM64 architecture identifier.</summary>
    /// <nova.when>Use to validate architecture selection.</nova.when>
    /// <nova.depends>Inu.Core</nova.depends>
    public SupportedArchitecture Architecture => SupportedArchitecture.Arm64;

    /// <summary>Reports that the ARM64 native backend is not yet bound.</summary>
    /// <nova.when>Call during architecture selection; a false result prevents unsupported boot.</nova.when>
    /// <nova.depends>Future ARM64 native binding table</nova.depends>
    /// <returns><see langword="false"/> until the ARM64 native backend is implemented.</returns>
    /// <example><code>bool ready = architecture.InitialiseEarly();</code></example>
    public bool InitialiseEarly() => false;

    /// <summary>Reports that per-processor ARM64 initialisation is unavailable.</summary>
    /// <nova.when>Call only after early initialisation succeeds.</nova.when>
    /// <nova.depends>Future ARM64 exception-vector and per-CPU implementation</nova.depends>
    /// <returns><see langword="false"/> until the ARM64 native backend is implemented.</returns>
    /// <example><code>bool ready = architecture.InitialiseProcessor(0);</code></example>
    public bool InitialiseProcessor(uint processorId) => false;

    /// <summary>Reports that ARM64 interrupt enable is unavailable.</summary>
    /// <nova.when>Call only after the ARM64 native backend becomes ready.</nova.when>
    /// <nova.depends>Future DAIF native operation</nova.depends>
    /// <returns><see langword="false"/> until implemented.</returns>
    /// <example><code>bool enabled = architecture.EnableInterrupts();</code></example>
    public bool EnableInterrupts() => false;

    /// <summary>Reports that ARM64 interrupt disable is unavailable.</summary>
    /// <nova.when>Call only after the ARM64 native backend becomes ready.</nova.when>
    /// <nova.depends>Future DAIF native operation</nova.depends>
    /// <returns><see langword="false"/> until implemented.</returns>
    /// <example><code>bool disabled = architecture.DisableInterrupts();</code></example>
    public bool DisableInterrupts() => false;

    /// <summary>Reports that ARM64 wait-for-interrupt halt is unavailable.</summary>
    /// <nova.when>Call only after the ARM64 native backend becomes ready.</nova.when>
    /// <nova.depends>Future WFI native operation</nova.depends>
    /// <returns><see langword="false"/> until implemented.</returns>
    /// <example><code>bool halted = architecture.Halt();</code></example>
    public bool Halt() => false;

    /// <summary>Returns no timestamp until the ARM64 counter backend is implemented.</summary>
    /// <nova.when>Call only after feature discovery confirms a system counter.</nova.when>
    /// <nova.depends>Future CNTVCT_EL0 native operation</nova.depends>
    /// <returns>Zero until implemented.</returns>
    /// <example><code>ulong ticks = architecture.ReadTimestamp();</code></example>
    public ulong ReadTimestamp() => 0;
}
