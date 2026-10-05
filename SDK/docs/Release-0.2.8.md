# Inu OS SDK 0.2.8

This release continues the behaviour-preserving decomposition of large Inu source components into smaller responsibility-focused source units for Kath composition and analysis.

## Decomposed in this release

- PCI: lifecycle, configuration access, BAR/capability/interrupt handling, MMIO, enumeration, and storage helpers.
- Virtual memory: lifecycle, mappings, page-table operations, entry encoding/protection, and state.
- Physical memory: lifecycle, allocation/release/statistics, extent maintenance, and record/state helpers.
- Kernel heap: lifecycle, allocation/release/statistics, growth, free-list/coalescing, and diagnostic metadata.
- TrueType: public API, font parsing, outline construction, and raster/scaling helpers.

No public subsystem contract, syscall ABI, filesystem policy, or end-user OS policy is intentionally changed by this decomposition. Split files remain implementation slices until they have explicit independent contracts, dependencies, ownership, and lifecycle suitable for selection by Kath.

The next decomposition target is the densely packed NVMe/xHCI driver code and remaining ACPI/HAL orchestration.
