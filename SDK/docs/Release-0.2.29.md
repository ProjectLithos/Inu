# Inu 0.2.29

## Opaque process-record contract

This release removes the shared `ProcessRecord*` representation from the process-management responsibilities introduced in 0.2.28.

- `KernelProcessRecordStore` is now the sole owner of the private process record/table layout, PID allocation, active-count state and record-backed resource metadata.
- Other process responsibilities use `KernelProcessRecordHandle`, an opaque handle that does not expose the table slot layout or record fields.
- Process creation, execution, diagnostics, termination, address-space ownership, foreground control and signalling obtain process state through record-store operations rather than dereferencing a shared private structure.
- The public `KernelProcesses` API remains the stable process facade.
- The record store is classified as internal required infrastructure.
- Lifecycle, address-space ownership, foreground control and signalling remain candidate components in this release: their record-layout coupling is gone, but their invocation/startup wiring is not yet independently registered/selectable.
- Thread management and scheduler policy remain unchanged.
