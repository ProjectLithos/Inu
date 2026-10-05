# Inu display drivers

Inu separates the generic graphics target from the mechanism that supplied it. The graphics core is `Inu.Kernel.Graphics`.

## Firmware and simple framebuffers

`FirmwareFramebuffer` adapts the UEFI GOP framebuffer captured during boot. It remains the boot-safe and real-hardware fallback and does not require a native GPU driver.

`SimpleFramebuffer` explicitly registers a CPU-visible linear framebuffer supplied by another firmware interface, bootloader, VESA-like environment, or platform. It accepts physical and CPU-visible virtual addresses, byte length, dimensions, scan-line pitch, and the generic Inu pixel format. Simple framebuffers do not claim hardware mode-setting capability; they expose the mode handed to Inu.

Both providers register through `KernelGraphics.RegisterDisplay`, so consumers can enumerate them, select a primary target, inspect their current framebuffer, and present dirty rectangles through the same API used by proper graphics drivers.

## VirtIO GPU

`Inu.Kernel.Virtio.Gpu` remains Inu's first proper graphics driver. It owns VirtIO PCI GPU device type 16, discovers displays, creates 2D resources and backing storage, selects scan-outs, transfers framebuffer changes to the host, flushes them, and can recreate resources for runtime resolution changes. For Kath 0.41.7 and later, QEMU exposes the same VirtIO-GPU 2D backend through the primary `virtio-vga` PCI frontend. This suppresses QEMU's separate default VGA adapter and keeps firmware GOP and the Inu VirtIO-GPU driver on one display device.

From IDE 0.40.0, kernel graphics startup promotes a successfully initialized VirtIO-GPU display to the preferred console target. `FramebufferConsole` resolves the `KernelGraphicsDisplayHandle` that owns its active backing memory and routes each completed dirty rectangle through `KernelGraphics.Present`. GOP therefore remains a direct-framebuffer no-op present path, while VirtIO-GPU performs the required `TRANSFER_TO_HOST_2D` and `RESOURCE_FLUSH` commands. Promotion is preflighted and non-fatal: if VirtIO-GPU cannot present, the console remains on the registered UEFI GOP framebuffer. Buffered console images are reused when large enough and replenished on a best-effort basis when the VirtIO resource requires a larger frame.

## Native GPUs

AMD, NVIDIA, and Intel native GPU drivers are intentionally deferred. Early physical-machine support should continue to use UEFI GOP or a simple linear framebuffer where available, while VirtIO GPU exercises the graphics-driver framework and mode/resource lifecycle in QEMU.
