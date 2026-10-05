# Interactive framebuffer console

Inu 0.23.0 extends the freestanding framebuffer console from output-only rendering into an interactive post-boot console view.

## Controls

With the QEMU SDL display focused:

- **Up Arrow** — scroll one visual line upward through retained output.
- **Down Arrow** — scroll one visual line downward toward live output.
- **1** — select the 8-pixel font preset.
- **2** — select the 16-pixel font preset.
- **3** — select the 24-pixel font preset (the default).
- **Ctrl+1** — switch to single-buffered output (render directly to the GOP scan-out framebuffer).
- **Ctrl+2** — switch to double-buffered output (one heap-backed software backbuffer plus the GOP front buffer).
- **Ctrl+3** — force triple-buffered output (two heap-backed software backbuffers plus the GOP front buffer).

The keyboard decoder accepts the normal PS/2 make-code sequences used by scan-code sets 1 and 2. Break/release sequences are ignored.

## Framebuffer buffering

Inu starts the earliest boot console in single-buffered mode because the kernel heap is intentionally not online yet. After `KernelHeap.Initialize()`, the bootstrap allocates two full framebuffer-sized, page-aligned backbuffers and attaches them to `KernelConsole`. The console then applies the automatic text-console policy, which selects double buffering by default. Triple buffering remains available as an explicit diagnostic/performance override.

UEFI GOP exposes one scan-out framebuffer and does not provide a portable page-flip primitive, so Inu implements double and triple buffering in software. Rendering occurs into the selected heap-backed backbuffer. The renderer tracks the rectangle modified by each operation and `Present()` copies only that dirty region to the GOP scan-out buffer. A normal glyph therefore transfers only its glyph-sized area instead of copying the entire framebuffer. Full-frame transfers are reserved for operations that genuinely dirty the whole display, such as a clear, font reflow, or explicit buffer-mode synchronization. In triple mode the same dirty region is copied into the alternate software backbuffer before it becomes the next draw target, keeping both software buffers coherent without a full-frame copy per character. Single mode bypasses the software buffers and writes directly to GOP.

The public API is `GetFramebufferBufferByteCount()`, `GetFramebufferBufferCapabilities()`, `ConfigureFramebufferBuffers(...)`, and `SetFramebufferBufferCount(UInt32)`. This is not QEMU-specific; QEMU/OVMF is the primary test environment, while the same GOP contract applies on compatible UEFI hardware.

## Retained history and reflow

`FramebufferConsole` retains 256 KiB of ASCII console history independently of the visible framebuffer pixels. This is a console scrollback policy, not a device/driver registry limit. When the retained buffer fills, the oldest console bytes are replaced by newer output.

The renderer derives visual lines from the current framebuffer width, glyph width, character advance, and explicit newlines. Changing font preset recalculates wrapping and redraws the visible history using the new metrics. Output that arrives while the user is scrolled back remains in history without forcing the view back to the live bottom.

## Post-boot state

Earlier Inu kernels ended the bootstrap in a permanent `CLI; HLT` loop. That state cannot accept keyboard input. Inu 0.23.0 therefore reports:

```text
Interactive console ready. Type to echo; Up/Down scroll; 1/2/3 font size; Ctrl+1/2/3 single/double/triple buffering.
```

and enters `KernelConsole.RunInteractive()`. The interactive loop enables interrupts and sleeps with `HLT`; keyboard servicing is scheduled through `Inu.Kernel.TimerDispatch`, and the console handles one pending input event through `KernelConsole.ServiceInput()`. There is no continuous `PollInput()/PAUSE` background loop in the generated kernel. The existing permanent `KernelPlatform.Halt()` API remains available to kernels that explicitly want a terminal non-interactive halt.

## Public console API

The high-level kernel console exposes `ScrollUp()`, `ScrollDown()`, `SetFontPreset(UInt32)`, framebuffer-buffering configuration/query methods, `ServiceInput()`, and `RunInteractive()`. Timer or interrupt dispatch selects when servicing runs; these APIs keep raw x64 port I/O out of the user kernel.

## Userland console controls

`font get`, `font set 1|2|3`, and `font list` control the 8/16/24 px font presets through Get/Set service 33. `buffering get`, `buffering set auto|1|2|3`, and `buffering list` control automatic/single/double/triple buffering through Get/Set service 34. Font defaults to preset 3; buffering defaults to automatic, which currently resolves to double buffering for the framebuffer text console.


## Font faces and PSF2

The framebuffer console no longer treats the embedded 8x16 bitmap as the renderer itself. `ConsoleFont` is the active font-face layer and the framebuffer renderer consumes its source metrics and glyph rows. `KernelConsole.InstallPsf2Font(address, length)` validates a PC Screen Font v2 image already present in kernel-accessible memory, installs it, and redraws retained output. `KernelConsole.GetFontInformation()` reports the active format, source glyph width/height, glyph count, and Unicode-table availability. `KernelConsole.UseEmbeddedFont()` restores the guaranteed boot font.

PSF2 faces may be up to 32 source pixels wide. When a PSF2 Unicode table is present, Inu resolves ASCII console characters through that table; otherwise direct glyph indices are used. The existing `font set 1|2|3` command controls rendered height only (8/16/24 px), so changing size does not change the selected face. This applies equally to QEMU/OVMF GOP output and compatible real UEFI GOP framebuffers.


## Line-batched presentation

Framebuffer glyphs are accumulated in the active render buffer and presented as completed strings/lines or scroll regions rather than copying the scan-out framebuffer after every character. Automatic bottom-of-screen scrolling moves the rendered pixel rows upward by exactly one active text-line height, clears only the newly exposed bottom strip, and presents the resulting dirty region once. Scrollback Up/Down redraws the requested viewport into the render buffer and performs one presentation for the completed viewport.
