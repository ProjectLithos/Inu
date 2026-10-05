using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Protection;
using Inu.Kernel.Smp;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Security;

/// <summary>Central process-isolation, user-pointer, W^X, syscall-policy and capability/handle authority.</summary>
public static unsafe partial class KernelSecurity
{
    private const UInt32 MaximumProcesses=16U, MaximumCapabilities=256U, MaximumSyscallService=4095U;
    private const UInt64 Present=1UL, Writable=2UL, User=4UL, Large=128UL, NoExecute=1UL<<63, AddressMask=0x000FFFFFFFFFF000UL;
    private struct SecurityState
    {
        internal fixed UInt64 ProcessIds[(Int32)MaximumProcesses],Roots[(Int32)MaximumProcesses],GuardBases[(Int32)MaximumProcesses],GuardBytes[(Int32)MaximumProcesses];
        internal fixed UInt32 SyscallMasks[(Int32)MaximumProcesses];
        internal fixed UInt64 CurrentProcessIds[256];
        internal fixed UInt64 CapabilityOwners[(Int32)MaximumCapabilities],CapabilityObjects[(Int32)MaximumCapabilities],CapabilityRights[(Int32)MaximumCapabilities];
        internal fixed UInt32 CapabilityGenerations[(Int32)MaximumCapabilities],CapabilityInUse[(Int32)MaximumCapabilities];
    }
#pragma warning disable CS0169
    private static SecurityState _state;
#pragma warning restore CS0169
    private static Boolean _initialized; private static UInt32 _registered;

    public static Boolean Initialize()
    {
        if(_initialized)return true;
        if(!KernelProtection.IsInitialized()||!KernelVirtualMemory.IsInitialized())return false;
        KernelProtectionCapabilities p=KernelProtection.GetCapabilities(); if(!p.ExecuteDisableEnabled)return false;
        _initialized=true; return true;
    }
    public static Boolean IsInitialized()=>_initialized;
    public static KernelSecurityCapabilities GetCapabilities()=>new(KernelProtection.GetCapabilities().ExecuteDisableEnabled,true,true,_registered,MaximumCapabilities);




}
