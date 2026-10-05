# Inu 0.0.207

- Fixes CA2014 build failures introduced by the runtime VFAT long-file-name creation path.
- Moves the reusable 32-byte VFAT LFN entry buffer outside the long-name emission loop.
- Moves the reusable 13-byte generated short-alias display buffer outside the alias-attempt loop.
- Preserves the 0.0.206 runtime VFAT LFN creation behaviour without per-iteration stack allocation.
