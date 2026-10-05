# Driver framework

Inu roadmap item 19 provides a heap-backed freestanding driver framework in `Inu.Kernel.Drivers`. The framework separates device drivers from bus-controller and interrupt-controller implementation details while preserving explicit kernel behaviour.

## Device and driver model

A discovered device is represented by `KernelDeviceIdentifier`, including its bus, vendor/device IDs, subsystem IDs, class code, revision, and bus-defined location. A driver registers a `KernelDriverMatchRule`; each field can be enabled independently, while the class code supports a bit mask for family drivers such as storage or network classes.

Drivers provide allocation-free function-pointer callbacks through `KernelDriverCallbacks`: probe, start, stop, remove, and optional interrupt dispatch. The registry metadata itself is allocated from the already-initialized Inu kernel heap. The framework owns the lifecycle transition from registered device through probe, bind, start, stop, and removal. A device can be bound to only one driver at a time.

## Dynamic registry storage

`KernelDrivers.Initialize()` uses `KernelDriverFrameworkOptions.DynamicDefault`. It starts with capacity for 64 drivers and 128 devices, but these numbers are initial heap allocations rather than architectural limits. When a table becomes full, Inu allocates a larger table from `KernelHeap`, copies the live records without changing slot-based handles, releases the old table, and continues. Capacity therefore grows with available kernel memory.

Driver-facing identifiers, handles, resource descriptions, match rules, options, interrupt requests, and capability snapshots are ordinary immutable `readonly struct` values with explicit constructors. They deliberately avoid C# record-generated runtime helpers so the public API remains compatible with the minimal freestanding CoreLib used by ILC.

For deterministic RTOS, appliance, or safety-oriented kernels, the SDK user can instead call `KernelDrivers.Initialize(KernelDriverFrameworkOptions.Fixed(drivers, devices))`. Fixed mode allocates exactly the requested registry capacities during initialization and never grows them. A dynamic configuration can also supply explicit maximum capacities when a deployment wants a ceiling without using fixed mode.

`KernelDriverCapabilities` reports registry mode, current capacities, configured maximums, and current use.

## Resources

Each device currently exposes up to eight compact resources. `KernelDeviceResource` covers MMIO ranges, I/O ports, interrupt sources, DMA resources, and bus-specific data. Resources are attached before binding and can be queried by the bound driver without requiring the driver to know how discovery was performed. This per-device resource representation is separate from the dynamically growing driver/device registry and can evolve independently.

## Interrupt isolation

Drivers never select PIC, I/O APIC, MSI, or MSI-X directly. A driver creates a `KernelDriverInterruptRequest` describing the logical source, priority, target processor, electrical trigger/polarity requirements, and an opaque driver cookie. `KernelDrivers.InstallInterruptBroker` connects that request to the platform interrupt stack. The returned `KernelDriverInterruptHandle` is opaque to the driver.

This keeps device code independent from the machine's selected interrupt-delivery mechanism and preserves the existing Inu interrupt-controller abstraction.

## Example matching methodology

A hardware-specific PCI driver can enable bus, vendor, and device matching. A family driver can enable the PCI bus and use a class-code mask. A platform driver can match only its bus and rely on the bus-defined location supplied during discovery.

Storage/filesystem work in roadmap item 20 can consume this framework through block-device drivers rather than embedding hardware knowledge in the filesystem layer.
