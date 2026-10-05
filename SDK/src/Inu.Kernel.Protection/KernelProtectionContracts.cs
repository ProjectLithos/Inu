using System;

namespace Inu.Kernel.Protection;

/// <summary>Reports the active x64 user/kernel protection boundary.</summary>
public enum KernelProtectionRing : Byte { Kernel = 0, User = 3 }

public readonly struct KernelProtectionCapabilities
{
    internal KernelProtectionCapabilities(Boolean writeProtect, Boolean nx, Boolean smepSupported, Boolean smepEnabled, Boolean smapSupported)
    { WriteProtectEnabled=writeProtect; ExecuteDisableEnabled=nx; SmepSupported=smepSupported; SmepEnabled=smepEnabled; SmapSupported=smapSupported; }
    public Boolean WriteProtectEnabled { get; }
    public Boolean ExecuteDisableEnabled { get; }
    public Boolean SmepSupported { get; }
    public Boolean SmepEnabled { get; }
    public Boolean SmapSupported { get; }
    public KernelProtectionRing KernelRing => KernelProtectionRing.Kernel;
    public KernelProtectionRing UserRing => KernelProtectionRing.User;
    public UInt16 UserDataSelector => KernelProtectionMath.UserDataSelector;
    public UInt16 UserCodeSelector => KernelProtectionMath.UserCodeSelector;
    public UInt64 MinimumUserAddress => KernelProtectionMath.MinimumUserAddress;
    public UInt64 MaximumUserAddress => KernelProtectionMath.MaximumUserAddress;
}

/// <summary>Describes a validated future ring-3 transition without performing it.</summary>
public readonly struct UserModeContext
{
    internal UserModeContext(UInt64 entryPoint, UInt64 stackTop, UInt64 argument) { EntryPoint=entryPoint; StackTop=stackTop; Argument=argument; }
    public UInt64 EntryPoint { get; }
    public UInt64 StackTop { get; }
    public UInt64 Argument { get; }
    public UInt16 CodeSelector => KernelProtectionMath.UserCodeSelector;
    public UInt16 DataSelector => KernelProtectionMath.UserDataSelector;
}
