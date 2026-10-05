# Inu USB subsystem

Inu treats USB as a bus and class-driver stack rather than as a collection of unrelated device drivers.

## Assemblies

- `Inu.Bus.Usb` owns descriptor parsing, addresses, devices, interfaces, endpoint metadata, host-controller routing and enumeration.
- `Inu.Usb.Xhci` is the primary host-controller implementation. It discovers PCI class `0C/03/30`, maps BAR0, resets and starts the controller, provisions DCBAA/scratchpads, command/event rings, device contexts and endpoint transfer rings, and provides synchronous control/bulk/interrupt transfers.
- `Inu.Usb.Hub` handles hub descriptors, port power/status/reset and downstream topology enumeration.
- `Inu.Usb.Hid` handles boot-protocol HID keyboards and mice. Keyboard events are consumed by the same kernel console input path as PS/2 input.
- `Inu.Usb.MassStorage` implements USB Bulk-Only Transport with SCSI READ CAPACITY, READ(10), WRITE(10), and SYNCHRONIZE CACHE and registers removable disks with `KernelStorage`.

## Host-controller policy

xHCI is the preferred USB host-controller architecture. Inu does not spend comparable implementation effort on UHCI, OHCI or EHCI; those can later be added as alternate implementations of the `UsbHostCallbacks` contract without changing USB class drivers.

## Topology

The USB bus passes parent-address and downstream-port information to the host controller before enumeration. This allows xHCI to build route strings and transaction-translator information for devices behind hubs while keeping HID, storage and hub drivers independent of xHCI details.

## Execution model

The current xHCI implementation uses synchronous ring submission and event-ring polling for early-kernel reliability. The public USB bus boundary is independent of interrupt delivery, so xHCI event processing can later move to the existing interrupt broker without changing class-driver APIs.
