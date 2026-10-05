# Inu SDK Memory Diagnostics

Inu 0.17.0 provides freestanding, SDK-side memory diagnostics without requiring a managed heap or reflection.

## Physical allocator statistics

`KernelMemoryDiagnostics.TryGetPhysicalAllocatorStatistics` reports total managed pages, free pages, permanently reserved/excluded pages, live allocated pages, the largest free extent, extent count, and live allocation count. The accounting invariant is `TotalPages = FreePages + ReservedPages + AllocatedPages`.

## Heap diagnostics

`KernelHeap.GetStatistics` remains the stable basic ABI. `KernelHeap.GetDiagnosticSnapshot` adds leak candidates, guard failures, double-free failures, guarded-allocation count, and tagged-allocation count.

## Page-table inspection

`KernelVirtualMemory.TryInspectPageTable` performs a read-only x64 walk and returns PML4, PDPT, PD, PT and leaf entry values, decoded protection, page size, mapped state, and translated physical address. 1 GiB, 2 MiB and 4 KiB leaves are supported.

## Leak detection

Call `KernelHeap.TryCreateLeakCheckpoint` before the operation under test. Allocations created after that checkpoint that remain live are enumerated with `GetLeakCandidateCount` and `TryGetLeakCandidate`. This is deterministic checkpoint-based leak detection suitable for kernel tests and diagnostics.

## Guards and canaries

`KernelHeap.TryAllocateGuarded` places independent 64-bit leading and trailing canaries around the user range. `TryValidateGuards` validates every live guarded allocation; `TryReleaseGuarded` refuses release when a canary has been corrupted so the fault remains inspectable.

## Double-free detection

Released allocation tokens are retained in a bounded tombstone ring. A repeated release is distinguished from an unknown allocation and reports `KernelHeapStatus.DoubleFreeDetected`; the diagnostic counter is monotonic for the current boot.

## Allocation tags

Live allocations can be tagged with `TrySetAllocationTag`. Freestanding metadata stores a stable 64-bit FNV-1a tag hash rather than a managed string reference. Leak-candidate records include the tag hash, allocation sequence, address, size, token, and guarded state.

## API facade

`Inu.Kernel.Memory.Diagnostics.KernelMemoryDiagnostics` is the common SDK entry point. It delegates to the canonical physical allocator, kernel heap, and active virtual-memory manager rather than maintaining a second memory model.
