# Inu 0.0.205

- Adds VFAT long filename (LFN) directory-entry generation to the FAT32 system-image builder.
- Removes the image-builder requirement that staged system paths use DOS 8.3 names.
- Generates unique DOS short aliases only as FAT compatibility entries while preserving the real long filename in LFN entries.
- Reads VFAT long filenames when preserving mutable `/USERS` state from an existing image.
- Extends the Inu FAT provider to recognise long names for lookup and directory enumeration while retaining short-name compatibility.
- Keeps LFN entry sequences within one FAT sector in newly generated images so the freestanding reader can consume them deterministically.
- Fixes `/System/Wallpapers` so the GUI wallpaper can be staged using its natural long directory name.
