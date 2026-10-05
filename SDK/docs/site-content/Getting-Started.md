# Getting started

Inu OS SDK is a source-component operating-system SDK. Kath uses the sibling Inu SDK, and selected SDK component source is copied into each generated OS for compilation by the IDE.

## Create an operating system

From Kath, choose **Create New OS**, select Microkernel, Hybrid or Monolithic, complete the authoritative configuration, and let the IDE generate the project. The editable operating-system project lives outside the SDK source tree.

From a terminal you can instead use:

```text
inu new MyOS
cd MyOS
inu build
inu run
```

`inu doctor` validates the SDK, manifests, compatibility baseline and required toolchain.

## Kernel entry point

```csharp
[KernelEntry]
public static bool KMain(IBootContext boot)
{
    // Initialise or start the facilities selected by this OS configuration.
    return CPU.Halt();
}
```

Generated projects keep `Kernel/Kernel.cs` high-level. Low-level architecture, memory, interrupt, driver and device work remains in SDK assemblies.

## Current kernel facilities

The current SDK provides architecture contracts; GDT/TSS and interrupt handling; physical and virtual memory; kernel address spaces and heap; ACPI discovery; SMP/per-CPU state; scheduler and threads; ring-3 protection; processes and executable loading; system calls; capability-based drivers; PCI/PCIe and VirtIO; USB; storage and VFS; networking; graphics/framebuffer; TrueType rasterisation; power management; separated timekeeping; synchronization; memory diagnostics; structured logging; panic/crash-dump support; and a formal test framework.

## Filesystems

Applications and services see the VFS contract. Filesystem implementations are providers below it. The SDK includes FatFs support while allowing other filesystem drivers to be selected independently.

## Networking

NIC drivers feed the common Ethernet stack. Standard contracts cover NICs, Ethernet, ARP/NDP, IPv4/IPv6, ICMP, UDP, TCP, sockets and DNS. VirtIO-net, E1000/E1000e and RTL8168/RTL8111-class adapters use this common boundary.

## Time and power

Timekeeping separates the monotonic clock, wall clock, high-resolution counter, scheduler tick and timeout service. Power management exposes ACPI shutdown, reboot and sleep policy, CPU power states and device suspend/resume coordination.

## Fonts and graphics

`Inu.Kernel.TrueType` validates and rasterises memory-resident `.ttf` fonts with scalable pixel sizes and anti-aliased coverage. It can be used by the framebuffer console or future GUI compositors.

## Packages and applications

Inu applications use the documented `.exe`/`.nexe` application format. The package manager uses ordinary ZIP files containing a root `Inu.Package.json` and supports applications, drivers, libraries, services and kernel extensions.

## Debugging and tests

Use **F5** in the IDE for the debugger path or `inu debug` from the CLI. The SDK supports NativeAOT source maps, breakpoints, stepping, CPU/thread/process context, memory/page-table/heap views, crash dumps, tracing and hardware-aware testing.

Run `inu test` for the SDK test framework. API compatibility checks protect public types, signatures, enum values, syscall IDs, driver ABI and debug formats.

## Read the reference

Use the **Assemblies** and **API index** pages in this offline site for source-derived public API reference. The **Guides** section contains the maintained subsystem documentation and samples guide. All links are relative so the site can be copied or opened offline.
