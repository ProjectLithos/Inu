using System;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Scheduler;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Protection;

/// <summary>Establishes and validates the x64 ring-0/ring-3 protection boundary.</summary>
public static class KernelProtection
{
    private static Boolean _initialized, _writeProtect, _executeDisable, _smepSupported, _smepEnabled, _smapSupported;

    /// <summary>Initializes supervisor write protection and available safe execution protections.</summary>
    public static Boolean Initialize()
    {
        if (_initialized) return true;
        if (!KernelVirtualMemory.IsInitialized() || !KernelScheduler.IsInitialized()) return false;
        if (!Native.EnableKernelWriteProtect()) return false;
        _writeProtect = Native.IsKernelWriteProtectEnabled();
        _executeDisable = Native.IsExecuteDisableEnabled();
        if (!_writeProtect || !_executeDisable) return false;
        _smepSupported = Native.SupportsSmep();
        _smapSupported = Native.SupportsSmap();
        _smepEnabled = !_smepSupported || Native.EnableSmep();
        if (!_smepEnabled) return false;
        _initialized = true;
        return true;
    }

    public static Boolean IsInitialized() => _initialized;

    /// <summary>Gets the current privilege-separation capabilities.</summary>
    public static KernelProtectionCapabilities GetCapabilities() => new(_writeProtect, _executeDisable, _smepSupported, _smepSupported && _smepEnabled, _smapSupported);

    /// <summary>Validates and maps one user-accessible page while refusing kernel-half addresses.</summary>
    public static Boolean TryMapUserPage(UInt64 virtualAddress, UInt64 physicalAddress, KernelVirtualMemoryProtection protection)
    {
        if (!_initialized || !KernelProtectionMath.IsUserRange(virtualAddress, 4096UL) || !IsUserProtectionAllowed(protection)) return false;
        KernelVirtualMemoryProtection userProtection = protection | KernelVirtualMemoryProtection.User | KernelVirtualMemoryProtection.Read;
        return KernelVirtualMemory.TryMap(virtualAddress, physicalAddress, KernelVirtualPageSize.Page4KiB, userProtection);
    }

    /// <summary>Changes one existing user mapping without allowing supervisor-only protection.</summary>
    public static Boolean TryProtectUserPage(UInt64 virtualAddress, KernelVirtualMemoryProtection protection)
    {
        if (!_initialized || !KernelProtectionMath.IsUserRange(virtualAddress, 4096UL) || !IsUserProtectionAllowed(protection)) return false;
        KernelVirtualMemoryProtection userProtection = protection | KernelVirtualMemoryProtection.User | KernelVirtualMemoryProtection.Read;
        return KernelVirtualMemory.TryProtect(virtualAddress, userProtection);
    }


    /// <summary>Enforces W^X for user mappings. NX is applied by the paging layer whenever Execute is absent.</summary>
    public static Boolean IsUserProtectionAllowed(KernelVirtualMemoryProtection protection)
        => !((protection & KernelVirtualMemoryProtection.Write) != 0 && (protection & KernelVirtualMemoryProtection.Execute) != 0);

    /// <summary>Creates a validated future ring-3 transition context for the process/syscall stages.</summary>
    public static Boolean TryCreateUserModeContext(UInt64 entryPoint, UInt64 stackTop, UInt64 argument, out UserModeContext context)
    {
        context=default;
        if (!_initialized || !KernelProtectionMath.IsValidUserEntry(entryPoint) || !KernelProtectionMath.IsValidUserStack(stackTop)) return false;
        context = new UserModeContext(entryPoint, stackTop, argument);
        return true;
    }
}
