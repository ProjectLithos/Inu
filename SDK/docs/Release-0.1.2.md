# Inu / Kath 0.1.2

0.1.2 corrects the graphical-session bootstrap compile regression introduced in 0.1.1.

- Calls `KernelCommandLine.EnsureSystemFileSystemRoot()` as a method before graphical-session autostart.
- Retains the 0.1.1 system-volume mount prerequisite and GUI fallback diagnostics.
- Keeps Desktop/Login as ordinary isolated ring-3 applications with OS-selected executable paths.
