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
    private static Boolean StartApplicationProcessor(UInt32 index)
    {
#if DEBUG
        DebugApTrace("StartApplicationProcessor CPU index = ", index);
#endif
        if(index>=_processorCount||index==_bootstrapIndex)
        {
#if DEBUG
            DebugApTrace("AP start rejected: invalid/BSP CPU index = ", index);
#endif
            return false;
        }
        if(!KernelSmpMath.TryGetStartupVector(_trampolineAddress,out Byte vector))
        {
#if DEBUG
            DebugApTrace("AP start FAILED: invalid SIPI vector for trampoline = ", _trampolineAddress);
#endif
            return false;
        }
        UInt64 pageTableRoot=Native.ReadPageTableRoot();
#if DEBUG
        DebugApTrace("AP startup target APIC ID = ", (_records+index)->ApicId);
        DebugApTrace("AP startup SIPI vector = ", vector);
        DebugApTrace("AP startup CR3 = ", pageTableRoot);
#endif
        if(pageTableRoot==0UL||pageTableRoot>0xFFFFFFFFUL)
        {
#if DEBUG
            DebugApTrace("AP start FAILED: CR3 not usable by low-memory trampoline = ", pageTableRoot);
#endif
            return false;
        }
        PerCpuRecord* record=_records+index;
        if(KernelFaultInjection.ShouldInject(KernelFaultKind.CpuOffline,"smp",out _))
        {
            record->StartupState=(UInt32)KernelProcessorStartupState.Offline;
#if DEBUG
            DebugApTrace("AP start stopped by CpuOffline fault injection, APIC ID = ", record->ApicId);
#endif
            return false;
        }
        if(!KernelSmpMath.IsXApicDestination(record->ApicId))
        {
            record->StartupState=(UInt32)KernelProcessorStartupState.Unsupported;
#if DEBUG
            DebugApTrace("AP start FAILED: APIC destination unsupported = ", record->ApicId);
#endif
            return false;
        }
        if(record->KernelStackTop==0UL)
        {
#if DEBUG
            DebugApTrace("allocating AP kernel stack bytes = ", ApplicationProcessorStackBytes);
#endif
            if(!KernelHeap.TryAllocate(ApplicationProcessorStackBytes,16UL,true,out KernelHeapAllocation stackAllocation))
            {
                record->StartupState=(UInt32)KernelProcessorStartupState.Failed;
#if DEBUG
                DebugApTrace("AP start FAILED: kernel-stack allocation, APIC ID = ", record->ApicId);
#endif
                return false;
            }
            record->KernelStackBase=stackAllocation.Address;record->KernelStackTop=stackAllocation.Address+ApplicationProcessorStackBytes;
        }
#if DEBUG
        DebugApTrace("AP kernel stack base = ", record->KernelStackBase);
        DebugApTrace("AP kernel stack top = ", record->KernelStackTop);
#endif
        if(record->DescriptorState==0UL)
        {
            if(!KernelHeap.TryAllocate(ApplicationProcessorDescriptorBytes,16UL,true,out KernelHeapAllocation descriptorAllocation))
            {
                record->StartupState=(UInt32)KernelProcessorStartupState.Failed;
#if DEBUG
                DebugApTrace("AP start FAILED: descriptor-state allocation, APIC ID = ", record->ApicId);
#endif
                return false;
            }
            record->DescriptorState=descriptorAllocation.Address;
        }
        if(record->EmergencyStackBase==0UL)
        {
            if(!KernelHeap.TryAllocate(ApplicationProcessorEmergencyStackBytes,4096UL,true,out KernelHeapAllocation emergencyAllocation))
            {
                record->StartupState=(UInt32)KernelProcessorStartupState.Failed;
#if DEBUG
                DebugApTrace("AP start FAILED: IST emergency-stack allocation, APIC ID = ", record->ApicId);
#endif
                return false;
            }
            record->EmergencyStackBase=emergencyAllocation.Address;
        }
        UInt64 doubleFaultStackTop=record->EmergencyStackBase+ApplicationProcessorIstStackBytes;
        UInt64 nmiStackTop=doubleFaultStackTop+ApplicationProcessorIstStackBytes;
        UInt64 machineCheckStackTop=nmiStackTop+ApplicationProcessorIstStackBytes;
#if DEBUG
        DebugApTrace("AP permanent descriptor state = ", record->DescriptorState);
        DebugApTrace("AP IST1 double-fault stack top = ", doubleFaultStackTop);
        DebugApTrace("AP IST2 NMI stack top = ", nmiStackTop);
        DebugApTrace("AP IST3 machine-check stack top = ", machineCheckStackTop);
#endif
        if(!Native.PrepareApplicationProcessorDescriptorState(record->DescriptorState,record->KernelStackTop,doubleFaultStackTop,nmiStackTop,machineCheckStackTop))
        {
            record->StartupState=(UInt32)KernelProcessorStartupState.Failed;
#if DEBUG
            DebugApTrace("AP start FAILED: PrepareApplicationProcessorDescriptorState, APIC ID = ", record->ApicId);
#endif
            return false;
        }
        record->StartupState=(UInt32)KernelProcessorStartupState.Starting;
        Native.AtomicStore64(&record->SchedulerReady,0UL);
#if DEBUG
        DebugApTrace("preparing low-memory AP trampoline at = ", _trampolineAddress);
#endif
        if(!Native.PrepareApplicationProcessorTrampolineWithDescriptorState(_trampolineAddress,pageTableRoot,record->KernelStackTop,record->DescriptorState))
        {
            record->StartupState=(UInt32)KernelProcessorStartupState.Failed;
#if DEBUG
            DebugApTrace("AP start FAILED: PrepareApplicationProcessorTrampoline, APIC ID = ", record->ApicId);
#endif
            return false;
        }
#if DEBUG
        DebugApTrace("trampoline prepared; sending INIT assert/deassert to APIC ID = ", record->ApicId);
#endif
        if(!SendInitSequence(record->ApicId))
        {
            record->StartupState=(UInt32)KernelProcessorStartupState.Failed;
#if DEBUG
            DebugApTrace("AP start FAILED during INIT sequence, APIC ID = ", record->ApicId);
#endif
            return false;
        }
#if DEBUG
        DebugApTrace("sending SIPI #1 to APIC ID = ", record->ApicId);
#endif
        if(!SendIpi(record->ApicId,StartupIpi|vector))
        {
            record->StartupState=(UInt32)KernelProcessorStartupState.Failed;
#if DEBUG
            DebugApTrace("AP start FAILED sending SIPI #1, APIC ID = ", record->ApicId);
#endif
            return false;
        }
        if(!KernelTime.DelayNanoseconds(StartupDelayNanoseconds))
        {
            record->StartupState=(UInt32)KernelProcessorStartupState.Failed;
#if DEBUG
            DebugApTrace("AP start FAILED waiting after SIPI #1, APIC ID = ", record->ApicId);
#endif
            return false;
        }
        UInt32 firstStatus=Native.GetApplicationProcessorStartupStatus(_trampolineAddress);
#if DEBUG
        DebugApTrace("trampoline startup marker after SIPI #1 = ", firstStatus);
        DebugApTrace("trampoline observed APIC ID after SIPI #1 = ", Native.GetApplicationProcessorObservedApicId(_trampolineAddress));
#endif
        if(firstStatus==0U)
        {
#if DEBUG
            DebugApTrace("sending SIPI #2 to APIC ID = ", record->ApicId);
#endif
            if(!SendIpi(record->ApicId,StartupIpi|vector))
            {
                record->StartupState=(UInt32)KernelProcessorStartupState.Failed;
#if DEBUG
                DebugApTrace("AP start FAILED sending SIPI #2, APIC ID = ", record->ApicId);
#endif
                return false;
            }
        }
#if DEBUG
        DebugApTrace("waiting for permanent-code AP handoff, APIC ID = ", record->ApicId);
#endif
        if(!WaitForStartup(record->ApicId))
        {
            record->StartupState=(UInt32)KernelProcessorStartupState.Failed;
#if DEBUG
            DebugApTrace("AP start FAILED waiting for handoff, APIC ID = ", record->ApicId);
            DebugApTrace("final trampoline startup marker = ", Native.GetApplicationProcessorStartupStatus(_trampolineAddress));
            DebugApTrace("final trampoline observed APIC ID = ", Native.GetApplicationProcessorObservedApicId(_trampolineAddress));
#endif
            return false;
        }
        record->StartupState=(UInt32)KernelProcessorStartupState.OnlineParked;_onlineCount++;
#if DEBUG
        DebugApTrace("AP native handoff COMPLETE; OnlineParked APIC ID = ", record->ApicId);
#endif
        return true;
    }

}
