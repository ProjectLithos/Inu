# Inu TrueType renderer

Kath 0.30.0 adds an allocation-free TrueType (`.ttf`) parser and glyph renderer to the SDK in `Inu.Kernel.TrueType`.

## Scope

`KernelTrueType` accepts a font already resident in kernel-accessible memory. It validates the SFNT directory and the required `head`, `maxp`, `hhea`, `hmtx`, `loca`, `glyf`, and `cmap` tables before exposing the face. The renderer supports TrueType quadratic `glyf` outlines, simple glyphs, the common XY-positioned composite-glyph form, Unicode BMP cmap format 4, full-Unicode cmap format 12, horizontal metrics, and legacy `kern` format-0 pairs.

The renderer deliberately does not allocate from the managed heap. The caller asks `TryGetRasterWorkspace` for the scratch requirement and supplies both the scratch region and an 8-bit coverage bitmap. This makes the same implementation usable during kernel boot, in drivers, in the console, and later in the GUI compositor.

## Public API

- `KernelTrueType.TryInspect` validates a memory-resident font and reports global metrics/capabilities.
- `TryGetGlyphIndex` maps a Unicode scalar value to a glyph.
- `TryGetGlyphMetrics` reports scaled pixel metrics.
- `TryGetRasterWorkspace` reports caller-owned scratch memory requirements.
- `TryRasterizeGlyph` produces an anti-aliased 8-bit coverage bitmap using 4x4 supersampling.
- `BlendCoverage32` alpha-composites that bitmap onto a packed 32-bit framebuffer.
- `TryGetKerning` exposes legacy TrueType `kern` pairs.
- `TryMeasureText` measures UTF-16 strings including surrogate pairs.

`pixelHeight` is the requested em height. Metrics and raster output are derived from `unitsPerEm`, so a single TTF face can be rendered at different sizes without pre-generated bitmap fonts.

## Rendering model

Glyph contours are decoded from `glyf`, quadratic curves are flattened into line edges in caller-provided scratch space, and the non-zero winding rule is evaluated at 16 samples per output pixel. Coverage values are 0–255. The 32-bit compositor blends RGB while preserving the destination high byte.

The renderer is pixel-format neutral at the raster stage. `BlendCoverage32` is a convenience path for the existing 32-bit Inu framebuffer; future graphics compositors can consume the coverage buffer themselves.

## Font loading

The renderer does not define filesystem policy. A kernel or service can load a `.ttf` through the VFS, package a font as a resource, or receive a memory address from a boot/application resource loader. The only requirement is that the font bytes remain resident while the renderer uses them.

## Deliberate boundaries

OpenType/CFF outlines are not TrueType `glyf` outlines and are not accepted by this renderer. GPOS kerning/substitution, shaping, bidirectional layout, hint bytecode execution, variable-font axes, colour glyphs, and complex-script shaping are separate higher-level concerns. Unsupported composite glyphs that use point-to-point attachment rather than XY component placement are rejected rather than rendered incorrectly.

Inu therefore treats this as the scalable TrueType raster layer, not as a complete text-layout engine. HarfBuzz-like shaping or a Inu-native shaping service can be layered above it later without changing the raster contract.
