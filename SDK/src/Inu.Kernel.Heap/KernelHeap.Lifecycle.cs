using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Memory;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Heap;

public static unsafe partial class KernelHeap
{
    public static Boolean IsInitialized() => _initialized;

    /// <summary>Gets the most recent heap status.</summary>
    public static KernelHeapStatus GetLastStatus() => _status;

    /// <summary>Gets a freestanding-safe symbolic status name.</summary>
    /// <returns>A stable status string.</returns>
    public static String GetLastStatusName()
    {
        if (_status == KernelHeapStatus.Success) return "Success";
        if (_status == KernelHeapStatus.DependencyNotInitialized) return "DependencyNotInitialized";
        if (_status == KernelHeapStatus.AlreadyInitialized) return "AlreadyInitialized";
        if (_status == KernelHeapStatus.InvalidParameter) return "InvalidParameter";
        if (_status == KernelHeapStatus.OutOfMemory) return "OutOfMemory";
        if (_status == KernelHeapStatus.MetadataCapacityExhausted) return "MetadataCapacityExhausted";
        if (_status == KernelHeapStatus.MappingFailed) return "MappingFailed";
        if (_status == KernelHeapStatus.AllocationNotFound) return "AllocationNotFound";
        if (_status == KernelHeapStatus.DoubleFreeDetected) return "DoubleFreeDetected";
        if (_status == KernelHeapStatus.GuardCorruptionDetected) return "GuardCorruptionDetected";
        return "Unknown";
    }

    /// <summary>Commits the first 64 KiB of the standard kernel-heap reservation.</summary>
    /// <returns><see langword="true"/> when dependencies and initial mappings are ready.</returns>
    public static Boolean Initialize()
    {
        if (_initialized)
        {
            _status = KernelHeapStatus.AlreadyInitialized;
            return false;
        }
        if (!KernelAddressSpace.IsInitialized() || !KernelVirtualMemory.IsInitialized() || !KernelPhysicalMemory.IsInitialized())
        {
            _status = KernelHeapStatus.DependencyNotInitialized;
            return false;
        }
        if (!InitializeDiagnosticMetadata()) return false;
        Reset();
        if (!Grow(GrowthPages)) return false;
        _initialized = true;
        _status = KernelHeapStatus.Success;
        SynchronizeDiagnosticHeader();
        return true;
    }

    /// <summary>Allocates raw virtual bytes using address-ordered first fit and optional zero filling.</summary>
    /// <returns><see langword="true"/> when a live allocation was created.</returns>
}
