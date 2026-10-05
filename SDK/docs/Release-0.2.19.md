# Inu 0.2.19

This release continues source-component decomposition through the x64 interrupt-delivery path.

- Split opaque route lifecycle/dispatch from hardware routing.
- Split PCI MSI-X/MSI/INTx policy from route ownership.
- Split I/O APIC discovery/GSI routing and MMIO handling.
- Split SMP/APIC affinity resolution.
- Split route allocation/growth/zeroing.
- Added canonical component definitions for the broker core and three future provider seams.
- Provider seams remain `candidate` until direct broker calls/state ownership are removed.

No Get/Set/Event ABI changes are introduced.
