# Unified Device Model

Inu exposes one authoritative hierarchical device model through `Inu.Kernel.Drivers`. Drivers do not own private raw-device lists as their primary system identity. Bus enumerators discover nodes into the common tree and subsystem drivers attach logical or virtual children to those nodes.

The canonical classes are `Pci`, `Usb`, `Acpi`, `Platform`, `Virtual`, and `Logical`. `Virtio` is retained as a compatibility alias of `Virtual`; `Synthetic` is retained as an alias of `Logical`.

Every `KernelDeviceNode` exposes its handle, parent, first child, next sibling, identifier, lifecycle state, bound driver, and failure state. Tooling can enumerate roots with `TryGetRootDevice`, all nodes with `TryGetDeviceNodeByIndex`, and aggregate counts/generation with `GetDeviceTreeSnapshot`.

Bus-specific code remains responsible for discovery details, but not for defining a parallel device identity model. PCI functions enter as PCI nodes. USB devices and interfaces enter as USB nodes with real parent relationships. ACPI/platform enumerators should publish firmware/platform objects in the same way. VirtIO/QEMU-backed devices use virtual nodes. Filesystems, namespaces, partitions, protocol endpoints, and other software-visible derivations use logical nodes.

The Kath Hardware Tree uses the matching `InuDeviceTreeNode`/`InuDeviceTreeSnapshot` protocol contract, so the UI and kernel agree on classes, hierarchy, lifecycle state, identifiers, and bound-driver metadata.
