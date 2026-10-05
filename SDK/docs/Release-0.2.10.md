# Inu OS SDK 0.2.10

This release continues behaviour-preserving decomposition of large HAL and kernel service implementations.

## Decomposed in this release

- Intel E1000/E1000e Ethernet driver.
- Realtek RTL8168/RTL8111 Ethernet driver.
- Kernel networking registry, routing, neighbours, frame I/O, and storage helpers.
- Kernel storage device registry/I/O, partitions/volumes, and registry storage.
- USB bus host registration, enumeration, transfers, device/interface management, and storage helpers.
- PS/2 lifecycle, service/configuration, keyboard decoding, mouse and i8042 I/O.
- AHCI lifecycle, disk discovery, block I/O, port control, and storage/MMIO helpers.

No Get/Set/Event ABI, filesystem policy, language policy, driver contract, or userland isolation policy is intentionally changed. These files are smaller implementation responsibilities; only slices with explicit independent contracts and lifecycle will later become separate Kath-selectable components.
