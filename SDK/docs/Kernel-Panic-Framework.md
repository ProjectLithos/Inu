# Inu Kernel Panic Framework

Inu 0.10.11 defines a real, structured panic subsystem in `Inu.Kernel.SubsystemContracts`.

## Panic record

Every panic records:

- panic reason and stable numeric code
- human-readable message
- CPU index
- current thread ID
- current process ID
- instruction pointer and stack pointer
- register snapshot
- allocation-free call-stack snapshot
- monotonic timestamp when the timer subsystem is available
- requested crash-dump behaviour
- requested debugger-break behaviour
- final halt/reboot policy

`KernelPanic.TryGetLastSnapshot` exposes the most recent `KernelPanicSnapshot` to the debugger and diagnostic tooling.

## Panic codes

`KernelPanicCode` defines stable SDK codes including assertion failure, unhandled exception, double fault, machine check, page fault, general-protection fault, out of memory, heap corruption, scheduler failure, driver failure, filesystem failure, security violation, watchdog expiry and user-requested panic.

## Freestanding safety

The boot kernel does **not** store an `IKernelPanicBackend` object. `KernelPanic.ConfigureFreestanding` installs raw managed function pointers for context capture, register capture, stack capture, crash-dump request, debugger break, halt and reboot.

That keeps the terminal panic path independent of managed-object allocation, GC write barriers and dynamic interface dispatch.

## Crash dump

When `writeCrashDump` is enabled the kernel emits a structured `[INU:PANIC]` record. In a Debug session the panic debugger break stops QEMU through x64 `INT3`; Kath then captures the formal **NOCD 1.0** crash dump introduced in 0.10.11.

The NOCD dump contains the full debugger-visible registers, unwind stack, page tables, processes, modules, heap, memory ranges, panic reason and driver state. Panic-time in-kernel state is retained separately in `KernelPanicSnapshot`.

## Debugger break

`breakDebugger` requests `InuX64PanicDebuggerBreak`, implemented as x64 `INT3`. Unlike the terminal stop loop this breakpoint can be continued. This is important for `DebuggerThenReboot`: after inspection/capture, Continue allows the panic policy to proceed to reboot.

## Controlled final policy

`KernelPanicPolicy` supports:

- `Halt`
- `Reboot`
- `DebuggerThenHalt`
- `DebuggerThenReboot`

A requested reboot uses the ACPI reset path. If reboot is unavailable or fails, the panic subsystem deliberately falls back to the terminal x64 halt path.

## Call-stack and registers

The freestanding snapshot captures authoritative RIP, RSP, RBP, RFLAGS and CR3 at the panic boundary. The fixed-size call-stack record supports eight addresses without allocation. When a debugger is attached, the IDE performs its existing NativeAOT unwind and stores the richer call stack and complete architectural registers in the NOCD dump.
