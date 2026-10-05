using System;

namespace Inu.Kernel.Architecture;

/// <summary>Architecture identifiers exposed to generic kernel code.</summary>
public enum KernelArchitectureKind : Byte
{
    Unknown = 0,
    X64 = 1,
    Arm64 = 2
}

/// <summary>Architecture capabilities expressed without x64/ARM64 register or instruction details.</summary>
public readonly struct KernelArchitectureCapabilities
{
    public KernelArchitectureCapabilities(Boolean interrupts, Boolean paging, Boolean smp, Boolean userMode, Boolean systemCalls, Boolean memoryMappedIo)
    {
        Interrupts = interrupts;
        Paging = paging;
        Smp = smp;
        UserMode = userMode;
        SystemCalls = systemCalls;
        MemoryMappedIo = memoryMappedIo;
    }

    public Boolean Interrupts { get; }
    public Boolean Paging { get; }
    public Boolean Smp { get; }
    public Boolean UserMode { get; }
    public Boolean SystemCalls { get; }
    public Boolean MemoryMappedIo { get; }
}

/// <summary>
/// Generic kernel-facing architecture boundary. Architecture implementations install identity and
/// capabilities here; code above this layer must not depend on x64/ARM64 namespaces or encodings.
/// </summary>
public static class KernelArchitecture
{
    private static Boolean _installed;
    private static KernelArchitectureKind _kind;
    private static KernelArchitectureCapabilities _capabilities;

    public static Boolean IsInstalled() => _installed;
    public static KernelArchitectureKind GetKind() => _kind;
    public static KernelArchitectureCapabilities GetCapabilities() => _capabilities;

    /// <summary>Installs the selected architecture descriptor during platform bootstrap.</summary>
    public static Boolean Install(KernelArchitectureKind kind, KernelArchitectureCapabilities capabilities)
    {
        if (kind == KernelArchitectureKind.Unknown) return false;
        if (_installed) return _kind == kind;
        _kind = kind;
        _capabilities = capabilities;
        _installed = true;
        return true;
    }
}
