using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Protection;
using Inu.Kernel.Smp;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Security;

/// <summary>Central process-isolation, user-pointer, W^X, syscall-policy and capability/handle authority.</summary>
public static unsafe partial class KernelSecurity
{
    private static Int32 FindProcess(UInt64 id){fixed(UInt64* ids=_state.ProcessIds)for(Int32 i=0;i<(Int32)MaximumProcesses;i++)if(ids[i]==id)return i;return -1;}
    private static Int32 FindFreeProcess(){fixed(UInt64* ids=_state.ProcessIds)for(Int32 i=0;i<(Int32)MaximumProcesses;i++)if(ids[i]==0UL)return i;return -1;}
    private static Boolean RangesOverlap(UInt64 a,UInt64 al,UInt64 b,UInt64 bl){if(al==0UL||bl==0UL)return false;UInt64 ae=a+al-1UL,be=b+bl-1UL;return a<=be&&b<=ae;}
    private static Boolean IsRangeUnmapped(UInt64 root,UInt64 address,UInt64 length){if(length==0UL)return true;for(UInt64 p=0;p<length;p+=4096UL)if(TryTranslate(root,address+p,out UInt64 _,out UInt64 _,out UInt64 _))return false;return true;}
    private static Boolean TryTable(UInt64 physical,out UInt64* table){table=(UInt64*)0;if(!KernelVirtualMemory.TryGetPageTableAccessAddress(physical,out UInt64 v))return false;table=(UInt64*)(nuint)v;return true;}
    private static Boolean TryTranslate(UInt64 root,UInt64 va,out UInt64 pa,out UInt64 flags,out UInt64 pageSize)
    {
        pa=flags=pageSize=0UL;if(!TryTable(root,out UInt64* pml4))return false;UInt64 e4=pml4[(va>>39)&511UL];if((e4&Present)==0||!TryTable(e4&AddressMask,out UInt64* pdpt))return false;UInt64 e3=pdpt[(va>>30)&511UL];if((e3&Present)==0)return false;if((e3&Large)!=0){pageSize=1UL<<30;flags=e3;pa=(e3&0x000FFFFFC0000000UL)|(va&(pageSize-1UL));return true;}if(!TryTable(e3&AddressMask,out UInt64* pd))return false;UInt64 e2=pd[(va>>21)&511UL];if((e2&Present)==0)return false;if((e2&Large)!=0){pageSize=1UL<<21;flags=e2;pa=(e2&0x000FFFFFFFE00000UL)|(va&(pageSize-1UL));return true;}if(!TryTable(e2&AddressMask,out UInt64* pt))return false;UInt64 e1=pt[(va>>12)&511UL];if((e1&Present)==0)return false;pageSize=4096UL;flags=e1;pa=(e1&AddressMask)|(va&4095UL);return true;
    }
    private static Boolean TryProtectLeaf(UInt64 root,UInt64 va,KernelVirtualMemoryProtection protection)
    {
        if(!TryTable(root,out UInt64* pml4))return false;UInt64 e4=pml4[(va>>39)&511UL];if((e4&Present)==0||!TryTable(e4&AddressMask,out UInt64* pdpt))return false;UInt64 e3=pdpt[(va>>30)&511UL];if((e3&Present)==0||(e3&Large)!=0||!TryTable(e3&AddressMask,out UInt64* pd))return false;UInt64 e2=pd[(va>>21)&511UL];if((e2&Present)==0||(e2&Large)!=0||!TryTable(e2&AddressMask,out UInt64* pt))return false;UInt64* leaf=&pt[(va>>12)&511UL];if((*leaf&Present)==0)return false;UInt64 f=Present|User;if((protection&KernelVirtualMemoryProtection.Write)!=0)f|=Writable;if((protection&KernelVirtualMemoryProtection.Execute)==0)f|=NoExecute;*leaf=(*leaf&AddressMask)|f;return true;
    }
}
