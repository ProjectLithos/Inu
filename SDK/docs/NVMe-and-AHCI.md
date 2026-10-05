# NVMe and AHCI / SATA

Inu 0.23.0 adds native modern and compatibility storage transports on top of the existing PCI and storage layers.

## NVMe

`Inu.Kernel.Nvme` discovers PCI class `01/08/02` controllers, maps BAR0, enables PCI memory/bus mastering, performs the NVMe disable/enable reset sequence, creates admin submission/completion queues, identifies the controller, creates I/O queue pair 1, discovers active namespaces, and registers each namespace as an independent `KernelStorage` physical block device. Reads and writes use synchronous NVMe NVM commands with PMM-backed DMA bounce pages; flush uses the NVM Flush command.

The driver exposes `TryEnableMsi` and `TryEnableMsix` so the architecture interrupt/vector allocator can program the selected message address/data without making the NVMe driver depend on PIC, IOAPIC, MSI, or MSI-X internals. Until a vector is supplied, queue completion uses polling.

## AHCI / SATA

`Inu.Kernel.Ahci` discovers PCI class `01/06/01` AHCI controllers, maps ABAR (BAR5), enables AHCI and PCI bus mastering, scans implemented ports, validates SATA link state/signature, allocates physical command-list/FIS/command-table memory, issues ATA IDENTIFY DEVICE, and registers each SATA disk independently with `KernelStorage`.

Read/write uses READ DMA EXT / WRITE DMA EXT with PRDT-backed DMA buffers. Flush uses FLUSH CACHE EXT when LBA48 is available and FLUSH CACHE otherwise. IDENTIFY data determines LBA48 capacity and logical-sector size.

The standard generated kernel initializes storage in the order NVMe, AHCI/SATA, then VirtIO so native NVMe is the primary modern storage path while retaining SATA and virtual-device support.
