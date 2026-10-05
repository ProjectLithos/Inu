# Inu 0.2.6

This release continues the behaviour-preserving source-component decomposition begun in 0.2.5.

## Decomposed implementation units

- Split `CommandExecutionHost` into responsibility-focused partial source units for command dispatch, userland bridges, CPU monitoring, subsystem reporting, sessions, console settings, and input.
- Split `FatFs` into boot-sector, lookup, allocation, naming/directory, mutation, and file-I/O units.
- Split `FramebufferConsole` into lifecycle, live-view, text-editing, history, rendering, and presentation units.
- Split `KernelProcesses` into runtime, creation, control, syscall, execution, diagnostics, memory, and storage units.
- Split existing bootstrap-kernel helper responsibilities for console input, networking, graphics, and boot tracing while deliberately retaining the central boot orchestration method intact until it can be staged with compile/runtime validation.

No new syscall numbers, filesystem policy, process policy, or OS-author policy are introduced by this decomposition. Source slices remain implementation units until they have meaningful independent contracts suitable for Kath composition.
