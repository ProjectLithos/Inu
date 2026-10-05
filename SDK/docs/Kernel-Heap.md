# Early allocator and kernel heap

Inu 0.4.1 introduces two deliberately separate allocation stages.

The **early allocator** is a bounded monotonic arena. It is suitable for tiny bootstrap metadata that must exist before the page-backed heap is available. Allocation is `aligned = (current + alignment - 1) & ~(alignment - 1)`. Individual frees are intentionally unsupported; the whole arena has bootstrap lifetime.

The **kernel heap** uses address-ordered first fit inside the `KernelHeap` virtual reservation defined by the 0.3.x address-space policy. When committed space cannot satisfy an allocation, the heap obtains contiguous 4 KiB frames from `KernelPhysicalMemory`, maps them read/write and non-executable through `KernelVirtualMemory`, then adds the newly committed range to its free list. Splitting preserves prefix and suffix fragments; release validates the opaque token and exact range before coalescing adjacent free blocks.

The SDK also exposes `IEarlyAllocator` and `IKernelHeap` plus `BumpEarlyAllocator` and `FirstFitKernelHeap`, so an OS author can replace Inu's default methodology without changing consumers of the contracts.

The freestanding kernel heap returns raw virtual addresses rather than managed objects. Inu still has no GC at this stage. Object allocation/runtime integration is a separate concern.

## Default boot order

1. physical memory manager
2. virtual memory manager
3. kernel address-space policy
4. bounded early allocator
5. page-backed kernel heap

The heap never allocates outside `KernelAddressSpace.KernelHeapBase .. KernelHeapBase + KernelHeapLength`.
