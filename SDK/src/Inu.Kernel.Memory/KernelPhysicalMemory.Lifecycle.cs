using System;
using Inu.Kernel.Console;

namespace Inu.Kernel.Memory;

public static unsafe partial class KernelPhysicalMemory
{
    public static Boolean IsInitialized() => _initialized;

    /// <summary>Gets the status produced by the most recent initialization/allocation/release operation.</summary>
    public static KernelPhysicalMemoryStatus GetLastStatus() => _lastStatus;

    /// <summary>Initializes the default extent manager directly from the retained final UEFI memory map.</summary>
    /// <returns><see langword="true"/> when every immediately allocatable firmware range was accepted.</returns>
    public static Boolean Initialize<TMemoryMap,TDescriptorLayout,TWorkspace>(TMemoryMap memoryMap, TDescriptorLayout descriptorLayout, TWorkspace workspace) where TMemoryMap : IFinalMemoryMapBufferContext where TDescriptorLayout : IMemoryDescriptorLayoutContext where TWorkspace : IBootstrapPageTableWorkspaceContext
    {
        if (_initialized) return SetFailure(KernelPhysicalMemoryStatus.AlreadyInitialized);
        if (!memoryMap.HasFinalMemoryMapBuffer() || !descriptorLayout.HasMemoryDescriptorLayout()) return SetFailure(KernelPhysicalMemoryStatus.InvalidParameter);

        UInt64 mapAddress = memoryMap.GetFinalMemoryMapAddress();
        UInt64 mapLength = memoryMap.GetFinalMemoryMapLength();
        UInt64 descriptorSize = descriptorLayout.GetMemoryDescriptorSize();
        if (mapAddress == 0UL || mapLength == 0UL || descriptorSize < 40UL || mapLength % descriptorSize != 0UL)
            return SetFailure(KernelPhysicalMemoryStatus.InvalidParameter);

        UInt64 descriptorCount = mapLength / descriptorSize;
        if (descriptorCount == 0UL || descriptorCount > 0x7FFFFFFFUL)
            return SetFailure(KernelPhysicalMemoryStatus.InvalidParameter);

        UInt64 bootstrapAddress = workspace.GetBootstrapPageTableWorkspaceAddress();
        UInt64 bootstrapPages = workspace.GetBootstrapPageTableWorkspacePages();
        if (bootstrapAddress == 0UL || bootstrapPages == 0UL || (bootstrapAddress & 0xFFFUL) != 0UL ||
            bootstrapPages > 0xFFFFFFFFFFFFFFFFUL / PageSize || bootstrapAddress > 0xFFFFFFFFFFFFFFFFUL - bootstrapPages * PageSize)
            return SetFailure(KernelPhysicalMemoryStatus.InvalidParameter);

        ResetState();
        _bootstrapWorkspaceAddress = bootstrapAddress;
        _bootstrapWorkspacePages = bootstrapPages;
        for (UInt64 index = 0; index < descriptorCount; index++)
        {
            UInt64 offset = index * descriptorSize;
            if (mapAddress > 0xFFFFFFFFFFFFFFFFUL - offset) return SetFailure(KernelPhysicalMemoryStatus.InvalidParameter);
            Byte* descriptor = (Byte*)(nuint)(mapAddress + offset);
            UInt32 type = *(UInt32*)(descriptor + 0);
            UInt64 start = *(UInt64*)(descriptor + 8);
            UInt64 pages = *(UInt64*)(descriptor + 24);
            UInt64 attributes = *(UInt64*)(descriptor + 32);
            if (pages == 0UL || (start & 0xFFFUL) != 0UL || pages > 0xFFFFFFFFFFFFFFFFUL / PageSize)
                return SetFailure(KernelPhysicalMemoryStatus.InvalidParameter);
            if ((attributes & UefiRuntimeAttribute) != 0UL) continue;
            if (type != UefiConventionalMemory) continue;
            if (!InsertFreeExtent(start / PageSize, pages)) return SetFailure(KernelPhysicalMemoryStatus.ExtentCapacityExhausted);
            if (_managedPages > 0xFFFFFFFFFFFFFFFFUL - pages) return SetFailure(KernelPhysicalMemoryStatus.InvalidParameter);
            _managedPages += pages;
            _freePages += pages;
        }

        if (_managedPages == 0UL) return SetFailure(KernelPhysicalMemoryStatus.InvalidParameter);
        _initialized = true;
        _lastStatus = KernelPhysicalMemoryStatus.Success;
        return true;
    }

    /// <summary>Allocates contiguous 4 KiB physical frames using first-fit extent selection.</summary>
    /// <param name="pageCount">Number of contiguous pages required.</param>
    /// <param name="alignmentPages">Power-of-two page alignment; use one for ordinary page alignment.</param>
    /// <param name="allocation">Receives the live allocation token and physical range.</param>
    /// <returns><see langword="true"/> when a matching free extent was split successfully.</returns>
}
