namespace Inu.Core;

/// <summary>Identifies architectures on which a public Inu API is supported.</summary>
[Flags]
public enum SupportedArchitecture
{
    /// <summary>No architecture has been selected.</summary>
    None = 0,
    /// <summary>The AMD64/x86-64 architecture.</summary>
    X64 = 1,
    /// <summary>The ARM64/AArch64 architecture.</summary>
    Arm64 = 2,
    /// <summary>The RISC-V 64-bit architecture.</summary>
    RiscV64 = 4,
    /// <summary>All architectures currently represented by the SDK contract.</summary>
    All = X64 | Arm64 | RiscV64
}

/// <summary>Identifies the earliest boot stage at which a public API may be used.</summary>
public enum BootStage
{
    /// <summary>The API is valid before managed runtime initialisation.</summary>
    NativeEntry = 0,
    /// <summary>The API is valid during managed bootstrap.</summary>
    ManagedBootstrap = 1,
    /// <summary>The API is valid after architecture services are initialised.</summary>
    ArchitectureInitialised = 2,
    /// <summary>The API is valid after memory services are initialised.</summary>
    MemoryInitialised = 3,
    /// <summary>The API is valid during normal kernel operation.</summary>
    KernelRunning = 4
}

/// <summary>Declares the architectures supported by a public API.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
public sealed class SupportedArchitectureAttribute : Attribute
{
    /// <summary>Creates architecture-support metadata.</summary>
    /// <param name="architectures">The supported architecture set.</param>
    public SupportedArchitectureAttribute(SupportedArchitecture architectures) => Architectures = architectures;

    /// <summary>Gets the supported architecture set.</summary>
    public SupportedArchitecture Architectures { get; }
}

/// <summary>Declares the earliest boot stage at which a public API may be called.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
public sealed class BootStageAttribute : Attribute
{
    /// <summary>Creates boot-stage metadata.</summary>
    /// <param name="stage">The earliest supported boot stage.</param>
    public BootStageAttribute(BootStage stage) => Stage = stage;

    /// <summary>Gets the earliest supported boot stage.</summary>
    public BootStage Stage { get; }
}
