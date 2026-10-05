# Inu 0.2.28

## Process-management decomposition

This release begins the process-management architecture split.

- Process record/PID storage owns table locking, allocation, lookup, snapshots and active-count changes.
- Process creation and termination are distinct lifecycle source units.
- Process address-space/allocation ownership is separated from executable-image population.
- Foreground process ownership and Ctrl-C cancellation state are isolated.
- Process signalling and kill-control handling are isolated from process startup.
- `KernelProcesses` remains the stable public facade.
- Thread management and scheduler policy are unchanged.

The new process seams are candidates rather than selectable components because they still share the private `ProcessRecord` representation.
