# Boot Capability Interfaces

Inu boot hand-off data is not a single policy object. `IBootContext` is only the minimal entry boundary. Each independently useful piece of boot information is exposed through a separate interface and source component.

Current freestanding capabilities include:

- `IBootFramebufferContext` — firmware/loader framebuffer information.
- `IFinalMemoryMapBufferContext` — retained final memory-map bytes.\n- `IMemoryDescriptorLayoutContext` — descriptor stride/version used to decode the map.\n- `IUefiMemoryMapKeyContext` — optional UEFI map key.\n- `IMemoryMapCaptureDiagnosticsContext` — optional capture/ExitBootServices diagnostics.
- `IBootstrapPageTableWorkspaceContext` — bootstrap page-table workspace.
- `IAcpiRootPointerContext` — ACPI RSDP hand-off.
- `IApplicationProcessorTrampolineContext` — x86 AP startup trampoline.
- `ISystemAssetBundleContext` — preloaded system assets.
- `IKernelImageContext` — loaded kernel image base.

The native bootloader ABI remains an internal transport layout. It is not the SDK-facing architecture contract. `NativeBootContext` implements each capability in a separate partial source file, allowing Kath/Inu component selection to copy only the capability source selected by the OS author.

Consumers must request the narrowest interface they actually need. For example, ACPI discovery accepts `IAcpiRootPointerContext`; physical-memory initialization accepts `IFinalMemoryMapBufferContext` plus `IMemoryDescriptorLayoutContext` and `IBootstrapPageTableWorkspaceContext`; framebuffer setup accepts only `IBootFramebufferContext`.

The higher-level `Inu.Boot.Contracts` assembly follows the same rule. The former overloaded `BootContext` value object has been replaced by independent contexts such as `BootFramebufferContext`, `UefiFinalMemoryMapContext`, `AcpiRootPointerContext`, and `ApplicationProcessorTrampolineContext`.

This is an intentional API-major change: SDK API 2.0 removes the old monolithic boot-context constructors rather than preserving them as compatibility shims, because retaining them would keep the unwanted architecture as a second authority.
