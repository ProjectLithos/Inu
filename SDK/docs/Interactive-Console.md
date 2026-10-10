# Interactive QEMU Console

Inu's framebuffer console accepts decoded keyboard input from PS/2 and USB HID devices and feeds the foreground ring-3 terminal process through the ordinary `Get` syscall path. The shell owns command-line editing and command history; the kernel owns presentation and decoded key delivery.

The stock shell supports:

- **Page Up / Page Down** — move through retained framebuffer scrollback one page at a time.
- **Typing while viewing scrollback** — returns the viewport to the live prompt before displaying the edit.
- **Up / Down** — browse previously submitted command lines. Pressing Enter runs the command currently displayed.
- **Left / Right** — move the insertion caret through the current command line.
- **Home / End** — move to the beginning or end of the current command line.
- **Delete / Backspace** — delete at, or immediately before, the caret without appending fake backspace bytes to retained history.
- **Ctrl+C** — requests cancellation of the active foreground process.

The command-history store belongs to the resident shell runtime, not the kernel command dispatcher. The shell remains command-name agnostic and still discovers/launches executables from the configured Commands path.

## Caret and colours

`Inu.Userland.Runtime.ConsolePresentation` is the SDK-facing API for text-console presentation. SDK authors can set foreground/background RGB values, caret mode, and caret height from their own startup/shell policy.

Caret modes are:

- `ConsoleCaretMode.Blinking`
- `ConsoleCaretMode.Visible`
- `ConsoleCaretMode.Off`

Caret height is `1..100` percent of the character body. A value of `1` is a thin underline; `100` is a full-cell block. The caret is drawn by pixel inversion so moving/blinking a block caret restores the underlying character exactly.

The optional stock `console` command gives the same controls to the end user:

    console
    console fg <red> <green> <blue>
    console bg <red> <green> <blue>
    console colors <fr> <fg> <fb> <br> <bg> <bb>
    console caret <blink|visible|off>
    console caret size <1-100>

RGB components are decimal values from 0 to 255.

Ctrl+1/2/3 still force framebuffer buffering modes, and Alt+1/2/3 select font-size presets.

## Generated HAL routing

The generated `InputHardwareStartup` must not consume navigation keys. PS/2 and USB HID Page Up/Page Down are routed to retained framebuffer scrollback, while Up/Down/Left/Right/Home/End/Delete are encoded as terminal-editing input and delivered to the foreground shell process. This keeps generated OSes on the same input contract as Inu's central bootstrap path.
