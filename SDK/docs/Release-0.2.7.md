# Inu 0.2.7

This release continues the behaviour-preserving source-component decomposition.

## Decomposed implementation units

- Split `KernelConsole` into presentation, configuration, output, framebuffer/capture, live-view, interactive-input, and transport/ANSI units.
- Split `KernelSmp` into topology/per-CPU state, discovery, AP startup, and IPI units.
- Split the generic `KernelVirtio` driver into transport, block, network/queue, and PCI/DMA/storage units.
- Split `KernelVirtioGpu` into driver lifecycle, mode management, GPU commands, transport/queue, and storage/helper units.
- Split `KernelGui` into input, compositor, syscall, rendering, and event/surface-storage units.
- Split `KernelSystemCalls` into registration, user-memory transfer, dispatch/diagnostics, registry, and native-message units.

These remain responsibility-focused source slices under the existing public subsystem contracts. No new OS policy, syscall number, ABI, scheduler role, GUI policy, or device policy is introduced. The native syscall model remains Get/Set/Event.
