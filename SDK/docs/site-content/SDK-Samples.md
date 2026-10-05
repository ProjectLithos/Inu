# SDK documentation and samples

Inu OS SDK 0.42.0 ships maintained sample projects under `SDK/samples`. They cover a hello kernel, interrupt handler, PCI driver, USB driver, VirtIO driver, filesystem provider, network client, isolated userland process, custom syscall and periodic service.

Samples are intentionally small and use public contracts only. Interrupt and timer callbacks avoid allocation. Driver examples register through `KernelDrivers`; filesystem code sits under `KernelVfs`; networking uses `KernelNetworkApi`; process creation goes through the VFS-backed isolated loader.

## Build

Run `SDK\samples\Build-Samples.bat`. It uses the bundled .NET toolchain when present and stops on the first failed sample.
