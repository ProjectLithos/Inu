# Inu 0.2.21

This release separates PCI configuration-space access from the PCI discovery core and promotes the concrete configuration transports into selectable source components.

- Added `KernelPciConfigurationServices` as the parent-owned PCI configuration transport registry.
- Added selectable x64 legacy CF8/CFC configuration-space transport.
- Added selectable PCI Express ECAM transport with provider-owned ACPI MCFG lookup and transient MMIO mapping state.
- Removed direct CF8/CFC and ECAM read/write implementation logic from `KernelPci`.
- PCI discovery now operates with legacy configuration, ECAM configuration, or both according to selected components.
- Default Drivers configurations continue to select both transports to preserve current x64 behaviour.
- Added canonical language-neutral component definitions for the registry and both transports.

No Get/Set/Event ABI changes are introduced.
