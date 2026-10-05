# PCI, PCIe and VirtIO

Inu 0.23.0 introduces the first built-in modern hardware bus and virtual-device drivers.

## PCI initialization

Call `KernelPci.Initialize()` after `KernelDrivers.Initialize()` and after the physical/virtual-memory, address-space, heap and ACPI services are online. The standard bootstrap performs this automatically.

When ACPI MCFG entries exist, Inu enumerates their PCIe ECAM segment and bus ranges. Otherwise x64 falls back to conventional PCI configuration mechanism #1 through ports `0xCF8` and `0xCFC`.

Each discovered PCI function becomes a `KernelDeviceBus.Pci` device in `KernelDrivers`. The packed `KernelDeviceIdentifier.Location` preserves the complete 16-bit segment, 8-bit bus, 5-bit device and 3-bit function address.

## BARs and MMIO

On x64 segment 0, conventional configuration fields below `0x100` use PCI Configuration Mechanism #1 (CF8/CFC) even when MCFG is present. This avoids per-function ECAM page remapping during enumeration. PCIe extended configuration (`0x100`-`0xFFF`) and non-zero PCI segments continue to use ACPI MCFG ECAM.

`KernelPci.TryGetBar` performs the standard BAR sizing transaction while temporarily disabling I/O and memory decoding. It recognizes I/O BARs, 32-bit memory BARs and paired 64-bit memory BARs.

`KernelPci.TryMapBar` and `KernelPci.TryMapMmio` map physical device ranges into the standard kernel MMIO reservation. Mappings use `KernelVirtualMemoryProtection.Device` and are separate from the RAM direct map.

## PCI capabilities

Use `KernelPci.TryGetCapability` or `KernelPci.TryFindCapability` for the conventional capability chain. On ECAM devices, `KernelPci.TryGetExtendedCapability` walks the PCIe extended-capability chain.

`KernelPci.TryGetMsiCapability` and `KernelPci.TryGetMsixCapability` expose interrupt capability discovery without making ordinary drivers depend on PIC, I/O APIC, MSI or MSI-X delivery details.

## VirtIO

Call `KernelVirtio.Initialize()` after `KernelStorage.Initialize()` and `KernelNetworking.Initialize()`. The standard bootstrap does this automatically.

The transport requires modern VirtIO PCI capabilities and negotiates `VIRTIO_F_VERSION_1`. Queue metadata and bounce buffers come from `KernelPhysicalMemory`; CPU access uses `KernelAddressSpace.TryPhysicalToDirectMap`.

Built-in device support is:

- VirtIO block (`VirtioDeviceType.Block`)
- VirtIO network (`VirtioDeviceType.Network`)
- VirtIO console (`VirtioDeviceType.Console`)
- VirtIO RNG (`VirtioDeviceType.EntropySource`)

Block devices register with `KernelStorage`; network devices register with `KernelNetworking`. Console and RNG expose direct high-level APIs on `KernelVirtio`.

## Polling and interrupts

The current VirtIO transport can operate by polling and exposes `KernelVirtio.Poll`. The generic driver interrupt callback also routes to the same receive-side poll path, so MSI/MSI-X routing can be connected through the existing opaque interrupt broker without changing the VirtIO device-driver API.
