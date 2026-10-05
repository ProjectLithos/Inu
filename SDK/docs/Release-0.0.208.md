# Inu 0.0.208

## Userland command completion repair

- Keeps the working VFAT long-filename implementation from 0.0.207.
- Gives `mkdir` a 30-second command-package timeout so the first writable FAT/VFAT operation, including lazy storage/VFS bring-up and directory metadata updates, is not falsely cancelled by the shell's generic five-second command timeout.
- The timeout remains package policy in `/System/Commands/MKDIR.CMD`; the shell is still non-blocking and Ctrl-C/process-kill semantics are unchanged.
- This specifically addresses the observed case where `mkdir thisistestingthelongfilename` reported a timeout even though a following `dir` proved the directory had been created successfully.

## GUI status

The Desktop and Login source remain materialised as userland source. This release does not pretend they are standalone ring-3 executables yet: the current compiler still lacks the ordinary independent userland-application build/autostart path needed to launch Desktop and then Login without falling back to the compatibility text login. That work remains the next process/userland milestone.
