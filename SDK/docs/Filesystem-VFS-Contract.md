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

## 0.0.71 OS-author path syntax policy

`FileSystem.SetPathPolicy(FileSystemPathPolicy)` makes the external userland path syntax an OS-author policy rather than a hard-coded `/` convention. The policy controls the external separator, case-sensitivity requirement, maximum component length, whether spaces and numbers are permitted, and an additional invalid-character set.

The VFS keeps an internal canonical `/` representation so filesystem drivers do not need to be rewritten for each user-facing separator. The userland syscall boundary normalizes the selected separator to that canonical form before VFS dispatch and converts process current-directory paths back to the selected external separator on return. A non-selected joiner is rejected rather than silently accepted.

Case sensitivity is only accepted when every mounted provider advertises matching semantics. This prevents the kernel API from claiming case-sensitive behaviour over a filesystem provider that cannot provide it.

The supplied shell discovers the active separator from its process current directory before constructing `/System/Commands`-equivalent paths, so changing the external joiner does not silently break command lookup.

Example:

```csharp
FileSystem.SetPathPolicy(new FileSystemPathPolicy(
    ':',          // external joiner
    false,        // case-insensitive
    20,           // maximum component length
    false,        // spaces not allowed
    true,         // numbers allowed
    "*?<>|"));    // additional invalid characters
```

With that policy, a userland absolute path is written as `:User:Dave:notes.txt`; the provider still receives the canonical VFS equivalent internally.

## 0.0.72 logical path categories

The OS author can map stable filesystem purposes to concrete paths with `FileSystem.SetLogicalPath`. Inu does not assume that users, fonts, commands, applications, libraries, or temporary data live under any particular directory name.

The initial categories are `ReadableUser`, `WritableUser`, `VisibleUser`, `ReadableFonts`, `WritableFonts`, `VisibleFonts`, `Commands`, `Applications`, `Libraries`, and `Temporary`. A mapping may contain policy text such as `{user}`; Inu stores that text but does not invent user-identity expansion before an identity/session policy exists.

Logical mappings are location policy only. They do not grant read/write/visibility permissions by themselves.

The supplied shell now obtains `FileSystemLogicalPath.Commands` through Get(`filesystem.logical-path`) and therefore no longer hard-codes `System/Commands`. The compatibility default remains `/System/Commands` until the OS author replaces or clears it.

## 0.0.81 path and command-search SDK surface

Userland code no longer needs to send `filesystem.*` Get/Set messages directly. `Inu.Userland.Runtime.FileSystemPaths` exposes `GetPathSeparator`, `SetPathSeparator`, `GetCommandsPath`, `GetCommandsPaths`, `SetCommandsPath`, `SetCommandsPaths`, and `BuildCommandsPath`.

Command directories are now an ordered search list rather than one fixed location. `BuildCommandsPath(command)` returns executable candidates for each configured directory in search order, including `.EXE` and extensionless forms. The compatibility default remains `/System/Commands` until OS policy changes it.
