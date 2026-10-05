using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Heap;
using Inu.Kernel.Storage;
using Inu.Kernel.Memory;
using Inu.Kernel.Security;
using Inu.ApplicationFormat;

namespace Inu.Kernel.Processes;

public static unsafe partial class KernelProcesses
{
    internal static Boolean TryCreateFromImageImplementation(UInt64 imageAddress,UInt64 imageLength,KernelProcessOwnership ownership,out KernelProcessInfo process,out InuApplicationLoadError error)
    {
        process=default;error=InuApplicationLoadError.None;
        if(!_initialized||imageAddress==0UL||imageLength==0UL||(ownership!=KernelProcessOwnership.Foreground&&ownership!=KernelProcessOwnership.Background)){error=InuApplicationLoadError.InvalidArgument;return false;}
        Byte* packageOrImage=(Byte*)(nuint)imageAddress;
        if(!InuApplicationLoader.TryResolveNativeImage(packageOrImage,imageLength,out Byte* image,out UInt64 nativeLength,out InuApplicationInfo application,out Boolean packaged,out error))return false;
        if(!ProcessExecutableMath.TryInspect(image,nativeLength,out ProcessExecutableInfo executable)){error=InuApplicationLoadError.MalformedExecutable;return false;}
        if(packaged&&application.EntryPointRva!=0UL&&(executable.EntryPoint<executable.ImageBase||executable.EntryPoint-executable.ImageBase!=application.EntryPointRva)){error=InuApplicationLoadError.EntryPointMismatch;return false;}
        if(packaged&&((application.Flags&InuApplicationFlags.PositionIndependent)!=0)!=executable.IsPositionIndependent){error=InuApplicationLoadError.UnsupportedFlags;return false;}

        if(!KernelProcessRecordStore.TryReserve(ownership,packaged?application.SyscallAbi:InuApplicationAbi.Inu,out KernelProcessRecordHandle record)){error=InuApplicationLoadError.ProcessTableFull;return false;}
        if(packaged)KernelProcessRecordStore.SetApplicationHashes(record,application.Id.Hash,application.Name.Hash,application.Version.Hash);

        KernelPhysicalAllocation* tables=stackalloc KernelPhysicalAllocation[(Int32)MaximumTablesPerProcess];UInt32 tableCount;
        if(!ProcessAddressSpace.TryCreate(tables,MaximumTablesPerProcess,out tableCount,out UInt64 root)){KernelProcessRecordStore.Abandon(record);error=InuApplicationLoadError.AddressSpaceUnavailable;return false;}
        if(!LoadSegments(image,nativeLength,executable,root,record,tables,ref tableCount)||!CreateStack(root,record,tables,ref tableCount)){KernelProcessAddressSpaceServices.ReleaseTemporary(record,tables,tableCount);error=InuApplicationLoadError.MappingFailed;return false;}
        KernelProcessRecordStore.SetTables(record,tables,tableCount);
        KernelProcessRecordStore.SetExecutable(record,root,executable.EntryPoint,executable.Format);
        UInt64 processId=KernelProcessRecordStore.GetId(record),guardBase=KernelProcessRecordStore.GetStackGuardBase(record);
        if(!KernelSecurity.RegisterProcessAddressSpace(processId,root,guardBase,4096UL)){KernelProcessAddressSpaceServices.ReleaseTemporary(record,tables,tableCount);error=InuApplicationLoadError.SecurityPolicyRejected;return false;}
        InuApplicationAbi abi=KernelProcessRecordStore.GetSyscallAbi(record);
        if(!ConfigureSyscallPolicy(processId,abi)||!KernelSecurity.TryValidateExecutableRange(processId,executable.EntryPoint,1UL)){KernelSecurity.UnregisterProcess(processId);KernelProcessAddressSpaceServices.ReleaseTemporary(record,tables,tableCount);error=InuApplicationLoadError.SecurityPolicyRejected;return false;}
        if(!KernelProcessRecordStore.Activate(record,out process)){KernelSecurity.UnregisterProcess(processId);KernelProcessAddressSpaceServices.ReleaseTemporary(record,tables,tableCount);return false;}return true;
    }

    internal static Boolean TryCreateFromFileImplementation(KernelMountNamespaceHandle mountNamespace,String path,KernelProcessOwnership ownership,out KernelProcessInfo process)
    {
        process=default;if(!_initialized||!KernelStorage.IsInitialized()||path==null)return false;if(!KernelVfs.Open(mountNamespace,path,KernelFileAccess.Read,out KernelFileHandle file))return false;
        if(!KernelVfs.TryGetFileInfo(file,out KernelVfsFileInfo info)||info.Type!=KernelFileType.File||info.Length==0UL||info.Length>InuApplicationFormat.MaximumPackageBytes){KernelVfs.Close(file);return false;}
        if(!KernelHeap.TryAllocate(info.Length,16UL,false,out KernelHeapAllocation image)){KernelVfs.Close(file);return false;}Byte* buffer=(Byte*)(nuint)image.Address;UInt64 total=0UL;Boolean ok=true;
        while(total<info.Length){UInt64 remaining=info.Length-total;UInt32 request=remaining>1048576UL?1048576U:(UInt32)remaining;if(!KernelVfs.Read(file,buffer+total,request,out UInt32 read)||read==0U){ok=false;break;}total+=read;}
        if(!KernelVfs.Close(file))ok=false;if(ok&&total==info.Length)ok=TryCreateFromImageImplementation(image.Address,info.Length,ownership,out process,out _);if(!KernelHeap.TryRelease(image))ok=false;return ok;
    }
    internal static Boolean TryCreateFromFileAsciiImplementation(KernelMountNamespaceHandle mountNamespace,Byte* path,UInt32 pathLength,KernelProcessOwnership ownership,out KernelProcessInfo process)
    {
        process=default;if(!_initialized||!KernelStorage.IsInitialized()||path==null||pathLength==0U)return false;if(!KernelVfs.OpenAscii(mountNamespace,path,pathLength,KernelFileAccess.Read,out KernelFileHandle file))return false;
        if(!KernelVfs.TryGetFileInfo(file,out KernelVfsFileInfo info)||info.Type!=KernelFileType.File||info.Length==0UL||info.Length>InuApplicationFormat.MaximumPackageBytes){KernelVfs.Close(file);return false;}
        if(!KernelHeap.TryAllocate(info.Length,16UL,false,out KernelHeapAllocation image)){KernelVfs.Close(file);return false;}Byte* buffer=(Byte*)(nuint)image.Address;UInt64 total=0UL;Boolean ok=true;
        while(total<info.Length){UInt64 remaining=info.Length-total;UInt32 request=remaining>1048576UL?1048576U:(UInt32)remaining;if(!KernelVfs.Read(file,buffer+total,request,out UInt32 read)||read==0U){ok=false;break;}total+=read;}
        if(!KernelVfs.Close(file))ok=false;if(ok&&total==info.Length)ok=TryCreateFromImageImplementation(image.Address,info.Length,ownership,out process,out _);if(!KernelHeap.TryRelease(image))ok=false;return ok;
    }

}
