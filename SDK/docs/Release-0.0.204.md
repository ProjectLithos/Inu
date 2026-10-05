# Inu 0.0.204

- Adds an ordinary userland desktop component and a separate ordinary userland login application.
- The desktop is intended to be visible before login; login is a GUI window above the desktop rather than a kernel/boot login screen.
- Adds the supplied dark hexagonal wallpaper as `INU-HEX.BMP` and stages `/System/Wallpapers` into the FAT32 system image and asset bundle.
- Adds 24/32-bit uncompressed BMP decoding and cover-scaling support to the userland GUI source.
- Adds userland `CreateDesktop` support using the existing GUI surface contract.
- Generated OS projects receive these userland sources through the existing canonical Userland workspace materialisation.

The actual authentication and process-launch bridge remains an OS-selected service boundary; the login presentation itself contains no kernel policy.
