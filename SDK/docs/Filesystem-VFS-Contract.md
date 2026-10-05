# Inu Filesystem VFS Contract

Inu 0.21.0 formalises a real virtual-filesystem boundary. The VFS is owned by `Inu.Kernel.Storage.KernelVfs`; individual filesystems are providers below it and do not define the process-visible filesystem API.

## Responsibilities

The VFS owns mount namespaces, mount-point routing, file and directory handles, synchronous I/O positioning, permission checks, provider capability discovery, and unmount safety. A filesystem driver owns on-disk interpretation and implements the callback table registered with `KernelVfs.RegisterFileSystem`.

## Mounts

`KernelVfs.Mount` associates a storage volume and a registered filesystem provider with an absolute path in a mount namespace. longest-prefix routing selects the active mount. Duplicate mount points within the same namespace are rejected. `Unmount` refuses to detach a filesystem while handles from that mount remain open.

## Files

`Open`, `Read`, `Write`, `Seek`, `Flush`, and `Close` are the synchronous file interface. The VFS owns the public handle and current position; providers receive their private cookie and the requested offset. VFS access checks happen before provider dispatch.

## Directories

`OpenDirectory`, `ReadDirectory`, `RewindDirectory`, and `CloseDirectory` provide a provider-independent enumeration contract. Directory names are copied into a caller-owned character buffer so the hot path does not require allocating managed strings. Entries report type, length and effective permissions.

## Permissions

`KernelFilePermissions` defines owner/group/other read, write and execute bits plus read-only, system and hidden attributes. Providers may implement `GetPermissions` and `SetPermissions`. The VFS checks effective read/write permission when opening a handle. A provider that cannot mutate permissions returns false from `SetPermissions` rather than silently pretending the change succeeded.

The FAT12/FAT16/FAT32 provider exposes read-only, hidden and system FAT metadata through this contract. chmod-style FAT metadata mutation is intentionally not implemented yet.

## Filesystem drivers

A driver supplies `KernelFileSystemCallbacks`: probe, mount/unmount, open, read/write, flush/close, directory enumeration and permission operations. `KernelFileSystemFeatures` advertises supported capabilities. FatFs is the first concrete provider using the complete VFS contract; future ext, ISO, network or synthetic filesystems plug in beneath the same interface.

## Async I/O

0.21.0 is intentionally synchronous. `KernelVfsIoModel.AsynchronousReserved` and `KernelFileSystemFeatures.AsyncIoReserved` reserve ABI vocabulary for later asynchronous request/complete APIs, but `KernelVfs.SupportsAsyncIo` returns false in this release. No synchronous API will be redefined when async I/O is added.


## 0.37.0 freestanding userland path API

The VFS exposes allocation-free normalized ASCII path calls for open, directory open, create file, create directory, delete file, remove empty directory and rename. These calls are intended for syscall/service boundaries where constructing managed `String` instances is inappropriate. Filesystem providers advertise `Create`, `Delete`, `Rename`, `Extend`, and `AsciiPaths` feature bits when implemented.
