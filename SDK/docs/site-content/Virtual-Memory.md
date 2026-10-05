# Virtual Memory

Inu 0.2.0 adds architecture-neutral virtual-memory contracts, checked x64 four-level page-table encoding/decoding, and a no-heap freestanding manager used by generated kernels.

The generated kernel initializes physical memory first, then attaches the virtual-memory manager to the active x64 CR3 hierarchy. Existing page-table frames are protected from physical reallocation before the manager accepts mapping operations.

Supported leaf sizes are 4 KiB, 2 MiB, and 1 GiB. The API supports map, unmap, protection replacement, translation, canonical-address validation, page-table allocation, and per-page TLB invalidation.

0.2.0 intentionally does not define the final kernel virtual-address layout. That policy remains the next architecture stage.

See `docs/Virtual-Memory-Management.md` for the complete mechanics, formulas, ownership rules, and bootstrap physical-access constraint.
