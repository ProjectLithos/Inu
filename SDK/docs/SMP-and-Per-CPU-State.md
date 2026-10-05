# SMP and per-CPU state

Inu 0.7.0 implements roadmap item 14 with a real x64 application-processor bootstrap path and a freestanding per-CPU state service in `Inu.Kernel.Smp`.

## Firmware hand-off and SIPI trampoline

x86 application processors begin a Startup IPI in real mode at `startupVector × 4096`, so the target page must be 4 KiB aligned and physically below 1 MiB. Inu therefore reserves one `EfiLoaderData` page with UEFI `AllocateMaxAddress` before the final `GetMemoryMap`/`ExitBootServices` pair. The page address becomes part of the native boot context and of the accepted final UEFI memory map, preventing the PMM from treating it as ordinary conventional memory.

The BSP copies a position-independent trampoline from `Cpu.asm` into that page. The trampoline installs a temporary GDT, enables PAE and IA32_EFER.LME, adopts the BSP's active CR3, enables paging/long mode, switches to a 64-bit code segment, adopts the AP's dedicated bootstrap stack, records the APIC ID observed by CPUID, publishes an online handshake, and parks with interrupts disabled.

This parked state is intentional. Roadmap item 14 owns processor discovery and bring-up; roadmap item 15 will release online APs into scheduler-managed execution after thread contexts and run queues exist.

## Processor discovery

`KernelSmp.Initialize(boot)` consumes the enabled processor records already validated by `KernelAcpi`. Inu assigns a stable logical index in MADT discovery order and identifies the BSP by comparing those records with the current processor's APIC ID.

The current bootstrap transport uses xAPIC physical destination IDs. APIC IDs above 255, an already-enabled x2APIC mode, or an unavailable Local APIC are represented explicitly as unsupported rather than silently targeted with truncated identifiers.

## INIT/SIPI sequence

For each startable AP, the BSP:

1. reserves a dedicated 16 KiB bootstrap kernel stack;
2. copies and patches the low-memory trampoline with the active CR3 and stack top;
3. waits for Local APIC ICR delivery-idle state;
4. sends INIT assert/deassert with the required bootstrap delays;
5. sends the first SIPI and, if necessary, a second SIPI;
6. waits on a monotonic timeout supplied by `KernelTime`;
7. validates that the AP-reported APIC ID matches the intended destination; and
8. marks the per-CPU record `OnlineParked` only after that handshake succeeds.

No AP is counted online merely because an IPI write succeeded.

## Per-CPU state

The per-CPU table and processor-owned bootstrap stacks are allocated from `KernelHeap` after the physical/virtual memory managers and kernel address-space policy are established. The earlier bounded allocator remains available for smaller pre-heap metadata and is not consumed by SMP stack growth. Each record retains:

- Inu logical processor index;
- APIC/x2APIC identifier;
- ACPI processor UID;
- BSP flag;
- startup lifecycle state;
- processor-owned bootstrap stack base and top; and
- a scheduler-context token reserved for roadmap item 15.

`TryGetProcessor` returns immutable `KernelProcessorState` snapshots. `TryGetCurrentProcessor` resolves the executing CPU through its current APIC ID. `TrySetSchedulerContext` is the hand-off point for the future scheduler without exposing the internal state-table layout.

## Failure policy

Fatal failures are limited to conditions that prevent a valid per-CPU model, such as having no ACPI processors, failing to reserve the state table, or failing to identify the BSP. If the machine can continue safely on the BSP but AP startup is unavailable, `KernelSmp.Initialize` succeeds with `Partial`, `TrampolineUnavailable`, or `LocalApicUnavailable` status and keeps unsupported APs offline.

This means an OS can decide whether reduced CPU availability is acceptable without the SDK fabricating SMP success.

## Independent validation

`Inu.Smp.Tests` validates SIPI address/vector rules, xAPIC destination limits, and per-CPU table sizing independently of firmware or QEMU. Template policy tests require the command-line and Visual Studio SDK copies of the SMP implementation and low-level native wrapper to remain identical to their authoritative sources.
