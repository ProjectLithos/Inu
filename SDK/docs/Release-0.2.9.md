# Inu OS SDK 0.2.9

This release continues the behaviour-preserving source-component decomposition.

## Decomposed in this release

- NVMe: driver/controller lifecycle, namespace discovery, block I/O, queue protocol, and backing storage/allocation helpers.
- xHCI: controller lifecycle/setup, USB device and transfer handling, ring/event protocol, and backing state/allocation helpers.
- ACPI: core discovery, MADT, platform tables, validation, MCFG, HPET, FADT, EC, power-management, and generic-register access.

No public subsystem contract, filesystem policy, kernel-language policy, firmware-dependence policy, or Get/Set/Event syscall ABI is intentionally changed. Split files remain implementation slices unless and until they expose an independently meaningful contract, dependency set, ownership, and lifecycle for Kath selection.
