# Interactive framebuffer console

The framebuffer console retains output independently of the live viewport. Page Up and Page Down move the viewport through retained history; when shell editing resumes the editable-tail operation returns presentation to the live prompt.

The shell owns its current editable line and a bounded command-history ring. Up/Down selects prior commands, Left/Right moves the insertion position, Home/End jump to the edges, and Delete/Backspace edit around the current caret. The kernel receives replacement editable tails through `Get/Set/Event`; command names remain entirely outside the kernel.

The caret supports three visibility modes: blinking, continuously visible, and off. Its height is configurable from a one-pixel-style underline through a full character-body block. Caret rendering inverts framebuffer pixels rather than painting and erasing a solid rectangle, so the glyph beneath an in-line block caret is restored exactly.

Foreground and background colours are stored as SDK/user-selected 24-bit RGB policy and are repacked for the active framebuffer pixel format when necessary. Changing either colour redraws retained console text consistently.

A persistent vertical scrollbar occupies the right edge of the framebuffer console. Its thumb represents the visible portion of retained history. Text layout reserves the scrollbar strip so glyphs never overwrite it.

The console remains double-buffered by default under the automatic policy. Caret, scrolling and line-editing updates use the existing dirty-region presentation path rather than forcing an unconditional full-frame copy.
