using System;
using Inu.Kernel.Drivers;

namespace Inu.Kernel.Storage;

public enum KernelStorageRegistryMode : Byte { Dynamic=0, Fixed=1 }
public enum KernelStorageDeviceKind : Byte { Unknown=0, Physical=1, Virtual=2, RamDisk=3, Optical=4, UsbMassStorage=5 }
public enum KernelPartitionScheme : Byte { None=0, Raw=1, Mbr=2, Gpt=3 }
public enum KernelFileSystemType : Byte { Unknown=0, Fat12=1, Fat16=2, Fat32=3, ExFat=4, Custom=255 }
public enum KernelFileType : Byte { Unknown=0, File=1, Directory=2, Device=3 }
public enum KernelFileAccess : Byte { Read=1, Write=2, ReadWrite=3 }
public enum KernelSeekOrigin : Byte { Begin=0, Current=1, End=2 }
public enum KernelBlockOperation : Byte { Read=1, Write=2, Flush=3 }
public enum KernelStorageRequestState : Byte { Pending=1, Completed=2, Failed=3 }

public readonly struct KernelStorageOptions
{
    public KernelStorageOptions(KernelStorageRegistryMode mode,UInt32 initialDevices,UInt32 initialVolumes,UInt32 initialProviders,UInt32 initialNamespaces,UInt32 initialMounts,UInt32 initialOpenFiles,UInt32 initialRequests,UInt32 maximumDevices,UInt32 maximumVolumes,UInt32 maximumProviders,UInt32 maximumNamespaces,UInt32 maximumMounts,UInt32 maximumOpenFiles,UInt32 maximumRequests)
    { RegistryMode=mode;InitialDevices=initialDevices;InitialVolumes=initialVolumes;InitialProviders=initialProviders;InitialNamespaces=initialNamespaces;InitialMounts=initialMounts;InitialOpenFiles=initialOpenFiles;InitialRequests=initialRequests;MaximumDevices=maximumDevices;MaximumVolumes=maximumVolumes;MaximumProviders=maximumProviders;MaximumNamespaces=maximumNamespaces;MaximumMounts=maximumMounts;MaximumOpenFiles=maximumOpenFiles;MaximumRequests=maximumRequests; }
    public KernelStorageRegistryMode RegistryMode { get; } public UInt32 InitialDevices { get; } public UInt32 InitialVolumes { get; } public UInt32 InitialProviders { get; } public UInt32 InitialNamespaces { get; } public UInt32 InitialMounts { get; } public UInt32 InitialOpenFiles { get; } public UInt32 InitialRequests { get; }
    public UInt32 MaximumDevices { get; } public UInt32 MaximumVolumes { get; } public UInt32 MaximumProviders { get; } public UInt32 MaximumNamespaces { get; } public UInt32 MaximumMounts { get; } public UInt32 MaximumOpenFiles { get; } public UInt32 MaximumRequests { get; }
    public static KernelStorageOptions DynamicDefault => new(KernelStorageRegistryMode.Dynamic,16U,32U,8U,4U,16U,64U,64U,UInt32.MaxValue,UInt32.MaxValue,UInt32.MaxValue,UInt32.MaxValue,UInt32.MaxValue,UInt32.MaxValue,UInt32.MaxValue);
    public static KernelStorageOptions Fixed(UInt32 devices,UInt32 volumes,UInt32 providers,UInt32 namespaces,UInt32 mounts,UInt32 openFiles,UInt32 requests)=>new(KernelStorageRegistryMode.Fixed,devices,volumes,providers,namespaces,mounts,openFiles,requests,devices,volumes,providers,namespaces,mounts,openFiles,requests);
}


