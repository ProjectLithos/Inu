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
    private static Boolean SendInitSequence(UInt32 apicId)
    {
#if DEBUG
        DebugApTrace("INIT assert -> APIC ID = ", apicId);
#endif
        if (!SendIpi(apicId, InitAssert)) return false;
        if (!KernelTime.DelayNanoseconds(InitDelayNanoseconds)) return false;
#if DEBUG
        DebugApTrace("INIT deassert -> APIC ID = ", apicId);
#endif
        if (!SendIpi(apicId, InitDeassert)) return false;
        return KernelTime.DelayNanoseconds(StartupDelayNanoseconds);
    }

    private static Boolean SendIpi(UInt32 apicId, UInt32 command)
    {
#if DEBUG
        if((command&0x700U)!=0U || command==InitAssert || command==InitDeassert)
        {
            DebugApTrace("ICR destination APIC ID = ", apicId);
            DebugApTrace("ICR command = ", command);
        }
#endif
        if (!WaitForIcrIdle()) return false;
        if (!Native.WriteMmio32(_localApicBase + LocalApicIcrHigh, apicId << 24)) return false;
        if (!Native.WriteMmio32(_localApicBase + LocalApicIcrLow, command)) return false;
        Boolean idle=WaitForIcrIdle();
#if DEBUG
        if(!idle) DebugApTrace("ICR remained delivery-pending after command = ", command);
#endif
        return idle;
    }

    private static Boolean WaitForIcrIdle()
    {
        if (!KernelTime.TryCreateDeadline(StartupTimeoutNanoseconds, out UInt64 deadline)) return false;
        while ((Native.ReadMmio32(_localApicBase + LocalApicIcrLow) & DeliveryPending) != 0U)
        {
            if (KernelTime.HasReached(deadline))
            {
#if DEBUG
                DebugApTrace("ICR idle wait TIMEOUT; ICR low = ", Native.ReadMmio32(_localApicBase + LocalApicIcrLow));
#endif
                return false;
            }
        }
        return true;
    }

    private static Boolean WaitForStartup(UInt32 expectedApicId)
    {
        if (!KernelTime.TryCreateDeadline(StartupTimeoutNanoseconds, out UInt64 deadline)) return false;
        while (Native.GetApplicationProcessorStartupStatus(_trampolineAddress) == 0U)
        {
            if (KernelTime.HasReached(deadline))
            {
#if DEBUG
                DebugApTrace("AP handoff marker TIMEOUT for APIC ID = ", expectedApicId);
#endif
                return false;
            }
        }
        UInt32 observed=Native.GetApplicationProcessorObservedApicId(_trampolineAddress);
#if DEBUG
        DebugApTrace("AP handoff marker observed APIC ID = ", observed);
#endif
        return observed == expectedApicId;
    }

    private static Boolean MarkRemainingUnsupported()
    {
        if (_records == null) return false;
        for (UInt32 index = 0U; index < _processorCount; index++) if (index != _bootstrapIndex && (_records + index)->StartupState == (UInt32)KernelProcessorStartupState.Offline) (_records + index)->StartupState = (UInt32)KernelProcessorStartupState.Unsupported;
        return true;
    }
}
