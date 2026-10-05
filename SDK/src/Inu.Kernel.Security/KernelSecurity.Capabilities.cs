using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Protection;
using Inu.Kernel.Smp;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Security;

/// <summary>Central process-isolation, user-pointer, W^X, syscall-policy and capability/handle authority.</summary>
public static unsafe partial class KernelSecurity
{
    public static Boolean TryCreateCapability(UInt64 processId,UInt64 objectId,KernelCapabilityRights rights,out KernelCapabilityHandle handle)
    {
        handle=default;if(!_initialized||FindProcess(processId)<0||objectId==0UL||rights==KernelCapabilityRights.None)return false;
        fixed(UInt32* use=_state.CapabilityInUse,gen=_state.CapabilityGenerations) fixed(UInt64* owners=_state.CapabilityOwners,objects=_state.CapabilityObjects,r=_state.CapabilityRights)
        for(UInt32 i=0;i<MaximumCapabilities;i++)if(use[i]==0U){UInt32 g=gen[i]+1U;if(g==0U)g=1U;gen[i]=g;use[i]=1U;owners[i]=processId;objects[i]=objectId;r[i]=(UInt64)rights;handle=new(((UInt64)g<<32)|(i+1U));return true;}
        return false;
    }
    public static Boolean TryResolveCapability(UInt64 processId,KernelCapabilityHandle handle,KernelCapabilityRights requiredRights,out UInt64 objectId)
    {
        objectId=0UL;if(!Decode(handle,out UInt32 slot,out UInt32 generation))return false;
        fixed(UInt32* use=_state.CapabilityInUse,gen=_state.CapabilityGenerations) fixed(UInt64* owners=_state.CapabilityOwners,objects=_state.CapabilityObjects,r=_state.CapabilityRights)
        {if(use[slot]==0U||gen[slot]!=generation||owners[slot]!=processId||((KernelCapabilityRights)r[slot]&requiredRights)!=requiredRights)return false;objectId=objects[slot];return true;}
    }
    /// <summary>Resolves a raw user-supplied capability value without exposing capability slot/generation encoding outside the security subsystem.</summary>
    public static Boolean TryResolveCapabilityValue(UInt64 processId,UInt64 handleValue,KernelCapabilityRights requiredRights,out UInt64 objectId)
    { return TryResolveCapability(processId,new KernelCapabilityHandle(handleValue),requiredRights,out objectId); }
    public static Boolean TryCloseCapability(UInt64 processId,KernelCapabilityHandle handle)
    {if(!Decode(handle,out UInt32 slot,out UInt32 generation))return false;fixed(UInt32* use=_state.CapabilityInUse,gen=_state.CapabilityGenerations)fixed(UInt64* owners=_state.CapabilityOwners,objects=_state.CapabilityObjects,r=_state.CapabilityRights){if(use[slot]==0U||gen[slot]!=generation||owners[slot]!=processId)return false;use[slot]=0U;owners[slot]=objects[slot]=r[slot]=0UL;return true;}}
    public static Boolean TryDuplicateCapability(UInt64 processId,KernelCapabilityHandle source,KernelCapabilityRights rights,out KernelCapabilityHandle duplicate)
    {duplicate=default;if(!TryResolveCapability(processId,source,rights,out UInt64 objectId))return false;return TryCreateCapability(processId,objectId,rights,out duplicate);}

    private static Boolean Decode(KernelCapabilityHandle h,out UInt32 slot,out UInt32 generation){slot=0;generation=0;UInt32 encoded=(UInt32)(h.Value&0xFFFFFFFFUL);if(encoded==0U||encoded>MaximumCapabilities)return false;slot=encoded-1U;generation=(UInt32)(h.Value>>32);return generation!=0U;}
    private static void RevokeProcessCapabilities(UInt64 processId){fixed(UInt32* use=_state.CapabilityInUse)fixed(UInt64* owners=_state.CapabilityOwners,objects=_state.CapabilityObjects,r=_state.CapabilityRights)for(UInt32 i=0;i<MaximumCapabilities;i++)if(use[i]!=0U&&owners[i]==processId){use[i]=0U;owners[i]=objects[i]=r[i]=0UL;}}
}
