# Inu SDK 0.39.0 — Professional foundation

This release makes the professional SDK contracts authoritative rather than roadmap-only.

## Driver model
The driver state machine now covers discover, probe, bind, start, stop, reset, suspend, resume, remove, fail and recover. Existing five-callback drivers remain source compatible. Every device is a node in one hierarchical model covering PCI, USB, ACPI, platform, virtual and logical devices. Capability grants remain per binding and are revoked on stop/failure/removal.

Driver manifests are schema v3 and carry IDs, architecture, minimum Inu version, API/ABI versions, dependencies, permissions/capabilities and signing metadata. Signing state is represented now; trust-chain enforcement remains policy-controlled so a later release can require signatures without changing the manifest schema.

## Diagnostics and reliability
`KernelLog` provides Trace/Debug/Info/Warning/Error/Critical records with subsystem, CPU, thread, process, timestamp and source. `KernelTelemetry` defines KernelTrace, KernelProfile, KernelBootEvent, KernelCounter and KernelDiagnosticEvent. Crash dumps are `NOCD` format 1.0 with versioned sections for CPU/registers/stack/page tables/processes/modules/heap/memory/panic/drivers. `KernelPanic` formalizes capture, dump, debugger break and halt/reboot policy.

## Verification and test contracts
The SDK defines kernel, unit, integration, boot, driver, stress, fault-injection and hardware-simulation tests. The QEMU matrix includes 1/2/4/8 CPUs, multiple RAM sizes, AHCI/NVMe/VirtIO block, E1000/VirtIO net, GOP/VirtIO GPU, xHCI and firmware variants. Fault-injection contracts cover allocation failure, I/O timeout, dropped interrupt, device reset, bad DMA, corrupt packet, page fault, CPU offline and filesystem error.

## Stable subsystem boundaries
Versioned contracts now cover architecture abstraction, SMP/per-CPU, synchronization, memory diagnostics, process security, executable/package metadata, VFS, network stack, power management and timekeeping. The x64 implementation remains current; ARM64 is represented as the next architecture without pretending it is complete.

## SDK operation
`inu.cmd` / `inu.ps1` provides `new`, `build`, `run`, `debug`, `test`, `pack`, and `doctor` entry points over the existing SDK scripts. `Inu.SdkManifest.json` schema v2 is the authoritative source for SDK/API/ABI/toolchain/format/project-schema versions. Reproducible-build provenance and compatibility manifests are versioned alongside it.
