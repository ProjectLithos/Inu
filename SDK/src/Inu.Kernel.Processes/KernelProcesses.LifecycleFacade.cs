using System;
using Inu.ApplicationFormat;
using Inu.Kernel.Storage;

namespace Inu.Kernel.Processes;

public static unsafe partial class KernelProcesses
{
    public static Boolean TryCreateFromImage(UInt64 imageAddress,UInt64 imageLength,out KernelProcessInfo process)=>KernelProcessLifecycleServices.TryCreateImage(imageAddress,imageLength,KernelProcessOwnership.Foreground,out process,out _);
    public static Boolean TryCreateFromImage(UInt64 imageAddress,UInt64 imageLength,KernelProcessOwnership ownership,out KernelProcessInfo process)=>KernelProcessLifecycleServices.TryCreateImage(imageAddress,imageLength,ownership,out process,out _);
    public static Boolean TryCreateFromImage(UInt64 imageAddress,UInt64 imageLength,KernelProcessOwnership ownership,out KernelProcessInfo process,out InuApplicationLoadError error)=>KernelProcessLifecycleServices.TryCreateImage(imageAddress,imageLength,ownership,out process,out error);
    public static Boolean TryCreateFromFile(KernelMountNamespaceHandle mountNamespace,String path,out KernelProcessInfo process)=>KernelProcessLifecycleServices.TryCreateFile(mountNamespace,path,KernelProcessOwnership.Foreground,out process);
    public static Boolean TryCreateFromFile(KernelMountNamespaceHandle mountNamespace,String path,KernelProcessOwnership ownership,out KernelProcessInfo process)=>KernelProcessLifecycleServices.TryCreateFile(mountNamespace,path,ownership,out process);
    public static Boolean TryCreateFromFileAscii(KernelMountNamespaceHandle mountNamespace,Byte* path,UInt32 pathLength,KernelProcessOwnership ownership,out KernelProcessInfo process)=>TryCreateFromFileAsciiImplementation(mountNamespace,path,pathLength,ownership,out process);
    public static Boolean TryTerminate(UInt64 processId,Int64 exitCode)=>KernelProcessLifecycleServices.TryTerminate(processId,exitCode);
}
