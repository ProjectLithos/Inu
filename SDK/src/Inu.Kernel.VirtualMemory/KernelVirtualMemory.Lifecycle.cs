using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Memory;

namespace Inu.Kernel.VirtualMemory;

public static unsafe partial class KernelVirtualMemory
{
    public static Boolean IsInitialized() => _initialized;

    /// <summary>Gets the status produced by the most recent virtual-memory operation.</summary>
    public static KernelVirtualMemoryStatus GetLastStatus() => _lastStatus;

    /// <summary>Gets a freestanding-safe symbolic name for the latest virtual-memory status.</summary>
    /// <returns>A stable status name that requires no enum formatting support.</returns>
    public static String GetLastStatusName()
    {
        if (_lastStatus == KernelVirtualMemoryStatus.Success) return "Success";
        if (_lastStatus == KernelVirtualMemoryStatus.InvalidParameter) return "InvalidParameter";
        if (_lastStatus == KernelVirtualMemoryStatus.NotInitialized) return "NotInitialized";
        if (_lastStatus == KernelVirtualMemoryStatus.AlreadyInitialized) return "AlreadyInitialized";
        if (_lastStatus == KernelVirtualMemoryStatus.NonCanonicalAddress) return "NonCanonicalAddress";
        if (_lastStatus == KernelVirtualMemoryStatus.AlreadyMapped) return "AlreadyMapped";
        if (_lastStatus == KernelVirtualMemoryStatus.NotMapped) return "NotMapped";
        if (_lastStatus == KernelVirtualMemoryStatus.PhysicalAllocationFailed) return "PhysicalAllocationFailed";
        if (_lastStatus == KernelVirtualMemoryStatus.PageTableCapacityExhausted) return "PageTableCapacityExhausted";
        if (_lastStatus == KernelVirtualMemoryStatus.ArchitectureOperationFailed) return "ArchitectureOperationFailed";
        if (_lastStatus == KernelVirtualMemoryStatus.UnsupportedPageSize) return "UnsupportedPageSize";
        if (_lastStatus == KernelVirtualMemoryStatus.UnsupportedProtection) return "UnsupportedProtection";
        return "Unknown";
    }

    /// <summary>Gets the current x64 root page-table physical address.</summary>
    public static UInt64 GetRootPhysicalAddress() => _rootPhysicalAddress;

    /// <summary>Initializes virtual memory by attaching to and protecting the active x64 page-table hierarchy.</summary>
    /// <returns><see langword="true"/> when the active root and all reachable page-table pages were accepted.</returns>
    public static Boolean Initialize()
    {
        if (_initialized) return SetFailure(KernelVirtualMemoryStatus.AlreadyInitialized);
        if (!KernelPhysicalMemory.IsInitialized()) return SetFailure(KernelVirtualMemoryStatus.InvalidParameter);
        KernelPhysicalMemoryStatistics physicalStatistics = KernelPhysicalMemory.GetStatistics();
        if (physicalStatistics.LiveAllocationCount != 0) return SetFailure(KernelVirtualMemoryStatus.InvalidParameter);
        UInt64 root = Native.ReadPageTableRoot() & AddressMask4KiB;
        if (root == 0UL || (root & 0xFFFUL) != 0UL) return SetFailure(KernelVirtualMemoryStatus.ArchitectureOperationFailed);
        ResetState();
        _rootPhysicalAddress = root;
        if (!CaptureDirectMapPlan()) return false;
        _executeDisableEnabled = Native.EnableExecuteDisable();
        _page1GiBSupported = Native.Supports1GiBPages();
        if (!ProtectTableHierarchy(root, 4)) return false;
        _initialized = true;
        _lastStatus = KernelVirtualMemoryStatus.Success;
        return true;
    }

    /// <summary>Installs one physical-to-virtual leaf mapping into the active x64 address space.</summary>
    /// <returns><see langword="true"/> when the leaf was installed and its translation invalidated.</returns>
}
