# Inu physical memory management

Inu 0.1.0 introduces the physical-frame allocator layer that consumes the normalised boot memory map and becomes the ownership authority for immediately allocatable RAM.

## Position in the boot sequence

The intended dependency order is:

```text
final firmware memory map
    -> normalised physical-memory map
    -> physical memory manager
    -> kernel address-space design
    -> virtual memory manager
    -> early allocator / kernel heap
```

The physical allocator does not depend on virtual-memory management or the kernel heap. Each built-in implementation is a value type and receives a `PhysicalAllocatorWorkspace` describing caller-owned writable metadata storage. The allocator implementations do not allocate their own backing arrays, use `ArrayPool`, or require garbage-collected metadata after initialisation.

The caller must reserve that metadata storage before exposing the corresponding physical pages to the allocator. A boot-reserved/static region is preferred for real freestanding use. Hosted tests may use unmanaged host memory to exercise the same contract.

The built-in managers are structs. During pre-heap freestanding boot, keep the selected manager as its concrete struct type or pass it through a constrained generic `ref` path. Converting a struct manager to an `IPhysicalMemoryManager` variable in ordinary managed code can box the struct; the interface exists as the compatibility contract, but pre-heap code must avoid boxing just as it avoids any other managed allocation.

## Assemblies

### `Inu.Memory.Physical.Contracts`

Provides the architecture-independent SDK contract:

- `IPhysicalMemoryManager`
- `PhysicalAllocatorMethod`
- `PhysicalMemoryPurpose`
- `PhysicalMemoryStatus`
- `PhysicalFrameRange`
- `PhysicalAllocationRequest`
- `PhysicalAllocation`
- `PhysicalReservation`
- `PhysicalMemoryStatistics`
- `PhysicalAllocatorWorkspace`

A custom allocator can implement `IPhysicalMemoryManager` and report `PhysicalAllocatorMethod.Custom` without changing any higher-level Inu subsystem.

### `Inu.Memory.Physical`

Provides three selectable implementations:

- `BitmapPhysicalMemoryManager`
- `BuddyPhysicalMemoryManager`
- `ExtentPhysicalMemoryManager`

All three use 4 KiB physical frames and consume only descriptors whose normalised availability is `AvailableAfterExitBootServices`.

## Common allocation contract

Create a request before allocating:

```csharp
bool valid = PhysicalAllocationRequest.TryCreate(
    pageCount: 8,
    alignmentPages: 8,
    minimumAddress: default,
    maximumAddressExclusive: new PhysicalAddress(0x1_0000_0000),
    purpose: PhysicalMemoryPurpose.Dma,
    out PhysicalAllocationRequest request);
```

`alignmentPages` must be a power of two. A zero `maximumAddressExclusive` means no explicit upper physical-address limit. A non-zero maximum is exclusive, allowing DMA32-style constraints to be represented without a special allocator API.

The fundamental bounds are:

```text
start >= minimum
start mod (alignmentPages * 4096) = 0
start + allocatedPages * 4096 <= maximumExclusive    when maximumExclusive != 0
```

Expected allocation exhaustion is reported through `bool` plus `PhysicalMemoryStatus`; it does not require an exception. Invalid construction of ordinary .NET objects may still use normal .NET exceptions where appropriate, but the physical allocation fast path remains status based.

## Opaque ownership tokens

Every successful allocation and reservation carries an opaque non-zero token. Release requires the token and exact range returned by the same manager. Once released, the record is invalidated.

This provides deterministic detection of:

- double free;
- stale allocation handles;
- stale reservation handles;
- an allocation or reservation from a different range/manager state.

The manager's allocation and reservation tables have explicit capacities supplied during `TryInitialize`. Exhausting those tables returns `PhysicalMemoryStatus.RecordCapacityExhausted` rather than allocating more metadata from a heap.

## Bitmap methodology

The bitmap allocator uses one ownership bit for every physical frame between the lowest and highest immediately allocatable frame:

```text
bitmapBytes = ceil(frameSpan / 8)
```

All bits begin in the unavailable state. Only ranges marked `AvailableAfterExitBootServices` by the normalised map are cleared to free. Holes, MMIO, firmware reservations and other non-allocatable ranges therefore remain unavailable even when they lie inside the bitmap's address span.

Allocation scans for a contiguous clear-bit run satisfying page count, alignment and address bounds. Exact reservations set the same bits but use a reservation record rather than an allocation record.

### Strengths

- simple ownership test;
- exact requested page count;
- predictable one-bit-per-address-span cost;
- straightforward exact fixed-range reservation.

### Trade-off

A sparse physical address space can make the bitmap much larger than the amount of usable RAM because holes still consume bitmap positions.

