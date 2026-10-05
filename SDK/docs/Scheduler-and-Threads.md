# Scheduler and Threads

Inu 0.8.0 introduces the kernel scheduling layer above SMP/per-CPU state. `KernelScheduler` owns fixed-capacity thread records, page-aligned kernel stacks, four priority bands, processor affinity, blocking/waking/termination state and per-CPU current-thread state.

The scheduler deliberately remains a kernel-thread facility. User-mode execution, processes and executable loading remain roadmap items 16-18. The Local APIC timer capability from item 13 is exposed as the preemption source; `OnTimerTick` and `Yield` use the same scheduling decision path so interrupt dispatch can hand off to it without changing scheduler policy.

Each online processor receives a scheduler-context pointer through the per-CPU record created in item 14. Threads use stable numeric IDs and an opaque 64-bit entry point/argument ABI so architecture-specific context setup can stay below the public scheduler contract.

## Current execution contract

The x64 switch ABI preserves Win64 non-volatile general-purpose registers, XMM6-XMM15, the stack pointer and the resume instruction pointer. Fresh kernel threads receive their opaque argument in RCX. At this roadmap stage kernel-thread entry points are non-returning; process/thread exit semantics are completed with the later process and user/kernel stages.
