using System;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Storage;

public static unsafe partial class KernelVfs
{
    public static Boolean Open(KernelMountNamespaceHandle ns,String path,KernelFileAccess access,out KernelFileHandle handle)
    {
        handle=default;if(!TryNamespace(ns)||!ValidAbsolutePath(path)||!ValidAccess(access))return false;
        Int32 mount=FindMount(ns,path);if(mount<0)return false;MountRecord* m=_mounts+mount;ProviderRecord* p=_providers+(Int32)m->Provider-1;
        UInt64 cookie=0,length=0;KernelFileType type=KernelFileType.Unknown;
        delegate*<UInt64,String,UInt32,KernelFileAccess,UInt64*,KernelFileType*,UInt64*,Boolean> open=(delegate*<UInt64,String,UInt32,KernelFileAccess,UInt64*,KernelFileType*,UInt64*,Boolean>)(void*)p->Open;
        if(!open(m->MountCookie,path,m->PathLength,access,&cookie,&type,&length))return false;
        KernelFilePermissions permissions=InferPermissions(p,type);
        TryProviderGetPermissions(p,m,path,&permissions);
        if(!PermissionsAllow(permissions,access)){CallClose(p,cookie);return false;}
        Int32 slot=FreeFile();if(slot<0){if(!GrowFiles()){CallClose(p,cookie);return false;}slot=FreeFile();}
        FileRecord* f=_files+slot;f->Used=1;f->Type=(Byte)type;f->Access=(Byte)access;f->Provider=m->Provider;f->Mount=(UInt32)mount+1U;
        f->Cookie=cookie;f->Length=length;f->Position=0;f->DirectoryIndex=0;f->Permissions=(UInt32)permissions;_openFileCount++;
        handle=new KernelFileHandle((UInt32)slot+1U);return true;
    }

    /// <summary>Opens an allocation-free normalized ASCII path. This is the syscall/service-facing path API used by freestanding userland.</summary>
    public static Boolean OpenAscii(KernelMountNamespaceHandle ns,Byte* path,UInt32 pathLength,KernelFileAccess access,out KernelFileHandle handle)
    {
        handle=default;if(!TryNamespace(ns)||!ValidAbsoluteAsciiPath(path,pathLength)||!ValidAccess(access))return false;
        Int32 mount=FindMountAscii(ns,path,pathLength);if(mount<0)return false;MountRecord* m=_mounts+mount;ProviderRecord* p=_providers+(Int32)m->Provider-1;if(p->OpenAscii==0)return false;
        UInt64 cookie=0,length=0;KernelFileType type=KernelFileType.Unknown;
        delegate*<UInt64,Byte*,UInt32,UInt32,KernelFileAccess,UInt64*,KernelFileType*,UInt64*,Boolean> open=(delegate*<UInt64,Byte*,UInt32,UInt32,KernelFileAccess,UInt64*,KernelFileType*,UInt64*,Boolean>)(void*)p->OpenAscii;
        if(!open(m->MountCookie,path,pathLength,m->PathLength,access,&cookie,&type,&length))return false;
        KernelFilePermissions permissions=InferPermissions(p,type);if(!PermissionsAllow(permissions,access)){CallClose(p,cookie);return false;}
        Int32 slot=FreeFile();if(slot<0){if(!GrowFiles()){CallClose(p,cookie);return false;}slot=FreeFile();}
        FileRecord* f=_files+slot;f->Used=1;f->Type=(Byte)type;f->Access=(Byte)access;f->Provider=m->Provider;f->Mount=(UInt32)mount+1U;
        f->Cookie=cookie;f->Length=length;f->Position=0;f->DirectoryIndex=0;f->Permissions=(UInt32)permissions;_openFileCount++;handle=new KernelFileHandle((UInt32)slot+1U);return true;
    }

