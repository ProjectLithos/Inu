# Inu Crash Dump Format

**Format name:** Inu Crash Dump  
**Magic:** `NOCD` (`0x4E4F4344`)  
**Current format version:** **1.1**  
**SDK ABI:** `CrashDumpAbiVersion = "1.0"`  
**IDE file extension:** `.nodump.json`

Inu 0.10.11 makes crash dumps a public SDK format. The format is designed so newer IDE versions can continue opening older dumps and so non-IDE tools can consume the same crash information.

## Compatibility contract

- The **major** format version is the breaking compatibility boundary.
- A reader supporting major version `1` must accept any `1.x` dump whose known sections can be decoded.
- A **minor** release may add optional fields and optional section kinds; it must not reinterpret existing fields.
- Every section has its own independent integer `version`.
- Readers must ignore **unknown section kinds** and **unknown fields**.
- If a known section has a newer section version, a reader may use the fields it understands and must not reject the entire dump solely because optional fields were added.
- Required top-level fields are `magic`, `format`, `formatVersion`, `architecture`, `createdUtc`, `producer`, and `sections`.
- Pre-0.10.11 IDE `schemaVersion: 1` JSON dumps are a legacy input format. Kath 0.10.11 retains an adapter for them; new producers must write the formal `NOCD` format.

## Section directory

Version 1.1 defines eleven standard sections. Version 1.0 readers remain compatible because section additions are a minor-version extension. Each section is represented as:

```json
{
  "version": 1,
  "available": true,
  "data": {},
  "note": "optional capture note"
}
```

An unavailable section remains present with `available: false`; this distinguishes “not captured” from “format does not define this data”.

| ID | JSON name | v1 contents |
|---:|---|---|
| 1 | `cpuState` | architecture, current CPU/thread/process, RIP/RSP/RBP, flags, page-table root, execution contexts |
| 2 | `registers` | named architectural register values |
| 3 | `stack` | unwind frames and captured stack memory |
| 4 | `pageTables` | CR3 and decoded x64 page-table walk for the fault/current address |
| 5 | `processes` | process IDs with associated threads/CPUs and current-process marker |
| 6 | `modules` | loaded/debug image modules, image/PDB paths, runtime relocation data |
| 7 | `heap` | initialization state, committed/allocated/free/peak bytes and heap blocks |
| 8 | `memoryRanges` | captured memory ranges, initially current stack and code windows |
| 9 | `panic` | panic/crash reason, exception vector/name, source location, diagnostic message |
| 10 | `drivers` | driver IDs, configuration/runtime state and diagnostic detail |
| 11 | `telemetry` | bounded recent telemetry/runtime output captured close to the failure |

## Top-level v1.1 JSON shape

```json
{
  "magic": "NOCD",
  "format": "Inu Crash Dump",
  "formatVersion": { "major": 1, "minor": 1 },
  "architecture": "x86_64",
  "createdUtc": "2026-08-19T00:00:00.000Z",
  "producer": { "product": "Kath", "version": "0.10.11" },
  "project": { "name": "ExampleOS", "root": "C:\\InuOSes\\ExampleOS" },
  "sections": {
    "cpuState": { "version": 1, "available": true, "data": {} },
    "registers": { "version": 1, "available": true, "data": [] },
    "stack": { "version": 1, "available": true, "data": {} },
    "pageTables": { "version": 1, "available": true, "data": {} },
    "processes": { "version": 1, "available": true, "data": [] },
    "modules": { "version": 1, "available": true, "data": [] },
    "heap": { "version": 1, "available": true, "data": {} },
    "memoryRanges": { "version": 1, "available": true, "data": {} },
    "panic": { "version": 1, "available": true, "data": {} },
    "drivers": { "version": 1, "available": true, "data": [] },
    "telemetry": { "version": 1, "available": true, "data": { "serialTail": "..." } }
  }
}
```

## Binary SDK representation

`Inu.Kernel.SubsystemContracts` exposes `KernelCrashDumpFormat`, `KernelCrashDumpHeader`, `KernelCrashDumpSection`, `KernelCrashSectionKind`, and the v1 record contracts. The same magic, major/minor rules, section IDs, and section versions apply to a future compact/binary producer. A binary reader uses the section directory offsets/lengths; the IDE JSON representation uses the named `sections` object.

## Capture completeness

The v1.1 telemetry section is intentionally bounded: producers should retain only the recent diagnostic tail needed to establish the events immediately preceding a failure rather than embedding an unbounded log.

The format describes what a crash dump *can* carry; a particular target may not expose every live diagnostic ABI yet. Producers must keep every standard section and set `available: false` or add a `note` when the target could not provide that state. This allows the format to evolve independently from individual kernel subsystem implementations.
