# Inu Kernel Address-Space Design

Inu 0.3.0 defines the permanent virtual-address policy that later heap, stack, MMIO, process, and user/kernel-separation work will consume.

## Standard x64 layout

The standard four-level x64 policy reserves the low canonical half for user mode and divides selected higher-half ranges by purpose. Reservations are policy boundaries; 0.3.0 does not eagerly populate every byte with page-table mappings.

| Purpose | Base | Length | Notes |
| --- | ---: | ---: | --- |
| Low-address guard | `0x0000000000000000` | 64 KiB | Intentionally outside the user region so null/low-pointer faults remain detectable. |
| User | `0x0000000000010000` | to `0x0000800000000000` | Low canonical half available to future process address spaces. |
| Kernel image | `0xFFFF800000000000` | 4 GiB | Target higher-half location for kernel image/static mappings. |
| Kernel heap | `0xFFFF810000000000` | 1 TiB | Reserved for roadmap item 11; no heap allocator is introduced here. |
| Kernel stacks | `0xFFFF820000000000` | 1 TiB | Stack arena with room for per-stack guard pages. |
| Direct physical map | `0xFFFF900000000000` | 64 TiB | Stable virtual window for physical memory; mapping population follows the address-space activation work. |
| MMIO | `0xFFFFD00000000000` | 16 TiB | Device mappings are kept out of ordinary RAM/direct-map allocation policy. |
| Page-table window | `0xFFFFFF0000000000` | 512 GiB | Dedicated page-table access reservation, avoiding dependence on permanent identity mapping. |

Unassigned gaps remain deliberately reserved for future kernel facilities and layout evolution.

## Contracts

`Inu.Memory.AddressSpace.Contracts` defines `KernelAddressSpaceRegion`, `KernelAddressSpaceLayout`, region roles, overlap/range validation, and `IKernelAddressSpacePolicy`. An SDK consumer can therefore replace the standard layout rather than inheriting Inu's choices.

`Inu.Memory.AddressSpace.X64` supplies `X64KernelAddressSpace`, the standard constants, x64 canonical-half validation, and checked direct-map physical/virtual transforms.

## Freestanding bootstrap

Generated x64 kernels include `Inu.Kernel.AddressSpace`. After physical memory and the VMM are initialized, the template calls:

```csharp
if (!KernelAddressSpace.Initialize()) return false;
```

This validates the compiled standard layout and, as of 0.4.1, activates the direct physical map over the PMM-managed ranges captured from the final UEFI memory map before the policy reports success. It does not move the current kernel image or eagerly populate the MMIO/page-table windows. The heap is still initialized separately after this direct-map bootstrap completes.

## Why reservations precede the heap

The heap must know which virtual range it owns before it asks the VMM and PMM for backing pages. Defining the layout first prevents heap growth from colliding with stacks, MMIO, page-table access, the direct map, or the future user/kernel boundary.

## Guard pages

0.3.0 reserves a large kernel-stack arena but does not choose a scheduler stack size. The later thread/scheduler layer can carve individual stacks from this arena and leave unmapped pages between them. A guard page is therefore a layout rule, not a separate allocator in this release.

## Direct-map formula

For the standard layout, a physical byte address `p` below 64 TiB can be represented by:

```text
virtual = 0xFFFF900000000000 + p
physical = virtual - 0xFFFF900000000000
```

The checked APIs reject addresses outside the configured window.

## Boundary with item 9

The 0.2.x VMM supplied mapping mechanics while retaining the inherited UEFI address space. 0.3.x supplied the destination policy. Inu 0.4.1 performs the first activation step by calculating and establishing the PMM-backed direct map before the heap requests high-half mappings; later releases can move the kernel image or replace the root without changing the layout contract.
