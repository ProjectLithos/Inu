# Storage and filesystems

Inu separates **block storage**, **VFS**, and **filesystem implementations**.

`Inu.Kernel.Storage` owns generic block-device registration, MBR/GPT volume discovery, namespaces, mount points and file-handle dispatch. It contains no FAT, ext, NTFS or other filesystem-format implementation.

## End-user selection

The base kernel initializes:

```csharp
if (!KernelStorage.Initialize()) return false;
```

and installs no filesystem automatically.

A user selects a filesystem by adding a kernel-side filesystem project below `KernelProjects` (or deliberately linking it elsewhere) and explicitly installing that provider.

For FatFs:

```csharp
using Inu.Filesystem.FatFs;

if (!KernelStorage.Initialize()) return false;
if (!FatFs.Install()) return false;
```

The Visual Studio SDK supplies **Inu Filesystem - FatFs** as an independent project template.

## FatFs 0.35.21 profile

The C#/.NET-compatible provider exposes FAT12, FAT16 and FAT32 through generic Inu VFS/block-device callbacks. In 0.37.0 it supports allocation-free ASCII path traversal, reads, seek/flush, file growth, file create/truncate, file deletion, directory create/remove, and rename/move. FAT updates are mirrored across all FAT copies.

Mutation currently creates FAT 8.3 short names. Long-file-name mutation and exFAT remain optional filesystem-provider work rather than hidden base-kernel functionality.
