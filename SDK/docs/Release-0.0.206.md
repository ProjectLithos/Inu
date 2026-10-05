# Inu 0.0.206

- Completes runtime VFAT long-file-name creation for FAT12/FAT16/FAT32 VFS writes.
- `mkdir` and file creation can now materialise names beyond DOS 8.3 using standard VFAT LFN entries plus generated short aliases.
- Preserves the 0.0.205 image-builder LFN support and long-name lookup/enumeration.

The graphical desktop/login autostart remains a separate incomplete path; 0.0.206 fixes the filesystem failure demonstrated by runtime `mkdir`.
