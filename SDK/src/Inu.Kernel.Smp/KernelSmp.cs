using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Acpi;
using Inu.Kernel.Console;
using Inu.Kernel.Heap;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Time;

namespace Inu.Kernel.Smp;

/// <summary>Discovers x64 processors, owns per-CPU bootstrap state, and starts xAPIC application processors.</summary>
public static unsafe partial class KernelSmp
{
    private const UInt32 MaximumProcessors = 256U;
    private const UInt64 ApplicationProcessorStackBytes = 16384UL;
    private const UInt64 ApplicationProcessorDescriptorBytes = 256UL;
    private const UInt64 ApplicationProcessorIstStackBytes = 16384UL;
    private const UInt64 ApplicationProcessorEmergencyStackBytes = ApplicationProcessorIstStackBytes * 3UL;
    private const UInt64 InitDelayNanoseconds = 10000000UL;
    private const UInt64 StartupDelayNanoseconds = 200000UL;
    private const UInt64 StartupTimeoutNanoseconds = 100000000UL;
    private const UInt32 LocalApicTaskPriority = 0x80U;
    private const UInt32 LocalApicSpurious = 0xF0U;
    private const UInt32 LocalApicIcrLow = 0x300U;
    private const UInt32 LocalApicIcrHigh = 0x310U;
    private const UInt32 LocalApicSoftwareEnable = 1U << 8;
    private const UInt32 LocalApicSpuriousVector = 0xFFU;
    private const UInt32 DeliveryPending = 0x1000U;
    private const UInt32 InitAssert = 0x0000C500U;
    private const UInt32 InitDeassert = 0x00008500U;
    private const UInt32 StartupIpi = 0x00000600U;
    private const UInt32 ApicBaseMsr = 0x1BU;
    private const UInt64 ApicEnabled = 1UL << 11;
    private const UInt64 X2ApicEnabled = 1UL << 10;
    private const UInt32 PerCpuStorageSlots = 8U;

    private struct PerCpuRecord
    {
        internal UInt32 Index;
        internal UInt32 ApicId;
        internal UInt32 AcpiUid;
        internal UInt32 Flags;
        internal UInt32 StartupState;
        // Low-level SIPI completion only proves the AP left the reusable trampoline.
        // SchedulerReady is published later by the AP idle thread after a real managed
        // thread-context transfer has succeeded on that processor.
        internal UInt64 SchedulerReady;
        internal UInt64 KernelStackBase;
        internal UInt64 KernelStackTop;
        internal UInt64 DescriptorState;
        internal UInt64 EmergencyStackBase;
        internal UInt64 SchedulerContext;
        internal fixed UInt64 Storage[8];
    }

    private static PerCpuRecord* _records;
    private static UInt32 _processorCount;
    private static UInt32 _onlineCount;
    private static UInt32 _bootstrapIndex;
    private static UInt64 _trampolineAddress;
    private static UInt64 _localApicBase;
    private static Boolean _xApicStartup;
    private static volatile Boolean _initialized;
    private static Byte _shutdownIpiVector;
    private static Byte _rescheduleIpiVector;
    private static Byte _tlbShootdownIpiVector;
    private static Byte _callFunctionIpiVector;
    private static KernelSmpStatus _status = KernelSmpStatus.NotInitialized;
    private static KernelCpuSet _kernelCpus = KernelCpuSet.All();
    private static KernelCpuSet _userlandCpus = KernelCpuSet.All();
    private static KernelCpuSet _guiCpus = KernelCpuSet.All();
    private static KernelCpuSet _driverCpus = KernelCpuSet.All();
    private static KernelCpuSet _interruptCpus = KernelCpuSet.All();
    private static KernelCpuSet _networkingCpus = KernelCpuSet.All();
    private static KernelCpuSet _storageCpus = KernelCpuSet.All();
    private static KernelCpuSet _realtimeCpus = KernelCpuSet.All();
    private static KernelCpuSet _backgroundCpus = KernelCpuSet.All();

#if DEBUG
    private static UInt64 _apDebugSerialGate;

    private static void DebugApTrace(String message)
    {
        DebugApTrace(message, 0UL, false);
    }

    private static void DebugApTrace(String message, UInt64 value)
    {
        DebugApTrace(message, value, true);
    }

