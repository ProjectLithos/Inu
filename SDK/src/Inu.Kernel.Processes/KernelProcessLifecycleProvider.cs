using System;
using Inu.ApplicationFormat;
using Inu.Kernel.Storage;
namespace Inu.Kernel.Processes;
public static unsafe class KernelProcessLifecycleProvider
{
    public static Boolean Register()=>KernelProcessLifecycleServices.Register(&CreateImage,&CreateFile,&Terminate);
    private static Boolean CreateImage(UInt64 address,UInt64 length,KernelProcessOwnership ownership,KernelProcessInfo* process,InuApplicationLoadError* error){if(process==null||error==null)return false;KernelProcessInfo value;InuApplicationLoadError e;Boolean ok=KernelProcesses.TryCreateFromImageImplementation(address,length,ownership,out value,out e);*process=value;*error=e;return ok;}
    private static Boolean CreateFile(KernelMountNamespaceHandle ns,String path,KernelProcessOwnership ownership,KernelProcessInfo* process){if(process==null)return false;KernelProcessInfo value;Boolean ok=KernelProcesses.TryCreateFromFileImplementation(ns,path,ownership,out value);*process=value;return ok;}
    private static Boolean Terminate(UInt64 id,Int64 exitCode)=>KernelProcesses.TryTerminateImplementation(id,exitCode);
}
