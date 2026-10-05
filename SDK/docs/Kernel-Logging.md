# Kernel logging

Inu structured kernel logging is the diagnostic contract for kernel and driver code.

## Levels

`KernelLogLevel` defines `Trace`, `Debug`, `Info`, `Warning`, `Error`, and `Critical`. Callers use the matching `KernelLog.Trace`, `Debug`, `Info`, `Warning`, `Error`, or `Critical` entry point. `KernelLog.SetMinimumLevel` changes the runtime filter without changing call sites.

## Record context

Every `KernelLogRecord` contains the severity, subsystem, CPU index, thread ID, process ID, monotonic timestamp in nanoseconds, source, and message. During early boot unavailable execution context is represented by zero. As the SMP, scheduler, time and process subsystems come online the default `KernelLogContextProvider` supplies their live values.

The `source` field should identify the producing API or method, for example `Kernel.KMain` or `KernelPci.Initialize`. `subsystem` should be a stable short name such as `memory`, `pci`, `usb`, `scheduler`, or `filesystem` so sinks and the IDE can filter without parsing message text.

## Sinks and filtering

`KernelLog.Configure` installs the initial sink and context provider. Up to four sinks can be active through `KernelLog.AddSink`, allowing serial/framebuffer output, debugger transport, an in-memory ring, or a persistent sink to consume the same records. `KernelLog.GetStatistics` reports records by severity and dropped writes, and `ResetStatistics` resets those counters.

The bootstrap `KernelConsoleLogSink` emits records as:

`[LEVEL][t=...][cpu=...][thread=...][process=...][subsystem][source] message`

This is intentionally stable enough for Kath diagnostic tooling to parse while remaining readable on the kernel console.

## Console versus logging

`KernelConsole` remains the presentation surface for interactive shell output and deliberately formatted status displays. Kernel diagnostics and lifecycle messages should use `KernelLog` rather than scattering `WriteLine` calls through subsystems.
