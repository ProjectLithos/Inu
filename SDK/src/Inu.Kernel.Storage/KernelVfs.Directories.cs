using System;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Storage;

public static unsafe partial class KernelVfs
{
    public static Boolean OpenDirectory(KernelMountNamespaceHandle ns,String path,out KernelDirectoryHandle handle)
    {
        handle=default;if(!Open(ns,path,KernelFileAccess.Read,out KernelFileHandle file))return false;
        if(!TryFile(file,out FileRecord* f)||f->Type!=(Byte)KernelFileType.Directory){Close(file);return false;}
        ProviderRecord* p=_providers+(Int32)f->Provider-1;if(p->ReadDirectory==0){Close(file);return false;}
        handle=new KernelDirectoryHandle(file.Value);return true;
    }

    public static Boolean OpenDirectoryAscii(KernelMountNamespaceHandle ns,Byte* path,UInt32 pathLength,out KernelDirectoryHandle handle)
    {
        handle=default;if(!OpenAscii(ns,path,pathLength,KernelFileAccess.Read,out KernelFileHandle file))return false;
        if(!TryFile(file,out FileRecord* f)||f->Type!=(Byte)KernelFileType.Directory){Close(file);return false;}ProviderRecord* p=_providers+(Int32)f->Provider-1;if(p->ReadDirectory==0){Close(file);return false;}
        handle=new KernelDirectoryHandle(file.Value);return true;
    }

    public static Boolean ReadDirectory(KernelDirectoryHandle handle,Char* nameBuffer,UInt32 nameCapacityChars,out UInt32 nameLength,out KernelFileType type,out UInt64 length,out KernelFilePermissions permissions)
    {
        nameLength=0;type=KernelFileType.Unknown;length=0;permissions=KernelFilePermissions.None;
        KernelFileHandle file=new(handle.Value);if(!TryFile(file,out FileRecord* f)||f->Type!=(Byte)KernelFileType.Directory||nameBuffer==null||nameCapacityChars==0)return false;
        ProviderRecord* p=_providers+(Int32)f->Provider-1;if(p->ReadDirectory==0)return false;
        delegate*<UInt64,UInt64,Char*,UInt32,UInt32*,KernelFileType*,UInt64*,KernelFilePermissions*,Boolean> readDirectory=(delegate*<UInt64,UInt64,Char*,UInt32,UInt32*,KernelFileType*,UInt64*,KernelFilePermissions*,Boolean>)(void*)p->ReadDirectory;
        UInt32 n=0;KernelFileType t=KernelFileType.Unknown;UInt64 l=0;KernelFilePermissions perms=KernelFilePermissions.None;
        if(!readDirectory(f->Cookie,f->DirectoryIndex,nameBuffer,nameCapacityChars,&n,&t,&l,&perms))return false;
        if(n>=nameCapacityChars)return false;nameLength=n;type=t;length=l;permissions=perms;f->DirectoryIndex++;return true;
    }

    public static Boolean RewindDirectory(KernelDirectoryHandle handle)
    {
        KernelFileHandle file=new(handle.Value);if(!TryFile(file,out FileRecord* f)||f->Type!=(Byte)KernelFileType.Directory)return false;f->DirectoryIndex=0;return true;
    }

    public static Boolean CloseDirectory(KernelDirectoryHandle handle)=>Close(new KernelFileHandle(handle.Value));

    public static Boolean TryGetPermissions(KernelMountNamespaceHandle ns,String path,out KernelFilePermissions permissions)
    {
        permissions=KernelFilePermissions.None;if(!TryNamespace(ns)||!ValidAbsolutePath(path))return false;Int32 mount=FindMount(ns,path);if(mount<0)return false;
        MountRecord* m=_mounts+mount;ProviderRecord* p=_providers+(Int32)m->Provider-1;if(p->GetPermissions==0)return false;
        delegate*<UInt64,String,UInt32,KernelFilePermissions*,Boolean> get=(delegate*<UInt64,String,UInt32,KernelFilePermissions*,Boolean>)(void*)p->GetPermissions;
        KernelFilePermissions value=KernelFilePermissions.None;if(!get(m->MountCookie,path,m->PathLength,&value))return false;permissions=value;return true;
    }

    public static Boolean TrySetPermissions(KernelMountNamespaceHandle ns,String path,KernelFilePermissions permissions)
    {
        if(!TryNamespace(ns)||!ValidAbsolutePath(path))return false;Int32 mount=FindMount(ns,path);if(mount<0)return false;
        MountRecord* m=_mounts+mount;ProviderRecord* p=_providers+(Int32)m->Provider-1;if(p->SetPermissions==0)return false;
        delegate*<UInt64,String,UInt32,KernelFilePermissions,Boolean> set=(delegate*<UInt64,String,UInt32,KernelFilePermissions,Boolean>)(void*)p->SetPermissions;
        return set(m->MountCookie,path,m->PathLength,permissions);
    }

    public static Boolean TryGetFileInfo(KernelFileHandle handle,out KernelVfsFileInfo info)
    {
        info=default;if(!TryFile(handle,out FileRecord* f))return false;
        info=new KernelVfsFileInfo(handle,(KernelFileType)f->Type,f->Length,f->Position,(KernelFileAccess)f->Access,(KernelFilePermissions)f->Permissions);return true;
    }
}
