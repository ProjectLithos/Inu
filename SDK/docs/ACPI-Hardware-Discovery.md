# ACPI and Hardware Discovery

Inu 0.5.0 implements roadmap item 12 with a boot-time ACPI hand-off and an allocation-free freestanding discovery API.

## Firmware hand-off

On x64 UEFI, `native/x64/Entry.asm` reads `EFI_SYSTEM_TABLE.NumberOfTableEntries` and `ConfigurationTable` while firmware state is still valid. It prefers `EFI_ACPI_20_TABLE_GUID` and falls back to the ACPI 1.0 table GUID. The selected Root System Description Pointer (RSDP) physical address is stored at offset `0x80` in the Inu native boot context before the final memory-map/`ExitBootServices` sequence.

The kernel does not scan the EBDA or BIOS ROM area and does not guess an RSDP address. Firmware discovery is explicit.

## Validation rules

`Inu.Kernel.Acpi.KernelAcpi` performs discovery without heap allocation.

- The RSDP must contain the `RSD PTR ` signature.
- The ACPI 1.0 20-byte checksum must be zero.
- Revision 2 or later RSDPs must have a valid extended length and extended checksum.
- XSDT is preferred when ACPI 2.0+ supplies one; otherwise RSDT is used.
- Every consumed System Description Table must have a minimum 36-byte header, bounded length, and valid checksum.
- Root entry counts are bounded to prevent malformed firmware tables from causing unbounded traversal.
- Child-table discovery skips invalid checksummed tables rather than exposing them as valid hardware descriptions.

## Public freestanding API

`KernelAcpi.Initialize(IAcpiRootPointerContext)` initializes discovery. Status can be queried with `GetLastStatus()` or the freestanding-safe `GetLastStatusName()`.

General table access is available through `TryGetTable(signature, out address, out length)`. Well-known signatures are exposed for MADT/APIC, FADT, HPET, and MCFG.

Hardware-specific helpers include:

- `GetProcessorCount()` and `TryGetProcessor(...)` for Local APIC and x2APIC processor records.
- `GetIoApicCount()` and `TryGetIoApic(...)`.
- `GetInterruptOverrideCount()` and `TryGetInterruptOverride(...)`.
- `TryGetLocalApicAddress(...)`, including MADT Local APIC Address Override handling.
- `GetPciEcamCount()` and `TryGetPciEcam(...)` for MCFG PCI Express configuration-space windows.
- `TryGetHpet(...)` for the ACPI HPET register block.

The generic table lookup remains available so SDK users can implement their own ACPI table decoders without changing the Inu kernel bootstrap.

## Memory ownership

ACPI reclaimable and ACPI NVS ranges remain excluded from ordinary early PMM allocation in the current memory policy. Inu therefore does not reclaim table storage during ACPI discovery. A later reclamation policy may copy required tables and explicitly release only ACPI reclaimable ranges after all consumers have completed initialization.

## Kernel ordering

The generated kernel performs ACPI discovery after descriptor/interrupt-controller bootstrap and before physical-memory initialization. This keeps firmware-provided ACPI physical mappings available during initial parsing and ensures later SMP, timer, PCI, and driver subsystems can consume validated topology rather than performing independent firmware scans.

## Extension methodology

An operating system built with Inu may use the built-in helpers, call `TryGetTable` and implement a custom table parser, or ignore optional tables entirely. Missing HPET or MCFG tables are not treated as a malformed ACPI root; only the root-pointer/root-table validation is mandatory for successful `KernelAcpi.Initialize`.

## 0.18.4 platform-driver extension

Inu 0.18.4 keeps the allocation-free root discovery above and adds `KernelAcpiMadt`, `KernelAcpiMcfg`, `KernelAcpiHpet`, `KernelAcpiFadt`, `KernelAcpiEc`, and `KernelAcpiPower`. The FADT layer understands legacy and extended GAS fields. ECDT controllers can be accessed through the standard EC byte protocol. Fixed-feature power-button status, FADT reset, and AML `_S5` soft-off are exposed as high-level services. S1/S3/S4 suspend/resume is deliberately deferred until the AML execution and device suspend/resume layers are capable of supporting those transitions safely.


## 0.2.23 provider boundary

FADT, fixed-feature power and ECDT embedded-controller handling are now selectable providers. Consumers use `KernelAcpiFadtServices`, `KernelAcpiPowerServices`, `KernelAcpiEcServices` and `KernelAcpiRegisterServices`; the old concrete service APIs are no longer part of the current surface.
