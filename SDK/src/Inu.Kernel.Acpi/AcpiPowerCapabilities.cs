using System;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Acpi;

/// <summary>Describes ACPI fixed-feature power-management capability.</summary>
public readonly struct AcpiPowerCapabilities
{
    internal AcpiPowerCapabilities(Boolean initialized, Boolean button, Boolean reset, Boolean shutdown, Byte s5a, Byte s5b)
    { Initialized=initialized; PowerButtonAvailable=button; ResetAvailable=reset; ShutdownAvailable=shutdown; S5TypeA=s5a; S5TypeB=s5b; }
    /// <summary>Gets whether FADT power management initialized.</summary>
    public Boolean Initialized { get; }
    /// <summary>Gets whether the fixed-feature power button can be polled.</summary>
    public Boolean PowerButtonAvailable { get; }
    /// <summary>Gets whether the FADT reset register is available.</summary>
    public Boolean ResetAvailable { get; }
    /// <summary>Gets whether S5 shutdown values were discovered from AML.</summary>
    public Boolean ShutdownAvailable { get; }
    /// <summary>Gets the PM1a S5 sleep type.</summary>
    public Byte S5TypeA { get; }
    /// <summary>Gets the PM1b S5 sleep type.</summary>
    public Byte S5TypeB { get; }
}
