# TrueTypeRenderer

Kath 0.30.0 sample showing the allocation-free TrueType API. A real kernel normally obtains `fontAddress`/`fontLength` from VFS or a packaged resource, allocates the reported workspace plus a coverage bitmap, and then blends the glyph into its framebuffer.
