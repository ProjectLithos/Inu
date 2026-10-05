# Formal kernel subsystem contracts

Inu defines one versioned public boundary for each major kernel subsystem. The first contract generation is `1.0` and is published by `Inu.Kernel.SubsystemContracts`.

Kernel and driver code should depend on these contracts when crossing subsystem boundaries. Hardware-specific or implementation-specific entry points remain internal to their owning subsystem. This prevents consumers from binding themselves to allocator internals, APIC details, scheduler tables, filesystem implementations, NIC drivers, graphics transports, input buses, ACPI machinery, or SMP implementation details.

| Subsystem | Public contract | Current implementation |
|---|---|---|
| Memory | `IKernelMemoryContract` | `Inu.Kernel.Memory` |
| Interrupts | `IKernelInterruptContract` | `Inu.Kernel.InterruptBroker` |
| Scheduler | `IKernelSchedulerContract` | `Inu.Kernel.Scheduler` |
| Processes | `IKernelProcessContract` | `Inu.Kernel.Processes` |
| Syscalls | `IKernelSyscallContract` | `Inu.Kernel.SystemCalls` |
| Drivers | `IKernelDriverContract` | `Inu.Kernel.Drivers` |
| Filesystem | `IKernelFilesystemContract` | `Inu.Kernel.Storage` |
| Networking | `IKernelNetworkingContract` | `Inu.Kernel.Networking` |
| Graphics | `IKernelGraphicsContract` | `Inu.Kernel.Graphics` |
| Input | `IKernelInputContract` | `Inu.Kernel.Ps2` |
| Time | `IKernelTimeContract` | `Inu.Kernel.Time` |
| Power | `IKernelPowerContract` | `Inu.Kernel.Acpi` |
| SMP | `IKernelSmpContract` | `Inu.Kernel.Smp` |

Every implementation reports `KernelSubsystemStatus` including the subsystem ID, lifecycle state, contract major/minor version and capability bits. Consumers use `IsCompatible` to require an exact major version and a minimum minor version. Breaking contract changes therefore require a new contract major version; additive changes can advance the minor version.

The machine-readable mapping is `Inu.SubsystemContracts.json` and the SDK manifest advertises the subsystem-contract version independently of the SDK release/API/ABI versions.
