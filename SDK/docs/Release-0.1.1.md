# Inu / Kath 0.1.1

0.1.1 is the first corrective release after the 0.1.0 GUI milestone.

## GUI startup correction

- Graphical-session autostart now ensures that the selected system FAT filesystem is discovered and mounted before Desktop/Login executables are opened.
- The GUI autostart path performs this prerequisite itself, so an existing 0.1.0-generated kernel that already calls `TryAutostartGraphicalSession()` benefits without requiring a new hard-coded filesystem path policy.
- The generated kernel also emits explicit GUI filesystem/autostart markers before falling back to the text recovery session.
- Desktop and Login remain ordinary isolated ring-3 applications.
- GUI executable locations remain OS policy and are not forced read-only or into a fixed directory by Inu.
