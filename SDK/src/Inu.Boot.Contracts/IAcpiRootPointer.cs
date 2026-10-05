using Inu.Primitives;

namespace Inu.Boot.Contracts;

/// <summary>Provides a captured ACPI Root System Description Pointer.</summary>
public interface IAcpiRootPointer : IBootContext
{
    PhysicalAddress AcpiRootPointerAddress { get; }
}

public readonly struct AcpiRootPointerContext : IAcpiRootPointer
{
    public AcpiRootPointerContext(PhysicalAddress acpiRootPointerAddress)
        => AcpiRootPointerAddress = acpiRootPointerAddress;

    public PhysicalAddress AcpiRootPointerAddress { get; }
}
