# Inu 0.2.23

This release completes the ACPI platform-provider split.

- FADT parsing is a selectable provider behind `KernelAcpiFadtServices`.
- Fixed-feature ACPI power is a selectable provider behind `KernelAcpiPowerServices`.
- ECDT embedded-controller transport is a selectable provider behind `KernelAcpiEcServices`.
- Generic ACPI GAS parsing and register access moved to internal `KernelAcpiRegisterServices`.
- RTC/CMOS consumes the FADT contract for the optional century register rather than the concrete parser.
- Higher-level power management consumes the ACPI power contract rather than the concrete provider.
- The old concrete `KernelAcpiFadt`, `KernelAcpiPower`, and `KernelAcpiEc` APIs are intentionally removed.
- Standard generated HAL/Drivers selections preserve the previous default platform behaviour while allowing the components to be omitted or substituted.

No Get/Set/Event ABI changes are introduced.
