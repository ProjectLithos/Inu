# Inu 0.2.20

This release promotes the decomposed interrupt-delivery seams into real selectable source components.

- Added parent-owned service contracts for I/O APIC GSI routing, PCI interrupt delivery, and interrupt affinity.
- Moved I/O APIC map state into `KernelIoApicRouter`.
- Moved PCI MSI-X/MSI/INTx delivery policy into `KernelPciMessageSignaledRouter`.
- Moved SMP-aware target/APIC resolution into `KernelInterruptAffinityResolver`.
- Removed concrete provider references from `KernelInterruptBroker` route lifecycle.
- Added Kath/default selection symbols for all three providers.
- Promoted all three component definitions from `candidate` to `selectable`.

No Get/Set/Event ABI changes are introduced.
