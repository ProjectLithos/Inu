# Framebuffer font

Inu framebuffer consoles use the embedded **Inu Mono** bitmap typeface.

## Coverage

The font contains the complete printable ASCII range, U+0020 through U+007E. Unsupported characters use a visible question-mark replacement glyph rather than an indistinguishable generic block.

## Font size

The renderer has one authoritative rendered-size input: `FontSize`. It is the glyph height in framebuffer pixels. The default is 16 pixels.

The embedded monochrome raster master is 8 pixels wide by 16 pixels high. For any supported `FontSize` from 8 through 128 pixels, the renderer derives the actual glyph width, character advance, line height, source row, and source column from `FontSize`. It does not expose or apply a second scale value.

At the default `FontSize` of 16 pixels, the resulting measurements are:

- rendered glyph width: 8 pixels;
- rendered glyph height: 16 pixels;
- character advance: 10 pixels;
- line height: 20 pixels.

Generated freestanding kernels pass an explicit 16-pixel size to `KernelConsole.Initialize(boot, 16U)`. Kernel authors can choose any supported pixel height through the overload, and `KernelConsole.FontSize` reports the exact value the renderer accepted. The reusable `Inu.Console.Framebuffer` assembly accepts the same exact pixel height through `FramebufferConfiguration.FontSize` and reports it through `FramebufferConsole.FontSize`.

## Descenders and baseline

Lowercase `g`, `j`, `p`, `q`, and `y` use the final rows of the raster master. The renderer samples every rendered row from the complete glyph, so descenders remain visible at every supported font size.

## Kernel compatibility

The freestanding implementation stores each source glyph as two packed 64-bit halves. It does not allocate arrays, require a garbage collector, load a font file, or call firmware after `ExitBootServices`. The reusable managed framebuffer assembly uses the same packed glyph values.

## Copies kept in sync

The source-policy tests require the authoritative freestanding font and renderer to be byte-identical to the command-line and Visual Studio template copies. They also compare the packed glyph values against the reusable framebuffer assembly and reject any reintroduction of a separate public scale property.

## Rendered specimen

`docs/assets/Inu-Mono-8x16.png` is the unscaled raster-master specimen and now matches the default 16-pixel glyph height. `docs/assets/Inu-Mono-32px.png` remains as a larger supported-size specimen.
