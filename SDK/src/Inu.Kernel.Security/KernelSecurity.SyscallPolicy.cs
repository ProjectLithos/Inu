using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Protection;
using Inu.Kernel.Smp;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Security;

/// <summary>Central process-isolation, user-pointer, W^X, syscall-policy and capability/handle authority.</summary>
public static unsafe partial class KernelSecurity
{
    /// <summary>Enables/disables one syscall ABI for a process. ABI values are Inu=1, Linux=2, NT=3.</summary>
    public static Boolean TrySetSyscallAbiPolicy(UInt64 processId,UInt32 abi,Boolean allowed)
    {Int32 slot=FindProcess(processId);if(slot<0||abi<1U||abi>3U)return false;fixed(UInt32* masks=_state.SyscallMasks){UInt32 bit=1U<<(Int32)abi;if(allowed)masks[slot]|=bit;else masks[slot]&=~bit;}return true;}
    public static Boolean TryValidateSyscall(UInt64 processId,UInt32 abi,UInt32 service)
    {Int32 slot=FindProcess(processId);if(slot<0||abi<1U||abi>3U||service>MaximumSyscallService)return false;fixed(UInt32* masks=_state.SyscallMasks)return (masks[slot]&(1U<<(Int32)abi))!=0U;}
    public static Boolean TryValidateCurrentSyscall(UInt32 abi,UInt32 service)=>TryGetCurrentProcess(out UInt64 processId)&&TryValidateSyscall(processId,abi,service);
}
