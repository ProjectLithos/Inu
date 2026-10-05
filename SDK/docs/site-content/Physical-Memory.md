# Physical Memory

Inu provides one contract and three selectable physical-frame allocator methodologies.

## Common contract

Higher-level SDK facilities can depend on `IPhysicalMemoryManager`. The built-in implementations are `BitmapPhysicalMemoryManager`, `BuddyPhysicalMemoryManager`, and `ExtentPhysicalMemoryManager`; a project-specific implementation reports `PhysicalAllocatorMethod.Custom`.

Every manager consumes a `NormalisedMemoryMap` and caller-owned `PhysicalAllocatorWorkspace`. Built-in allocator metadata therefore does not depend on the future kernel heap.

The built-in managers are structs. Pre-heap code should use the concrete struct directly or a constrained generic `ref` path; converting a struct manager to an interface variable can box it in ordinary managed code.

## Allocation constraints

`PhysicalAllocationRequest` describes contiguous page count, power-of-two page alignment, a minimum physical address, an optional exclusive maximum address, and ownership purpose. This covers ordinary page frames as well as constraints such as memory below 4 GiB.

## Ownership safety

Successful allocations and reservations return opaque tokens. The exact handle must be supplied on release; stale and double releases fail deterministically. Allocation and reservation record capacities are bounded at initialisation.

## Method selection

Use bitmap when simple per-frame state is preferred and the address space is reasonably dense. Use buddy for power-of-two blocks, natural alignment, split/coalesce behaviour, and visible internal fragmentation. Use extent for sparse maps, exact page counts, and metadata that scales with free-range fragmentation.

The complete algorithms, formulas, examples, and custom implementation guidance are maintained in `docs/Physical-Memory-Management.md` in the source tree.
