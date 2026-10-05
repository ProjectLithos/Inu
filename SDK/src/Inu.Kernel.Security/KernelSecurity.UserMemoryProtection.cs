using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Protection;
using Inu.Kernel.Smp;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Security;

/// <summary>Central process-isolation, user-pointer, W^X, syscall-policy and capability/handle authority.</summary>
public static unsafe partial class KernelSecurity
{
    /// <summary>Validates every page of a user range against that process's own page-table root.</summary>
    public static Boolean TryValidateUserPointer(UInt64 processId,UInt64 address,UInt64 length,KernelUserMemoryAccess access)
    {
        if(!_initialized||!KernelProtectionMath.IsUserRange(address,length)||processId==0UL)return false; Int32 slot=FindProcess(processId);if(slot<0)return false;
        fixed(UInt64* roots=_state.Roots,guards=_state.GuardBases,guardBytes=_state.GuardBytes)
        {
            if(RangesOverlap(address,length,guards[slot],guardBytes[slot]))return false; UInt64 current=address,remaining=length;
            while(remaining!=0UL){if(!TryTranslate(roots[slot],current,out UInt64 _,out UInt64 flags,out UInt64 pageSize))return false;if((flags&User)==0UL)return false;if((access&KernelUserMemoryAccess.Write)!=0 && (flags&Writable)==0UL)return false;if((access&KernelUserMemoryAccess.Execute)!=0 && (flags&NoExecute)!=0UL)return false;UInt64 rem=pageSize-(current&(pageSize-1UL));UInt64 step=remaining<rem?remaining:rem;current+=step;remaining-=step;}
        }
        return true;
    }

    /// <summary>Applies page permissions to a private process range. W+X is rejected unconditionally.</summary>
    public static Boolean TryProtectUserRange(UInt64 processId,UInt64 address,UInt64 length,KernelVirtualMemoryProtection protection)
    {
        if(!_initialized||!IsUserProtectionAllowed(protection)||!KernelProtectionMath.IsUserRange(address,length)||(address&4095UL)!=0UL||(length&4095UL)!=0UL)return false;Int32 slot=FindProcess(processId);if(slot<0)return false;
        fixed(UInt64* roots=_state.Roots,guards=_state.GuardBases,guardBytes=_state.GuardBytes){if(RangesOverlap(address,length,guards[slot],guardBytes[slot]))return false;for(UInt64 p=0;p<length;p+=4096UL)if(!TryProtectLeaf(roots[slot],address+p,protection))return false;}
        return true;
    }
    public static Boolean IsUserProtectionAllowed(KernelVirtualMemoryProtection protection)
    { if((protection&KernelVirtualMemoryProtection.User)==0)return false; return !((protection&KernelVirtualMemoryProtection.Write)!=0&&(protection&KernelVirtualMemoryProtection.Execute)!=0); }
    public static Boolean TryValidateExecutableRange(UInt64 processId,UInt64 address,UInt64 length)=>TryValidateUserPointer(processId,address,length,KernelUserMemoryAccess.Read|KernelUserMemoryAccess.Execute);
}
