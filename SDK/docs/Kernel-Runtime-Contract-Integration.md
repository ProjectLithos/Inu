# Kernel Runtime Contract Integration

Inu's formal subsystem contracts are now part of kernel boot policy.

`KernelSubsystemRuntime` resolves the 13 public subsystem IDs to the live kernel implementations and produces `KernelSubsystemStatus` snapshots using subsystem contract version 1.0. The kernel does not enter the interactive runtime unless every required subsystem is initialized and contract-compatible.

The driver framework follows the same principle. `KernelDriverCapabilityDeclaration` states the maximum authority a driver may request. During binding, the kernel derives concrete grants from the device resources discovered by PCI and records opaque `KernelDriverCapabilityGrant` tokens. The declaration alone is never considered permission.

Current PCI policy derives grants for MMIO, I/O ports, interrupt delivery, MSI/MSI-X, DMA, PCI configuration, physical-memory ranges and global timer/network/filesystem services where declared and permitted.
