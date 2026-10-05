# Inu / Kath 0.2.17

Clock-source componentisation and split-integrity repair.

- Repairs a malformed `KernelTime.cs` source slice left by an earlier mechanical decomposition pass.
- Adds `KernelClockSourceServices` as the source-neutral registration and dispatch boundary owned by the clock core.
- Moves HPET MMIO/discovery/counter state into `KernelHpetClockSource`.
- Moves invariant-TSC calibration/frequency/start state into `KernelInvariantTscClockSource`.
- Removes concrete HPET/TSC implementation references from `KernelTime`; Local APIC calibration now consumes the selected monotonic source contract.
- Promotes HPET and invariant-TSC definitions from `candidate` to `selectable`.
- Adds `INU_COMPONENT_TIME_HPET_CLOCK_SOURCE` and `INU_COMPONENT_TIME_INVARIANT_TSC_CLOCK_SOURCE` to the standard HAL defaults while preserving OS-author control over generated source composition.
- Strengthens decomposition validation by checking for orphaned method bodies in addition to brace/preprocessor balance.
