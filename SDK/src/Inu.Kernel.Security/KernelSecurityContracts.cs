using System;

namespace Inu.Kernel.Security;

/// <summary>Read-only diagnostic snapshot of one process virtual mapping.</summary>
public readonly struct KernelUserMappingInspection
{
    internal KernelUserMappingInspection(UInt64 processId,UInt64 virtualAddress,UInt64 rootPhysicalAddress,Boolean mapped,UInt64 physicalAddress,UInt64 rawFlags,UInt64 pageSize)
    { ProcessId=processId;VirtualAddress=virtualAddress;RootPhysicalAddress=rootPhysicalAddress;Mapped=mapped;PhysicalAddress=physicalAddress;RawFlags=rawFlags;PageSize=pageSize; }
    public UInt64 ProcessId { get; }
    public UInt64 VirtualAddress { get; }
    public UInt64 RootPhysicalAddress { get; }
    public Boolean Mapped { get; }
    public UInt64 PhysicalAddress { get; }
    public UInt64 RawFlags { get; }
    public UInt64 PageSize { get; }
}



/// <summary>Identifies the two formally separated x64 address-space domains.</summary>
public enum KernelAddressSpaceDomain : Byte { Kernel=0, User=1 }
/// <summary>Identifies the architectural privilege levels used by Inu.</summary>
public enum KernelPrivilegeRing : Byte { Kernel=0, User=3 }
[Flags] public enum KernelUserMemoryAccess : UInt32 { None=0, Read=1, Write=2, Execute=4 }
[Flags] public enum KernelCapabilityRights : UInt64 { None=0, Read=1, Write=2, Execute=4, Map=8, Wait=16, Signal=32, Invoke=64, Manage=128, Duplicate=256, Transfer=512, All=0x3FF }

/// <summary>An opaque process-scoped capability handle. Slot and generation are intentionally not public API.</summary>
public readonly struct KernelCapabilityHandle
{
    internal KernelCapabilityHandle(UInt64 value){Value=value;}
    public UInt64 Value { get; }
    public Boolean IsValid => Value!=0UL;
}

public readonly struct KernelAddressSpaceSecurityInfo
{
    internal KernelAddressSpaceSecurityInfo(UInt64 processId,UInt64 root,KernelAddressSpaceDomain domain,UInt64 guardBase,UInt64 guardBytes)
    { ProcessId=processId; RootPhysicalAddress=root; Domain=domain; GuardBase=guardBase; GuardBytes=guardBytes; }
    public UInt64 ProcessId { get; }
    public UInt64 RootPhysicalAddress { get; }
    public KernelAddressSpaceDomain Domain { get; }
    public UInt64 GuardBase { get; }
    public UInt64 GuardBytes { get; }
    public KernelPrivilegeRing PrivilegeRing => Domain==KernelAddressSpaceDomain.Kernel ? KernelPrivilegeRing.Kernel : KernelPrivilegeRing.User;
}

public readonly struct KernelSecurityCapabilities
{
    internal KernelSecurityCapabilities(Boolean nx,Boolean wx,Boolean guard,UInt32 processes,UInt32 handles)
    { NxEnabled=nx; WriteXorExecuteEnforced=wx; GuardPagesSupported=guard; RegisteredProcesses=processes; MaximumCapabilityHandles=handles; }
    public Boolean NxEnabled { get; }
    public Boolean WriteXorExecuteEnforced { get; }
    public Boolean GuardPagesSupported { get; }
    public UInt32 RegisteredProcesses { get; }
    public UInt32 MaximumCapabilityHandles { get; }
    public KernelPrivilegeRing KernelRing => KernelPrivilegeRing.Kernel;
    public KernelPrivilegeRing UserRing => KernelPrivilegeRing.User;
}
