using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Heap;
using Inu.Kernel.Storage;

namespace Inu.Filesystem.FatFs;

public enum FatFsFormat : Byte { Unknown=0, Fat12=12, Fat16=16, Fat32=32 }

public readonly struct FatFsCapabilities
{
    public FatFsCapabilities(Boolean installed,Boolean fat12,Boolean fat16,Boolean fat32,Boolean exFat,Boolean longFileNames,Boolean read,Boolean write,Boolean extend,UInt32 maximumSectorSize)
    { Installed=installed;Fat12=fat12;Fat16=fat16;Fat32=fat32;ExFat=exFat;LongFileNames=longFileNames;Read=read;Write=write;Extend=extend;MaximumSectorSize=maximumSectorSize; }
    public Boolean Installed { get; } public Boolean Fat12 { get; } public Boolean Fat16 { get; } public Boolean Fat32 { get; }
    public Boolean ExFat { get; } public Boolean LongFileNames { get; } public Boolean Read { get; } public Boolean Write { get; }
    public Boolean Extend { get; } public UInt32 MaximumSectorSize { get; }
}

/// <summary>
/// Selectable Inu C# FAT12/FAT16/FAT32 provider. 0.37.0 adds allocation-free ASCII paths,
/// file growth, VFAT long-name create plus short-name delete/rename and directory create/remove for userland VFS calls.
/// </summary>
public static unsafe partial class FatFs
{
    private const UInt32 MaximumSectorSize=65536U;
    private struct VolumeInfo
    {
        internal Byte Format,SectorsPerCluster,FatCount;
        internal UInt16 BytesPerSector,ReservedSectors,RootEntryCount;
        internal UInt32 SectorsPerFat,RootDirectorySectors,FirstRootSector,FirstDataSector,ClusterCount,RootCluster;
        internal UInt64 TotalSectors;
        internal Boolean ReadOnly;
    }
    private struct MountContext
    {
        internal KernelHeapAllocation Allocation;
        internal UInt32 Volume;
        internal VolumeInfo Info;
        internal UInt32 AllocationHint;
    }
    private struct FileContext
    {
        internal KernelHeapAllocation Allocation; internal UInt64 Mount; internal UInt32 FirstCluster;
        internal UInt64 Length,DirectorySector; internal UInt32 DirectoryOffset; internal Byte Type,Attributes;
    }
    private static Boolean _installed;

    public static Boolean Install()
    {
        if(_installed)return true;if(!KernelVfs.IsInitialized())return false;
        KernelFileSystemFeatures features=KernelFileSystemFeatures.Read|KernelFileSystemFeatures.Write|KernelFileSystemFeatures.Directories|KernelFileSystemFeatures.Permissions|KernelFileSystemFeatures.Create|KernelFileSystemFeatures.Delete|KernelFileSystemFeatures.Rename|KernelFileSystemFeatures.Extend|KernelFileSystemFeatures.AsciiPaths;
        KernelFileSystemCallbacks f12=new(&Probe12,&Mount12,&Unmount,&Open,&Read,&Write,&Flush,&Close,&ReadDirectory,&GetPermissions,&SetPermissions,&OpenAscii,&CreateFileAscii,&CreateDirectoryAscii,&DeleteFileAscii,&RemoveDirectoryAscii,&RenameAscii,features);
        KernelFileSystemCallbacks f16=new(&Probe16,&Mount16,&Unmount,&Open,&Read,&Write,&Flush,&Close,&ReadDirectory,&GetPermissions,&SetPermissions,&OpenAscii,&CreateFileAscii,&CreateDirectoryAscii,&DeleteFileAscii,&RemoveDirectoryAscii,&RenameAscii,features);
        KernelFileSystemCallbacks f32=new(&Probe32,&Mount32,&Unmount,&Open,&Read,&Write,&Flush,&Close,&ReadDirectory,&GetPermissions,&SetPermissions,&OpenAscii,&CreateFileAscii,&CreateDirectoryAscii,&DeleteFileAscii,&RemoveDirectoryAscii,&RenameAscii,features);
        if(!KernelVfs.RegisterFileSystem(KernelFileSystemType.Fat12,f12)||!KernelVfs.RegisterFileSystem(KernelFileSystemType.Fat16,f16)||!KernelVfs.RegisterFileSystem(KernelFileSystemType.Fat32,f32))return false;
        _installed=true;return true;
    }
    public static FatFsCapabilities GetCapabilities()=>new(_installed,true,true,true,false,true,true,true,true,MaximumSectorSize);

    private static Boolean Probe12(KernelStorageVolumeHandle v)=>Probe(v,FatFsFormat.Fat12);
    private static Boolean Probe16(KernelStorageVolumeHandle v)=>Probe(v,FatFsFormat.Fat16);
    private static Boolean Probe32(KernelStorageVolumeHandle v)=>Probe(v,FatFsFormat.Fat32);
    private static Boolean Mount12(KernelStorageVolumeHandle v,UInt64* c)=>Mount(v,FatFsFormat.Fat12,c);
    private static Boolean Mount16(KernelStorageVolumeHandle v,UInt64* c)=>Mount(v,FatFsFormat.Fat16,c);
    private static Boolean Mount32(KernelStorageVolumeHandle v,UInt64* c)=>Mount(v,FatFsFormat.Fat32,c);

    private static Boolean Probe(KernelStorageVolumeHandle volume,FatFsFormat expected)
    {
        if(!TryReadBoot(volume,out VolumeInfo info,out KernelHeapAllocation scratch))return false;Boolean ok=info.Format==(Byte)expected;KernelHeap.TryRelease(scratch);return ok;
    }
    private static Boolean Mount(KernelStorageVolumeHandle volume,FatFsFormat expected,UInt64* cookie)
    {
        if(cookie==null||!TryReadBoot(volume,out VolumeInfo info,out KernelHeapAllocation scratch))return false;KernelHeap.TryRelease(scratch);if(info.Format!=(Byte)expected)return false;
        if(!KernelHeap.TryAllocate((UInt64)sizeof(MountContext),16,true,out KernelHeapAllocation allocation))return false;MountContext* context=(MountContext*)(nuint)allocation.Address;context->Allocation=allocation;context->Volume=volume.Value;context->Info=info;context->AllocationHint=2U;*cookie=allocation.Address;return true;
    }
    private static Boolean Unmount(UInt64 mountCookie)
    { if(mountCookie==0)return false;MountContext* mount=(MountContext*)(nuint)mountCookie;KernelHeapAllocation allocation=mount->Allocation;return KernelHeap.TryRelease(allocation); }

    

    

    

    

    

    
    

    

    
    

    

    

    

    

    

    

    

    

    

    

    

    

    
    
    
    
    
    
    
    

    

    

    

    

    
    
    

    
    
    
    
    
    

    

    
    
    
    

    

    

    
    
    
    
    
    

    
    

    
    
    
    
    
    
    
    
    
    

    
    
    

    
    
    
    
    
    
}
