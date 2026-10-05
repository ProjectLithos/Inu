# Inu OS SDK 0.2.12

This release begins converting decomposed source slices into components with independent state ownership.

## Network component ownership

- Added the explicit required `Kernel.Networking.InterfaceRegistry` component definition.
- Added `KernelRouteManager`, which now owns route records, allocation/growth, counters, route resolution, and interface cleanup.
- Added `KernelNeighborCache`, which now owns IPv4/IPv6 neighbour records, allocation/growth, counters, lookup/update operations, and interface cleanup.
- Kept the existing `KernelNetworking` route and neighbour APIs as compatibility facades.
- Removed route and neighbour private storage from the `KernelNetworking` monolith.

Route management and neighbour caching remain `candidate` rather than `selectable` in this release because network startup still binds them directly. This is deliberate: Kath must not present a component as independently selectable until startup/dependency resolution can omit or substitute it without patching parent source.

No syscall ABI, filesystem policy, firmware/kernel separation, process-isolation policy, or public networking API is intentionally changed.
