# Editable Inu kernel

This project requires **Inu SDK 0.25.0 or later**.

Edit `Kernel\Kernel.cs`. Do not move the file and do not add native imports to it. The line marked `USER CODE` is safe to change immediately. The generated bootstrap validates the final UEFI memory map and initializes the default no-heap physical-memory manager before entering the interactive console.

Build without starting QEMU:

```text
Build-Kernel.bat
```

Build and start QEMU:

```text
Run-Kernel.bat
```

The wrappers use `C:\Inu` by default. Set the `INU_SDK_ROOT` environment variable when the SDK is installed elsewhere.

The `Sdk` directory and `Kernel\Kernel.cs` are generated implementation/orchestration surfaces. A Inu SDK update refreshes them to the current authoritative runtime/configuration contract.


## Workspace projects in 0.35.15

The kernel workspace has three extension roots:

- `KernelProjects` - independently authored kernel drivers/libraries. Configuration decides which projects are active and linked into the kernel; folder placement alone has no architectural effect.
- `Userland` - independently compiled user-mode applications, services, drivers and libraries. `Build-WorkspaceProjects.ps1` builds every project below this directory before the kernel build.
- `Tests` - independently executable Inu test programs, also built by the workspace build script.

The Inu Visual Studio extension supplies independent **Add > New Project** templates for kernel drivers, kernel libraries, userland applications, userland services, userland drivers, userland libraries and tests. There is no fixed driver/application/project count.

Visual Studio 0.35.15 loads only authored workspace projects beneath `KernelProjects`, `Userland`, and `Tests` into solution folders. The copied `Sdk` dependency tree remains on disk and resolves through project references without flooding Solution Explorer.


Inu 0.25.0 initializes the physical-memory manager and then attaches the no-heap x64 virtual-memory manager to the active page-table hierarchy before the post-boot interactive console begins. The `System/Inu.Kernel.VirtualMemory` project is generated into the OS source tree from the SDK upstream; application/kernel code should consume its high-level API rather than raw CR3 or `INVLPG` imports.

## Address-space policy

Inu 0.25.0 initializes the standard x64 kernel address-space policy after physical and virtual memory, then prints its symbolic status and standard region bases with freestanding-safe console routines. `KernelAddressSpace` owns the heap, stack, direct-map, MMIO, and page-table reservations.

## Early allocator and kernel heap

After the address-space policy is active, generated 0.25.0 kernels initialize a bounded 64 KiB early allocator and then the page-backed first-fit kernel heap. Heap growth obtains physical frames from `KernelPhysicalMemory`, maps them through `KernelVirtualMemory`, and stays inside the standard `KernelHeap` virtual reservation. The generated sample allocates and releases one 256-byte block before entering the interactive console.

## ACPI and hardware discovery

Inu captures the firmware ACPI RSDP before `ExitBootServices` and exposes it only through the independently selectable `IAcpiRootPointerContext` capability. `KernelAcpi` validates RSDP/RSDT/XSDT; `KernelAcpiMadt`, `KernelAcpiMcfg` and `KernelAcpiHpet` expose platform topology; `KernelAcpiFadtProvider`, `KernelAcpiEcProvider` and `KernelAcpiPowerProvider` register selectable FADT, ECDT EC and fixed-feature power services through neutral contracts. S1/S3/S4 sleep transitions are intentionally deferred.

## Timers and clocks

Inu 0.25.0 initializes HPET, explicit TSC capability detection, invariant-TSC calibration/selection, the calibrated Local APIC timer, and stable RTC/CMOS calendar reads. Generated `Kernel.cs` uses only the high-level `KernelTime`, `KernelHpet`, `KernelTsc`, `KernelLocalApicTimer`, and `KernelRtcCmos` APIs; CPUID, RDTSC, CMOS port I/O, and timer MMIO remain SDK-owned.


## Scheduler and threads

Inu 0.25.0 initializes `KernelScheduler` after SMP. Use the high-level scheduler API for kernel-thread creation, priority, affinity, blocking/waking, yield and timer-tick scheduling decisions; user processes are provided by `Inu.Kernel.Processes`.

