# Formal SMP / per-CPU API

Kath 0.15.0 standardises the public SMP and CPU-local scheduling surface around the existing ACPI/xAPIC implementation.

## CPU enumeration and current CPU
`KernelSmp.TryEnumerateProcessor` returns a stable logical index, APIC ID, ACPI UID, online flag, BSP flag and lifecycle state. `KernelSmp.TryGetCurrentProcessorIndex` resolves the calling CPU without exposing architecture internals.

## Per-CPU storage
Eight fixed 64-bit CPU-local slots are available through `TryGetPerCpuValue` / `TrySetPerCpuValue`. Named kernel slots cover Scheduler, Interrupt, Memory and Diagnostics; four User slots are reserved for stable subsystem tokens. The scheduler slot remains synchronized with the legacy scheduler-context property.

## IPIs
`ConfigureIpiVector` assigns vectors for Reschedule, TLB shootdown, call-function and cooperative CPU shutdown. `TrySendIpi` targets a logical CPU and validates online state, vector range and xAPIC destination support.

## CPU affinity
`KernelCpuAffinity` provides a value-type affinity mask and `KernelScheduler.SetAffinity(threadId, KernelCpuAffinity)` applies it. CPU indices 0-63 are represented by the mask.

## Startup and shutdown
`TryStartProcessor` reuses the real INIT/SIPI AP startup path for one offline AP. Shutdown is cooperative: a CPU-shutdown vector must be configured, `TryShutdownProcessor` sends it, and the target calls `NotifyCurrentProcessorOffline` before parking. The BSP cannot be shut down through this API.

## CPU-local schedulers
`KernelScheduler.TryGetLocalScheduler` and `TryGetCurrentLocalScheduler` expose processor index, stable scheduler token, current thread ID, switch/preemption counters and last-dispatch timestamp.
