# Inu/Kath 0.1.5

GUI framebuffer presentation ownership fix.

- The graphical session takes exclusive ownership of physical framebuffer presentation at the normal user-session boundary.
- The text console stops presenting to the framebuffer before Desktop/Login start, preventing stale console buffers, caret ticks, or later text writes from overwriting compositor frames.
- CLI fallback restores console framebuffer presentation before the text session starts.
- The selected framebuffer buffering policy is preserved. Single, double, triple, and automatic modes are not hard-coded by GUI takeover.
- Serial diagnostics remain active while the GUI owns the display.
