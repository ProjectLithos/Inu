# Inu + Kath 0.2.33

## Scheduler CPU-placement boundary

This release separates CPU runnable-set ownership and CPU-placement policy from scheduler dispatch/context switching. `KernelCpuRunQueueServices` exposes read-only per-CPU runnable-set/load state without exposing `CpuScheduleState` or `ThreadRecord`.

`KernelThreadPlacementServices` is the neutral placement registry. The default `KernelLoadAwarePlacementPolicy` owns initial affinity-aware placement, wake-time rebalance, migration residency hysteresis and remote work stealing. The priority scheduling policy now selects among CPU-owned runnable threads and delegates cross-CPU placement/migration to the placement service.

`Kernel.Scheduler.LoadAwarePlacementPolicy` is selectable through `INU_COMPONENT_SCHEDULER_LOAD_AWARE_PLACEMENT`. The normal Scheduler profile selects it together with `INU_COMPONENT_SCHEDULER_PRIORITY_POLICY`; another placement implementation may be registered instead.

No artificial linked-list run queue was introduced: the existing scheduler still represents a CPU runnable set through thread ownership plus CPU-local state. This release isolates that ownership behind a contract so the representation can be replaced later without changing scheduling policy.