public readonly struct KernelStorageDeviceHandle { public KernelStorageDeviceHandle(UInt32 value){Value=value;} public UInt32 Value { get; } }
public readonly struct KernelStorageVolumeHandle { public KernelStorageVolumeHandle(UInt32 value){Value=value;} public UInt32 Value { get; } }
public readonly struct KernelMountNamespaceHandle { public KernelMountNamespaceHandle(UInt32 value){Value=value;} public UInt32 Value { get; } }
public readonly struct KernelMountHandle { public KernelMountHandle(UInt32 value){Value=value;} public UInt32 Value { get; } }
public readonly struct KernelFileHandle { public KernelFileHandle(UInt32 value){Value=value;} public UInt32 Value { get; } }
public readonly struct KernelStorageRequestHandle { public KernelStorageRequestHandle(UInt32 value){Value=value;} public UInt32 Value { get; } }
public readonly struct KernelBlockRequest
{
    public KernelBlockRequest(KernelStorageDeviceHandle device,KernelBlockOperation operation,UInt64 firstBlock,UInt32 blockCount,UInt64 bufferAddress,UInt32 bufferBytes,UInt64 cookie) { Device=device;Operation=operation;FirstBlock=firstBlock;BlockCount=blockCount;BufferAddress=bufferAddress;BufferBytes=bufferBytes;Cookie=cookie; }
    public KernelStorageDeviceHandle Device { get; } public KernelBlockOperation Operation { get; } public UInt64 FirstBlock { get; } public UInt32 BlockCount { get; } public UInt64 BufferAddress { get; } public UInt32 BufferBytes { get; } public UInt64 Cookie { get; }
}
public readonly struct KernelStorageRequestInfo
{
    public KernelStorageRequestInfo(KernelStorageRequestHandle handle,KernelStorageRequestState state,KernelBlockRequest request) { Handle=handle;State=state;Request=request; }
    public KernelStorageRequestHandle Handle { get; } public KernelStorageRequestState State { get; } public KernelBlockRequest Request { get; }
}

public readonly struct KernelStorageGeometry
{
    public KernelStorageGeometry(UInt32 logicalBlockSize,UInt32 physicalBlockSize,UInt64 blockCount,Boolean readOnly,Boolean removable)
    { LogicalBlockSize=logicalBlockSize;PhysicalBlockSize=physicalBlockSize;BlockCount=blockCount;ReadOnly=readOnly;Removable=removable; }
    public UInt32 LogicalBlockSize { get; }
    public UInt32 PhysicalBlockSize { get; }
    public UInt64 BlockCount { get; }
    public Boolean ReadOnly { get; }
    public Boolean Removable { get; }
    public UInt64 CapacityBytes => (UInt64)LogicalBlockSize*BlockCount;
}

public readonly struct KernelPartitionInfo
{
    public KernelPartitionInfo(KernelPartitionScheme scheme,UInt32 index,UInt64 firstBlock,UInt64 blockCount,UInt32 mbrType,UInt64 typeGuidLow,UInt64 typeGuidHigh)
    { Scheme=scheme;Index=index;FirstBlock=firstBlock;BlockCount=blockCount;MbrType=mbrType;TypeGuidLow=typeGuidLow;TypeGuidHigh=typeGuidHigh; }
    public KernelPartitionScheme Scheme { get; }
    public UInt32 Index { get; }
    public UInt64 FirstBlock { get; }
    public UInt64 BlockCount { get; }
    public UInt32 MbrType { get; }
    public UInt64 TypeGuidLow { get; }
    public UInt64 TypeGuidHigh { get; }
}

public readonly struct KernelStorageCapabilities
{
    public KernelStorageCapabilities(Boolean initialized,KernelStorageRegistryMode mode,UInt32 devices,UInt32 volumes,UInt32 mounts,UInt32 openFiles,UInt32 queuedRequests,UInt32 deviceCapacity,UInt32 volumeCapacity,UInt32 mountCapacity,UInt32 openFileCapacity,UInt32 requestCapacity)
    { Initialized=initialized;RegistryMode=mode;Devices=devices;Volumes=volumes;Mounts=mounts;OpenFiles=openFiles;QueuedRequests=queuedRequests;DeviceCapacity=deviceCapacity;VolumeCapacity=volumeCapacity;MountCapacity=mountCapacity;OpenFileCapacity=openFileCapacity;RequestCapacity=requestCapacity; }
    public Boolean Initialized { get; }
    public KernelStorageRegistryMode RegistryMode { get; }
    public UInt32 Devices { get; }
    public UInt32 Volumes { get; }
    public UInt32 Mounts { get; }
    public UInt32 OpenFiles { get; }
    public UInt32 QueuedRequests { get; }
    public UInt32 DeviceCapacity { get; }
    public UInt32 VolumeCapacity { get; }
    public UInt32 MountCapacity { get; }
    public UInt32 OpenFileCapacity { get; }
    public UInt32 RequestCapacity { get; }
}

