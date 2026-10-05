# Inu / Kath 0.1.4

0.1.4 repairs graphical-session startup at the normal user-session launch point.

- GUI application paths are now interpreted as true mounted-VFS paths. `/BIN/INU-DESKTOP.EXE` and `/BIN/INU-LOGIN.EXE` are staged at the FAT root `/BIN`, not accidentally under `/INU/BIN`.
- The text-login account probe is no longer started during generic command-line initialization. It begins only when the text recovery session is actually selected, so it cannot race or print `Unable to load the Inu user registry.` while a graphical session is starting.
- Graphical login continues to use the `session.login` Event syscall and can create the first account when no account database exists.
- Desktop/Login remain ordinary isolated ring-3 processes. Their filesystem locations remain OS-author policy.
- CLI remains the recovery fallback only when graphical session startup fails.
