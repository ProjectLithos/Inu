# Interactive QEMU Console

Inu's framebuffer console accepts decoded keyboard input from PS/2 and USB HID devices and feeds it into the freestanding `Inu.Kernel.CommandLine` service.

The command line provides a 256-byte editable input buffer, printable ASCII echo, Backspace, Enter submission, case-insensitive command matching, and a coder-defined prompt from `Userland/<OSName>/Shell.cs`. Up/Down remain scrollback controls. Ctrl+1/2/3 force framebuffer buffering modes; Alt+1/2/3 force font-size presets, leaving ordinary digits available for command arguments.

Built-in control commands are available immediately after boot:

- `help`
- `font get`, `font set 1|2|3`, `font list`
- `buffering get`, `buffering set auto|1|2|3`, `buffering list`
- `keyboard get`, `keyboard set English_UK|English_USA`, `keyboard list`
- `echo <text>`

These built-in commands are the freestanding console control surface for services that already exist in the SDK. The ELF64/PE32+ ring-3 process loader remains a separate subsystem; filesystem-backed user executables can be launched through `KernelProcesses` once a shell/process-launch command has a runtime-safe path-string ABI and executable files are present on a mounted volume.
