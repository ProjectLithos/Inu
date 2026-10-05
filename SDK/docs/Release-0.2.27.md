# Inu 0.2.27

## Selectable PCI storage and network drivers

- NVMe is now explicitly represented as selectable `Kernel.Storage.NvmeDriver`.
- AHCI/SATA is now explicitly represented as selectable `Kernel.Storage.AhciDriver`.
- Intel E1000/E1000e is now selectable as `Kernel.Networking.E1000Driver`.
- Realtek RTL8168/RTL8111 is now selectable as `Kernel.Networking.Rtl8168Driver`.
- Bootstrap and generated HAL startup reference these concrete drivers only when their component selection symbols are present.
- The networking service loop also omits concrete NIC service calls when the corresponding driver component is not selected.
- Standard Storage and Networking configurations continue to select the existing drivers by default, preserving current behaviour while allowing smaller target-specific kernels.

## Component model

These drivers already owned their device-specific state and registered devices/interfaces through the driver, storage and networking contracts. This release makes that existing separation visible to Kath as canonical language-neutral selectable components instead of leaving driver inclusion hard-wired in bootstrap code.
