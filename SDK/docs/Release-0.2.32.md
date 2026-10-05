# Inu + Kath 0.2.32

## Scheduler policy contract

The scheduler core now exposes an opaque runnable-thread view through `KernelRunnableThreadServices`. Replaceable scheduling policies can enumerate runnable threads, inspect priority/affinity/CPU ownership, query processor load, and request migration without seeing `ThreadRecord`, `_threads`, `_cpus`, or other private scheduler storage.

`KernelSchedulingPolicyServices` is the registration/dispatch boundary for scheduling policy. The default `KernelPrioritySchedulingPolicy` registers local selection, balanced selection/work stealing, and dispatchable-work probing. `Kernel.Scheduler.PriorityPolicy` is therefore promoted from `candidate` to `selectable`.

The normal Scheduler profile selects `INU_COMPONENT_SCHEDULER_PRIORITY_POLICY` by default. An OS author may omit it only when another policy registers against the same contract before scheduler initialization.

## 0.2.31 dispatch repair

The 0.2.31 extraction accidentally left a fragment of `HasDispatchableWork` in `KernelScheduler.Dispatch.cs` after moving the priority-selection implementation. That malformed source has been repaired. Dispatchable-work probing is now part of the registered scheduling-policy contract, which also prevents the same responsibility from being split between policy and dispatch again.

## Validation

This environment does not provide `dotnet`, so NativeAOT compilation was not run. Release validation checks component definitions/catalogue membership, scheduler concrete-coupling rules, preprocessor and delimiter balance, source-manifest hashes, and ZIP integrity.