## User/kernel separation in 0.25.0

Generated kernels initialize `KernelProtection` after the scheduler. Ring-3 mappings are restricted to canonical lower-half user addresses, the hardware supervisor write-protect bit is enforced, SMEP is enabled when supported, and user transition contexts are validated without entering user mode before a syscall return path exists.

## Processes and executable loading

Generated kernels initialize `KernelProcesses` after the protected syscall boundary. The process loader accepts validated in-memory x64 ELF64 `ET_EXEC` and PE32+ images, creates private lower-half address spaces and guarded user stacks, and can enter ring 3 through the x64 user transition path.

## Driver framework

Inu 0.25.0 initializes `KernelDrivers` after the process facility. Drivers register allocation-free lifecycle callbacks and match rules; bus enumerators register devices and resources. The registry is heap-backed and grows dynamically by default, with explicit fixed-capacity mode available for deterministic kernels. Interrupt requests are routed through an opaque broker so ordinary drivers do not know whether the platform uses I/O APIC, MSI, or MSI-X.

## PCI / PCIe in 0.25.0

Generated kernels initialize `KernelPci` immediately after `KernelDrivers`. PCIe configuration uses ACPI MCFG ECAM when firmware supplies it; x64 systems without ECAM fall back to PCI configuration mechanism #1. The dedicated PCI assembly enumerates functions, discovers vendor/device/class information, sizes and maps BARs, walks conventional and extended capabilities, and discovers MSI/MSI-X without exposing raw configuration I/O in `Kernel.cs`.

## VirtIO in 0.25.0

Generated kernels initialize `KernelVirtio` after storage and networking. The modern PCI transport negotiates VirtIO 1.x features and split virtqueues and supplies block, network, console and RNG drivers. DMA queue/buffer memory is backed by the PMM and device BARs are mapped through the standard MMIO reservation.

## Serial debugging

Inu 0.25.0 keeps the legacy 16550-compatible COM1 UART active from the earliest kernel output, then initializes `KernelSerial` after PCI and VirtIO. Compatible PCI communications-class UARTs are discovered and configured at 115200 8N1, started VirtIO console devices are attached, and `KernelConsole` mirrors later debug output to the available secondary serial transports without allowing a secondary failure to suppress primary COM1/framebuffer diagnostics.

## Storage and filesystems

Inu 0.25.0 initializes `KernelStorage` after the heap-backed driver framework and installs the built-in FAT32 provider. Storage-capable drivers register block devices; MBR/GPT discovery exposes volumes; `KernelVfs` supplies namespaces, mount points and file handles. FAT32 starts read-only with 8.3 path traversal and cluster-chain reads, while custom filesystem providers may implement writes through the same VFS contract.

## Networking in 0.25.0

Generated kernels initialise `KernelNetworking` after storage. VirtIO-net is the first built-in NIC, followed by Intel E1000/E1000e and Realtek RTL8168/RTL8111-class PCI/PCIe adapters. NIC drivers register link-layer transmit/receive callbacks, while the kernel owns Ethernet, ARP, IPv4, ICMP, UDP and TCP socket foundations. Intel I219/I225 is intentionally left for the later dedicated driver family. Registries grow from the kernel heap by default; explicitly fixed network capacities remain available for deterministic kernels.


## Interactive framebuffer console

Inu 0.25.0 retains framebuffer console output after boot. With the QEMU SDL window focused, Up and Down move through scrollback one visual line at a time. Number keys `1`, `2`, and `3` select 8-, 16-, and 24-pixel rendered font sizes. Changing size reflows and redraws retained output, and returning to the bottom resumes the live view. The keyboard path accepts PS/2 scan-code sets 1 and 2.

## PS/2 input in 0.25.0

The generated kernel initializes the i8042 controller and services PS/2 keyboard and mouse input through the timer-dispatch input service. Full `English_UK` and `English_USA` translation tables are installed. Userland changes the active layout only through protected Get/Set service 32; hardware ports remain kernel-owned.