public readonly unsafe struct KernelBlockDeviceCallbacks
{
    public readonly delegate*<UInt64,UInt32,Byte*,UInt32,Boolean> ReadBlocks;
    public readonly delegate*<UInt64,UInt32,Byte*,UInt32,Boolean> WriteBlocks;
    public readonly delegate*<Boolean> Flush;
    public KernelBlockDeviceCallbacks(delegate*<UInt64,UInt32,Byte*,UInt32,Boolean> readBlocks,delegate*<UInt64,UInt32,Byte*,UInt32,Boolean> writeBlocks,delegate*<Boolean> flush)
    { ReadBlocks=readBlocks;WriteBlocks=writeBlocks;Flush=flush; }
}

/// <summary>Provides block callbacks that receive the owning generic device, allowing one driver implementation to serve multiple devices.</summary>
public readonly unsafe struct KernelContextualBlockDeviceCallbacks
{
    /// <summary>Reads blocks for the supplied generic device.</summary>
    public readonly delegate*<KernelDeviceHandle,UInt64,UInt32,Byte*,UInt32,Boolean> ReadBlocks;
    /// <summary>Writes blocks for the supplied generic device.</summary>
    public readonly delegate*<KernelDeviceHandle,UInt64,UInt32,Byte*,UInt32,Boolean> WriteBlocks;
    /// <summary>Flushes the supplied generic device.</summary>
    public readonly delegate*<KernelDeviceHandle,Boolean> Flush;
    /// <summary>Creates contextual block callbacks.</summary>
    public KernelContextualBlockDeviceCallbacks(delegate*<KernelDeviceHandle,UInt64,UInt32,Byte*,UInt32,Boolean> readBlocks,delegate*<KernelDeviceHandle,UInt64,UInt32,Byte*,UInt32,Boolean> writeBlocks,delegate*<KernelDeviceHandle,Boolean> flush)
    { ReadBlocks=readBlocks;WriteBlocks=writeBlocks;Flush=flush; }
}

[Flags]
public enum KernelFilePermissions : UInt32
{
    None=0,OwnerRead=1U<<0,OwnerWrite=1U<<1,OwnerExecute=1U<<2,
    GroupRead=1U<<3,GroupWrite=1U<<4,GroupExecute=1U<<5,
    OtherRead=1U<<6,OtherWrite=1U<<7,OtherExecute=1U<<8,
    ReadOnly=1U<<16,System=1U<<17,Hidden=1U<<18
}

[Flags]
public enum KernelFileSystemFeatures : UInt32
{
    None=0,Read=1U<<0,Write=1U<<1,Directories=1U<<2,Permissions=1U<<3,
    CaseSensitive=1U<<4,RemovableMedia=1U<<5,Create=1U<<6,Delete=1U<<7,
    Rename=1U<<8,Extend=1U<<9,AsciiPaths=1U<<10,AsyncIoReserved=1U<<31
}

public enum KernelVfsIoModel : Byte { Synchronous=1,AsynchronousReserved=2 }

public readonly struct KernelDirectoryHandle { public KernelDirectoryHandle(UInt32 value){Value=value;} public UInt32 Value { get; } }

public readonly struct KernelVfsProviderInfo
{
    public KernelVfsProviderInfo(KernelFileSystemType type,KernelFileSystemFeatures features,UInt32 providerId)
    { Type=type;Features=features;ProviderId=providerId; }
    public KernelFileSystemType Type { get; }
    public KernelFileSystemFeatures Features { get; }
    public UInt32 ProviderId { get; }
}

