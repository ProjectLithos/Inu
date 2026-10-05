using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Acpi;
using Inu.Kernel.Console;
using Inu.Kernel.Heap;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Time;

namespace Inu.Kernel.Smp;

public static unsafe partial class KernelSmp
{
    private static Boolean PopulateProcessorRecords()
    {
        if (_records == null) return false;
        for (UInt32 index = 0U; index < _processorCount; index++)
        {
            if (!KernelAcpi.TryGetProcessor(index, out AcpiProcessorInfo acpi)) return false;
            PerCpuRecord* record = _records + index;
            record->Index = index; record->ApicId = acpi.ApicId; record->AcpiUid = acpi.AcpiUid;
            record->Flags = acpi.IsX2Apic ? 1U : 0U; record->StartupState = (UInt32)KernelProcessorStartupState.Offline;
            record->SchedulerReady = 0UL; record->KernelStackBase = 0UL; record->KernelStackTop = 0UL; record->DescriptorState = 0UL; record->EmergencyStackBase = 0UL; record->SchedulerContext = 0UL; for(UInt32 slot=0U;slot<PerCpuStorageSlots;slot++) record->Storage[slot]=0UL;
        }
        return true;
    }

    private static Boolean FindProcessor(UInt32 apicId, out UInt32 index)
    {
        index = 0U;
        if (_records == null) return false;
        for (UInt32 candidate = 0U; candidate < _processorCount; candidate++) if ((_records + candidate)->ApicId == apicId) { index = candidate; return true; }
        return false;
    }

    private static Boolean StartApplicationProcessors()
    {
        if (!KernelSmpMath.TryGetStartupVector(_trampolineAddress, out Byte vector))
        {
#if DEBUG
            DebugApTrace("startup vector validation FAILED for trampoline = ", _trampolineAddress);
#endif
            return false;
        }
        UInt64 pageTableRoot = Native.ReadPageTableRoot();
#if DEBUG
        DebugApTrace("startup vector = ", vector);
        DebugApTrace("BSP CR3/page-table root = ", pageTableRoot);
#endif
        if (pageTableRoot == 0UL || pageTableRoot > 0xFFFFFFFFUL) { MarkRemainingUnsupported(); return false; }
        for (UInt32 index = 0U; index < _processorCount; index++)
        {
            if (index == _bootstrapIndex) continue;
            StartApplicationProcessor(index);
        }
        return true;
    }

}