    private static unsafe void DebugApTrace(String message, UInt64 value, Boolean hasValue)
    {
        // Static fields are managed variables in C# even in Inu's no-GC bootstrap.
        // Pin the diagnostic gate before passing its address to the native atomic helpers.
        fixed (UInt64* gate = &_apDebugSerialGate)
        {
            UInt64 observed = 0UL;
            for (UInt32 spin = 0U; spin < 100000U; spin++)
            {
                observed = 0UL;
                if (Native.AtomicCompareExchange64(gate, 0UL, 1UL, &observed) && observed == 0UL) break;
                Native.Pause();
            }
            if (observed != 0UL) return;
            if(!Native.BeginSerialRecord()){Native.AtomicStore64(gate,0UL);return;}
            DebugSerialWrite("[SMP-AP] ");
            DebugSerialWrite(message);
            if (hasValue)
            {
                DebugSerialWrite("0x");
                DebugSerialWriteHex(value);
            }
            Native.WriteSerial((Byte)'\r');
            Native.WriteSerial((Byte)'\n');
            Native.EndSerialRecord();
            Native.AtomicStore64(gate, 0UL);
        }
    }

    private static void DebugSerialWrite(String value)
    {
        if (value == null) return;
        for (Int32 index = 0; index < value.Length; index++)
        {
            Char character = value[index];
            Native.WriteSerial((Byte)((UInt32)character <= 0x7FU ? character : '?'));
        }
    }

    private static void DebugSerialWriteHex(UInt64 value)
    {
        const String digits = "0123456789ABCDEF";
        Boolean started = false;
        for (Int32 shift = 60; shift >= 0; shift -= 4)
        {
            UInt32 nibble = (UInt32)((value >> shift) & 0xFUL);
            if (!started && nibble == 0U && shift != 0) continue;
            started = true;
            Native.WriteSerial((Byte)digits[(Int32)nibble]);
        }
    }
#endif

    /// <summary>Initializes per-CPU records and starts each xAPIC application processor through INIT/SIPI.</summary>
    /// <returns><see langword="true"/> when per-CPU state is usable; individual AP failures are reported through status and processor snapshots.</returns>
    public static Boolean Initialize<TBoot>(TBoot boot) where TBoot : IApplicationProcessorTrampolineContext
    {
#if DEBUG
        DebugApTrace("Initialize enter");
#endif
        if (_initialized) return true;
        if (!KernelAcpi.IsInitialized()) return false;
        _rescheduleIpiVector=0xE0; _processorCount = KernelAcpi.GetProcessorCount();
#if DEBUG
        DebugApTrace("ACPI processor count = ", _processorCount);
#endif
        if (_processorCount == 0U) { _status = KernelSmpStatus.NoProcessors; return false; }
        if (_processorCount > MaximumProcessors) _processorCount = MaximumProcessors;
        if (!KernelSmpMath.TryGetStateTableBytes(_processorCount, (UInt32)sizeof(PerCpuRecord), out UInt64 tableBytes)) { _status = KernelSmpStatus.StateAllocationFailed; return false; }
        if (!KernelHeap.TryAllocate(tableBytes, 64UL, true, out KernelHeapAllocation stateAllocation)) { _status = KernelSmpStatus.StateAllocationFailed; return false; }
        _records = (PerCpuRecord*)stateAllocation.Address;
        if (!PopulateProcessorRecords()) { _status = KernelSmpStatus.NoProcessors; return false; }
        UInt32 currentApicId = Native.GetCurrentApicId();
#if DEBUG
        DebugApTrace("BSP APIC ID = ", currentApicId);
        DebugApTrace("BSP EFER.NXE enabled = ", Native.IsExecuteDisableEnabled() ? 1UL : 0UL);
#endif
        if (!FindProcessor(currentApicId, out _bootstrapIndex)) { _status = KernelSmpStatus.BootstrapProcessorNotFound; return false; }
        PerCpuRecord* bsp = _records + _bootstrapIndex;
        bsp->Flags |= 2U; bsp->StartupState = (UInt32)KernelProcessorStartupState.BootstrapProcessor;
        // The BSP is already executing managed scheduler-owned kernel context. APs publish
        // this only after their first scheduler context transfer reaches the idle thread.
        bsp->SchedulerReady = 1UL;
        _onlineCount = 1U;
        _trampolineAddress = boot.GetApplicationProcessorTrampolineAddress();
#if DEBUG
        DebugApTrace("trampoline physical address = ", _trampolineAddress);
#endif
        UInt64 apicBaseMsr = Native.ReadModelSpecificRegister(ApicBaseMsr);
        if ((apicBaseMsr & ApicEnabled) != 0UL && (apicBaseMsr & X2ApicEnabled) == 0UL)
        {
            _localApicBase = apicBaseMsr & 0x0000000FFFFFF000UL;
            if (_localApicBase == 0UL) KernelAcpi.TryGetLocalApicAddress(out _localApicBase);
            _xApicStartup = _localApicBase != 0UL;
        }
#if DEBUG
        DebugApTrace("Local APIC base = ", _localApicBase);
        DebugApTrace(_xApicStartup ? "xAPIC startup transport available" : "xAPIC startup transport unavailable");
#endif
        // Establish an explicit usable policy before generated OS configuration is applied.
        // Do not rely on managed static-field initialization during freestanding bootstrap.
        KernelCpuSet defaultRoleCpus = KernelCpuSet.All();
        _kernelCpus = defaultRoleCpus;
        _userlandCpus = defaultRoleCpus;
        _guiCpus = defaultRoleCpus;
        _driverCpus = defaultRoleCpus;
        _interruptCpus = defaultRoleCpus;
        _networkingCpus = defaultRoleCpus;
        _storageCpus = defaultRoleCpus;
        _realtimeCpus = defaultRoleCpus;
        _backgroundCpus = defaultRoleCpus;
        _initialized = true;
        if (_processorCount == 1U) { _status = KernelSmpStatus.Success; return true; }
        if (!KernelSmpMath.IsValidStartupTrampoline(_trampolineAddress)) { MarkRemainingUnsupported(); _status = KernelSmpStatus.TrampolineUnavailable; return true; }
        if (!_xApicStartup) { MarkRemainingUnsupported(); _status = KernelSmpStatus.LocalApicUnavailable; return true; }
#if DEBUG
        DebugApTrace("starting application processors");
#endif
        StartApplicationProcessors();
#if DEBUG
        DebugApTrace("AP startup complete; online count = ", _onlineCount);
#endif
        _status = _onlineCount == _processorCount ? KernelSmpStatus.Success : KernelSmpStatus.Partial;
        return true;
    }

