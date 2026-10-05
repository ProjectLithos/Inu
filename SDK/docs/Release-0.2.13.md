# Inu OS SDK 0.2.13

- Removed direct `KernelRouteManager` and `KernelNeighborCache` references from the `KernelNetworking` core.
- Added `KernelNetworkExtensionServices`, a freestanding function-pointer registry for optional networking services.
- Added `KernelNetworkInterfaceRegistryContract` so independently owned networking components validate interfaces through an explicit contract.
- Promoted route management and neighbour caching from `candidate` to `selectable`.
- Added default component symbols `INU_COMPONENT_NETWORK_ROUTE_MANAGER` and `INU_COMPONENT_NETWORK_NEIGHBOR_CACHE`; Kath/generated source may omit either symbol and its source component.
- Preserved existing `KernelNetworking` route/neighbour APIs as compatibility facades.
- Preserved the existing default kernel behaviour by selecting both components in the standard configuration.
