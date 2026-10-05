# Inu / Kath 0.1.3

## User-session launch fix

- Moves graphical-session selection to the normal user-session launch point, after the command/session runtime and interrupts are online.
- Adds `KernelCommandLine.PrepareSessionEnvironment()` which gives the ordinary storage/VFS session services time to become ready before GUI/CLI selection.
- Removes the GUI-specific private FAT/storage bootstrap from `KernelProcesses.TryAutostartGraphicalSession`; GUI startup now consumes the same prepared session environment as the CLI.
- Keeps the text login as a recovery fallback when the selected graphical session cannot start.
- Desktop and Login remain separate isolated ring-3 applications, and their executable locations remain OS-author policy.
