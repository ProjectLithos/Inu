using System;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Acpi;

/// <summary>Describes an ECDT-discovered ACPI Embedded Controller.</summary>
public readonly struct AcpiEcInfo
{
    internal AcpiEcInfo(AcpiGenericAddress control, AcpiGenericAddress data, UInt32 uid, Byte gpe)
    { Control = control; Data = data; UniqueId = uid; Gpe = gpe; }
    /// <summary>Gets the EC command/status register.</summary>
    public AcpiGenericAddress Control { get; }
    /// <summary>Gets the EC data register.</summary>
    public AcpiGenericAddress Data { get; }
    /// <summary>Gets the EC UID.</summary>
    public UInt32 UniqueId { get; }
    /// <summary>Gets the EC GPE number.</summary>
    public Byte Gpe { get; }
}
