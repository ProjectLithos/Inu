# Inu system calls

Inu 0.10.11 introduced one x64 SYSCALL/SYSRET privilege transition and three dispatch methodologies; the same protected boundary remains active in 0.11.0 processes.

## Inu Get / Set / Event

The native ABI separates query-style Get operations, mutation-style Set operations, and notification/action Event operations. Service IDs are independent inside each class. Built-in bootstrap services expose ABI version, monotonic time, online CPU count, scheduler quantum control, and scheduler yield. SDK authors may register custom handlers without editing the low-level entry stub.

## Linux-style services

Linux-style services preserve the original numeric syscall ID inside the explicit Linux ABI namespace and use negative errno-like return values. The bootstrap includes `sched_yield` (x86-64 service 24) and permits bounded custom handlers for additional Linux-style services. This is an ABI methodology/compatibility layer; Inu does not claim to implement the Linux kernel.

## Microsoft Windows / NT-style services

Microsoft Windows/NT-style services preserve a numeric service ID inside the explicit NT ABI namespace and use NTSTATUS-style return values. Windows service-number tables are version-dependent, so Inu deliberately does not hard-code a misleading universal Windows syscall table. SDK authors register the service IDs appropriate to their compatibility target. Inu does not depend on the Windows kernel.

## Protected entry

The native entry configures EFER.SCE, STAR, LSTAR and FMASK. SYSCALL enters with interrupts masked, SWAPGS selects the per-CPU syscall state, and execution moves to a dedicated 32 KiB kernel stack before managed dispatch. SYSRETQ restores the user RIP, flags and stack.

## User memory

`TryCopyFromUser` and `TryCopyToUser` validate every page as user-accessible through the active page tables. After these guarded paths exist, SMAP is enabled when supported and STAC/CLAC are used only inside the copy window.
