using System;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Storage;

/// <summary>
/// Inu virtual filesystem. Owns namespaces, mount routing, handles, permissions and
/// synchronous file/directory I/O. Filesystem implementations are providers below this layer.
/// </summary>
public static unsafe partial class KernelVfs
{
    private struct ProviderRecord
    {
        internal Byte Used,Type;
        internal UInt32 Features;
        internal UInt64 Probe,Mount,Unmount,Open,Read,Write,Flush,Close,ReadDirectory,GetPermissions,SetPermissions;
        internal UInt64 OpenAscii,CreateFileAscii,CreateDirectoryAscii,DeleteFileAscii,RemoveDirectoryAscii,RenameAscii;
    }
    private struct NamespaceRecord { internal Byte Used; }
    private struct MountRecord
    {
        internal Byte Used;
        internal UInt32 Namespace,Volume,Provider,PathLength;
        internal UInt64 MountCookie;
        internal KernelHeapAllocation PathAllocation;
    }
    private struct FileRecord
    {
        internal Byte Used,Type,Access;
        internal UInt32 Provider,Mount,Permissions;
        internal UInt64 Cookie,Position,Length,DirectoryIndex;
    }

    private static ProviderRecord* _providers;
    private static NamespaceRecord* _namespaces;
    private static MountRecord* _mounts;
    private static FileRecord* _files;
    private static KernelHeapAllocation _providerAllocation,_namespaceAllocation,_mountAllocation,_fileAllocation;
    private static UInt32 _providerCapacity,_namespaceCapacity,_mountCapacity,_fileCapacity,_providerCount,_namespaceCount,_mountCount,_openFileCount;
    private static UInt32 _maximumProviders,_maximumNamespaces,_maximumMounts,_maximumOpenFiles;
    private static KernelStorageRegistryMode _mode;
    private static Boolean _initialized;

    public static UInt32 ProviderCount=>_providerCount;
    public static UInt32 MountCount=>_mountCount;
    public static UInt32 OpenFileCount=>_openFileCount;
    public static UInt32 MountCapacity=>_mountCapacity;
    public static UInt32 OpenFileCapacity=>_fileCapacity;
    public static KernelMountNamespaceHandle DefaultNamespace=>new(1U);
    public static KernelVfsIoModel IoModel=>KernelVfsIoModel.Synchronous;
    public static Boolean SupportsAsyncIo=>false;

    internal static Boolean Initialize(KernelStorageOptions options)
    {
        if(_initialized)return true;
        _mode=options.RegistryMode;
        _providerCapacity=options.InitialProviders;_namespaceCapacity=options.InitialNamespaces;
        _maximumProviders=options.MaximumProviders;_maximumNamespaces=options.MaximumNamespaces;
        _maximumMounts=options.MaximumMounts;_maximumOpenFiles=options.MaximumOpenFiles;
        if(!AllocProviders(_providerCapacity,out _providerAllocation,out _providers)||
           !AllocNamespaces(_namespaceCapacity,out _namespaceAllocation,out _namespaces)||
           !AllocMounts(options.InitialMounts,out _mountAllocation,out _mounts)||
           !AllocFiles(options.InitialOpenFiles,out _fileAllocation,out _files))return false;
        _mountCapacity=options.InitialMounts;_fileCapacity=options.InitialOpenFiles;
        _namespaces->Used=1;_namespaceCount=1;_initialized=true;KernelPathIndex.Initialize();return true;
    }

    public static Boolean IsInitialized()=>_initialized;

    public static Boolean CreateNamespace(out KernelMountNamespaceHandle handle)
    {
        handle=default;if(!_initialized)return false;Int32 slot=FreeNamespace();
        if(slot<0){if(!GrowNamespaces())return false;slot=FreeNamespace();}
        (_namespaces+slot)->Used=1;_namespaceCount++;handle=new KernelMountNamespaceHandle((UInt32)slot+1U);return true;
    }

    /// <summary>Registers a filesystem driver beneath the VFS.</summary>
    public static Boolean RegisterFileSystem(KernelFileSystemType type,KernelFileSystemCallbacks callbacks)
    {
        if(!_initialized||type==KernelFileSystemType.Unknown||callbacks.Probe==null||callbacks.Mount==null||callbacks.Unmount==null||callbacks.Open==null||callbacks.Read==null||callbacks.Close==null)return false;
        if(FindProvider(type)>=0)return false;
        Int32 slot=FreeProvider();if(slot<0){if(!GrowProviders())return false;slot=FreeProvider();}
        ProviderRecord* p=_providers+slot;
        p->Used=1;p->Type=(Byte)type;p->Features=(UInt32)callbacks.Features;
        p->Probe=(UInt64)(void*)callbacks.Probe;p->Mount=(UInt64)(void*)callbacks.Mount;p->Unmount=(UInt64)(void*)callbacks.Unmount;
        p->Open=(UInt64)(void*)callbacks.Open;p->Read=(UInt64)(void*)callbacks.Read;p->Write=(UInt64)(void*)callbacks.Write;
        p->Flush=(UInt64)(void*)callbacks.Flush;p->Close=(UInt64)(void*)callbacks.Close;
        p->ReadDirectory=(UInt64)(void*)callbacks.ReadDirectory;p->GetPermissions=(UInt64)(void*)callbacks.GetPermissions;p->SetPermissions=(UInt64)(void*)callbacks.SetPermissions;
        p->OpenAscii=(UInt64)(void*)callbacks.OpenAscii;p->CreateFileAscii=(UInt64)(void*)callbacks.CreateFileAscii;p->CreateDirectoryAscii=(UInt64)(void*)callbacks.CreateDirectoryAscii;
        p->DeleteFileAscii=(UInt64)(void*)callbacks.DeleteFileAscii;p->RemoveDirectoryAscii=(UInt64)(void*)callbacks.RemoveDirectoryAscii;p->RenameAscii=(UInt64)(void*)callbacks.RenameAscii;
        _providerCount++;return true;
    }

    public static Boolean TryGetProviderInfo(KernelFileSystemType type,out KernelVfsProviderInfo info)
    {
        info=default;Int32 i=FindProvider(type);if(i<0)return false;ProviderRecord* p=_providers+i;
        info=new KernelVfsProviderInfo(type,(KernelFileSystemFeatures)p->Features,(UInt32)i+1U);return true;
    }

}
