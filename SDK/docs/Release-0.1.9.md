# Inu/Kath 0.1.9

0.1.9 completes graphical-session input activation for generated microkernel operating systems. Kath now materializes the currently available PS/2 and USB HID input mechanisms into the kernel execution domain whenever those input providers are selected, including GUI-first microkernel configurations.

At the normal graphical-session boundary the generated kernel asks the HAL to verify and bind the configured input providers before Desktop and Login are launched. The HAL publishes explicit `NOKMAIN:GUI-INPUT:*` diagnostics, rebinds decoded keyboard and pointer callbacks to the active GUI router, and leaves GUI startup available even when a deliberately input-less OS configuration is selected.

The GUI remains asynchronous, SMP-safe and buffering-mode agnostic. This release does not assign the GUI permanently to a particular CPU and does not impose a fixed input-device policy; Kath derives the provider set from the OS author's selected input components.
