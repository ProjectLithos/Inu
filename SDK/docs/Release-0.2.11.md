# Inu OS SDK 0.2.11

This release continues source-component decomposition and introduces the first language-neutral component-definition catalog.

## Decomposed

- Kernel security: address spaces, user-memory/W^X protection, syscall policy, capabilities, and page-table helpers.
- Managed interrupt dispatch: deferred work, user-fault handling, lifecycle/vector operations, and dispatch path.
- Kernel timekeeping: clock sources, monotonic/deadline operations, delays, and local-APIC timer operations.
- Kernel graphics: display registry, mode/presentation operations, and backing storage helpers.

## Component definitions

The new `components/definitions` layer defines components once, independently of implementation language. The first five definitions are intentionally candidates rather than independently selectable components because their current implementation slices still share parent-owned state. This prevents Kath from exposing false modularity.

No syscall ABI, filesystem policy, firmware/kernel separation, language policy, process isolation, or existing public subsystem contract is intentionally changed.
