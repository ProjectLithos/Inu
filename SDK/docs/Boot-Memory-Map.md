# Boot memory map

Inu captures and retains the UEFI memory map whose key is accepted by `ExitBootServices`. Earlier copies are diagnostic-only and must never be supplied to the physical allocator because any boot-services allocation can change the map and its key.

## Assemblies

- `Inu.Memory.Contracts` contains platform-independent descriptors, ownership and availability types, reservations, immutable diagnostics, result types, and bounded workspaces.
- `Inu.Boot.Memory` contains UEFI descriptor translation, final-map acquisition, the checked `NativeUefiMemoryMapSource` adapter over the retained native buffer, reservation planning, and the normaliser implementations.

## Final UEFI sequence

The x64 native entry reserves a fixed map buffer before the final firmware call. It then performs:

```text
GetMemoryMap(final preallocated buffer)
-> ExitBootServices(image handle, returned map key)
-> retry GetMemoryMap only when ExitBootServices reports a stale key
-> seal the accepted map and expose it to managed bootstrap
```

No allocation and no other firmware call occurs between a successful `GetMemoryMap` and the corresponding `ExitBootServices` call.

## Normalisation versions

### Strict

Use `StrictMemoryMapNormaliser` when firmware is trusted and an incompatible overlap should stop boot. Explicit Inu reservations are still overlaid and split correctly.

### Safety priority

Use `SafetyPriorityMemoryMapNormaliser` for the default real-hardware policy. When firmware ranges overlap, bad memory, MMIO, framebuffer, runtime services, firmware reservations, ACPI NVS, page tables, boot structures, the kernel image, and early allocations take priority over reclaimable or usable memory.

### Conservative

Use `ConservativeMemoryMapNormaliser` when uncertain ownership must never become allocatable. Every incompatible firmware overlap becomes `FirmwareReserved`. Runtime conflicts remain `RuntimeOwned`, mixed runtime code/data is retained explicitly, protection flags are combined, and contradictory cache modes are reduced to the safest single mode.

## Required reservations

Before normalisation, create a `MemoryReservationPlan` and add the page-rounded ranges for:

```csharp
MemoryReservationPlan reservations = new(16);
reservations.TryAddKernelImage(kernelStart, kernelPages);
reservations.TryAddBootStructures(bootContextStart, bootContextPages);
reservations.TryAddFramebuffer(framebufferStart, framebufferPages, MemoryCacheAttributes.WriteCombining);
reservations.TryAddMemoryMappedIo(apicStart, apicPages);
reservations.TryAddPageTables(pageTableStart, pageTablePages);
reservations.TryAddEarlyAllocation(earlyStart, earlyPages);
```

Call `TryValidateRequiredReservations` before normalisation. Kernel-image and boot-structure reservations are mandatory; framebuffer and MMIO requirements are selected from the active platform. Page-table and early-allocation ranges must be added whenever they exist.

Normalisation sorts all source boundaries, rejects zero-length, unaligned, and overflowing ranges, splits overlaps, applies the reservations, and merges compatible neighbours. When an explicit reservation covers firmware runtime memory, the reservation type is retained while runtime ownership, runtime code/data status, protection flags, and the safest cache mode remain preserved. `NormalisedMemoryMap` never exposes its backing array; diagnostics use `TryGetDescriptor` or `MemoryMapDiagnosticCursor`.

## Physical allocator hand-off (0.1.0)

After normalisation, `NormalisedMemoryMap` is the authoritative input to `Inu.Memory.Physical`. The built-in bitmap, buddy, and extent managers admit only descriptors whose `Availability` is `AvailableAfterExitBootServices`; runtime, firmware, MMIO, framebuffer, ACPI-reclaimable, bad, and other non-immediate ranges remain outside ordinary allocation.

Physical allocator metadata must itself be placed in memory reserved before allocator initialisation. The later kernel address-space and VMM layers consume physical allocations rather than returning to the firmware map for ownership decisions.

## Early Boot Services reclaim boundary

After `ExitBootServices`, UEFI marks `BootServicesCode` and `BootServicesData` as reclaimable by the operating system. Inu deliberately does **not** put those ranges into the early PMM free list yet. The kernel can still be executing with firmware-provided bootstrap state, especially the inherited x64 stack, inside Boot Services data pages. Reusing such a page as a page-table frame could overwrite the live stack.

The early PMM therefore allocates only `ConventionalMemory`. Boot Services ranges remain deferred-reclaim memory until Inu installs a kernel-owned stack and a later explicit reclaim operation can prove those firmware pages are no longer live. The VMM direct-map bootstrap continues to calculate its storage from the final memory-map-derived PMM ranges; it simply operates on the safe ConventionalMemory subset during this phase.
