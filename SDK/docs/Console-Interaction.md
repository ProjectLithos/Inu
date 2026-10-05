# Interactive framebuffer console

Inu renders a vertical caret at the active command insertion point. The caret blinks from the 1 ms console service tick and is hidden while the user is browsing scrollback.

A persistent vertical scrollbar occupies the right edge of the framebuffer console. Its thumb represents the visible portion of retained console history and moves as Up/Down changes the scrollback offset. The text layout reserves the scrollbar strip so glyphs never overwrite it.

The console remains double-buffered by default under the automatic policy. Caret and scrollbar changes are dirty-region updates rather than whole-frame redraws.
