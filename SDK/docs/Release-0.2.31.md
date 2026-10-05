# Inu + Kath 0.2.31

## Thread and scheduler decomposition

- Isolates kernel-thread record ownership.
- Splits thread creation/role lifecycle, retirement, and state/control into focused sources.
- Isolates the current priority-first local-selection/work-stealing policy from dispatch/context switching.
- Adds language-neutral definitions for thread record storage, thread lifecycle, state control, and scheduling policy.
- Keeps lifecycle/state/policy as `candidate` while they still consume scheduler-owned record/CPU state directly.
- Leaves process APIs, Get/Set/Event ABI, and scheduler behaviour unchanged.

Next: replace direct `ThreadRecord*` access with an opaque runnable-thread/store contract and register scheduling policy through a neutral selector interface.
