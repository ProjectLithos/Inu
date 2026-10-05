# ACPI Platform Drivers

`KernelAcpi` is the validated RSDP/RSDT/XSDT table registry. Table-specific consumers and platform features are separate source components above that registry.

MADT, MCFG and HPET interpretation register through their neutral service contracts. In 0.2.23 the remaining platform pieces use the same model:

- `KernelAcpiFadtProvider` owns FADT parsing and publishes `acpi.fadt` through `KernelAcpiFadtServices`.
- `KernelAcpiRegisterServices` owns shared Generic Address Structure parsing plus System I/O and System Memory register access. It is internal dependency infrastructure rather than an OS policy choice.
- `KernelAcpiPowerProvider` owns fixed-feature power-button, reset and AML sleep-state policy. Higher-level power management consumes `KernelAcpiPowerServices` only.
- `KernelAcpiEcProvider` owns ECDT discovery and EC byte transport and publishes it through `KernelAcpiEcServices`.

FADT, fixed-feature ACPI power and ECDT embedded-controller support are independently selectable. Omitting one does not make the ACPI root-table registry invalid. Dependencies are explicit: the power provider requires FADT and generic register access; the EC provider requires table lookup and generic register access.

The old concrete `KernelAcpiFadt`, `KernelAcpiPower` and `KernelAcpiEc` APIs are intentionally removed rather than retained as compatibility shims. Kernel code should consume the service contracts or higher-level subsystem APIs.
