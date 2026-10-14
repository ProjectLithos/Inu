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
## Kernel-owned blank-disk provisioning

When an SDK user selects NVMe or AHCI for a QEMU target, QEMU supplies a persistent raw writable disk for that controller. Inu does not pre-partition or pre-format that image on the host. During kernel storage startup, a completely blank selected NVMe/AHCI disk is given a two-partition GPT layout. During filesystem startup, partitions created by that kernel boot are FAT32-formatted; partition 1 is seeded with `/USERS`, `/WORK`, and `/TEMP`, becomes the preferred writable root, and partition 2 is exposed as an additional mount. Existing GPT disks are preserved and are never reformatted merely because they are selected. Non-blank disks without a supported GPT are rejected rather than erased automatically.

