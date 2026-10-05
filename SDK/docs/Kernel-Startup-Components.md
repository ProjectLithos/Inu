# Kernel startup components

Kath and Inu treat kernel startup as OS-owned composition. The generated kernel entry does not call a single monolithic boot initializer.

The standard C# source set is split into independently selectable stages:

- `BootDiagnosticsStartup` — bootstrap console, structured diagnostics, panic transport and final-map validation.
- `PlatformTablesStartup` — x64 GDT/TSS, IDT and legacy PIC state.
- `AcpiStartup` — ACPI root discovery and selected ACPI providers.
- `TimeStartup` — selected clock providers and kernel timekeeping.
- `MemoryRuntimeStartup` — physical/virtual memory, address space, heap and NativeAOT runtime.
- `GraphicsStartup` — graphics registry and optional firmware framebuffer promotion.
- `SmpStartup` — AP startup and OS-selected CPU roles.
- `SchedulerRuntimeStartup` — scheduler, managed interrupt dispatcher and GC root-map transition.
- `ProtectionStartup` — user/kernel protection, security policy and syscall registration.
- `ProcessRuntimeStartup` — kernel process bookkeeping when selected.
- `TimerDispatchStartup` — timer dispatch when selected.
- `UserlandCommandStartup` — system assets and userland command host when selected.
- `InterruptRuntimeStartup` — final runtime interrupt enable.
- `TextConsoleSessionStartup` — text-only user session.
- `DesktopOrTextSessionStartup` — graphical session with text fallback.

`Kernel/Kernel.cs` is deliberately ordinary OS-owned source. Kath may add, remove, replace or reorder these calls according to the architecture chosen by the OS author. Generic `TBoot` constraints are the union of only the boot capabilities consumed by the selected stages.

There is no `BootStartup.Initialize()` compatibility path. Reintroducing one would recreate a second hidden architecture and is intentionally prohibited by the template policy checks.
