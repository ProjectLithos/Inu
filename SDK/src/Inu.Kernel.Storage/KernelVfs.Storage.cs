using System;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Storage;

public static unsafe partial class KernelVfs
{
    private static Boolean CallUnmount(ProviderRecord* p,UInt64 cookie){delegate*<UInt64,Boolean> fn=(delegate*<UInt64,Boolean>)(void*)p->Unmount;return fn(cookie);}
    private static Boolean CallClose(ProviderRecord* p,UInt64 cookie){delegate*<UInt64,Boolean> fn=(delegate*<UInt64,Boolean>)(void*)p->Close;return fn(cookie);}
    private static Boolean ValidAccess(KernelFileAccess a)=>a==KernelFileAccess.Read||a==KernelFileAccess.Write||a==KernelFileAccess.ReadWrite;
    private static Boolean ValidAbsolutePath(String path)=>FileSystemPathPolicyRuntime.ValidateCanonicalPath(path);
    private static Boolean ValidAbsoluteAsciiPath(Byte* path,UInt32 length)=>FileSystemPathPolicyRuntime.ValidateCanonicalAscii(path,length);
    private static Int32 FindProvider(KernelFileSystemType type){for(Int32 i=0;i<(Int32)_providerCapacity;i++)if((_providers+i)->Used!=0&&(_providers+i)->Type==(Byte)type)return i;return -1;}
    private static Int32 FindExactMount(KernelMountNamespaceHandle ns,String path,UInt32 length){for(Int32 i=0;i<(Int32)_mountCapacity;i++){MountRecord* m=_mounts+i;if(m->Used!=0&&m->Namespace==ns.Value&&m->PathLength==length&&MountPathEquals(m,path,length))return i;}return -1;}
    private static Int32 FindMount(KernelMountNamespaceHandle ns,String path){Int32 best=-1;UInt32 bestLength=0;for(Int32 i=0;i<(Int32)_mountCapacity;i++){MountRecord* m=_mounts+i;if(m->Used==0||m->Namespace!=ns.Value||m->PathLength>(UInt32)path.Length||m->PathLength<bestLength)continue;if(!MountPathEquals(m,path,m->PathLength))continue;if(m->PathLength>1&&path.Length>m->PathLength&&path[(Int32)m->PathLength]!='/')continue;best=i;bestLength=m->PathLength;}return best;}
    private static Int32 FindMountAscii(KernelMountNamespaceHandle ns,Byte* path,UInt32 length){Int32 best=-1;UInt32 bestLength=0;for(Int32 i=0;i<(Int32)_mountCapacity;i++){MountRecord* m=_mounts+i;if(m->Used==0||m->Namespace!=ns.Value||m->PathLength>length||m->PathLength<bestLength)continue;if(!MountPathEqualsAscii(m,path,length,m->PathLength))continue;if(m->PathLength>1&&length>m->PathLength&&path[m->PathLength]!='/')continue;best=i;bestLength=m->PathLength;}return best;}
    private static Boolean MountPathEquals(MountRecord* m,String path,UInt32 length){if(m->PathAllocation.Address==0||length!=m->PathLength)return false;Char* saved=(Char*)(nuint)m->PathAllocation.Address;for(UInt32 i=0;i<length;i++){Char a=saved[i],b=path[(Int32)i];if(!FileSystemPathPolicyRuntime.CaseSensitive){if(a>='A'&&a<='Z')a=(Char)(a+32);if(b>='A'&&b<='Z')b=(Char)(b+32);}if(a!=b)return false;}return true;}
    private static Boolean MountPathEqualsAscii(MountRecord* m,Byte* path,UInt32 pathLength,UInt32 length){if(m->PathAllocation.Address==0||path==null||pathLength<length||length!=m->PathLength)return false;Char* saved=(Char*)(nuint)m->PathAllocation.Address;for(UInt32 i=0;i<length;i++){Char a=saved[i];Byte b=path[i];if(!FileSystemPathPolicyRuntime.CaseSensitive){if(a>='A'&&a<='Z')a=(Char)(a+32);if(b>='A'&&b<='Z')b=(Byte)(b+32);}if(a!=(Char)b)return false;}return true;}
    private static Boolean AllocateMountPath(String path,UInt32 length,out KernelHeapAllocation allocation){allocation=default;UInt64 bytes=((UInt64)length+1UL)*2UL;if(!KernelHeap.TryAllocate(bytes,16,true,out allocation))return false;Char* d=(Char*)(nuint)allocation.Address;for(UInt32 i=0;i<length;i++)d[i]=path[(Int32)i];d[length]='\0';return true;}
    private static Int32 NormalizeMountLength(String path){Int32 n=path.Length;while(n>1&&path[n-1]=='/')n--;return n;}
    private static Boolean TryNamespace(KernelMountNamespaceHandle h){Int32 i=(Int32)h.Value-1;return _initialized&&i>=0&&(UInt32)i<_namespaceCapacity&&(_namespaces+i)->Used!=0;}
    private static Boolean TryFile(KernelFileHandle h,out FileRecord* f){f=null;Int32 i=(Int32)h.Value-1;if(!_initialized||i<0||(UInt32)i>=_fileCapacity||(_files+i)->Used==0)return false;f=_files+i;return true;}
    private static Int32 FreeProvider(){for(Int32 i=0;i<(Int32)_providerCapacity;i++)if((_providers+i)->Used==0)return i;return -1;}
    private static Int32 FreeNamespace(){for(Int32 i=0;i<(Int32)_namespaceCapacity;i++)if((_namespaces+i)->Used==0)return i;return -1;}
    private static Int32 FreeMount(){for(Int32 i=0;i<(Int32)_mountCapacity;i++)if((_mounts+i)->Used==0)return i;return -1;}
    private static Int32 FreeFile(){for(Int32 i=0;i<(Int32)_fileCapacity;i++)if((_files+i)->Used==0)return i;return -1;}
    private static Boolean GrowProviders(){if(_mode!=KernelStorageRegistryMode.Dynamic)return false;UInt32 next=KernelStorageMath.NextCapacity(_providerCapacity,_maximumProviders);if(!AllocProviders(next,out KernelHeapAllocation a,out ProviderRecord* n))return false;Copy((Byte*)_providers,(Byte*)n,(UInt64)_providerCapacity*(UInt64)sizeof(ProviderRecord));KernelHeapAllocation old=_providerAllocation;_providerAllocation=a;_providers=n;_providerCapacity=next;return KernelHeap.TryRelease(old);}
    private static Boolean GrowNamespaces(){if(_mode!=KernelStorageRegistryMode.Dynamic)return false;UInt32 next=KernelStorageMath.NextCapacity(_namespaceCapacity,_maximumNamespaces);if(!AllocNamespaces(next,out KernelHeapAllocation a,out NamespaceRecord* n))return false;Copy((Byte*)_namespaces,(Byte*)n,(UInt64)_namespaceCapacity*(UInt64)sizeof(NamespaceRecord));KernelHeapAllocation old=_namespaceAllocation;_namespaceAllocation=a;_namespaces=n;_namespaceCapacity=next;return KernelHeap.TryRelease(old);}
    private static Boolean GrowMounts(){if(_mode!=KernelStorageRegistryMode.Dynamic)return false;UInt32 next=KernelStorageMath.NextCapacity(_mountCapacity,_maximumMounts);if(next<=_mountCapacity||!AllocMounts(next,out KernelHeapAllocation a,out MountRecord* n))return false;Copy((Byte*)_mounts,(Byte*)n,(UInt64)_mountCapacity*(UInt64)sizeof(MountRecord));KernelHeapAllocation old=_mountAllocation;_mountAllocation=a;_mounts=n;_mountCapacity=next;return KernelHeap.TryRelease(old);}
    private static Boolean GrowFiles(){if(_mode!=KernelStorageRegistryMode.Dynamic)return false;UInt32 next=KernelStorageMath.NextCapacity(_fileCapacity,_maximumOpenFiles);if(next<=_fileCapacity||!AllocFiles(next,out KernelHeapAllocation a,out FileRecord* n))return false;Copy((Byte*)_files,(Byte*)n,(UInt64)_fileCapacity*(UInt64)sizeof(FileRecord));KernelHeapAllocation old=_fileAllocation;_fileAllocation=a;_files=n;_fileCapacity=next;return KernelHeap.TryRelease(old);}
    private static Boolean AllocProviders(UInt32 n,out KernelHeapAllocation a,out ProviderRecord* p){a=default;p=null;if(!KernelHeap.TryAllocate((UInt64)n*(UInt64)sizeof(ProviderRecord),64,true,out a))return false;p=(ProviderRecord*)(nuint)a.Address;return true;}
    private static Boolean AllocNamespaces(UInt32 n,out KernelHeapAllocation a,out NamespaceRecord* p){a=default;p=null;if(!KernelHeap.TryAllocate((UInt64)n*(UInt64)sizeof(NamespaceRecord),64,true,out a))return false;p=(NamespaceRecord*)(nuint)a.Address;return true;}
    private static Boolean AllocMounts(UInt32 n,out KernelHeapAllocation a,out MountRecord* p){a=default;p=null;if(!KernelHeap.TryAllocate((UInt64)n*(UInt64)sizeof(MountRecord),64,true,out a))return false;p=(MountRecord*)(nuint)a.Address;return true;}
    private static Boolean AllocFiles(UInt32 n,out KernelHeapAllocation a,out FileRecord* p){a=default;p=null;if(!KernelHeap.TryAllocate((UInt64)n*(UInt64)sizeof(FileRecord),64,true,out a))return false;p=(FileRecord*)(nuint)a.Address;return true;}
    private static void Clear(Byte* p,Int32 n){for(Int32 i=0;i<n;i++)p[i]=0;}
    private static void Copy(Byte* s,Byte* d,UInt64 n){for(UInt64 i=0;i<n;i++)d[i]=s[i];}

    internal static Boolean CanAdoptCaseSensitivity(Boolean caseSensitive)
    {
        if(!_initialized||_mountCount==0U)return true;
        for(Int32 i=0;i<(Int32)_mountCapacity;i++)
        {
            MountRecord* m=_mounts+i;if(m->Used==0)continue;
            ProviderRecord* p=_providers+(Int32)m->Provider-1;
            Boolean providerCaseSensitive=(((KernelFileSystemFeatures)p->Features)&KernelFileSystemFeatures.CaseSensitive)!=0;
            if(providerCaseSensitive!=caseSensitive)return false;
        }
        return true;
    }
}