public readonly struct KernelVfsMountInfo
{
    public KernelVfsMountInfo(KernelMountHandle handle,KernelMountNamespaceHandle mountNamespace,KernelStorageVolumeHandle volume,KernelFileSystemType fileSystemType,UInt32 pathLength)
    { Handle=handle;MountNamespace=mountNamespace;Volume=volume;FileSystemType=fileSystemType;PathLength=pathLength; }
    public KernelMountHandle Handle { get; }
    public KernelMountNamespaceHandle MountNamespace { get; }
    public KernelStorageVolumeHandle Volume { get; }
    public KernelFileSystemType FileSystemType { get; }
    public UInt32 PathLength { get; }
}

/// <summary>Callbacks supplied by a filesystem driver beneath the VFS.</summary>
public readonly unsafe struct KernelFileSystemCallbacks
{
    public readonly delegate*<KernelStorageVolumeHandle,Boolean> Probe;
    public readonly delegate*<KernelStorageVolumeHandle,UInt64*,Boolean> Mount;
    public readonly delegate*<UInt64,Boolean> Unmount;
    public readonly delegate*<UInt64,String,UInt32,KernelFileAccess,UInt64*,KernelFileType*,UInt64*,Boolean> Open;
    public readonly delegate*<UInt64,UInt64,Byte*,UInt32,UInt32*,Boolean> Read;
    public readonly delegate*<UInt64,UInt64,Byte*,UInt32,UInt32*,Boolean> Write;
    public readonly delegate*<UInt64,Boolean> Flush;
    public readonly delegate*<UInt64,Boolean> Close;
    public readonly delegate*<UInt64,UInt64,Char*,UInt32,UInt32*,KernelFileType*,UInt64*,KernelFilePermissions*,Boolean> ReadDirectory;
    public readonly delegate*<UInt64,String,UInt32,KernelFilePermissions*,Boolean> GetPermissions;
    public readonly delegate*<UInt64,String,UInt32,KernelFilePermissions,Boolean> SetPermissions;

    // Allocation-free path callbacks used by freestanding userland services. Paths are
    // normalized absolute ASCII paths using '/'. pathOffset is the VFS mount-prefix length.
    public readonly delegate*<UInt64,Byte*,UInt32,UInt32,KernelFileAccess,UInt64*,KernelFileType*,UInt64*,Boolean> OpenAscii;
    public readonly delegate*<UInt64,Byte*,UInt32,UInt32,Boolean,Boolean> CreateFileAscii;
    public readonly delegate*<UInt64,Byte*,UInt32,UInt32,Boolean> CreateDirectoryAscii;
    public readonly delegate*<UInt64,Byte*,UInt32,UInt32,Boolean> DeleteFileAscii;
    public readonly delegate*<UInt64,Byte*,UInt32,UInt32,Boolean> RemoveDirectoryAscii;
    public readonly delegate*<UInt64,Byte*,UInt32,UInt32,Byte*,UInt32,UInt32,Boolean> RenameAscii;
    public readonly KernelFileSystemFeatures Features;

    public KernelFileSystemCallbacks(
        delegate*<KernelStorageVolumeHandle,Boolean> probe,
        delegate*<KernelStorageVolumeHandle,UInt64*,Boolean> mount,
        delegate*<UInt64,Boolean> unmount,
        delegate*<UInt64,String,UInt32,KernelFileAccess,UInt64*,KernelFileType*,UInt64*,Boolean> open,
        delegate*<UInt64,UInt64,Byte*,UInt32,UInt32*,Boolean> read,
        delegate*<UInt64,UInt64,Byte*,UInt32,UInt32*,Boolean> write,
        delegate*<UInt64,Boolean> flush,
        delegate*<UInt64,Boolean> close)
        : this(probe,mount,unmount,open,read,write,flush,close,null,null,null,null,null,null,null,null,null,
            KernelFileSystemFeatures.Read|(write==null?KernelFileSystemFeatures.None:KernelFileSystemFeatures.Write)) { }

    public KernelFileSystemCallbacks(
        delegate*<KernelStorageVolumeHandle,Boolean> probe,
        delegate*<KernelStorageVolumeHandle,UInt64*,Boolean> mount,
        delegate*<UInt64,Boolean> unmount,
        delegate*<UInt64,String,UInt32,KernelFileAccess,UInt64*,KernelFileType*,UInt64*,Boolean> open,
        delegate*<UInt64,UInt64,Byte*,UInt32,UInt32*,Boolean> read,
        delegate*<UInt64,UInt64,Byte*,UInt32,UInt32*,Boolean> write,
        delegate*<UInt64,Boolean> flush,
        delegate*<UInt64,Boolean> close,
        delegate*<UInt64,UInt64,Char*,UInt32,UInt32*,KernelFileType*,UInt64*,KernelFilePermissions*,Boolean> readDirectory,
        delegate*<UInt64,String,UInt32,KernelFilePermissions*,Boolean> getPermissions,
        delegate*<UInt64,String,UInt32,KernelFilePermissions,Boolean> setPermissions,
        KernelFileSystemFeatures features)
        : this(probe,mount,unmount,open,read,write,flush,close,readDirectory,getPermissions,setPermissions,null,null,null,null,null,null,features) { }

    public KernelFileSystemCallbacks(
        delegate*<KernelStorageVolumeHandle,Boolean> probe,
        delegate*<KernelStorageVolumeHandle,UInt64*,Boolean> mount,
        delegate*<UInt64,Boolean> unmount,
        delegate*<UInt64,String,UInt32,KernelFileAccess,UInt64*,KernelFileType*,UInt64*,Boolean> open,
        delegate*<UInt64,UInt64,Byte*,UInt32,UInt32*,Boolean> read,
        delegate*<UInt64,UInt64,Byte*,UInt32,UInt32*,Boolean> write,
        delegate*<UInt64,Boolean> flush,
        delegate*<UInt64,Boolean> close,
        delegate*<UInt64,UInt64,Char*,UInt32,UInt32*,KernelFileType*,UInt64*,KernelFilePermissions*,Boolean> readDirectory,
        delegate*<UInt64,String,UInt32,KernelFilePermissions*,Boolean> getPermissions,
        delegate*<UInt64,String,UInt32,KernelFilePermissions,Boolean> setPermissions,
        delegate*<UInt64,Byte*,UInt32,UInt32,KernelFileAccess,UInt64*,KernelFileType*,UInt64*,Boolean> openAscii,
        delegate*<UInt64,Byte*,UInt32,UInt32,Boolean,Boolean> createFileAscii,
        delegate*<UInt64,Byte*,UInt32,UInt32,Boolean> createDirectoryAscii,
        delegate*<UInt64,Byte*,UInt32,UInt32,Boolean> deleteFileAscii,
        delegate*<UInt64,Byte*,UInt32,UInt32,Boolean> removeDirectoryAscii,
        delegate*<UInt64,Byte*,UInt32,UInt32,Byte*,UInt32,UInt32,Boolean> renameAscii,
        KernelFileSystemFeatures features)
    {
        Probe=probe;Mount=mount;Unmount=unmount;Open=open;Read=read;Write=write;Flush=flush;Close=close;
        ReadDirectory=readDirectory;GetPermissions=getPermissions;SetPermissions=setPermissions;
        OpenAscii=openAscii;CreateFileAscii=createFileAscii;CreateDirectoryAscii=createDirectoryAscii;
        DeleteFileAscii=deleteFileAscii;RemoveDirectoryAscii=removeDirectoryAscii;RenameAscii=renameAscii;Features=features;
    }
}

public readonly struct KernelVfsFileInfo
{
    public KernelVfsFileInfo(KernelFileHandle handle,KernelFileType type,UInt64 length,UInt64 position,KernelFileAccess access,KernelFilePermissions permissions)
    { Handle=handle;Type=type;Length=length;Position=position;Access=access;Permissions=permissions; }
    public KernelFileHandle Handle { get; }
    public KernelFileType Type { get; }
    public UInt64 Length { get; }
    public UInt64 Position { get; }
    public KernelFileAccess Access { get; }
    public KernelFilePermissions Permissions { get; }
}
