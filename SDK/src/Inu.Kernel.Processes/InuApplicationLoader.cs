using System;
using Inu.ApplicationFormat;

namespace Inu.Kernel.Processes;

/// <summary>Bridges the Inu .exe package container to the existing validated native image loader.</summary>
public static unsafe class InuApplicationLoader
{
    // AbiMajor/AbiMinor in the application package describe the selected syscall personality.
    // Native Inu moved to structured Get/Set/Event messages in syscall ABI 2.0. Linux/NT
    // compatibility personalities retain their existing 1.0 contracts.
    public const UInt16 SupportedInuAbiMajor=2;
    public const UInt16 SupportedInuAbiMinor=0;
    public const UInt16 SupportedCompatibilityAbiMajor=1;
    public const UInt16 SupportedCompatibilityAbiMinor=0;

    public static Boolean TryResolveNativeImage(Byte* packageOrImage,UInt64 length,out Byte* nativeImage,out UInt64 nativeLength,out InuApplicationInfo packageInfo,out Boolean packaged)
        =>TryResolveNativeImage(packageOrImage,length,out nativeImage,out nativeLength,out packageInfo,out packaged,out _);

    /// <summary>Validates package policy and returns a specific failure reason without allocating or partially creating a process.</summary>
    public static Boolean TryResolveNativeImage(Byte* packageOrImage,UInt64 length,out Byte* nativeImage,out UInt64 nativeLength,out InuApplicationInfo packageInfo,out Boolean packaged,out InuApplicationLoadError error)
    {
        nativeImage=packageOrImage;nativeLength=length;packageInfo=default;packaged=false;error=InuApplicationLoadError.None;
        if(packageOrImage==null||length==0UL){error=InuApplicationLoadError.InvalidArgument;return false;}
        if(!InuApplicationPackage.IsPackage(packageOrImage,length))return true;
        if(!InuApplicationPackage.TryInspect(packageOrImage,length,out packageInfo)){error=InuApplicationLoadError.MalformedPackage;return false;}
        if(packageInfo.Architecture!=InuApplicationArchitecture.X64){error=InuApplicationLoadError.UnsupportedArchitecture;return false;}
        if(!IsSupportedSyscallAbi(packageInfo)){error=InuApplicationLoadError.UnsupportedAbi;return false;}
        const InuApplicationFlags known=InuApplicationFlags.Signed|InuApplicationFlags.HasResources|InuApplicationFlags.PositionIndependent;
        if((packageInfo.Flags&~known)!=0){error=InuApplicationLoadError.UnsupportedFlags;return false;}
        if((packageInfo.Flags&InuApplicationFlags.Signed)!=0){error=InuApplicationLoadError.SignatureRequired;return false;}
        if(((packageInfo.Flags&InuApplicationFlags.HasResources)!=0)!=(packageInfo.ResourceCount!=0U)){error=InuApplicationLoadError.MalformedPackage;return false;}
        // Dependency resolution and package-declared capability grants must be explicit.  Until the
        // resolver/broker supplies those launch resources, fail before any address-space allocation.
        if(packageInfo.DependencyCount!=0U){error=InuApplicationLoadError.DependenciesUnavailable;return false;}
        if(packageInfo.CapabilityCount!=0U){error=InuApplicationLoadError.CapabilitiesUnavailable;return false;}
        if(!InuApplicationPackage.TryGetNativeImage(packageOrImage,length,packageInfo,out nativeImage,out nativeLength)){error=InuApplicationLoadError.NativeImageUnavailable;return false;}
        packaged=true;return true;
    }

    public static Boolean IsSupportedSyscallAbi(InuApplicationInfo packageInfo)
    {
        return packageInfo.SyscallAbi switch
        {
            InuApplicationAbi.Inu => packageInfo.AbiMajor==SupportedInuAbiMajor && packageInfo.AbiMinor<=SupportedInuAbiMinor,
            InuApplicationAbi.Linux => packageInfo.AbiMajor==SupportedCompatibilityAbiMajor && packageInfo.AbiMinor<=SupportedCompatibilityAbiMinor,
            InuApplicationAbi.WindowsNt => packageInfo.AbiMajor==SupportedCompatibilityAbiMajor && packageInfo.AbiMinor<=SupportedCompatibilityAbiMinor,
            _ => false
        };
    }
}
