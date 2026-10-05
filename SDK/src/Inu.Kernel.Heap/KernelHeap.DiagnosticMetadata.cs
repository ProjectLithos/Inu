using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Memory;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.Heap;

public static unsafe partial class KernelHeap
{
    private static State* GetState() => (State*)unchecked((nuint)(DiagnosticMetadataAddress + DiagnosticStateOffset));

    private static Boolean InitializeDiagnosticMetadata()
    {
        if (_diagnosticMetadataReady) return true;
        UInt64 pages = DiagnosticMetadataLength / PageSize;
        if (!KernelPhysicalMemory.TryAllocate(pages, 1UL, out KernelPhysicalAllocation physical))
        {
            _status = KernelHeapStatus.OutOfMemory;
            return false;
        }
        UInt64 mapped = 0UL;
        KernelVirtualMemoryProtection protection = KernelVirtualMemoryProtection.Read | KernelVirtualMemoryProtection.Write | KernelVirtualMemoryProtection.Global;
        for (UInt64 page = 0UL; page < pages; page++)
        {
            UInt64 virtualAddress = DiagnosticMetadataAddress + page * PageSize;
            UInt64 physicalAddress = physical.StartAddress + page * PageSize;
            if (!KernelVirtualMemory.TryMap(virtualAddress, physicalAddress, KernelVirtualPageSize.Page4KiB, protection))
            {
                for (UInt64 undo = 0UL; undo < mapped; undo++) KernelVirtualMemory.TryUnmap(DiagnosticMetadataAddress + undo * PageSize);
                KernelPhysicalMemory.TryRelease(physical);
                _status = KernelHeapStatus.MappingFailed;
                return false;
            }
            mapped++;
        }
        Byte* bytes = (Byte*)unchecked((nuint)DiagnosticMetadataAddress);
        for (UInt64 i = 0UL; i < DiagnosticMetadataLength; i++) bytes[i] = 0;
        _diagnosticMetadataReady = true;
        SynchronizeDiagnosticHeader();
        return true;
    }

    private static void SynchronizeDiagnosticHeader()
    {
        if (!_diagnosticMetadataReady) return;
        UInt64* qwords = (UInt64*)unchecked((nuint)DiagnosticMetadataAddress);
        UInt32* dwords = (UInt32*)unchecked((nuint)DiagnosticMetadataAddress);
        Byte* bytes = (Byte*)unchecked((nuint)DiagnosticMetadataAddress);
        qwords[0] = DiagnosticMagic;
        dwords[2] = DiagnosticMetadataVersion;
        dwords[3] = MaximumBlocks;
        qwords[2] = _committed;
        qwords[3] = _allocated;
        qwords[4] = _peak;
        qwords[5] = _nextToken;
        dwords[12] = (UInt32)_live;
        dwords[13] = (UInt32)_status;
        bytes[56] = _initialized ? (Byte)1 : (Byte)0;
    }
}
