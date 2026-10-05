# Current SDK status and extension points

Inu OS SDK 0.44.18 is now broad enough to build and debug configurable x64 UEFI operating systems through either Kath 0.44.18 or the standalone `inu` CLI.

## Stable foundations

The SDK has formal subsystem contracts, API/ABI versioning, compatibility tests, capability-based drivers, architecture boundaries, memory management, SMP, scheduling, process isolation, system calls, VFS, networking, power, timekeeping, packaging, diagnostics and testing. These should be extended additively within the current public API major version.

## Hardware growth

New hardware support should normally be added below the existing driver contracts rather than by exposing device-specific behaviour to kernels or applications. High-value future areas include additional physical NICs, broader USB classes, NVMe/AHCI refinements, audio, and later real GPU acceleration.

## Filesystem growth

New filesystems plug into the VFS provider contract. The process-visible API should not depend on FAT-specific structures. Journaling, caching, permissions and asynchronous I/O can evolve behind that boundary.

## Networking growth

The standard network API already fixes the layer boundaries from NIC through DNS. Further TCP robustness, IPv6 services, DHCPv6, routing policy and higher-level protocols should remain behind the socket-facing contracts.

## Text and GUI growth

The TrueType renderer supplies scalable glyph rasterisation. Text shaping, bidirectional layout, GPOS/GSUB, variable fonts and a window/compositor system are intentionally separate layers that can be added without changing the current raster contract.

## SDK quality

Every release should continue to run the API compatibility gate, generated-template checks, test framework, documentation audit and CLI `doctor` checks. Public APIs should carry useful XML documentation and samples rather than placeholder text.
