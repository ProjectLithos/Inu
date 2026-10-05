# Inu/Kath 0.1.6

0.1.6 moves graphical-session construction off the bootstrap/session thread. The session boundary now queues a scheduler one-shot on the OS-selected GUI CPU set; filesystem checks, compositor initialisation and executable loading run there while the bootstrap CPU waits interruptibly rather than executing the work synchronously.

Desktop and Login remain ordinary isolated ring-3 processes. After their images are created they are started by independent scheduler one-shots using the configured GUI and Userland CPU role masks, so multi-CPU systems may execute them concurrently. Single-CPU systems use exactly the same path and remain supported.

The framebuffer presentation handoff remains buffering-mode agnostic: single, double and triple buffering are unchanged. If asynchronous graphical startup fails, framebuffer presentation is restored and the selected text recovery session is entered.
