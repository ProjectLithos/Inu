# Inu Filesystem - FatFs

This filesystem project is optional. Inu's base kernel installs no filesystem.

Add this project below `KernelProjects`, then after `KernelStorage.Initialize()` call:

```csharp
using Inu.Filesystem.FatFs;
if (!FatFs.Install()) return false;
```

Current profile: FAT12/FAT16/FAT32, VFAT long-file-name lookup, enumeration and creation, VFS seek/flush, file growth, and writable file/directory operations. Standard DOS 8.3 entries remain fully supported and are generated as aliases for long names. exFAT is not advertised yet.
