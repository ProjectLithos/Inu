using System;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Storage;

public static unsafe partial class KernelVfs
{
    public static Boolean Mount(KernelMountNamespaceHandle ns,KernelStorageVolumeHandle volume,KernelFileSystemType type,String path,out KernelMountHandle handle)
    {
        handle=default;if(!TryNamespace(ns)||volume.Value==0||!ValidAbsolutePath(path))return false;
        UInt32 pathLength=(UInt32)NormalizeMountLength(path);
        if(FindExactMount(ns,path,pathLength)>=0)return false;
        Int32 provider=FindProvider(type);if(provider<0)return false;ProviderRecord* p=_providers+provider;Boolean providerCaseSensitive=(((KernelFileSystemFeatures)p->Features)&KernelFileSystemFeatures.CaseSensitive)!=0;if(providerCaseSensitive!=FileSystemPathPolicyRuntime.CaseSensitive)return false;
        delegate*<KernelStorageVolumeHandle,Boolean> probe=(delegate*<KernelStorageVolumeHandle,Boolean>)(void*)p->Probe;if(!probe(volume))return false;
        UInt64 cookie=0;delegate*<KernelStorageVolumeHandle,UInt64*,Boolean> mount=(delegate*<KernelStorageVolumeHandle,UInt64*,Boolean>)(void*)p->Mount;if(!mount(volume,&cookie))return false;
        KernelHeapAllocation pathAllocation=default;if(!AllocateMountPath(path,pathLength,out pathAllocation)){CallUnmount(p,cookie);return false;}
        Int32 slot=FreeMount();if(slot<0){if(!GrowMounts()){KernelHeap.TryRelease(pathAllocation);CallUnmount(p,cookie);return false;}slot=FreeMount();}
        MountRecord* m=_mounts+slot;m->Used=1;m->Namespace=ns.Value;m->Volume=volume.Value;m->Provider=(UInt32)provider+1U;
        m->PathLength=pathLength;m->MountCookie=cookie;m->PathAllocation=pathAllocation;_mountCount++;handle=new KernelMountHandle((UInt32)slot+1U);return true;
    }

    public static Boolean Unmount(KernelMountHandle handle)
    {
        Int32 i=(Int32)handle.Value-1;if(!_initialized||i<0||(UInt32)i>=_mountCapacity||(_mounts+i)->Used==0)return false;
        for(Int32 f=0;f<(Int32)_fileCapacity;f++)if((_files+f)->Used!=0&&(_files+f)->Mount==handle.Value)return false;
        MountRecord* m=_mounts+i;ProviderRecord* p=_providers+(Int32)m->Provider-1;
        if(!CallUnmount(p,m->MountCookie))return false;KernelHeapAllocation pathAllocation=m->PathAllocation;Clear((Byte*)m,sizeof(MountRecord));_mountCount--;return KernelHeap.TryRelease(pathAllocation);
    }

    /// <summary>Returns whether a volume currently has any live mount. Driver teardown uses this to refuse unsafe hot removal.</summary>
    public static Boolean IsVolumeMounted(KernelStorageVolumeHandle volume)
    { if(!_initialized||volume.Value==0U)return false;for(Int32 i=0;i<(Int32)_mountCapacity;i++)if((_mounts+i)->Used!=0&&(_mounts+i)->Volume==volume.Value)return true;return false; }

    public static Boolean TryGetMountInfo(KernelMountHandle handle,out KernelVfsMountInfo info)
    {
        info=default;Int32 i=(Int32)handle.Value-1;if(!_initialized||i<0||(UInt32)i>=_mountCapacity)return false;MountRecord* m=_mounts+i;if(m->Used==0)return false;
        ProviderRecord* p=_providers+(Int32)m->Provider-1;
        info=new KernelVfsMountInfo(handle,new KernelMountNamespaceHandle(m->Namespace),new KernelStorageVolumeHandle(m->Volume),(KernelFileSystemType)p->Type,m->PathLength);return true;
    }

}
