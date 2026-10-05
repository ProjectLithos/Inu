# Serial debugging and console transports

Inu 0.23.0 treats serial output as a kernel debugging facility rather than an application detail.

## 16550 UART

`KernelConsole` initializes the legacy PC-compatible COM1 16550 path before heap, PCI, driver, storage, or networking initialization. This preserves diagnostic output for failures very early in boot. The default is 115200 baud, 8 data bits, no parity, one stop bit, and FIFO enabled.

`KernelSerial.InitializeEarly16550()` exposes the same early facility explicitly. `KernelSerial.TryReadLegacyByte` provides a non-blocking receive primitive.

## PCI UART

After `KernelPci` is online, `KernelSerial` enumerates PCI base class `0x07`, subclass `0x00` serial controllers. Standard 8250 through 16950-compatible programming interfaces are accepted. The driver enables the appropriate PCI I/O or memory decode, finds a BAR of at least eight registers, and configures the UART as 115200 8N1. Vendor-specific programming interfaces are reported as discovered but are not guessed.

The capability snapshot separately reports PCI UARTs discovered and PCI UARTs successfully brought online.

## VirtIO console

The existing modern VirtIO PCI transport starts console devices with receive queue 0 and transmit queue 1. `KernelSerial` discovers those started consoles and attaches the first as a secondary debug mirror through `KernelVirtio.WriteConsole` while still reporting the total number of VirtIO consoles.

## Debug-output reliability

COM1 remains the primary serial target. PCI UART and VirtIO console output is best-effort: secondary transmit failures increment a diagnostic counter but do not make `KernelConsole.Write` fail. Framebuffer output therefore remains usable even if a later serial transport stalls or disappears.
