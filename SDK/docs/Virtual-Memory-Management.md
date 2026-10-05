# Inu Virtual Memory Management

Inu 0.2.0 introduces architecture-neutral virtual-memory contracts, x64 four-level page-table mechanics, and a freestanding bootstrap virtual-memory manager.

## Scope

The virtual-memory layer is responsible for translating virtual addresses to physical pages, installing and removing mappings, changing protection, decoding translations, allocating intermediate page-table pages, and invalidating stale processor translations.

0.2.0 deliberately does **not** impose the final kernel virtual-address layout. Kernel image placement, direct physical mapping, MMIO windows, heap placement, stack regions, guard pages, and the user/kernel split belong to the separate kernel address-space design stage.

## Architecture-neutral contracts

`Inu.Memory.Virtual.Contracts` defines:

- `IVirtualMemoryManager`
- `VirtualMappingRequest`
- `VirtualAddressRange`
- `VirtualTranslation`
- `VirtualMemoryStatistics`
- `VirtualMemoryProtection`
- `VirtualPageSize`
- `VirtualMemoryStatus`
- `VirtualMemoryMethod`

SDK consumers can implement `IVirtualMemoryManager` themselves instead of using the supplied x64 methodology.

## x64 methodology

`Inu.Memory.Virtual.X64` supplies checked helpers for the four-level long-mode hierarchy:

- canonical-address validation;
- PML4, PDPT, page-directory, and page-table index extraction;
- 4 KiB leaf encoding and decoding;
- 2 MiB large-page encoding and decoding;
- 1 GiB large-page encoding and decoding;
- non-leaf table-pointer encoding;
- present and large-page inspection.

For a canonical virtual address `v`, the four table indices are:

```text
PML4 = (v >> 39) & 0x1FF
PDPT = (v >> 30) & 0x1FF
PD   = (v >> 21) & 0x1FF
PT   = (v >> 12) & 0x1FF
```

The offset is selected by the leaf size:

```text
4 KiB: offset = v & 0xFFF
2 MiB: offset = v & 0x1FFFFF
1 GiB: offset = v & 0x3FFFFFFF
```

## Freestanding kernel manager

`Inu.Kernel.VirtualMemory` is the no-heap bootstrap implementation used by generated kernels. It:

1. requires `KernelPhysicalMemory` to be initialized;
2. reads the active x64 CR3 root;
3. walks the inherited UEFI-created page-table hierarchy;
4. permanently excludes every discovered page-table frame from ordinary physical allocation;
5. allocates new intermediate table pages from `KernelPhysicalMemory` when mappings require them;
6. supports mapping, unmapping, protection changes, and translation for 4 KiB, 2 MiB, and 1 GiB leaves;
7. enables EFER.NXE only when CPUID reports execute-disable support and rejects NX-dependent mappings otherwise;
8. checks CPUID before allowing 1 GiB leaf creation;
9. invalidates affected TLB entries using `INVLPG`;
10. reports inherited table pages, created table pages, and manager-created leaf counts.

### Bootstrap physical-access rule

Inu 0.4.1 removes the assumption that an arbitrary PMM allocation can be dereferenced as an identity address. At VMM initialization it snapshots the PMM-managed ranges derived from the retained final UEFI memory map. While the permanent direct map is being created, each new page-table frame is selected only when the PMM reports it free **and** a walk of the active x64 tables proves that virtual address `p` currently translates to physical address `p`.

`KernelAddressSpace.Initialize()` then asks the VMM to populate the standard direct-map window over the calculated PMM-managed ranges. Large 1 GiB or 2 MiB leaves are used where alignment and processor support allow; 4 KiB leaves cover boundaries. Once this succeeds, newly allocated page-table frames are accessed through `DirectMapBase + physicalAddress` rather than by assuming permanent identity mapping. Inherited active table frames that are not part of PMM-managed RAM continue to use the identity access that was already validated during hierarchy discovery.

## Protection model

The architecture-neutral and freestanding protection models cover:

- read;
- write;
- execute;
- user access;
- global translation intent;
- uncached/device memory;
- write-through caching.

x64 execute permission is represented by clearing NX; a non-executable mapping sets NX. Parent entries are not silently widened when an inherited hierarchy would prevent a requested write, user, or execute permission. In that case the bootstrap manager reports `UnsupportedProtection` rather than weakening unrelated mappings.

## Physical-memory ownership

Page-table pages must never be returned to the general physical allocator while they remain reachable from an active root. 0.2.0 therefore adds `KernelPhysicalMemory.TryExcludePage`, which permanently removes a currently-free 4 KiB page from early allocation and succeeds harmlessly when that page was already unavailable.

New intermediate page tables are normal live physical allocations and remain owned for the lifetime of the bootstrap address space. Bootstrap table pages are exact PMM allocations chosen from the intersection of free memory-map-derived ranges and currently identity-reachable mappings. Empty-table reclamation remains intentionally deferred until the address-space ownership/lifetime model is defined.

## Example

```csharp
if (!KernelPhysicalMemory.Initialize(boot)) return false;
if (!KernelVirtualMemory.Initialize()) return false;

KernelVirtualMemoryProtection protection =
    KernelVirtualMemoryProtection.Read |
    KernelVirtualMemoryProtection.Write;

if (!KernelVirtualMemory.TryMap(
        0xFFFF800000400000UL,
        0x00400000UL,
        KernelVirtualPageSize.Page4KiB,
        protection))
    return false;
```

The example demonstrates the mechanics only; 0.2.0 does not prescribe that virtual address as part of a final Inu kernel layout.

## 0.3.0 address-space policy

Roadmap item 10 is now defined by `docs/Kernel-Address-Space-Design.md`. The VMM mechanics remain separate from the selected layout so SDK consumers can replace the policy without replacing the page-table engine.