## Buddy methodology

The buddy allocator decomposes usable RAM into aligned power-of-two blocks. For a request of `N` pages with alignment `A` pages:

```text
requestOrder   = ceil(log2(N))
alignmentOrder = ceil(log2(A))
targetOrder    = max(requestOrder, alignmentOrder)
allocatedPages = 2 ^ targetOrder
```

A larger free block is recursively split until the selected target block is reached. On release, its buddy address is:

```text
buddyFrame = blockStartFrame XOR blockPages
```

Two free buddies of the same order coalesce into the next order. This repeats recursively.

`PhysicalAllocation.RequestedPageCount` retains the caller's original request while `PhysicalAllocation.Range.PageCount` reports the actual rounded buddy block. Higher-level code can therefore measure internal fragmentation instead of silently assuming an exact allocation.

Arbitrary fixed reservations are still exact: the implementation splits intersecting free blocks down to individual frames for the requested reserved range, then coalesces again when the reservation is released.

### Strengths

- fast deterministic split/coalesce model;
- naturally aligned power-of-two blocks;
- useful for page tables, large pages and kernel arenas.

### Trade-off

Non-power-of-two allocations have internal fragmentation because the owned block is rounded upward.

## Extent methodology

The extent allocator keeps a sorted table of free `(startFrame, pageCount)` ranges. Allocation selects a matching range, removes the requested interval, and retains zero, one or two resulting free extents.

For a candidate free extent `[Fstart, Fend)` the first aligned candidate is:

```text
candidate = align_up(max(Fstart, minimumFrame), alignmentPages)
```

The candidate is valid when:

```text
candidate + requestedPages <= min(Fend, maximumFrameExclusive)
```

Release inserts the exact range back into sorted order and merges adjacent extents.

### Strengths

- metadata scales with fragmentation rather than address-space span;
- exact page counts;
- efficient for sparse maps and large contiguous runs;
- natural exact fixed-range reservations.

### Trade-off

Allocation is a range scan rather than a constant-time bit operation, and fragmentation increases the number of live extents.

## Selecting an implementation

The SDK does not force one physical allocator on an operating-system author.

```csharp
ExtentPhysicalMemoryManager allocator = default;

if (!ExtentPhysicalMemoryManager.TryGetRequiredWorkspaceBytes(
        map, allocationCapacity: 256, reservationCapacity: 64, out ulong bytes))
    return false;

if (!PhysicalAllocatorWorkspace.TryCreate(metadataAddress, bytes, out PhysicalAllocatorWorkspace workspace))
    return false;

if (!allocator.TryInitialize(map, workspace, 256, 64, out PhysicalMemoryStatus status))
    return false;
```

The same higher-level request/release contract applies to bitmap and buddy implementations.

## Custom implementation

An SDK consumer may implement a completely different methodology:

```csharp
public sealed class ProjectPhysicalMemoryManager : IPhysicalMemoryManager
{
    public PhysicalAllocatorMethod Method => PhysicalAllocatorMethod.Custom;
    public bool IsInitialized => false;
    public ulong PageSize => 4096;

    // Implement TryInitialize, TryAllocate, TryRelease,
    // TryReserve, TryReleaseReservation and GetStatistics.
}
```

Examples of valid custom strategies include segregated free lists, zone-based allocation, NUMA-local frame allocators, per-node buddies, coloured-page allocation, or a hardware-specific DMA pool. Higher-level Inu code should depend on `IPhysicalMemoryManager`, not on the implementation's internal metadata layout.

## Reservations after initialisation

`TryReserve` removes an exact currently-free range from ordinary allocation and records its purpose. This is intended for discoveries that occur after the physical allocator is live, such as later hardware metadata or a newly established allocator-owned structure.

It deliberately cannot reserve pages that are already allocated or reserved. The operation is all-or-nothing; buddy reservation rolls back already split pages if any page in the requested range is not free.

## Statistics

`GetStatistics` reports:

- total managed pages;
- free pages;
- allocated pages;
- reserved pages;
- largest currently free run/block;
- active allocation records;
- active reservation records.

For bitmap and extent methods, an allocation owns exactly the requested pages. For buddy, `AllocatedPages` includes rounded pages because those frames are genuinely unavailable to other consumers.

## Deliberate 0.1.0 boundaries

0.1.0 does **not** implement virtual mappings, kernel virtual-address layout, a heap, ACPI reclaim, NUMA placement, per-CPU frame caches, or DMA/IOMMU translation. Those belong to later layers in the roadmap.

ACPI-reclaimable memory is not made immediately allocatable by this release. It remains outside the manager until the later ACPI/hardware-discovery lifecycle can prove that the relevant tables have been consumed safely.