    public static Boolean CreateFileAscii(KernelMountNamespaceHandle ns,Byte* path,UInt32 pathLength,Boolean overwrite)
    {
        if(!TryNamespace(ns)||!ValidAbsoluteAsciiPath(path,pathLength))return false;Int32 mount=FindMountAscii(ns,path,pathLength);if(mount<0)return false;
        MountRecord* m=_mounts+mount;ProviderRecord* p=_providers+(Int32)m->Provider-1;if(p->CreateFileAscii==0)return false;
        delegate*<UInt64,Byte*,UInt32,UInt32,Boolean,Boolean> fn=(delegate*<UInt64,Byte*,UInt32,UInt32,Boolean,Boolean>)(void*)p->CreateFileAscii;Boolean ok=fn(m->MountCookie,path,pathLength,m->PathLength,overwrite);if(ok)KernelPathIndex.NotifyCreated(ns,path,pathLength,KernelFileType.File);return ok;
    }

    public static Boolean CreateDirectoryAscii(KernelMountNamespaceHandle ns,Byte* path,UInt32 pathLength)
    {
        if(!TryNamespace(ns)||!ValidAbsoluteAsciiPath(path,pathLength))return false;Int32 mount=FindMountAscii(ns,path,pathLength);if(mount<0)return false;
        MountRecord* m=_mounts+mount;ProviderRecord* p=_providers+(Int32)m->Provider-1;if(p->CreateDirectoryAscii==0)return false;
        delegate*<UInt64,Byte*,UInt32,UInt32,Boolean> fn=(delegate*<UInt64,Byte*,UInt32,UInt32,Boolean>)(void*)p->CreateDirectoryAscii;Boolean ok=fn(m->MountCookie,path,pathLength,m->PathLength);if(ok)KernelPathIndex.NotifyCreated(ns,path,pathLength,KernelFileType.Directory);return ok;
    }

    public static Boolean DeleteFileAscii(KernelMountNamespaceHandle ns,Byte* path,UInt32 pathLength)
    {
        if(!TryNamespace(ns)||!ValidAbsoluteAsciiPath(path,pathLength))return false;Int32 mount=FindMountAscii(ns,path,pathLength);if(mount<0)return false;
        MountRecord* m=_mounts+mount;ProviderRecord* p=_providers+(Int32)m->Provider-1;if(p->DeleteFileAscii==0)return false;
        delegate*<UInt64,Byte*,UInt32,UInt32,Boolean> fn=(delegate*<UInt64,Byte*,UInt32,UInt32,Boolean>)(void*)p->DeleteFileAscii;Boolean ok=fn(m->MountCookie,path,pathLength,m->PathLength);if(ok)KernelPathIndex.NotifyDeleted(ns,path,pathLength,false);return ok;
    }

    public static Boolean RemoveDirectoryAscii(KernelMountNamespaceHandle ns,Byte* path,UInt32 pathLength)
    {
        if(!TryNamespace(ns)||!ValidAbsoluteAsciiPath(path,pathLength))return false;Int32 mount=FindMountAscii(ns,path,pathLength);if(mount<0)return false;
        MountRecord* m=_mounts+mount;ProviderRecord* p=_providers+(Int32)m->Provider-1;if(p->RemoveDirectoryAscii==0)return false;
        delegate*<UInt64,Byte*,UInt32,UInt32,Boolean> fn=(delegate*<UInt64,Byte*,UInt32,UInt32,Boolean>)(void*)p->RemoveDirectoryAscii;Boolean ok=fn(m->MountCookie,path,pathLength,m->PathLength);if(ok)KernelPathIndex.NotifyDeleted(ns,path,pathLength,true);return ok;
    }

    public static Boolean RenameAscii(KernelMountNamespaceHandle ns,Byte* source,UInt32 sourceLength,Byte* destination,UInt32 destinationLength)
    {
        if(!TryNamespace(ns)||!ValidAbsoluteAsciiPath(source,sourceLength)||!ValidAbsoluteAsciiPath(destination,destinationLength))return false;
        Int32 sourceMount=FindMountAscii(ns,source,sourceLength),destinationMount=FindMountAscii(ns,destination,destinationLength);if(sourceMount<0||sourceMount!=destinationMount)return false;
        MountRecord* m=_mounts+sourceMount;ProviderRecord* p=_providers+(Int32)m->Provider-1;if(p->RenameAscii==0)return false;
        delegate*<UInt64,Byte*,UInt32,UInt32,Byte*,UInt32,UInt32,Boolean> fn=(delegate*<UInt64,Byte*,UInt32,UInt32,Byte*,UInt32,UInt32,Boolean>)(void*)p->RenameAscii;
        Boolean ok=fn(m->MountCookie,source,sourceLength,m->PathLength,destination,destinationLength,m->PathLength);if(ok)KernelPathIndex.NotifyRenamed(ns,source,sourceLength,destination,destinationLength);return ok;
    }

