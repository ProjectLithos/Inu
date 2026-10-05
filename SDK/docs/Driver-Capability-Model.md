# Capability-based driver model

Inu drivers declare the maximum privileges they need. Declaration is not authorization: the kernel grants each privilege explicitly for a bound device and returns an opaque `KernelDriverCapabilityGrant`.

## Capabilities

- MMIO ranges
- port I/O ranges
- IRQ
- MSI
- MSI-X
- DMA
- PCI configuration
- physical-memory ranges
- timers
- networking
- filesystem access

`KernelDriverCapabilityDeclaration` is registered with the driver. `KernelDriverCapabilityRequest` asks for one concrete privilege. MMIO, port-I/O and physical-memory requests must name a bounded range. The request is rejected if the privilege was not declared or if the range is outside the resources assigned to the device. PCI configuration grants are restricted to PCI devices. IRQ/MSI/MSI-X and DMA grants require corresponding device resources.

Kernel-issued grants are binding-specific and revocable. `ValidateCapabilityGrant` now has an operation-scoped overload that verifies the capability kind, the requested range, and whether the issued access mode covers the requested read/write operation. `TryGetCapabilityGrant` also has an operation-scoped overload so a driver can obtain the exact live grant needed for one access rather than accepting an arbitrary grant of the same capability.

Grants are lifecycle-bound authority. A failed start revokes every grant for the binding. Stopping a device revokes every grant, and a later start must reacquire the declaration from current device resources before the driver callback executes. Removing the device destroys all grants with the device record. This prevents stale tokens from retaining authority across stop/failure/restart transitions.

For in-kernel drivers, Inu cannot obtain hardware-enforced isolation merely from a C# token because the driver shares kernel privilege. The SDK therefore treats direct privileged calls from generated `DriverProjects` as a build-time policy violation unless the source obtains or validates a live `KernelDriverCapabilityGrant`; the kernel still validates the token/range/access at the capability boundary. Fully hardware-isolated drivers can later place the same contract across a process/service boundary without changing the manifest vocabulary.

The generated `Inu.Driver.json` schema is version 2 and records the driver's declared maximum privilege set. The IDE static analyzer checks obvious privileged API use against that declaration before boot.
