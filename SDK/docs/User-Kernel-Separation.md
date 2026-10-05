# User/kernel separation

Inu 0.10.11 establishes the x64 protection boundary required before system calls and processes. Ring 0 remains the kernel privilege level and ring 3 is reserved for user execution.

The installed GDT ABI uses selector `0x23` for ring-3 64-bit code and `0x1B` for ring-3 data/stack. User virtual addresses are restricted to `0x0000000000010000` through `0x00007FFFFFFFFFFF`; the first 64 KiB remains a null/low-address guard.

`KernelProtection.Initialize()` enables CR0.WP and enables SMEP when the processor supports it. SMAP support is reported but intentionally not enabled until the system-call stage supplies explicit guarded copy-in/copy-out primitives. NX is supplied by the existing virtual-memory stage.

`TryMapUserPage` and `TryProtectUserPage` force the page-table User bit and refuse kernel-half addresses. `TryCreateUserModeContext` validates ring-3 RIP and an ABI-aligned RSP. Inu does not enter ring 3 during bootstrap in this stage because a safe return path belongs to system calls (item 17) and executable loading belongs to processes (item 18).
