# Linux kernel console fonts

Inu can install five optional Linux-kernel bitmap console faces as PSF2 assets:

- `linux-vga-8x8.psf` — VGA 8×8
- `linux-vga-8x16.psf` — VGA 8×16
- `linux-vga-6x11.psf` — VGA 6×11
- `linux-sun-8x16.psf` — Sun 8×16
- `linux-sun-12x22.psf` — Sun 12×22

Run `Install-InuFonts.bat` from the repository root. The installer downloads the exact upstream C font tables pinned to Linux commit `a13307e97d5c54b65720bb71fa379960ded1e51a`, installs the upstream source tables and converted PSF2 assets under `.toolchain/Fonts/LinuxKernel`, keeping generated/downloaded files out of the source tree.

These font assets are third-party GPL-2.0 material from the Linux kernel and are intentionally kept separate from Inu-owned source. See `THIRD-PARTY-NOTICES.md`.
