# Process Isolation and Security

Inu 0.18.0 formalises the x64 kernel/user security boundary.

## Address spaces
Each user process owns a private lower-half page-table root. The kernel upper half is shared supervisor-only. `KernelSecurity.TryGetAddressSpace` reports the authoritative root and domain.

## User pointers
`KernelSecurity.TryValidateUserPointer` walks the target process page tables, checks every page, rejects guard ranges, requires the user bit, and validates read/write/execute access. Syscall copies use this process-specific validator before SMAP access is enabled.

## Executable permissions, W^X and NX
User mappings may be writable or executable, never both. The process loader rejects W+X executable segments. Non-executable leaves carry x86-64 NX. Process entry points are verified against an executable user mapping before the process becomes runnable.

## Guard pages
Each initial user stack has one deliberately unmapped 4 KiB guard page immediately below the stack allocation. User-pointer validation explicitly rejects registered guard ranges.

## Privilege rings
Inu uses ring 0 for kernel execution and ring 3 for user execution. The protection capability snapshot exposes both rings and the ring-3 code/data selectors.

## Syscall validation
Each registered process has an ABI policy for Inu Get/Set/Event, Linux-style and NT-style syscalls. The syscall dispatcher validates ABI/service policy against the current process before dispatch. User buffers are validated against that process's page tables.

## Capability handles
`KernelCapabilityHandle` is opaque and process-scoped. Handles encode a bounded slot plus generation, preventing stale-handle reuse. Rights are explicit bit flags; resolve requires all requested rights. Closing, duplication and process-wide revocation are supported without exposing kernel object addresses as authority.