    public static Boolean Read(KernelFileHandle handle,Byte* buffer,UInt32 bytesToRead,out UInt32 bytesRead)
    {
        bytesRead=0;if(!TryFile(handle,out FileRecord* f)||buffer==null||(f->Access!=(Byte)KernelFileAccess.Read&&f->Access!=(Byte)KernelFileAccess.ReadWrite))return false;
        if(bytesToRead==0)return true;if(f->Type!=(Byte)KernelFileType.File)return false;
        ProviderRecord* p=_providers+(Int32)f->Provider-1;delegate*<UInt64,UInt64,Byte*,UInt32,UInt32*,Boolean> read=(delegate*<UInt64,UInt64,Byte*,UInt32,UInt32*,Boolean>)(void*)p->Read;
        UInt32 count=0;if(!read(f->Cookie,f->Position,buffer,bytesToRead,&count))return false;if(count>bytesToRead)return false;bytesRead=count;f->Position+=count;return true;
    }

    public static Boolean Write(KernelFileHandle handle,Byte* buffer,UInt32 bytesToWrite,out UInt32 bytesWritten)
    {
        bytesWritten=0;if(!TryFile(handle,out FileRecord* f)||buffer==null||(f->Access!=(Byte)KernelFileAccess.Write&&f->Access!=(Byte)KernelFileAccess.ReadWrite))return false;
        if(bytesToWrite==0)return true;if(f->Type!=(Byte)KernelFileType.File)return false;
        ProviderRecord* p=_providers+(Int32)f->Provider-1;if(p->Write==0)return false;
        delegate*<UInt64,UInt64,Byte*,UInt32,UInt32*,Boolean> write=(delegate*<UInt64,UInt64,Byte*,UInt32,UInt32*,Boolean>)(void*)p->Write;
        UInt32 count=0;if(!write(f->Cookie,f->Position,buffer,bytesToWrite,&count)||count>bytesToWrite)return false;bytesWritten=count;f->Position+=count;if(f->Position>f->Length)f->Length=f->Position;return true;
    }

    public static Boolean Seek(KernelFileHandle handle,Int64 offset,KernelSeekOrigin origin,out UInt64 position)
    {
        position=0;if(!TryFile(handle,out FileRecord* f)||f->Type!=(Byte)KernelFileType.File)return false;
        UInt64 basis=origin==KernelSeekOrigin.Begin?0UL:origin==KernelSeekOrigin.Current?f->Position:f->Length;
        if(offset<0){UInt64 amount=(UInt64)(-offset);if(amount>basis)return false;position=basis-amount;}
        else{UInt64 amount=(UInt64)offset;if(UInt64.MaxValue-basis<amount)return false;position=basis+amount;}
        f->Position=position;return true;
    }

    public static Boolean Flush(KernelFileHandle handle)
    {
        if(!TryFile(handle,out FileRecord* f))return false;ProviderRecord* p=_providers+(Int32)f->Provider-1;if(p->Flush==0)return true;
        delegate*<UInt64,Boolean> flush=(delegate*<UInt64,Boolean>)(void*)p->Flush;return flush(f->Cookie);
    }

    public static Boolean Close(KernelFileHandle handle)
    {
        if(!TryFile(handle,out FileRecord* f))return false;ProviderRecord* p=_providers+(Int32)f->Provider-1;if(!CallClose(p,f->Cookie))return false;
        Clear((Byte*)f,sizeof(FileRecord));_openFileCount--;return true;
    }

}
