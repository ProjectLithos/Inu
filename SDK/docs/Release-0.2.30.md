# Inu 0.2.30

## Process service-provider promotion

- Promotes process lifecycle, address-space ownership, foreground control and process signalling from candidate to selectable components.
- Adds neutral process-service registries so `KernelProcesses` remains the stable facade while concrete providers may be omitted or substituted.
- Keeps `Kernel.Processes.RecordStore` internal and opaque.
- Default Processes configurations still select all four providers.
- Thread and scheduler policy remain unchanged.
