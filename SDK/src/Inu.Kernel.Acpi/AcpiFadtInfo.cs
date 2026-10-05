using System;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Acpi;

/// <summary>Describes the Fixed ACPI Description Table power-management registers.</summary>
public readonly struct AcpiFadtInfo
{
    internal AcpiFadtInfo(UInt32 flags, UInt16 sci, UInt32 smiCommand, Byte acpiEnable, Byte acpiDisable, Byte pm1EventLength, Byte pm1ControlLength,
        AcpiGenericAddress pm1aEvent, AcpiGenericAddress pm1bEvent, AcpiGenericAddress pm1aControl, AcpiGenericAddress pm1bControl,
        AcpiGenericAddress reset, Byte resetValue, UInt64 firmwareControl, UInt64 dsdt, Byte centuryRegister)
    { Flags = flags; SciInterrupt = sci; SmiCommandPort = smiCommand; AcpiEnableValue = acpiEnable; AcpiDisableValue = acpiDisable; Pm1EventLength = pm1EventLength; Pm1ControlLength = pm1ControlLength; Pm1aEvent = pm1aEvent; Pm1bEvent = pm1bEvent; Pm1aControl = pm1aControl; Pm1bControl = pm1bControl; ResetRegister = reset; ResetValue = resetValue; FirmwareControlAddress = firmwareControl; DsdtAddress = dsdt; CenturyRegister = centuryRegister; }
    /// <summary>Gets the FADT feature flags.</summary>
    public UInt32 Flags { get; }
    /// <summary>Gets the ACPI SCI interrupt.</summary>
    public UInt16 SciInterrupt { get; }
    /// <summary>Gets the SMI command I/O port.</summary>
    public UInt32 SmiCommandPort { get; }
    /// <summary>Gets the firmware ACPI-enable command value.</summary>
    public Byte AcpiEnableValue { get; }
    /// <summary>Gets the firmware ACPI-disable command value.</summary>
    public Byte AcpiDisableValue { get; }
    /// <summary>Gets the PM1 event-block byte length.</summary>
    public Byte Pm1EventLength { get; }
    /// <summary>Gets the PM1 control-block byte length.</summary>
    public Byte Pm1ControlLength { get; }
    /// <summary>Gets PM1a event registers.</summary>
    public AcpiGenericAddress Pm1aEvent { get; }
    /// <summary>Gets PM1b event registers.</summary>
    public AcpiGenericAddress Pm1bEvent { get; }
    /// <summary>Gets PM1a control registers.</summary>
    public AcpiGenericAddress Pm1aControl { get; }
    /// <summary>Gets PM1b control registers.</summary>
    public AcpiGenericAddress Pm1bControl { get; }
    /// <summary>Gets the FADT reset register.</summary>
    public AcpiGenericAddress ResetRegister { get; }
    /// <summary>Gets the FADT reset value.</summary>
    public Byte ResetValue { get; }
    /// <summary>Gets the physical FACS address used for firmware waking vectors.</summary>
    public UInt64 FirmwareControlAddress { get; }
    /// <summary>Gets the DSDT physical address.</summary>
    public UInt64 DsdtAddress { get; }
    /// <summary>Gets the CMOS century register index advertised by ACPI, or zero when absent.</summary>
    public Byte CenturyRegister { get; }
    /// <summary>Gets whether ACPI reset is advertised.</summary>
    public Boolean SupportsReset() => (Flags & (1U << 10)) != 0U && ResetRegister.IsPresent();
}