    /// <summary>Enables the calling xAPIC processor for normal runtime interrupts after INIT/SIPI reset.</summary>
    /// <remarks>INIT resets processor-local APIC software state. The BSP enables its own local APIC during timer calibration, but that does not enable each AP. Runtime IPIs and Local APIC timer interrupts cannot reliably wake an AP until its own SVR software-enable bit is set.</remarks>
    public static Boolean EnableCurrentProcessorRuntimeInterrupts()
    {
        if(!_initialized||!_xApicStartup||_localApicBase==0UL)return false;
        UInt64 apicBase=Native.ReadModelSpecificRegister(ApicBaseMsr);
        if((apicBase&X2ApicEnabled)!=0UL)return false;
        if((apicBase&ApicEnabled)==0UL)
        {
            if(!Native.WriteModelSpecificRegister(ApicBaseMsr,apicBase|ApicEnabled))return false;
            apicBase=Native.ReadModelSpecificRegister(ApicBaseMsr);
            if((apicBase&ApicEnabled)==0UL)return false;
        }
        // Accept all interrupt priorities on this AP and software-enable its Local APIC.
        if(!Native.WriteMmio32(_localApicBase+LocalApicTaskPriority,0U))return false;
        UInt32 spurious=Native.ReadMmio32(_localApicBase+LocalApicSpurious);
        UInt32 vector=spurious&0xFFU;if(vector<0x10U)vector=LocalApicSpuriousVector;
        spurious=(spurious&0xFFFFFF00U)|vector|LocalApicSoftwareEnable;
        if(!Native.WriteMmio32(_localApicBase+LocalApicSpurious,spurious))return false;
        if((Native.ReadMmio32(_localApicBase+LocalApicSpurious)&LocalApicSoftwareEnable)==0U)return false;
        // INIT resets processor-local LVT policy. Reapply only the NMI routing ACPI
        // declares for this AP rather than inheriting reset/firmware state.
        return KernelAcpiNmi.ConfigureCurrentProcessor();
    }

    /// <summary>Gets whether the per-CPU state table completed initialization.</summary>
}
