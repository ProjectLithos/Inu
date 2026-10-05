using System;

namespace Inu.Kernel.Drivers;

/// <summary>Allocation-light validation shared by driver-package installers and tests.</summary>
public static class KernelDriverPackageValidation
{
    public const UInt32 CurrentSchemaVersion=3U;
    public const UInt32 MaximumDeviceIds=64U;
    public const UInt32 MaximumDependencies=64U;
    public const UInt32 MaximumIdentityLength=160U;
    public const UInt32 MaximumPathLength=260U;

    public static Boolean IsValid(KernelDriverPackageManifest manifest)
    {
        if(manifest.SchemaVersion!=CurrentSchemaVersion||!TextLength(manifest.Id,3U,MaximumIdentityLength)||!TextLength(manifest.Name,1U,MaximumIdentityLength))return false;
        if(!IsVersion(manifest.Version,3)||!IsVersion(manifest.MinimumInuVersion,3)||!IsVersion(manifest.SdkApiVersion,2)||!IsVersion(manifest.DriverAbiVersion,2))return false;
        if((Byte)manifest.Kind<(Byte)KernelDriverPackageKind.Platform||(Byte)manifest.Kind>(Byte)KernelDriverPackageKind.Virtio)return false;
        if((Byte)manifest.Architecture<(Byte)KernelDriverArchitecture.Any||(Byte)manifest.Architecture>(Byte)KernelDriverArchitecture.Arm64)return false;
        if((Byte)manifest.PayloadKind<(Byte)KernelDriverPayloadKind.KernelModule||(Byte)manifest.PayloadKind>(Byte)KernelDriverPayloadKind.UserlandService)return false;
        if((Byte)manifest.BindingPolicy<(Byte)KernelDriverBindingPolicy.Automatic||(Byte)manifest.BindingPolicy>(Byte)KernelDriverBindingPolicy.Disabled)return false;
        if(!SafePath(manifest.PayloadPath,false)||!SafePath(manifest.InstallRoot,true))return false;
        if(manifest.DeviceIds==null||manifest.Dependencies==null||(UInt32)manifest.DeviceIds.Length>MaximumDeviceIds||(UInt32)manifest.Dependencies.Length>MaximumDependencies)return false;
        for(Int32 i=0;i<manifest.DeviceIds.Length;i++)if(!TextLength(manifest.DeviceIds[i],1U,128U)||HasDuplicate(manifest.DeviceIds,i))return false;
        for(Int32 i=0;i<manifest.Dependencies.Length;i++)if(!TextLength(manifest.Dependencies[i],1U,192U)||HasDuplicate(manifest.Dependencies,i))return false;
        UInt64 known=(UInt64)(KernelDriverCapability.Mmio|KernelDriverCapability.PortIo|KernelDriverCapability.Interrupt|KernelDriverCapability.Msi|KernelDriverCapability.MsiX|KernelDriverCapability.Dma|KernelDriverCapability.PciConfig|KernelDriverCapability.PhysicalMemory|KernelDriverCapability.Timers|KernelDriverCapability.Networking|KernelDriverCapability.Filesystem);
        if((((UInt64)manifest.Permissions)&~known)!=0UL)return false;
        if((Byte)manifest.SigningState<(Byte)KernelDriverSigningState.Unsigned||(Byte)manifest.SigningState>(Byte)KernelDriverSigningState.Revoked||manifest.SigningState==KernelDriverSigningState.Revoked)return false;
        if((Byte)manifest.SigningState>=(Byte)KernelDriverSigningState.Signed&&(!TextLength(manifest.SigningAlgorithm,1U,64U)||!TextLength(manifest.SignerId,1U,256U)||!TextLength(manifest.SignatureDigest,1U,256U)))return false;
        return true;
    }

    private static Boolean TextLength(String value,UInt32 minimum,UInt32 maximum)=>value!=null&&(UInt32)value.Length>=minimum&&(UInt32)value.Length<=maximum;
    private static Boolean HasDuplicate(String[] values,Int32 index){for(Int32 i=0;i<index;i++)if(TextEquals(values[i],values[index]))return true;return false;}
    private static Boolean TextEquals(String a,String b){if(a==null||b==null||a.Length!=b.Length)return false;for(Int32 i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    private static Boolean IsVersion(String value,Int32 numericParts)
    {
        if(value==null||value.Length==0)return false;Int32 parts=1;Boolean digit=false;
        for(Int32 i=0;i<value.Length;i++){Char c=value[i];if(c>='0'&&c<='9'){digit=true;continue;}if(c=='.'){if(!digit||parts>=numericParts)return false;parts++;digit=false;continue;}if(numericParts==3&&(c=='-'||c=='+'))return digit&&parts==numericParts&&i+1<value.Length;return false;}
        return digit&&parts==numericParts;
    }
    private static Boolean SafePath(String value,Boolean absolute)
    {
        if(value==null||value.Length==0||value.Length>MaximumPathLength)return false;if(absolute&&value[0]!='/')return false;if(!absolute&&value[0]=='/')return false;
        for(Int32 i=0;i<value.Length;i++){Char c=value[i];if(c=='\\'||c=='\0')return false;if(c=='.'&&i+1<value.Length&&value[i+1]=='.'&&(i==0||value[i-1]=='/')&&(i+2==value.Length||value[i+2]=='/'))return false;}return true;
    }
}
