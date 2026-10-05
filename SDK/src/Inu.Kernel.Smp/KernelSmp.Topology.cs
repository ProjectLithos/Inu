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
    public static Boolean IsInitialized() => _initialized;
    /// <summary>Gets the latest SMP initialization status.</summary>
    public static KernelSmpStatus GetLastStatus() => _status;
    /// <summary>Gets a freestanding-safe SMP status name.</summary>
    public static String GetLastStatusName()
    {
        if (_status == KernelSmpStatus.Success) return "Success";
        if (_status == KernelSmpStatus.Partial) return "Partial";
        if (_status == KernelSmpStatus.NoProcessors) return "NoProcessors";
        if (_status == KernelSmpStatus.StateAllocationFailed) return "StateAllocationFailed";
        if (_status == KernelSmpStatus.BootstrapProcessorNotFound) return "BootstrapProcessorNotFound";
        if (_status == KernelSmpStatus.TrampolineUnavailable) return "TrampolineUnavailable";
        if (_status == KernelSmpStatus.LocalApicUnavailable) return "LocalApicUnavailable";
        return "NotInitialized";
    }
    /// <summary>Gets the number of enabled processors represented by per-CPU state.</summary>
    public static UInt32 GetProcessorCount() => _processorCount;
    /// <summary>Gets the number of processors that have completed the Inu bootstrap handshake.</summary>
    public static UInt32 GetOnlineProcessorCount() => _onlineCount;
    /// <summary>Gets the logical index of the firmware bootstrap processor.</summary>
    public static UInt32 GetBootstrapProcessorIndex() => _bootstrapIndex;
    /// <summary>Gets an immutable capability snapshot.</summary>
    public static KernelSmpCapabilities GetCapabilities() => new(_processorCount, _onlineCount, _bootstrapIndex, _trampolineAddress, _xApicStartup, _xApicStartup, _xApicStartup && KernelSmpMath.IsValidStartupTrampoline(_trampolineAddress), _shutdownIpiVector >= 32U, PerCpuStorageSlots);

    /// <summary>Assigns the logical CPUs eligible for one execution role. At least one discovered CPU must be selected.</summary>
    public static Boolean SetRoleCpuSet(KernelCpuRole role, KernelCpuSet cpus)
    {
        if(!_initialized||cpus.IsEmpty()||!ContainsDiscoveredCpu(cpus))return false;
        if(role==KernelCpuRole.Kernel)_kernelCpus=cpus; else if(role==KernelCpuRole.Userland)_userlandCpus=cpus; else if(role==KernelCpuRole.Gui)_guiCpus=cpus; else if(role==KernelCpuRole.Drivers)_driverCpus=cpus; else if(role==KernelCpuRole.Interrupts)_interruptCpus=cpus; else if(role==KernelCpuRole.Networking)_networkingCpus=cpus; else if(role==KernelCpuRole.Storage)_storageCpus=cpus; else if(role==KernelCpuRole.Realtime)_realtimeCpus=cpus; else if(role==KernelCpuRole.Background)_backgroundCpus=cpus; else return false;
        return true;
    }

    /// <summary>Gets the configured logical-CPU set for an execution role.</summary>
    public static KernelCpuSet GetRoleCpuSet(KernelCpuRole role)
    { if(role==KernelCpuRole.Kernel)return _kernelCpus;if(role==KernelCpuRole.Userland)return _userlandCpus;if(role==KernelCpuRole.Gui)return _guiCpus;if(role==KernelCpuRole.Drivers)return _driverCpus;if(role==KernelCpuRole.Interrupts)return _interruptCpus;if(role==KernelCpuRole.Networking)return _networkingCpus;if(role==KernelCpuRole.Storage)return _storageCpus;if(role==KernelCpuRole.Realtime)return _realtimeCpus;return _backgroundCpus; }

    /// <summary>Reports whether a logical CPU is assigned to an execution role.</summary>
    public static Boolean ProcessorHasRole(UInt32 processorIndex, KernelCpuRole role)
    { return processorIndex<_processorCount && GetRoleCpuSet(role).Contains(processorIndex); }

    /// <summary>Finds the first online processor assigned to a role, useful as a stable fallback affinity target.</summary>
    public static Boolean TryGetFirstOnlineProcessorForRole(KernelCpuRole role,out UInt32 processorIndex)
    { processorIndex=0U;KernelCpuSet set=GetRoleCpuSet(role);for(UInt32 i=0U;i<_processorCount;i++){if(!set.Contains(i))continue;PerCpuRecord* r=_records+i;if(r->StartupState==(UInt32)KernelProcessorStartupState.BootstrapProcessor||r->StartupState==(UInt32)KernelProcessorStartupState.OnlineParked){processorIndex=i;return true;}}return false; }

    private static Boolean ContainsDiscoveredCpu(KernelCpuSet cpus)
    { for(UInt32 i=0U;i<_processorCount;i++)if(cpus.Contains(i))return true;return false; }

    /// <summary>Gets one per-CPU state snapshot by Inu logical processor index.</summary>
    public static Boolean TryGetProcessor(UInt32 index, out KernelProcessorState processor)
    {
        processor = default;
        if (!_initialized || _records == null || index >= _processorCount) return false;
        PerCpuRecord* record = _records + index;
        processor = new KernelProcessorState(record->Index, record->ApicId, record->AcpiUid, (record->Flags & 1U) != 0U, (record->Flags & 2U) != 0U, (KernelProcessorStartupState)record->StartupState, record->KernelStackBase, record->KernelStackTop, record->SchedulerContext);
        return true;
    }

    /// <summary>Resolves the processor executing the current code to its per-CPU state snapshot.</summary>
    public static Boolean TryGetCurrentProcessor(out KernelProcessorState processor)
    {
        processor = default;
        if (!_initialized || !FindProcessor(Native.GetCurrentApicId(), out UInt32 index)) return false;
        return TryGetProcessor(index, out processor);
    }


    /// <summary>Enumerates one logical CPU through the stable formal SMP surface.</summary>
    public static Boolean TryEnumerateProcessor(UInt32 index, out KernelCpuInfo cpu)
    { cpu=default; if(!TryGetProcessor(index,out KernelProcessorState state))return false; Boolean online=state.StartupState==KernelProcessorStartupState.BootstrapProcessor||state.StartupState==KernelProcessorStartupState.OnlineParked; cpu=new KernelCpuInfo(state.Index,state.ApicId,state.AcpiUid,online,state.IsBootstrapProcessor,state.StartupState); return true; }

    /// <summary>Gets the zero-based logical CPU currently executing this code.</summary>
    public static Boolean TryGetCurrentProcessorIndex(out UInt32 processorIndex)
    { processorIndex=0U; if(!TryGetCurrentProcessor(out KernelProcessorState state))return false; processorIndex=state.Index; return true; }

    /// <summary>Publishes that the calling processor has completed a real managed scheduler context transfer.</summary>
    /// <remarks>This is deliberately separate from OnlineParked: the native SIPI handoff marker only makes the low-memory trampoline reusable.</remarks>
    public static Boolean NotifyCurrentProcessorSchedulerReady()
    {
        if(!_initialized||_records==null||!TryGetCurrentProcessorIndex(out UInt32 index)||index>=_processorCount)
        {
#if DEBUG
            DebugApTrace("scheduler-ready publication rejected");
#endif
            return false;
        }
        Boolean stored=Native.AtomicStore64(&((_records+index)->SchedulerReady),1UL);
#if DEBUG
        DebugApTrace(stored ? "scheduler-ready published for CPU = " : "scheduler-ready atomic store FAILED for CPU = ", index);
#endif
        return stored;
    }

    /// <summary>Gets whether one processor has executed its managed CPU-local scheduler thread.</summary>
    public static Boolean IsProcessorSchedulerReady(UInt32 processorIndex)
    {
        if(!_initialized||_records==null||processorIndex>=_processorCount)return false;
        UInt64 ready=0UL;return Native.AtomicLoad64(&((_records+processorIndex)->SchedulerReady),&ready)&&ready!=0UL;
    }

    /// <summary>Gets the number of online processors whose managed CPU-local scheduler is proven live.</summary>
    public static UInt32 GetSchedulerReadyProcessorCount()
    {
        if(!_initialized||_records==null)return 0U;UInt32 count=0U;
        for(UInt32 i=0U;i<_processorCount;i++){PerCpuRecord* r=_records+i;if(r->StartupState!=(UInt32)KernelProcessorStartupState.BootstrapProcessor&&r->StartupState!=(UInt32)KernelProcessorStartupState.OnlineParked)continue;if(IsProcessorSchedulerReady(i))count++;}
        return count;
    }

    /// <summary>Reads one stable 64-bit per-CPU storage slot.</summary>
    public static Boolean TryGetPerCpuValue(UInt32 processorIndex, KernelPerCpuStorageKey key, out UInt64 value)
    { value=0UL; UInt32 slot=(UInt32)key; if(!_initialized||_records==null||processorIndex>=_processorCount||slot>=PerCpuStorageSlots)return false; value=(_records+processorIndex)->Storage[slot]; return true; }

    /// <summary>Writes one stable 64-bit per-CPU storage slot.</summary>
    public static Boolean TrySetPerCpuValue(UInt32 processorIndex, KernelPerCpuStorageKey key, UInt64 value)
    { UInt32 slot=(UInt32)key; if(!_initialized||_records==null||processorIndex>=_processorCount||slot>=PerCpuStorageSlots)return false; (_records+processorIndex)->Storage[slot]=value; if(key==KernelPerCpuStorageKey.Scheduler)(_records+processorIndex)->SchedulerContext=value; return true; }

    /// <summary>Configures a standard runtime IPI vector. Vectors below 32 are rejected.</summary>
    public static Boolean ConfigureIpiVector(KernelIpiPurpose purpose, Byte vector)
    { if(vector<32U)return false; if(purpose==KernelIpiPurpose.Reschedule)_rescheduleIpiVector=vector; else if(purpose==KernelIpiPurpose.TlbShootdown)_tlbShootdownIpiVector=vector; else if(purpose==KernelIpiPurpose.CallFunction)_callFunctionIpiVector=vector; else if(purpose==KernelIpiPurpose.CpuShutdown)_shutdownIpiVector=vector; else return false; return true; }

    /// <summary>Sends a fixed-delivery IPI to an online logical CPU.</summary>
    public static Boolean TrySendIpi(UInt32 processorIndex, Byte vector)
    { if(!_initialized||!_xApicStartup||vector<32U||processorIndex>=_processorCount)return false; PerCpuRecord* r=_records+processorIndex; if(r->StartupState!=(UInt32)KernelProcessorStartupState.BootstrapProcessor&&r->StartupState!=(UInt32)KernelProcessorStartupState.OnlineParked)return false; if(!KernelSmpMath.IsXApicDestination(r->ApicId))return false; return SendIpi(r->ApicId,(UInt32)vector); }

    /// <summary>Sends one configured standard IPI purpose to an online logical CPU.</summary>
    public static Boolean TrySendIpi(UInt32 processorIndex, KernelIpiPurpose purpose)
    { Byte vector=0; if(purpose==KernelIpiPurpose.Reschedule)vector=_rescheduleIpiVector; else if(purpose==KernelIpiPurpose.TlbShootdown)vector=_tlbShootdownIpiVector; else if(purpose==KernelIpiPurpose.CallFunction)vector=_callFunctionIpiVector; else if(purpose==KernelIpiPurpose.CpuShutdown)vector=_shutdownIpiVector; return vector>=32U&&TrySendIpi(processorIndex,vector); }

    /// <summary>Starts one offline application processor through the existing INIT/SIPI transport.</summary>
    public static Boolean TryStartProcessor(UInt32 processorIndex)
    { if(!_initialized||processorIndex>=_processorCount||processorIndex==_bootstrapIndex||!_xApicStartup||!KernelSmpMath.IsValidStartupTrampoline(_trampolineAddress))return false; PerCpuRecord* record=_records+processorIndex; if(record->StartupState==(UInt32)KernelProcessorStartupState.OnlineParked)return true; return StartApplicationProcessor(processorIndex); }

    /// <summary>Requests cooperative shutdown of an application processor using the configured CPU-shutdown IPI.</summary>
    public static Boolean TryShutdownProcessor(UInt32 processorIndex)
    { if(processorIndex>=_processorCount||processorIndex==_bootstrapIndex||_shutdownIpiVector<32U)return false; if(!TrySendIpi(processorIndex,KernelIpiPurpose.CpuShutdown))return false; (_records+processorIndex)->StartupState=(UInt32)KernelProcessorStartupState.ShutdownRequested; return true; }

    /// <summary>Completes cooperative shutdown bookkeeping on the calling CPU before it parks.</summary>
    public static Boolean NotifyCurrentProcessorOffline()
    { if(!TryGetCurrentProcessorIndex(out UInt32 index)||index==_bootstrapIndex)return false; PerCpuRecord* r=_records+index; if(r->StartupState==(UInt32)KernelProcessorStartupState.OnlineParked||r->StartupState==(UInt32)KernelProcessorStartupState.ShutdownRequested){Native.AtomicStore64(&r->SchedulerReady,0UL);r->StartupState=(UInt32)KernelProcessorStartupState.Offline;if(_onlineCount>0U)_onlineCount--;return true;}return false; }

    /// <summary>Gets the opaque CPU-local scheduler state token.</summary>
    public static Boolean TryGetSchedulerContext(UInt32 processorIndex,out UInt64 schedulerContext)
    { schedulerContext=0UL; if(!_initialized||_records==null||processorIndex>=_processorCount)return false; schedulerContext=(_records+processorIndex)->SchedulerContext; return schedulerContext!=0UL; }

    /// <summary>Stores the scheduler-owned context token reserved in one processor's per-CPU record.</summary>
    public static Boolean TrySetSchedulerContext(UInt32 index, UInt64 schedulerContext)
    {
        if (!_initialized || _records == null || index >= _processorCount) return false;
        (_records + index)->SchedulerContext = schedulerContext;
        (_records + index)->Storage[(UInt32)KernelPerCpuStorageKey.Scheduler] = schedulerContext;
        return true;
    }

}
