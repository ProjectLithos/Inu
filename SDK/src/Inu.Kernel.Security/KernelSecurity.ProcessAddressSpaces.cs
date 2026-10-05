using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Protection;
using Inu.Kernel.Smp;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Security;

/// <summary>Central process-isolation, user-pointer, W^X, syscall-policy and capability/handle authority.</summary>
public static unsafe partial class KernelSecurity
{
    /// <summary>Registers one private lower-half process address space and its intentionally unmapped guard range.</summary>
    public static Boolean RegisterProcessAddressSpace(UInt64 processId,UInt64 rootPhysicalAddress,UInt64 guardBase,UInt64 guardBytes)
    {
        if(!_initialized||processId==0UL||rootPhysicalAddress==0UL)return false; Int32 slot=FindProcess(processId); if(slot<0)slot=FindFreeProcess(); if(slot<0)return false;
        fixed(UInt64* ids=_state.ProcessIds,roots=_state.Roots,guards=_state.GuardBases,bytes=_state.GuardBytes) fixed(UInt32* masks=_state.SyscallMasks)
        { if(ids[slot]==0UL)_registered++; ids[slot]=processId; roots[slot]=rootPhysicalAddress; guards[slot]=guardBase; bytes[slot]=guardBytes; masks[slot]=0x0EU; }
        return guardBytes==0UL || IsRangeUnmapped(rootPhysicalAddress,guardBase,guardBytes);
    }
    public static Boolean UnregisterProcess(UInt64 processId)
    {
        Int32 slot=FindProcess(processId); if(slot<0)return false; RevokeProcessCapabilities(processId);
        fixed(UInt64* ids=_state.ProcessIds,roots=_state.Roots,guards=_state.GuardBases,bytes=_state.GuardBytes) fixed(UInt32* masks=_state.SyscallMasks)
        { ids[slot]=roots[slot]=guards[slot]=bytes[slot]=0UL; masks[slot]=0U; }
        if(_registered!=0U)_registered--; fixed(UInt64* current=_state.CurrentProcessIds)for(Int32 i=0;i<256;i++)if(current[i]==processId)current[i]=0UL; return true;
    }
    /// <summary>Updates the process identity used by protected syscall entry/exit.</summary>
    public static Boolean SetCurrentProcess(UInt64 processId){if(processId!=0UL&&FindProcess(processId)<0)return false;UInt32 cpu=0U;if(KernelSmp.IsInitialized()&&!KernelSmp.TryGetCurrentProcessorIndex(out cpu))return false;if(cpu>=256U)return false;fixed(UInt64* current=_state.CurrentProcessIds)current[cpu]=processId;return true;}
    public static Boolean TryGetCurrentProcess(out UInt64 processId){processId=0UL;if(!_initialized)return false;UInt32 cpu=0U;if(KernelSmp.IsInitialized()&&!KernelSmp.TryGetCurrentProcessorIndex(out cpu))return false;if(cpu>=256U)return false;fixed(UInt64* current=_state.CurrentProcessIds)processId=current[cpu];return processId!=0UL;}

    public static Boolean TryGetAddressSpace(UInt64 processId,out KernelAddressSpaceSecurityInfo info)
    {
        info=default; if(processId==0UL){UInt64 kr=KernelVirtualMemory.GetRootPhysicalAddress();if(kr==0UL)return false;info=new(0UL,kr,KernelAddressSpaceDomain.Kernel,0UL,0UL);return true;}
        Int32 slot=FindProcess(processId);if(slot<0)return false;fixed(UInt64* roots=_state.Roots,guards=_state.GuardBases,bytes=_state.GuardBytes){info=new(processId,roots[slot],KernelAddressSpaceDomain.User,guards[slot],bytes[slot]);return true;}
    }

    /// <summary>Inspects one process mapping without switching CR3. Intended for bounded fault diagnostics.</summary>
    public static Boolean TryInspectUserMapping(UInt64 processId,UInt64 address,out KernelUserMappingInspection inspection)
    {
        inspection=default;if(!_initialized||processId==0UL)return false;Int32 slot=FindProcess(processId);if(slot<0)return false;
        fixed(UInt64* roots=_state.Roots){UInt64 root=roots[slot];if(!TryTranslate(root,address,out UInt64 physical,out UInt64 flags,out UInt64 pageSize)){inspection=new(processId,address,root,false,0UL,0UL,0UL);return true;}inspection=new(processId,address,root,true,physical,flags,pageSize);return true;}
    }
}
