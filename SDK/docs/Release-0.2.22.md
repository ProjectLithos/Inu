# Inu 0.2.22

This release separates the first ACPI table consumers from the generic ACPI root-table registry.

- `KernelAcpi` remains the RSDP/RSDT/XSDT validation and generic table lookup core.
- MADT topology parsing is owned by `KernelAcpiMadtProvider` and dispatched through `KernelAcpiMadtServices`.
- MCFG/ECAM discovery is owned by `KernelAcpiMcfgProvider` and dispatched through `KernelAcpiMcfgServices`.
- HPET table interpretation is owned by `KernelAcpiHpetProvider` and dispatched through `KernelAcpiHpetServices`.
- MADT, MCFG and HPET table consumers are selectable source components; omitting one leaves the ACPI root registry valid and its facade reports the service as unavailable.
- FADT, ACPI fixed-feature power, and the embedded controller are recorded as candidates only because their current dependencies still cross concrete implementation boundaries.
- Existing x64 HAL/Drivers defaults continue to select MADT, MCFG and HPET discovery so standard generated kernels retain their current capabilities.

No Get/Set/Event ABI changes are introduced.
