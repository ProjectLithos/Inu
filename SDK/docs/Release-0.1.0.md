# Inu 0.1.0

## Graphical userland milestone

- Builds `Userland/Desktop/Desktop.asm` and `Userland/Login/Login.asm` from the materialized OS source tree into standalone PE32+ ring-3 applications.
- Stages `INU-DESKTOP.EXE` and `INU-LOGIN.EXE` under `/bin` on the FAT32 system volume.
- Autostarts the desktop first and the graphical login second after graphics, VFS, processes and syscalls are online.
- Displays the supplied dark hexagonal wallpaper as the full desktop background using compositor cover scaling.
- Adds a `session.login` Event contract so the ring-3 login app can authenticate an existing account or create the first account without moving account policy into the GUI compositor.
- Keeps the text login as a recovery fallback only when graphical-session autostart cannot start.
- GUI applications remain ordinary isolated background ring-3 processes and communicate through the existing Get/Set/Event syscall ABI.

## Graphical-session path repair

The 0.1.0 repair removes the hidden fixed-path assumption from GUI autostart. Kath now exposes the Desktop and Login executable paths as OS configuration values (defaulting to `/BIN/INU-DESKTOP.EXE` and `/BIN/INU-LOGIN.EXE` for compatibility). The image builder stages the binaries at the selected VFS paths and the generated OS configures the process runtime with those same paths. Read-only/writable policy remains entirely the OS author's choice. Existing 0.1.0-generated sources retain the compatible `/BIN` defaults, so they can boot the GUI without regeneration.

### Graphical-session startup repair
The generated OS kernel now invokes graphical-session autostart after interrupts are enabled. The autostart service initializes its required storage/VFS and GUI mechanisms when they are not already online. When Desktop and Login start successfully, the bootstrap CPU remains in the scheduler/interrupt host loop instead of entering the text recovery login. Text login remains the fallback only when graphical startup fails.
