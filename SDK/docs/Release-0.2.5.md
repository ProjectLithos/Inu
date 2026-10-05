# Inu 0.2.5

Inu 0.2.5 begins the source-component decomposition of large kernel implementation units without changing their public contracts or runtime behaviour.

## Decomposed in this release

- `Inu.Kernel.Drivers.KernelDrivers` is split into registry, lifecycle, inspection, capabilities, interrupts/events, and backing-storage source units.
- `Inu.Kernel.Storage.KernelVfs` is split into provider/namespace state, mounts, file I/O, directory I/O, permissions, and internal storage/routing helpers.
- `Inu.Kernel.Scheduler.KernelScheduler` is split into lifecycle/state, threads, dispatch, AP execution, role workers, placement, accounting/GC coordination, and low-level storage helpers.
- The stable `Scheduler` facade now has its own source file.
- `docs/Component-Decomposition.md` records the decomposition rules so later splitting remains consistent with Inu's source-component architecture.

The split is intentionally ABI- and behaviour-preserving. These source units are not automatically separate end-user policy choices; a unit becomes independently selectable only when it has a meaningful contract, dependency set, and lifecycle.
