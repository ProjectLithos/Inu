# Inu / Kath 0.0.199

## First isolated ring-3 GUI stack

0.0.199 adds the first complete GUI architecture on top of Inu's existing graphics, input, process-isolation and three-operation syscall facilities.

The kernel now contains a dedicated GUI broker/compositor with kernel-owned surface backing stores, z-order, window visibility/lifecycle, focus, a software pointer, hit-testing, keyboard/pointer event routing and process-owned event queues. Applications never receive the physical framebuffer or compositor memory. A surface update is copied from a validated ring-3 buffer through `Event(gui.surface.present)`, and every lifecycle/focus operation checks that the calling PID owns the target surface.

The native ABI remains exactly **Get / Set / Event**. GUI messages are semantic operations inside that ABI: `gui.capabilities`, `gui.surface.create`, `gui.surface.destroy`, `gui.surface.geometry`, `gui.surface.visibility`, `gui.surface.present`, `gui.focus`, `gui.event.next` and `gui.window.close`.

Input routing supports both PS/2 and USB HID keyboards, while pointer routing consumes both PS/2 and USB HID mouse state. When a GUI surface has focus, transitions are queued for its owning process rather than being sent to the debug shell. Surface ownership is also tied into process teardown so a terminated or faulted ring-3 process cannot leave compositor objects behind.

A userland GUI library supplies the syscall-facing client, a BGRA32 software canvas, `Panel`, `Label`, `Button` and `TextBox` controls, plus a userland `DesktopShell` foundation. The desktop policy therefore remains an ordinary ring-3 concern; the kernel only owns protection, arbitration and composition.

The compositor stays dormant during the text/debug boot shell and activates when the first surface is shown, preserving the existing framebuffer console boot path.
