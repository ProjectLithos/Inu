# Structured Kernel Telemetry

Inu kernels expose structured telemetry through `Inu.Kernel.Contracts.KernelTelemetry`. The contract is designed so debuggers, profilers, tracing tools, test harnesses and the Kath do not need to infer kernel state from human-readable console messages.

## Event families

`KernelTrace(subsystem, name, ...)` records discrete kernel activity such as scheduler transitions, interrupt delivery, driver state changes, storage operations and syscall activity.

`KernelProfile(subsystem, name, samples, durationNanoseconds)` records sampled or measured work. `value0` carries the sample count and `value1` carries the duration in nanoseconds.

`KernelBootEvent(name, stage, phase, ...)` describes boot progress explicitly. `KernelBootPhase` supports `Instant`, `Begin`, `End`, `Warning` and `Failed`.

`KernelCounter(subsystem, name, value)` records structured counters such as interrupt totals, bytes transferred, allocations, context switches or packets.

`KernelDiagnosticEvent(subsystem, name, code, detail)` records a machine-readable diagnostic code with a human-readable detail string.

## Common record fields

Every `KernelTelemetryEvent` includes:

- telemetry kind;
- subsystem and event name;
- timestamp in nanoseconds;
- two 64-bit event-specific numeric values;
- correlation/sequence ID;
- detail text;
- CPU index;
- thread ID;
- process ID.

The active `IKernelLogContextProvider` supplies CPU, thread, process and monotonic-time context when the caller does not provide an explicit timestamp.

## Sinks and statistics

`KernelTelemetry.Configure` installs the primary sink and context provider. `KernelTelemetry.AddSink` allows up to four simultaneous sinks so serial/debugger output, in-memory collection, test capture or future binary transports can coexist.

`KernelTelemetry.GetStatistics()` reports emitted totals for trace, profile, boot, counter and diagnostic events plus dropped events. `ResetStatistics()` resets these totals.

## IDE transport

The bundled kernel console sink serializes events using stable Inu records beginning with `[INU:...]`. The IDE parses these records directly and uses the nanosecond timestamps and durations to populate the Tracing + Boot Analyser and Performance Profiler.

Human-readable kernel logging is separate. Console milestone parsing is retained only as backward compatibility for kernels that predate the structured telemetry protocol.
