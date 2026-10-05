# Third-party notices

## DejaVu Sans Mono Bold 2.37

Inu Mono is a renamed monochrome bitmap rasterisation derived from DejaVu Sans Mono Bold 2.37. The original TrueType font file is not included in Inu.

Fonts are (c) Bitstream (see below). DejaVu changes are in public domain.

### Bitstream Vera Fonts Copyright

Copyright (c) 2003 by Bitstream, Inc. All Rights Reserved. Bitstream Vera is a trademark of Bitstream, Inc.

Permission is hereby granted, free of charge, to any person obtaining a copy of the fonts accompanying this license ("Fonts") and associated documentation files (the "Font Software"), to reproduce and distribute the Font Software, including without limitation the rights to use, copy, merge, publish, distribute, and/or sell copies of the Font Software, and to permit persons to whom the Font Software is furnished to do so, subject to the following conditions:

The above copyright and trademark notices and this permission notice shall be included in all copies of one or more of the Font Software typefaces.

The Font Software may be modified, altered, or added to, and in particular the designs of glyphs or characters in the Fonts may be modified and additional glyphs or characters may be added to the Fonts, only if the fonts are renamed to names not containing either the words "Bitstream" or the word "Vera".

This License becomes null and void to the extent applicable to Fonts or Font Software that has been modified and is distributed under the "Bitstream Vera" names.

The Font Software may be sold as part of a larger software package but no copy of one or more of the Font Software typefaces may be sold by itself.

THE FONT SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO ANY WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT OF COPYRIGHT, PATENT, TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL BITSTREAM OR THE GNOME FOUNDATION BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, INCLUDING ANY GENERAL, SPECIAL, INDIRECT, INCIDENTAL, OR CONSEQUENTIAL DAMAGES, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF THE USE OR INABILITY TO USE THE FONT SOFTWARE OR FROM OTHER DEALINGS IN THE FONT SOFTWARE.

Except as contained in this notice, the names of Gnome, the Gnome Foundation, and Bitstream Inc., shall not be used in advertising or otherwise to promote the sale, use or other dealings in this Font Software without prior written authorization from the Gnome Foundation or Bitstream Inc., respectively. For further information, contact: fonts at gnome dot org.

## Inu distribution note

Inu 0.0.97 reissues this notice as part of the incremental ChangedFiles payload. The embedded framebuffer glyph data continues to carry the DejaVu/Bitstream provenance and licensing terms above; reissuing the notice repairs installations where a prior incremental archive did not contain this required repository-root file.

## Linux kernel console fonts

Inu's optional Linux-kernel console font pack obtains the following bitmap font tables from the upstream Linux kernel and converts them byte-for-byte into PSF2 glyph storage for the Inu framebuffer console: VGA 8x8, VGA 8x16, VGA 6x11, Sun 8x16, and Sun 12x22.

Upstream repository: `https://github.com/torvalds/linux`

Pinned upstream commit: `a13307e97d5c54b65720bb71fa379960ded1e51a`

Upstream paths: `lib/fonts/font_8x8.c`, `font_8x16.c`, `font_6x11.c`, `font_sun8x16.c`, and `font_sun12x22.c`.

Each of these upstream source files identifies its license as `GPL-2.0`. The downloaded source tables and generated PSF2 files remain GPL-2.0 third-party material. They are installed under `.toolchain/Fonts/LinuxKernel` and are not represented as Inu-owned font data.

## FatFs

Inu 0.35.21 includes a selectable C#/.NET-compatible port/adaptation of the FatFs filesystem model and portability boundary. FatFs is Copyright (C) ChaN and is distributed under its permissive BSD-style license. The selectable project carries `LICENSE-FatFs.txt`. The base Inu kernel does not include or install the FatFs provider unless the end user selects it.
