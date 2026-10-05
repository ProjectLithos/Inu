using System;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Storage;

public static unsafe partial class KernelVfs
{
    private static KernelFilePermissions InferPermissions(ProviderRecord* p,KernelFileType type)
    {
        KernelFilePermissions value=KernelFilePermissions.OwnerRead|KernelFilePermissions.GroupRead|KernelFilePermissions.OtherRead;
        if((p->Features&(UInt32)KernelFileSystemFeatures.Write)!=0)value|=KernelFilePermissions.OwnerWrite;
        if(type==KernelFileType.Directory)value|=KernelFilePermissions.OwnerExecute|KernelFilePermissions.GroupExecute|KernelFilePermissions.OtherExecute;
        return value;
    }
    private static Boolean PermissionsAllow(KernelFilePermissions p,KernelFileAccess access)
    {
        Boolean read=(p&(KernelFilePermissions.OwnerRead|KernelFilePermissions.GroupRead|KernelFilePermissions.OtherRead))!=0;
        Boolean write=(p&KernelFilePermissions.OwnerWrite)!=0&&(p&KernelFilePermissions.ReadOnly)==0;
        return access==KernelFileAccess.Read?read:access==KernelFileAccess.Write?write:read&&write;
    }
    private static void TryProviderGetPermissions(ProviderRecord* p,MountRecord* m,String path,KernelFilePermissions* permissions)
    {
        if(p->GetPermissions==0)return;delegate*<UInt64,String,UInt32,KernelFilePermissions*,Boolean> get=(delegate*<UInt64,String,UInt32,KernelFilePermissions*,Boolean>)(void*)p->GetPermissions;
        KernelFilePermissions value=*permissions;if(get(m->MountCookie,path,m->PathLength,&value))*permissions=value;
    }
}
