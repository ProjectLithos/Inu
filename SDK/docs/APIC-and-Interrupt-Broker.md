# APIC and opaque interrupt broker

Inu 0.23.0 completes the x64 driver interrupt-routing boundary. Device drivers request an interrupt through `KernelDrivers.TryRequestInterrupt` and receive only an opaque `KernelDriverInterruptHandle`; they do not select or program PIC, I/O APIC, MSI, MSI-X, Local APIC, or x2APIC delivery.

## Platform mechanisms

`Inu.Kernel.InterruptBroker` reports Local APIC, I/O APIC, x2APIC, MSI, and MSI-X capabilities. Local APIC/x2APIC terminate interrupt delivery at the processor. I/O APIC provides pin/GSI routing. PCI message delivery is selected internally by the broker.

For PCI functions the broker uses the policy:

1. MSI-X when the function exposes a usable MSI-X table.
2. MSI when MSI-X is unavailable or cannot be programmed.
3. I/O APIC/INTx as the fallback when a usable PCI interrupt line is available.

The decision is not returned to the device driver. `KernelInterruptBroker.TryGetRoute` exists for kernel diagnostics only.

## Driver contract

A driver supplies a `KernelDriverInterruptRequest` containing its generic device handle, source hint, priority, processor target, trigger/polarity hints, and an opaque driver cookie. The broker allocates the IDT vector, registers the callback with `KernelInterruptDispatch`, configures the delivery mechanism, and returns an opaque handle. Interrupt entry then dispatches through `KernelDrivers.DispatchInterrupt`, so the driver's interrupt callback receives the same framework context regardless of how the hardware interrupt arrived.

## PCI ownership

Generic PCI MSI and MSI-X programming now lives in `Inu.Kernel.Pci` beneath the broker. NVMe no longer exposes driver-specific MSI/MSI-X programming methods. Intel E1000/E1000e and Realtek RTL8168/RTL8111 request broker-managed interrupts and enable their device-local interrupt causes only after the broker supplies a route. Timer dispatch remains available as a service/fallback methodology without changing the driver's interrupt-delivery abstraction.

## Current INTx limitation

The initial PCI INTx fallback uses the firmware-programmed PCI Interrupt Line field and MADT-discovered I/O APIC ranges. A complete ACPI AML `_PRT` PCI-routing interpreter remains a future enhancement for machines whose firmware requires namespace routing rather than a usable Interrupt Line value.
