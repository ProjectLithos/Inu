using System;
using System.Runtime;
using Inu.Arch.X64;
using Inu.Kernel.Heap;

namespace Inu.Runtime.NativeAot;

/// <summary>Describes the managed-runtime stage reached by the freestanding Inu kernel.</summary>
public enum NativeAotRuntimePhase
{
    NoGcBootstrap = 0,
    Initializing = 1,
    Managed = 2,
    Failed = 3
}

/// <summary>Reports the last managed-runtime failure without allocating a managed object.</summary>
public enum NativeAotRuntimeFailure
{
    None = 0,
    KernelHeapUnavailable = 1,
    SegmentAllocationFailed = 2,
    InvalidMethodTable = 3,
    AllocationOverflow = 4,
    ManagedHeapExhausted = 5,
    AtomicGateUnavailable = 6,
    GarbageCollectorMetadataExhausted = 7,
    InvalidReadyToRunModule = 8,
    TypeManagerCapacityExceeded = 9
}

/// <summary>Snapshot of the NativeAOT managed heap.</summary>
public readonly struct NativeAotRuntimeStatistics
{
    internal NativeAotRuntimeStatistics(
        NativeAotRuntimePhase phase,
        NativeAotRuntimeFailure failure,
        UInt64 reservedBytes,
        UInt64 allocatedBytes,
        UInt64 allocationCount,
        UInt32 segmentCount)
    {
        Phase = phase;
        Failure = failure;
        ReservedBytes = reservedBytes;
        AllocatedBytes = allocatedBytes;
        AllocationCount = allocationCount;
        SegmentCount = segmentCount;
    }

    public NativeAotRuntimePhase Phase { get; }
    public NativeAotRuntimeFailure Failure { get; }
    public UInt64 ReservedBytes { get; }
    public UInt64 AllocatedBytes { get; }
    public UInt64 AllocationCount { get; }
    public UInt32 SegmentCount { get; }
}

/// <summary>
/// Freestanding NativeAOT allocator backed by the Inu kernel heap. Objects never move.
/// Reclaimed blocks are owned by NativeAotGarbageCollector and are reused before new arena space.
/// </summary>
public static unsafe class NativeAotRuntime
{
    private const UInt64 SegmentSize = 4UL * 1024UL * 1024UL;
    private const UInt64 ObjectAlignment = 16UL;
    private const UInt32 MaximumSegments = 16U;
    private const UInt64 MaximumManagedBytes = SegmentSize * MaximumSegments;

    private unsafe struct RuntimeSegments
    {
        internal fixed UInt64 Starts[(Int32)MaximumSegments];
        internal fixed UInt64 Lengths[(Int32)MaximumSegments];
        internal fixed UInt64 Used[(Int32)MaximumSegments];
    }

    private static RuntimeSegments _segments;
    private static UInt64 _allocationGate;
    private static UInt64 _reservedBytes;
    private static UInt64 _allocatedBytes;
    private static UInt64 _allocationCount;
    private static UInt32 _segmentCount;
    private static NativeAotRuntimePhase _phase;
    private static NativeAotRuntimeFailure _failure;

    public static Boolean Initialize()
    {
        if (_phase == NativeAotRuntimePhase.Managed) return true;
        if (_phase == NativeAotRuntimePhase.Initializing || _phase == NativeAotRuntimePhase.Failed) return false;
        if (!KernelHeap.IsInitialized())
        {
            Fail(NativeAotRuntimeFailure.KernelHeapUnavailable);
            return false;
        }

        _phase = NativeAotRuntimePhase.Initializing;
        _failure = NativeAotRuntimeFailure.None;
        _reservedBytes = 0UL;
        _allocatedBytes = 0UL;
        _allocationCount = 0UL;
        _segmentCount = 0U;
        _allocationGate = 0UL;

        if (!AddSegment(SegmentSize))
        {
            Fail(NativeAotRuntimeFailure.SegmentAllocationFailed);
            return false;
        }

        NativeAotGarbageCollector.Initialize();
        _phase = NativeAotRuntimePhase.Managed;
        return true;
    }

    public static NativeAotRuntimePhase GetPhase() => _phase;
    public static NativeAotRuntimeFailure GetLastFailure() => _failure;
    public static Boolean IsManaged() => _phase == NativeAotRuntimePhase.Managed;

    public static String GetPhaseName()
    {
        if (_phase == NativeAotRuntimePhase.NoGcBootstrap) return "NoGcBootstrap";
        if (_phase == NativeAotRuntimePhase.Initializing) return "Initializing";
        if (_phase == NativeAotRuntimePhase.Managed) return "ManagedNativeAot";
        return "Failed";
    }

    public static String GetLastFailureName()
    {
        if (_failure == NativeAotRuntimeFailure.None) return "None";
        if (_failure == NativeAotRuntimeFailure.KernelHeapUnavailable) return "KernelHeapUnavailable";
        if (_failure == NativeAotRuntimeFailure.SegmentAllocationFailed) return "SegmentAllocationFailed";
        if (_failure == NativeAotRuntimeFailure.InvalidMethodTable) return "InvalidMethodTable";
        if (_failure == NativeAotRuntimeFailure.AllocationOverflow) return "AllocationOverflow";
        if (_failure == NativeAotRuntimeFailure.ManagedHeapExhausted) return "ManagedHeapExhausted";
        if (_failure == NativeAotRuntimeFailure.AtomicGateUnavailable) return "AtomicGateUnavailable";
        if (_failure == NativeAotRuntimeFailure.GarbageCollectorMetadataExhausted) return "GarbageCollectorMetadataExhausted";
        if (_failure == NativeAotRuntimeFailure.InvalidReadyToRunModule) return "InvalidReadyToRunModule";
        if (_failure == NativeAotRuntimeFailure.TypeManagerCapacityExceeded) return "TypeManagerCapacityExceeded";
        return "Unknown";
    }

    public static NativeAotRuntimeStatistics GetStatistics()
        => new(_phase, _failure, _reservedBytes, _allocatedBytes, _allocationCount, _segmentCount);

    private static Boolean AddSegment(UInt64 requestedBytes)
    {
        if (_segmentCount >= MaximumSegments || requestedBytes == 0UL) return false;
        if (_reservedBytes > MaximumManagedBytes - requestedBytes) return false;
        if (!KernelHeap.TryAllocate(requestedBytes, 4096UL, true, out KernelHeapAllocation allocation)) return false;

        fixed (RuntimeSegments* segments = &_segments)
        {
            UInt32 index = _segmentCount;
            segments->Starts[index] = allocation.Address;
            segments->Lengths[index] = allocation.ByteCount;
            segments->Used[index] = 0UL;
            _segmentCount = index + 1U;
            _reservedBytes += allocation.ByteCount;
        }
        return true;
    }

    internal static Boolean EnterHeapGate()
    {
        fixed (UInt64* gate = &_allocationGate)
        {
            for (UInt32 attempt = 0U; attempt < 1000000U; attempt++)
            {
                if (!X64ArchitectureBoundary.AtomicCompareExchange64(gate, 0UL, 1UL, out UInt64 previous))
                {
                    Fail(NativeAotRuntimeFailure.AtomicGateUnavailable);
                    return false;
                }
                if (previous == 0UL) return true;
                X64ArchitectureBoundary.SpinWaitHint();
            }
        }
        Fail(NativeAotRuntimeFailure.AtomicGateUnavailable);
        return false;
    }

    internal static void ExitHeapGate()
    {
        fixed (UInt64* gate = &_allocationGate)
        {
            X64ArchitectureBoundary.AtomicStore64(gate, 0UL);
        }
    }

    private static void* TryAllocateFromSegmentsUnderHeapGate(UInt64 byteCount)
    {
        fixed (RuntimeSegments* segments = &_segments)
        {
            for (UInt32 i = 0U; i < _segmentCount; i++)
            {
                UInt64 used = segments->Used[i];
                UInt64 length = segments->Lengths[i];
                if (used > length || byteCount > length - used) continue;
                UInt64 address = segments->Starts[i] + used;
                segments->Used[i] = used + byteCount;
                return (void*)(nuint)address;
            }
        }
        return null;
    }

    private static void* AllocateObject(void* methodTable, UInt32 elementCount, Boolean variableSized)
    {
        if (_phase != NativeAotRuntimePhase.Managed || methodTable == null)
            return RuntimeFailPointer(NativeAotRuntimeFailure.InvalidMethodTable);

        UInt32 baseSize = *((UInt32*)((Byte*)methodTable + 4));
        UInt16 componentSize = *((UInt16*)methodTable);
        if (baseSize < (UInt32)sizeof(void*))
            return RuntimeFailPointer(NativeAotRuntimeFailure.InvalidMethodTable);

        UInt64 byteCount = baseSize;
        if (variableSized && elementCount != 0U)
        {
            UInt64 componentBytes = (UInt64)componentSize * elementCount;
            if (componentSize != 0U && componentBytes / componentSize != elementCount)
                return RuntimeFailPointer(NativeAotRuntimeFailure.AllocationOverflow);
            if (byteCount > UInt64.MaxValue - componentBytes)
                return RuntimeFailPointer(NativeAotRuntimeFailure.AllocationOverflow);
            byteCount += componentBytes;
        }

        if (byteCount > UInt64.MaxValue - (ObjectAlignment - 1UL))
            return RuntimeFailPointer(NativeAotRuntimeFailure.AllocationOverflow);
        byteCount = (byteCount + ObjectAlignment - 1UL) & ~(ObjectAlignment - 1UL);

        if (!EnterHeapGate())
            return RuntimeFailPointer(NativeAotRuntimeFailure.AtomicGateUnavailable);

        void* result = null;

        // Mature-GC pressure policy: do not wait for the current arena segment or the
        // allocation metadata table to be completely exhausted before collecting.
        if (NativeAotGarbageCollector.ShouldCollectForAllocationPressureUnderHeapGate(byteCount))
        {
            ExitHeapGate();
            NativeAotGarbageCollector.CollectForAllocationPressure();
            if (!EnterHeapGate())
                return RuntimeFailPointer(NativeAotRuntimeFailure.AtomicGateUnavailable);
        }

        Boolean reused = NativeAotGarbageCollector.TryReuseUnderHeapGate(byteCount, methodTable, out result);
        if (!reused) result = TryAllocateFromSegmentsUnderHeapGate(byteCount);

        // Allocation pressure requests a scheduler-coordinated collection outside the heap
        // gate. Remote CPUs must be able to acknowledge their safepoint while this CPU waits.
        if (result == null && NativeAotGarbageCollector.IsRootMapReady())
        {
            ExitHeapGate();
            NativeAotGarbageCollector.Collect();
            if (!EnterHeapGate())
                return RuntimeFailPointer(NativeAotRuntimeFailure.AtomicGateUnavailable);

            reused = NativeAotGarbageCollector.TryReuseUnderHeapGate(byteCount, methodTable, out result);
            if (!reused) result = TryAllocateFromSegmentsUnderHeapGate(byteCount);
        }

        if (result == null && _segmentCount < MaximumSegments)
        {
            UInt64 nextSegmentSize = byteCount > SegmentSize ? AlignPage(byteCount) : SegmentSize;
            if (nextSegmentSize != 0UL &&
                _reservedBytes <= MaximumManagedBytes - nextSegmentSize &&
                AddSegment(nextSegmentSize))
                result = TryAllocateFromSegmentsUnderHeapGate(byteCount);
        }

        if (result != null)
        {
            Zero(result, byteCount);
            *((void**)result) = methodTable;
            if (variableSized)
                *((UInt32*)((Byte*)result + sizeof(void*))) = elementCount;

            if (!reused && !NativeAotGarbageCollector.TrackAllocationUnderHeapGate(result, byteCount, methodTable))
            {
                ExitHeapGate();
                return RuntimeFailPointer(NativeAotRuntimeFailure.GarbageCollectorMetadataExhausted);
            }

            _allocatedBytes += byteCount;
            _allocationCount++;
            NativeAotGarbageCollector.NoteSuccessfulAllocationUnderHeapGate(byteCount);
        }

        ExitHeapGate();

        if (result == null)
            return RuntimeFailPointer(NativeAotRuntimeFailure.ManagedHeapExhausted);
        return result;
    }

    private static UInt64 AlignPage(UInt64 value)
    {
        if (value > UInt64.MaxValue - 4095UL) return 0UL;
        return (value + 4095UL) & ~4095UL;
    }

    private static Boolean HasComponentSize(void* methodTable)
        => methodTable != null && ((*((UInt32*)methodTable) & 0x80000000U) != 0U);

    internal static void Zero(void* address, UInt64 byteCount)
    {
        Byte* bytes = (Byte*)address;
        for (UInt64 i = 0UL; i < byteCount; i++) bytes[i] = 0;
    }

    private static void Fail(NativeAotRuntimeFailure failure)
    {
        _failure = failure;
        _phase = NativeAotRuntimePhase.Failed;
    }

    private static void* RuntimeFailPointer(NativeAotRuntimeFailure failure)
    {
        Fail(failure);
        return null;
    }

    // .NET 10 NativeAOT FunctionPointerOps stores GenericMethodDescriptor values in
    // native (non-GC) memory. Keep that policy intact and provide only the platform
    // allocator primitive that the freestanding Inu target must supply.
    [RuntimeExport("InuNativeAlloc")]
    private static void* InuNativeAlloc(UInt64 byteCount, UInt64 alignment)
    {
        if (byteCount == 0UL) return null;
        if (alignment < (UInt64)sizeof(void*)) alignment = (UInt64)sizeof(void*);
        if ((alignment & (alignment - 1UL)) != 0UL) return null;
        if (!KernelHeap.TryAllocate(byteCount, alignment, true, out KernelHeapAllocation allocation)) return null;
        return (void*)(nuint)allocation.Address;
    }

    [RuntimeExport("RhpNewFast")]
    private static void* RhpNewFast(void* methodTable) => AllocateObject(methodTable, 0U, false);

    // 0.0.135: dictionary-only allocator wrappers. Ordinary compiled allocations keep
    // using RhpNewFast/RhpNewFinalizable without serial noise; the AllocateObject
    // NativeLayout cell returned by Inu's TypeLoader points at these wrappers so a
    // failing shared generic activation reports its exact MethodTable and result.
    [RuntimeExport("InuRhpNewFastDiagnostic")]
    private static void* InuRhpNewFastDiagnostic(void* methodTable)
    {
        NativeAotExceptionRuntime.TraceStage(0x1A20UL);
        NativeAotExceptionRuntime.TraceValue(0x1A21UL, (UInt64)(nuint)methodTable);
        void* result = AllocateObject(methodTable, 0U, false);
        NativeAotExceptionRuntime.TraceValue(0x1A22UL, (UInt64)(nuint)result);
        NativeAotExceptionRuntime.TraceStage(0x1A23UL);
        return result;
    }

    [RuntimeExport("RhpNewFinalizable")]
    private static void* RhpNewFinalizable(void* methodTable) => AllocateObject(methodTable, 0U, false);

    [RuntimeExport("InuRhpNewFinalizableDiagnostic")]
    private static void* InuRhpNewFinalizableDiagnostic(void* methodTable)
    {
        NativeAotExceptionRuntime.TraceStage(0x1A24UL);
        NativeAotExceptionRuntime.TraceValue(0x1A25UL, (UInt64)(nuint)methodTable);
        void* result = AllocateObject(methodTable, 0U, false);
        NativeAotExceptionRuntime.TraceValue(0x1A26UL, (UInt64)(nuint)result);
        NativeAotExceptionRuntime.TraceStage(0x1A27UL);
        return result;
    }

    [RuntimeExport("RhpNewObject")]
    private static void* RhpNewObject(void* methodTable, UInt32 allocationFlags)
        => AllocateObject(methodTable, 0U, false);

    [RuntimeExport("RhpNewArray")]
    private static void* RhpNewArray(void* methodTable, Int32 elementCount)
    {
        if (elementCount < 0) throw new OverflowException();
        return AllocateObject(methodTable, (UInt32)elementCount, true);
    }

    [RuntimeExport("RhpNewArrayFast")]
    private static void* RhpNewArrayFast(void* methodTable, Int32 elementCount)
    {
        if (elementCount < 0) throw new OverflowException();
        return AllocateObject(methodTable, (UInt32)elementCount, true);
    }

    [RuntimeExport("RhpNewPtrArrayFast")]
    private static void* RhpNewPtrArrayFast(void* methodTable, Int32 elementCount)
    {
        if (elementCount < 0) throw new OverflowException();
        return AllocateObject(methodTable, (UInt32)elementCount, true);
    }

    [RuntimeExport("RhNewString")]
    private static void* RhNewString(void* methodTable, Int32 characterCount)
        => characterCount < 0 ? RuntimeFailPointer(NativeAotRuntimeFailure.AllocationOverflow) : AllocateObject(methodTable, (UInt32)characterCount, true);

    [RuntimeExport("RhpNewVariableSizeObject")]
    private static void* RhpNewVariableSizeObject(void* methodTable, Int32 elementCount)
        => elementCount < 0 ? RuntimeFailPointer(NativeAotRuntimeFailure.AllocationOverflow) : AllocateObject(methodTable, (UInt32)elementCount, true);

    [RuntimeExport("RhpGcAlloc")]
    private static void* RhpGcAlloc(void* methodTable, UInt32 allocationFlags, Int64 elementCount, void* transitionFrame)
        => elementCount < 0L || elementCount > Int32.MaxValue
            ? RuntimeFailPointer(NativeAotRuntimeFailure.AllocationOverflow)
            : AllocateObject(methodTable, (UInt32)elementCount, HasComponentSize(methodTable));

    private const UInt32 ReadyToRunSignature = 0x00525452U;
    private const UInt32 MaximumTypeManagers = 128U;
    private const UInt32 MaximumFrozenSegments = 128U;
    private const Int32 ModuleInfoHasEndPointer = 0x1;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct ReadyToRunHeader
    {
        internal UInt32 Signature;
        internal UInt16 MajorVersion;
        internal UInt16 MinorVersion;
        internal UInt32 Flags;
        internal UInt16 NumberOfSections;
        internal Byte EntrySize;
        internal Byte EntryType;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct ModuleInfoRow
    {
        internal Int32 SectionId;
        internal Int32 Flags;
        internal IntPtr Start;
        internal IntPtr End;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct TypeManagerRecord
    {
        internal IntPtr OsHandle;
        internal IntPtr ReadyToRunHeader;
    }

    private unsafe struct TypeManagerTable
    {
        internal fixed Byte Records[(Int32)(MaximumTypeManagers * 16U)];
    }

    private static TypeManagerTable _typeManagers;
    private static UInt32 _typeManagerCount;
    private static UInt64 _registeredOsModule;

    // The loaded PE image remains resident for the lifetime of the kernel.  Exposing
    // its base to the collector allows a conservative scan of writable image sections
    // as a safety net for NativeAOT GC statics whose section metadata is unavailable.
    internal static UInt64 RegisteredOsModuleBase => _registeredOsModule;

    [RuntimeExport("RhpRegisterOsModule")]
    private static void RhpRegisterOsModule(IntPtr osModule) => _registeredOsModule = (UInt64)(nuint)(void*)osModule;

    [RuntimeExport("RhpCreateTypeManager")]
    private static IntPtr RhpCreateTypeManager(IntPtr osModule, IntPtr moduleHeader, IntPtr* classlibFunctions, Int32 classlibFunctionCount)
    {
        if (moduleHeader == IntPtr.Zero || classlibFunctionCount < 0) return IntPtr.Zero;
        ReadyToRunHeader* header = (ReadyToRunHeader*)(void*)moduleHeader;
        if (header->Signature != ReadyToRunSignature || header->NumberOfSections > 4096U || header->EntrySize < sizeof(ModuleInfoRow))
        {
            Fail(NativeAotRuntimeFailure.InvalidReadyToRunModule);
            return IntPtr.Zero;
        }

        // 0.0.104: retain the useful ReadyToRun diagnostics without serialising every
        // section on every boot. The full table dump was valuable while the row-layout
        // bug was unknown, but after 0.0.101 it only consumes TCG/serial time before the
        // managed acceptance deadline. Keep startup/static rows plus every reflection-map
        // blob consumed by the .NET 10 GVM slice.
        NativeAotExceptionRuntime.TraceStage(0x193UL);
        NativeAotExceptionRuntime.TraceValue(0x194UL, header->NumberOfSections);
        Byte* diagnosticRows = (Byte*)header + sizeof(ReadyToRunHeader);
        for (UInt32 diagnosticIndex = 0U; diagnosticIndex < header->NumberOfSections; diagnosticIndex++)
        {
            ModuleInfoRow* diagnosticRow = (ModuleInfoRow*)(diagnosticRows + (UInt64)diagnosticIndex * header->EntrySize);
            Int32 diagnosticSectionId = diagnosticRow->SectionId;
            Boolean diagnosticRelevant =
                diagnosticSectionId == 201 || // GCStaticRegion
                diagnosticSectionId == 204 || // TypeManagerIndirection
                diagnosticSectionId == 205 || // EagerCctor
                diagnosticSectionId == 206 || // FrozenObjectRegion
                diagnosticSectionId == 207 || // DehydratedData
                diagnosticSectionId == 213 || // ModuleInitializerList
                diagnosticSectionId == 308 || // CommonFixups
                diagnosticSectionId == 318 || // GenericVirtualMethodTable
                diagnosticSectionId == 322 || // GenericMethodsTemplateMap
                diagnosticSectionId == 330 || // NativeLayoutInfo
                diagnosticSectionId == 331 || // NativeReferences
                diagnosticSectionId == 335 || // GenericMethodsHashtable
                diagnosticSectionId == 336;   // ExactMethodInstantiationsHashtable
            if (!diagnosticRelevant) continue;

            UInt64 diagnosticStart = (UInt64)(nuint)(void*)diagnosticRow->Start;
            UInt64 diagnosticEnd = (UInt64)(nuint)(void*)diagnosticRow->End;
            Boolean diagnosticHasEnd = (diagnosticRow->Flags & ModuleInfoHasEndPointer) != 0;
            NativeAotExceptionRuntime.TraceValue(0x195UL, (UInt64)(UInt32)diagnosticSectionId);
            NativeAotExceptionRuntime.TraceValue(0x198UL, (UInt64)(UInt32)diagnosticRow->Flags);
            NativeAotExceptionRuntime.TraceValue(0x196UL, diagnosticHasEnd
                ? (diagnosticEnd >= diagnosticStart ? diagnosticEnd - diagnosticStart : UInt64.MaxValue)
                : (UInt64)sizeof(void*));
        }
        NativeAotExceptionRuntime.TraceStage(0x197UL);
        if (_typeManagerCount >= MaximumTypeManagers)
        {
            Fail(NativeAotRuntimeFailure.TypeManagerCapacityExceeded);
            return IntPtr.Zero;
        }
        fixed (TypeManagerTable* table = &_typeManagers)
        {
            UInt32 index = _typeManagerCount++;
            TypeManagerRecord* record = (TypeManagerRecord*)(table->Records + index * (UInt32)sizeof(TypeManagerRecord));
            record->OsHandle = osModule;
            record->ReadyToRunHeader = moduleHeader;
            return (IntPtr)record;
        }
    }

    [RuntimeExport("RhGetModuleSection")]
    private static IntPtr RhGetModuleSection(IntPtr typeManager, Int32 sectionType, Int32* length)
    {
        if (length != null) *length = 0;
        if (typeManager == IntPtr.Zero) return IntPtr.Zero;
        TypeManagerRecord* record = (TypeManagerRecord*)(void*)typeManager;
        if (record->ReadyToRunHeader == IntPtr.Zero) return IntPtr.Zero;
        ReadyToRunHeader* header = (ReadyToRunHeader*)(void*)record->ReadyToRunHeader;
        if (header->Signature != ReadyToRunSignature || header->NumberOfSections > 4096U || header->EntrySize < sizeof(ModuleInfoRow))
            return IntPtr.Zero;

        Byte* rows = (Byte*)header + sizeof(ReadyToRunHeader);
        for (UInt32 i = 0U; i < header->NumberOfSections; i++)
        {
            ModuleInfoRow* row = (ModuleInfoRow*)(rows + (UInt64)i * header->EntrySize);
            if (row->SectionId != sectionType) continue;
            UInt64 start = (UInt64)(nuint)(void*)row->Start;
            if (start == 0UL) return IntPtr.Zero;

            // .NET 10 NativeAOT TypeManager::ModuleInfoRow::GetLength() only reads
            // End when ModuleInfoFlags.HasEndPointer is set. Pointer-only rows such as
            // TypeManagerIndirection have no valid End pointer and are exactly one pointer
            // wide. 0.0.100 incorrectly interpreted every row as Start/End, which could
            // silently reject pointer-only runtime sections and leave module metadata
            // indirections uninitialised.
            Int32 sectionLength;
            if ((row->Flags & ModuleInfoHasEndPointer) != 0)
            {
                UInt64 end = (UInt64)(nuint)(void*)row->End;
                if (end < start || end - start > Int32.MaxValue) return IntPtr.Zero;
                sectionLength = (Int32)(end - start);
            }
            else
            {
                sectionLength = sizeof(void*);
            }

            if (length != null) *length = sectionLength;
            return row->Start;
        }
        return IntPtr.Zero;
    }

    // .NET 10 NativeAOT startup allocation ABI. StartupCodeHelpers uses this
    // exact export for GC-static base objects. Inu's heap is non-moving, so
    // GC allocation flags are currently advisory, but the result slot and symbol
    // contract match RuntimeImports.RhAllocateNewObject.
    [RuntimeExport("RhAllocateNewObject")]
    private static void RhAllocateNewObject(IntPtr pEEType, UInt32 flags, void* pResult)
    {
        if (pResult == null) return;
        void* value = AllocateObject((void*)pEEType, 0U, false);
        *((void**)pResult) = value;
    }

    [RuntimeExport("RhRegisterStaticRoot")]
    private static Boolean RhRegisterStaticRoot(void** rootSlot) => NativeAotGarbageCollector.RegisterStaticRoot(rootSlot);

    [RuntimeExport("RhRegisterStaticBase")]
    private static Boolean RhRegisterStaticBase(void* objectAddress) => NativeAotGarbageCollector.RegisterStaticBase(objectAddress);

    // NativeAOT roots its GC-static spine through a normal GC handle. Inu is non-moving,
    // so a permanent pinned/strong root is equivalent for reachability and keeps the
    // complete spine -> module-spine -> static-base graph alive across every collection.
    [RuntimeExport("RhRegisterStrongRoot")]
    private static Boolean RhRegisterStrongRoot(void* objectAddress) => NativeAotGarbageCollector.RegisterPinnedRoot(objectAddress);

    [RuntimeExport("RhSealStaticRoots")]
    private static void RhSealStaticRoots() => NativeAotGarbageCollector.SealRootMap();

    [RuntimeExport("RhRegisterFrozenSegment")]
    private static IntPtr RhRegisterFrozenSegment(void* start, UIntPtr allocated, UIntPtr committed, UIntPtr reserved)
    {
        UInt64 byteCount = (UInt64)(nuint)allocated;
        return NativeAotGarbageCollector.RegisterFrozenSegment(start, byteCount) ? (IntPtr)start : IntPtr.Zero;
    }

    // RhpAssignRef, RhpCheckedAssignRef, and RhpByRefAssignRef are compiler-known
    // x64 leaf helpers, not ordinary managed-call helpers. Their canonical symbols
    // live in SDK/native/x64/Runtime.asm so the JIT's register-preservation and
    // byref write-barrier ABI cannot be changed by managed code generation.
}

/// <summary>Failure reason for the non-moving mark/sweep collector.</summary>
public static unsafe class NativeAotGcExports
{
    [RuntimeExport("InuGcCollect")]
    private static Boolean Collect() => NativeAotGarbageCollector.Collect();

    [RuntimeExport("InuGcWaitForPendingFinalizers")]
    private static void WaitForPendingFinalizers() => NativeAotGarbageCollector.DrainFinalizerQueue();

    // Called only by the low-address slow path of the compiler-known x64 reference
    // barriers. The leaf helper performs the store first and preserves the volatile
    // registers that NativeAOT may keep live across the helper call.
    [RuntimeExport("InuGcReferenceWrite")]
    private static void ReferenceWrite(void* destination)
    {
        _ = NativeAotGarbageCollector.RegisterImageStaticWrite(destination);
    }

    [RuntimeExport("RhSuppressFinalize")]
    private static void SuppressFinalize(Object value) => NativeAotGarbageCollector.SuppressFinalize(value);

    [RuntimeExport("RhReRegisterForFinalize")]
    private static Boolean ReRegisterForFinalize(Object value) => NativeAotGarbageCollector.ReRegisterForFinalize(value);
}

public enum NativeAotGcFailure
{
    None = 0,
    MetadataExhausted = 1,
    RootTableExhausted = 2,
    ThreadTableExhausted = 3,
    MarkStackExhausted = 4,
    ThreadsNotAtSafepoint = 5,
    RootMapNotReady = 6,
    HeapGateUnavailable = 7,
    CollectionCoordinatorUnavailable = 8,
    FinalizerQueueExhausted = 9,
    CollectionCancelled = 10,
    CollectionTimedOut = 11
}

/// <summary>Snapshot of mark/sweep activity.</summary>
public readonly struct NativeAotGcStatistics
{
    internal NativeAotGcStatistics(
        UInt64 collections,
        UInt64 reclaimedObjects,
        UInt64 reclaimedBytes,
        UInt64 reusedObjects,
        UInt64 reusedBytes,
        UInt64 writeBarrierStores,
        UInt32 liveObjects,
        UInt64 liveBytes,
        UInt32 pendingFinalizers,
        UInt64 finalizersRun,
        UInt64 pressureCollections,
        UInt32 allocationsSinceCollection,
        UInt64 bytesSinceCollection,
        NativeAotGcFailure failure)
    {
        Collections = collections;
        ReclaimedObjects = reclaimedObjects;
        ReclaimedBytes = reclaimedBytes;
        ReusedObjects = reusedObjects;
        ReusedBytes = reusedBytes;
        WriteBarrierStores = writeBarrierStores;
        LiveObjects = liveObjects;
        LiveBytes = liveBytes;
        PendingFinalizers = pendingFinalizers;
        FinalizersRun = finalizersRun;
        PressureCollections = pressureCollections;
        AllocationsSinceCollection = allocationsSinceCollection;
        BytesSinceCollection = bytesSinceCollection;
        LastFailure = failure;
    }

    public UInt64 Collections { get; }
    public UInt64 ReclaimedObjects { get; }
    public UInt64 ReclaimedBytes { get; }
    public UInt64 ReusedObjects { get; }
    public UInt64 ReusedBytes { get; }
    public UInt64 WriteBarrierStores { get; }
    public UInt32 LiveObjects { get; }
    public UInt64 LiveBytes { get; }
    public UInt32 PendingFinalizers { get; }
    public UInt64 FinalizersRun { get; }
    public UInt64 PressureCollections { get; }
    public UInt32 AllocationsSinceCollection { get; }
    public UInt64 BytesSinceCollection { get; }
    public NativeAotGcFailure LastFailure { get; }
}

/// <summary>
/// Mature Inu GC for the current x64 NativeAOT kernel path: stop-the-world,
/// non-moving mark/sweep with adaptive pressure collection, registered static/frozen/
/// pinned roots, scheduler stack roots, finalization and reclaimed-block reuse.
/// Stack scanning remains conservative and accepts interior pointers. No object moves.
/// </summary>
public static unsafe class NativeAotGarbageCollector
{
    private const Int32 MaximumTrackedAllocations = 16384;
    private const Int32 MaximumRootSlots = 2048;
    private const Int32 MaximumStaticBases = 2048;
    private const Int32 MaximumPinnedRoots = 512;
    private const Int32 MaximumFrozenSegments = 128;
    private const Int32 MaximumThreads = 256;
    private const UInt32 PressureAllocationThreshold = 1024U;
    private const UInt64 PressureByteThreshold = 512UL * 1024UL;
    private const UInt32 MetadataReserveRecords = 64U;
    private const UInt32 HasPointersFlag = 0x01000000U;
    private const UInt32 HasFinalizerFlag = 0x00100000U;

    private const Byte RecordUnused = 0;
    private const Byte RecordLive = 1;
    private const Byte RecordFree = 2;
    private const Byte FinalizerNone = 0;
    private const Byte FinalizerRegistered = 1;
    private const Byte FinalizerPending = 2;
    private const Byte FinalizerRan = 3;
    private const Byte FinalizerPendingReregister = 4;

    private unsafe struct GcTables
    {
        internal fixed UInt64 Addresses[MaximumTrackedAllocations];
        internal fixed UInt64 Sizes[MaximumTrackedAllocations];
        internal fixed UInt64 MethodTables[MaximumTrackedAllocations];
        internal fixed Byte States[MaximumTrackedAllocations];
        internal fixed Byte Marks[MaximumTrackedAllocations];
        internal fixed Byte FinalizerStates[MaximumTrackedAllocations];
        internal fixed UInt32 MarkStack[MaximumTrackedAllocations];
        // Collection-local address order.  Conservative root scanning performs millions of
        // candidate lookups under QEMU; a sorted live-record index turns each lookup from an
        // O(n) metadata walk into O(log n) while retaining support for interior pointers.
        internal fixed UInt32 AddressOrder[MaximumTrackedAllocations];
        internal fixed UInt32 FinalizerQueue[MaximumTrackedAllocations];

        internal fixed UInt64 RootSlots[MaximumRootSlots];
        // A NativeAOT GC-static region cell permanently points at its synthetic
        // static-base object. Keep that base address separately so the module-lifetime
        // static storage itself is always rooted even if the cell contents are later
        // inspected through a different code path.
        internal fixed UInt64 RootBaseAddresses[MaximumRootSlots];
        // NativeAOT synthetic GC-static bases are module-lifetime managed objects.
        // Register them directly so the collector never depends solely on recovering
        // the base through an image-owned indirection cell.
        internal fixed UInt64 StaticBases[MaximumStaticBases];
        internal fixed UInt64 PinnedRoots[MaximumPinnedRoots];
        internal fixed UInt64 FrozenStarts[MaximumFrozenSegments];
        internal fixed UInt64 FrozenLengths[MaximumFrozenSegments];

        internal fixed UInt64 ThreadIds[MaximumThreads];
        internal fixed UInt64 StackLow[MaximumThreads];
        internal fixed UInt64 StackHigh[MaximumThreads];
        internal fixed UInt64 StackScanLow[MaximumThreads];
        internal fixed Byte ThreadUsed[MaximumThreads];
        internal fixed UInt64 ThreadAtSafepoint[MaximumThreads];
    }

    private static GcTables _tables;
    private static UInt32 _trackedHighWater;
    private static UInt32 _rootSlotCount;
    private static UInt32 _staticBaseCount;
    private static UInt32 _pinnedRootCount;
    private static UInt32 _frozenSegmentCount;
    private static UInt32 _threadCount;
    private static UInt64 _collectionCount;
    private static UInt64 _reclaimedObjects;
    private static UInt64 _reclaimedBytes;
    private static UInt64 _reusedObjects;
    private static UInt64 _reusedBytes;
    private static UInt64 _writeBarrierStores;
    private static UInt32 _finalizerQueueHead;
    private static UInt32 _finalizerQueueCount;
    private static UInt64 _finalizersRun;
    private static UInt32 _executingFinalizerRecord = UInt32.MaxValue;
    private static UInt64 _pressureCollections;
    private static UInt32 _allocationsSinceCollection;
    private static UInt64 _bytesSinceCollection;
    private static UInt64 _collectionCoordinator;
    private static UInt64 _collectionAbortProbe;
    private static UInt32 _addressOrderCount;
    private static Boolean _rootMapReady;
    private static NativeAotGcFailure _lastFailure;

    internal static void Initialize()
    {
        fixed (GcTables* tables = &_tables)
        {
            Byte* raw = (Byte*)tables;
            UInt64 bytes = (UInt64)sizeof(GcTables);
            for (UInt64 i = 0; i < bytes; i++) raw[i] = 0;
        }

        _trackedHighWater = 0U;
        _rootSlotCount = 0U;
        _staticBaseCount = 0U;
        _pinnedRootCount = 0U;
        _frozenSegmentCount = 0U;
        _threadCount = 0U;
        _collectionCount = 0UL;
        _reclaimedObjects = 0UL;
        _reclaimedBytes = 0UL;
        _reusedObjects = 0UL;
        _reusedBytes = 0UL;
        _writeBarrierStores = 0UL;
        _finalizerQueueHead = 0U;
        _finalizerQueueCount = 0U;
        _finalizersRun = 0UL;
        _executingFinalizerRecord = UInt32.MaxValue;
        _pressureCollections = 0UL;
        _allocationsSinceCollection = 0U;
        _bytesSinceCollection = 0UL;
        _collectionCoordinator = 0UL;
        _collectionAbortProbe = 0UL;
        _addressOrderCount = 0U;
        _rootMapReady = false;
        _lastFailure = NativeAotGcFailure.None;
    }

    /// <summary>Registers the scheduler-owned stop-the-world coordinator used for SMP collections.</summary>
    public static Boolean RegisterCollectionCoordinator(delegate*<Boolean> coordinator)
    {
        if (coordinator == null) return false;
        _collectionCoordinator = (UInt64)(void*)coordinator;
        return true;
    }

    /// <summary>Registers a scheduler-owned cancellation/deadline probe consulted during mark/sweep.</summary>
    public static Boolean RegisterCollectionAbortProbe(delegate*<NativeAotGcFailure> probe)
    {
        if (probe == null || _collectionAbortProbe != 0UL) return false;
        _collectionAbortProbe = (UInt64)(void*)probe;
        return true;
    }

    private static Boolean CollectionAbortRequested()
    {
        UInt64 raw = _collectionAbortProbe;
        if (raw == 0UL) return false;
        delegate*<NativeAotGcFailure> probe = (delegate*<NativeAotGcFailure>)(void*)(nuint)raw;
        NativeAotGcFailure failure = probe();
        if (failure == NativeAotGcFailure.None) return false;
        _lastFailure = failure;
        return true;
    }

    /// <summary>Requests one collection through the scheduler stop-the-world rendezvous when installed.</summary>
    public static Boolean Collect()
    {
        UInt64 coordinator = _collectionCoordinator;
        if (coordinator != 0UL)
        {
            delegate*<Boolean> callback = (delegate*<Boolean>)(void*)(nuint)coordinator;
            return callback();
        }
        return CollectAtSafepoint();
    }

    /// <summary>Runs mark/sweep after the scheduler has proven every registered stack stable.</summary>
    public static Boolean CollectAtSafepoint()
    {
        if (!NativeAotRuntime.EnterHeapGate())
        {
            _lastFailure = NativeAotGcFailure.HeapGateUnavailable;
            return false;
        }
        Boolean result = CollectUnderHeapGate();
        NativeAotRuntime.ExitHeapGate();
        return result;
    }

    /// <summary>
    /// Signals that startup/module registration has supplied all runtime static
    /// root slots. Automatic collection remains disabled until this is called.
    /// </summary>
    public static void SealRootMap()
    {
        _rootMapReady = true;
        _lastFailure = NativeAotGcFailure.None;
    }

    public static Boolean IsRootMapReady() => _rootMapReady;

    internal static Boolean ShouldCollectForAllocationPressureUnderHeapGate(UInt64 requestedBytes)
    {
        if (!_rootMapReady || _collectionCoordinator == 0UL) return false;
        if (_trackedHighWater >= MaximumTrackedAllocations - MetadataReserveRecords) return true;
        if (_allocationsSinceCollection >= PressureAllocationThreshold) return true;
        if (_bytesSinceCollection >= PressureByteThreshold) return true;
        return requestedBytes != 0UL && _bytesSinceCollection > UInt64.MaxValue - requestedBytes;
    }

    internal static void NoteSuccessfulAllocationUnderHeapGate(UInt64 bytes)
    {
        if (_allocationsSinceCollection != UInt32.MaxValue) _allocationsSinceCollection++;
        if (_bytesSinceCollection <= UInt64.MaxValue - bytes) _bytesSinceCollection += bytes;
        else _bytesSinceCollection = UInt64.MaxValue;
    }

    internal static Boolean CollectForAllocationPressure()
    {
        Boolean result = Collect();
        if (result) _pressureCollections++;
        return result;
    }

    /// <summary>Registers the address of a managed-reference static/root slot.</summary>
    public static Boolean RegisterStaticRoot(void** rootSlot)
    {
        if (rootSlot == null) return false;
        if (!NativeAotRuntime.EnterHeapGate())
        {
            _lastFailure = NativeAotGcFailure.HeapGateUnavailable;
            return false;
        }

        Boolean result = false;
        fixed (GcTables* tables = &_tables)
        {
            UInt64 slot = (UInt64)(nuint)rootSlot;
            UInt64 baseAddress = *(UInt64*)rootSlot;
            for (UInt32 i = 0U; i < _rootSlotCount; i++)
            {
                if (tables->RootSlots[i] == slot)
                {
                    // GC-static cells are module-lifetime indirections. Refresh the
                    // remembered base if startup registration is repeated, but never
                    // throw away a previously valid base merely because the cell is
                    // temporarily zero while a module is being initialized.
                    if (baseAddress != 0UL) tables->RootBaseAddresses[i] = baseAddress;
                    result = true;
                    break;
                }
            }

            if (!result && _rootSlotCount < MaximumRootSlots)
            {
                UInt32 index = _rootSlotCount++;
                tables->RootSlots[index] = slot;
                tables->RootBaseAddresses[index] = baseAddress;
                result = true;
            }
        }

        if (!result) _lastFailure = NativeAotGcFailure.RootTableExhausted;
        NativeAotRuntime.ExitHeapGate();
        return result;
    }

    /// <summary>Registers a synthetic NativeAOT GC-static base as module-lifetime storage.
    /// The base itself and every managed reference stored in its payload are roots for
    /// every collection.</summary>
    public static Boolean RegisterStaticBase(void* objectAddress)
    {
        if (objectAddress == null) return false;
        if (!NativeAotRuntime.EnterHeapGate())
        {
            _lastFailure = NativeAotGcFailure.HeapGateUnavailable;
            return false;
        }

        Boolean result = false;
        fixed (GcTables* tables = &_tables)
        {
            UInt64 address = (UInt64)(nuint)objectAddress;
            for (UInt32 i = 0U; i < _staticBaseCount; i++)
            {
                if (tables->StaticBases[i] != address) continue;
                result = true;
                break;
            }

            if (!result && _staticBaseCount < MaximumStaticBases)
            {
                tables->StaticBases[_staticBaseCount++] = address;
                result = true;
            }
        }

        if (!result) _lastFailure = NativeAotGcFailure.RootTableExhausted;
        NativeAotRuntime.ExitHeapGate();
        return result;
    }

    /// <summary>Registers an immutable NativeAOT frozen-object region. Frozen objects
    /// are image-owned and never swept, but references stored in the region are
    /// treated as roots so they can keep managed-heap objects alive.</summary>
    public static Boolean RegisterFrozenSegment(void* start, UInt64 byteCount)
    {
        if (start == null || byteCount == 0UL) return false;
        if (!NativeAotRuntime.EnterHeapGate())
        {
            _lastFailure = NativeAotGcFailure.HeapGateUnavailable;
            return false;
        }
        Boolean result = false;
        fixed (GcTables* tables = &_tables)
        {
            UInt64 address = (UInt64)(nuint)start;
            for (UInt32 i = 0U; i < _frozenSegmentCount; i++)
            {
                if (tables->FrozenStarts[i] == address && tables->FrozenLengths[i] == byteCount)
                {
                    result = true;
                    break;
                }
            }
            if (!result && _frozenSegmentCount < MaximumFrozenSegments)
            {
                UInt32 index = _frozenSegmentCount++;
                tables->FrozenStarts[index] = address;
                tables->FrozenLengths[index] = byteCount;
                result = true;
            }
        }
        if (!result) _lastFailure = NativeAotGcFailure.RootTableExhausted;
        NativeAotRuntime.ExitHeapGate();
        return result;
    }

    /// <summary>
    /// Pins a managed object address as an explicit root. This is useful for
    /// runtime-owned handles that do not live in a normal static reference slot.
    /// </summary>
    public static Boolean RegisterPinnedRoot(void* objectAddress)
    {
        if (objectAddress == null) return false;
        if (!NativeAotRuntime.EnterHeapGate())
        {
            _lastFailure = NativeAotGcFailure.HeapGateUnavailable;
            return false;
        }

        Boolean result = false;
        fixed (GcTables* tables = &_tables)
        {
            UInt64 address = (UInt64)(nuint)objectAddress;
            for (UInt32 i = 0U; i < _pinnedRootCount; i++)
            {
                if (tables->PinnedRoots[i] == address)
                {
                    result = true;
                    break;
                }
            }

            if (!result && _pinnedRootCount < MaximumPinnedRoots)
            {
                tables->PinnedRoots[_pinnedRootCount++] = address;
                result = true;
            }
        }

        if (!result) _lastFailure = NativeAotGcFailure.RootTableExhausted;
        NativeAotRuntime.ExitHeapGate();
        return result;
    }

    public static Boolean UnregisterPinnedRoot(void* objectAddress)
    {
        if (objectAddress == null) return false;
        if (!NativeAotRuntime.EnterHeapGate()) return false;

        Boolean result = false;
        fixed (GcTables* tables = &_tables)
        {
            UInt64 address = (UInt64)(nuint)objectAddress;
            for (UInt32 i = 0U; i < _pinnedRootCount; i++)
            {
                if (tables->PinnedRoots[i] != address) continue;
                UInt32 last = _pinnedRootCount - 1U;
                tables->PinnedRoots[i] = tables->PinnedRoots[last];
                tables->PinnedRoots[last] = 0UL;
                _pinnedRootCount = last;
                result = true;
                break;
            }
        }

        NativeAotRuntime.ExitHeapGate();
        return result;
    }

    /// <summary>Registers a scheduler-owned stack range for conservative root scanning.</summary>
    public static Boolean RegisterThreadStack(UInt64 threadId, void* stackLow, void* stackHigh)
    {
        UInt64 low = (UInt64)(nuint)stackLow;
        UInt64 high = (UInt64)(nuint)stackHigh;
        if (low == 0UL || high == 0UL || low == high) return false;
        if (low > high)
        {
            UInt64 temporary = low;
            low = high;
            high = temporary;
        }

        if (!NativeAotRuntime.EnterHeapGate())
        {
            _lastFailure = NativeAotGcFailure.HeapGateUnavailable;
            return false;
        }

        Boolean result = false;
        fixed (GcTables* tables = &_tables)
        {
            Int32 free = -1;
            for (Int32 i = 0; i < MaximumThreads; i++)
            {
                if (tables->ThreadUsed[i] == 0)
                {
                    if (free < 0) free = i;
                    continue;
                }
                if (tables->ThreadIds[i] != threadId) continue;
                tables->StackLow[i] = low;
                tables->StackHigh[i] = high;
                if (tables->StackScanLow[i] < low || tables->StackScanLow[i] > high) tables->StackScanLow[i] = low;
                result = true;
                break;
            }

            if (!result && free >= 0)
            {
                tables->ThreadUsed[free] = 1;
                tables->ThreadIds[free] = threadId;
                tables->StackLow[free] = low;
                tables->StackHigh[free] = high;
                tables->StackScanLow[free] = low;
                tables->ThreadAtSafepoint[free] = 1UL;
                _threadCount++;
                result = true;
            }
        }

        if (!result) _lastFailure = NativeAotGcFailure.ThreadTableExhausted;
        NativeAotRuntime.ExitHeapGate();
        return result;
    }

    /// <summary>
    /// The scheduler sets this while the corresponding stack is stable. This is intentionally
    /// lock-free: a CPU can acknowledge a stop-the-world IPI while the collector owns the heap gate.
    /// </summary>
    public static Boolean SetThreadAtSafepoint(UInt64 threadId, Boolean atSafepoint)
        => SetThreadAtSafepoint(threadId, atSafepoint, null);

    public static Boolean SetThreadAtSafepoint(UInt64 threadId, Boolean atSafepoint, void* scanLow)
    {
        fixed (GcTables* tables = &_tables)
        {
            for (Int32 i = 0; i < MaximumThreads; i++)
            {
                if (tables->ThreadUsed[i] == 0 || tables->ThreadIds[i] != threadId) continue;
                if (atSafepoint && scanLow != null)
                {
                    UInt64 candidate = (UInt64)(nuint)scanLow;
                    UInt64 low = tables->StackLow[i];
                    UInt64 high = tables->StackHigh[i];
                    if (candidate < low) candidate = low;
                    if (candidate > high) candidate = high;
                    if (!X64ArchitectureBoundary.AtomicStore64(&tables->StackScanLow[i], candidate)) return false;
                }
                return X64ArchitectureBoundary.AtomicStore64(&tables->ThreadAtSafepoint[i], atSafepoint ? 1UL : 0UL);
            }
        }
        return false;
    }

    public static Boolean UnregisterThreadStack(UInt64 threadId)
    {
        if (!NativeAotRuntime.EnterHeapGate()) return false;
        Boolean result = false;
        fixed (GcTables* tables = &_tables)
        {
            for (Int32 i = 0; i < MaximumThreads; i++)
            {
                if (tables->ThreadUsed[i] == 0 || tables->ThreadIds[i] != threadId) continue;
                tables->ThreadUsed[i] = 0;
                tables->ThreadIds[i] = 0UL;
                tables->StackLow[i] = 0UL;
                tables->StackHigh[i] = 0UL;
                tables->StackScanLow[i] = 0UL;
                tables->ThreadAtSafepoint[i] = 0UL;
                if (_threadCount != 0U) _threadCount--;
                result = true;
                break;
            }
        }
        NativeAotRuntime.ExitHeapGate();
        return result;
    }

    /// <summary>
    /// Called only after the scheduler has physically parked every remote processor.
    /// At that point every registered stack is immutable. Mark all registered ranges
    /// stable and conservatively scan from the registered low bound. This closes the
    /// publication race where CurrentSlot can change immediately before the GC IPI:
    /// correctness comes from the proven world-stop, not from a transient scheduler slot.
    /// </summary>
    public static Boolean PrepareRegisteredStacksForWorldStoppedScan()
    {
        fixed (GcTables* tables = &_tables)
        {
            for (Int32 i = 0; i < MaximumThreads; i++)
            {
                if ((i & 0x0F) == 0 && CollectionAbortRequested()) return false;
                if (tables->ThreadUsed[i] == 0) continue;
                UInt64 low = tables->StackLow[i];
                if (!X64ArchitectureBoundary.AtomicStore64(&tables->StackScanLow[i], low)) return false;
                if (!X64ArchitectureBoundary.AtomicStore64(&tables->ThreadAtSafepoint[i], 1UL)) return false;
            }
        }
        return true;
    }

    internal static Boolean CanCollectUnderHeapGate()
    {
        if (!_rootMapReady)
        {
            _lastFailure = NativeAotGcFailure.RootMapNotReady;
            return false;
        }

        fixed (GcTables* tables = &_tables)
        {
            for (Int32 i = 0; i < MaximumThreads; i++)
            {
                if (tables->ThreadUsed[i] == 0) continue;
                UInt64 stable = 0UL;
                if (!X64ArchitectureBoundary.AtomicLoad64(&tables->ThreadAtSafepoint[i], out stable) || stable == 0UL)
                {
                    NativeAotExceptionRuntime.TraceValue(0x16AUL, tables->ThreadIds[i]);
                    NativeAotExceptionRuntime.TraceValue(0x16BUL, (UInt64)(UInt32)i);
                    _lastFailure = NativeAotGcFailure.ThreadsNotAtSafepoint;
                    return false;
                }
            }
        }

        return true;
    }

    internal static Boolean CollectUnderHeapGate()
    {
        if (!CanCollectUnderHeapGate()) return false;

        fixed (GcTables* tables = &_tables)
        {
            if (CollectionAbortRequested()) return false;
            for (UInt32 i = 0U; i < _trackedHighWater; i++)
            {
                tables->Marks[i] = 0;
                if ((i & 0xFFU) == 0U && CollectionAbortRequested()) return false;
            }
            if (!BuildAddressOrder(tables)) return false;

            TraceFinalizerRecords(tables, 0x146UL); // snapshot after mark reset, before roots

            UInt32 stackCount = 0U;

            // Synthetic GC-static bases are allocated managed objects with module
            // lifetime. Root and scan the bases directly first; root-slot indirections
            // remain registered as a compatibility/refresh path, not as the sole source
            // of truth for static storage lifetime.
            for (UInt32 i = 0U; i < _staticBaseCount; i++)
            {
                if ((i & 0x3FU) == 0U && CollectionAbortRequested()) return false;
                UInt64 staticBase = tables->StaticBases[i];
                if (staticBase == 0UL) continue;
                if (!MarkRegisteredStaticBase(tables, staticBase, ref stackCount)) return false;
            }

            for (UInt32 i = 0U; i < _rootSlotCount; i++)
            {
                if ((i & 0x3FU) == 0U && CollectionAbortRequested()) return false;
                UInt64 slotAddress = tables->RootSlots[i];
                if (slotAddress == 0UL) continue;

                // NativeAOT GC-static region entries root synthetic static-base objects.
                // The static base itself has module lifetime, so root the address captured
                // at registration even if a later read of the image-owned cell is stale
                // or temporarily unavailable. Then also honour the current cell value in
                // case startup legitimately refreshed it.
                UInt64 rememberedBase = tables->RootBaseAddresses[i];
                if (rememberedBase != 0UL &&
                    !MarkStaticRootCandidate(tables, rememberedBase, slotAddress, ref stackCount)) return false;

                UInt64 candidate = *(UInt64*)(nuint)slotAddress;
                if (candidate != 0UL && candidate != rememberedBase &&
                    !MarkStaticRootCandidate(tables, candidate, slotAddress, ref stackCount)) return false;
            }

            for (UInt32 i = 0U; i < _pinnedRootCount; i++)
            {
                if ((i & 0x3FU) == 0U && CollectionAbortRequested()) return false;
                if (!MarkCandidate(tables, tables->PinnedRoots[i], 2UL, i, ref stackCount)) return false;
            }

            // Frozen objects live outside the managed allocation table and are never
            // swept. Conservatively scan their pointer-sized slots as roots.
            for (UInt32 i = 0U; i < _frozenSegmentCount; i++)
            {
                if (CollectionAbortRequested()) return false;
                UInt64 start = tables->FrozenStarts[i] & ~7UL;
                UInt64 length = tables->FrozenLengths[i];
                if (start == 0UL || length < 8UL) continue;
                UInt64 end = start + (length & ~7UL);
                if (end < start) continue;
                for (UInt64 cursor = start; cursor + 8UL <= end; cursor += 8UL)
                {
                    if (((cursor - start) & 0x7FFUL) == 0UL && CollectionAbortRequested()) return false;
                    UInt64 candidate = *(UInt64*)(nuint)cursor;
                    if (!MarkCandidate(tables, candidate, 3UL, cursor, ref stackCount)) return false;
                }
            }

            for (Int32 i = 0; i < MaximumThreads; i++)
            {
                if ((i & 0x0F) == 0 && CollectionAbortRequested()) return false;
                if (tables->ThreadUsed[i] == 0) continue;
                UInt64 registeredLow = tables->StackLow[i];
                UInt64 scanLow = tables->StackScanLow[i];
                UInt64 low = (scanLow >= registeredLow ? scanLow : registeredLow) & ~7UL;
                UInt64 high = tables->StackHigh[i] & ~7UL;
                if (high < low)
                {
                    UInt64 temporary = low;
                    low = high;
                    high = temporary;
                }

                for (UInt64 cursor = low; cursor + 8UL <= high; cursor += 8UL)
                {
                    if (((cursor - low) & 0x7FFUL) == 0UL && CollectionAbortRequested()) return false;
                    UInt64 candidate = *(UInt64*)(nuint)cursor;
                    if (!MarkCandidate(tables, candidate, 4UL, cursor, ref stackCount)) return false;
                }
            }

            while (stackCount != 0U)
            {
                if ((stackCount & 0x3FU) == 0U && CollectionAbortRequested()) return false;
                UInt32 index = tables->MarkStack[--stackCount];
                UInt64 methodTable = tables->MethodTables[index];
                if (methodTable == 0UL) continue;

                UInt32 flags = *(UInt32*)(nuint)methodTable;
                if ((flags & HasPointersFlag) == 0U) continue;

                UInt64 address = tables->Addresses[index];
                UInt64 size = tables->Sizes[index];
                for (UInt64 offset = 8UL; offset + 8UL <= size; offset += 8UL)
                {
                    if ((offset & 0x7FFUL) == 0UL && CollectionAbortRequested()) return false;
                    UInt64 candidate = *(UInt64*)(nuint)(address + offset);
                    if (!MarkCandidate(tables, candidate, 5UL, address + offset, ref stackCount)) return false;
                }
            }

            TraceFinalizerRecords(tables, 0x14CUL); // snapshot after roots/object graph, before finalizer discovery

            // Unreachable finalizable objects survive this collection, are queued exactly once,
            // and have their outgoing references traced before sweep. After their finalizer runs,
            // a later collection may reclaim them unless user code resurrected them.
            for (UInt32 i = 0U; i < _trackedHighWater; i++)
            {
                if ((i & 0xFFU) == 0U && CollectionAbortRequested()) return false;
                if (tables->States[i] != RecordLive || tables->Marks[i] != 0) continue;
                Byte finalizerState = tables->FinalizerStates[i];
                if (finalizerState == FinalizerPending || finalizerState == FinalizerPendingReregister)
                {
                    if (!MarkRecord(tables, i, 6UL, i, ref stackCount)) return false;
                    continue;
                }
                if (finalizerState != FinalizerRegistered) continue;
                NativeAotExceptionRuntime.TraceStage(0x135UL); // unreachable finalizable record discovered
                NativeAotExceptionRuntime.TraceValue(0x136UL, i);
                NativeAotExceptionRuntime.TraceValue(0x137UL, tables->Addresses[i]);
                if (!EnqueueFinalizerUnderHeapGate(tables, i)) return false;
                NativeAotExceptionRuntime.TraceStage(0x138UL); // finalizer queued
                NativeAotExceptionRuntime.TraceValue(0x139UL, _finalizerQueueCount);
                if (!MarkRecord(tables, i, 7UL, i, ref stackCount)) return false;
            }

            while (stackCount != 0U)
            {
                if ((stackCount & 0x3FU) == 0U && CollectionAbortRequested()) return false;
                UInt32 index = tables->MarkStack[--stackCount];
                UInt64 methodTable = tables->MethodTables[index];
                if (methodTable == 0UL) continue;
                UInt32 flags = *(UInt32*)(nuint)methodTable;
                if ((flags & HasPointersFlag) == 0U) continue;
                UInt64 address = tables->Addresses[index];
                UInt64 size = tables->Sizes[index];
                for (UInt64 offset = 8UL; offset + 8UL <= size; offset += 8UL)
                {
                    if ((offset & 0x7FFUL) == 0UL && CollectionAbortRequested()) return false;
                    UInt64 candidate = *(UInt64*)(nuint)(address + offset);
                    if (!MarkCandidate(tables, candidate, 5UL, address + offset, ref stackCount)) return false;
                }
            }

            UInt64 reclaimedBytes = 0UL;
            UInt64 reclaimedObjects = 0UL;
            for (UInt32 i = 0U; i < _trackedHighWater; i++)
            {
                if ((i & 0xFFU) == 0U && CollectionAbortRequested()) return false;
                if (tables->States[i] != RecordLive) continue;
                if (tables->Marks[i] != 0)
                {
                    tables->Marks[i] = 0;
                    continue;
                }

                // NativeAOT GC-static base objects have module lifetime. They must
                // never be recycled even if a transient metadata problem prevented
                // the normal root pass from setting their mark bit.
                if (IsRegisteredStaticBase(tables, tables->Addresses[i]))
                    continue;

                tables->States[i] = RecordFree;
                tables->MethodTables[i] = 0UL;
                tables->FinalizerStates[i] = FinalizerNone;
                reclaimedBytes += tables->Sizes[i];
                reclaimedObjects++;
            }

            _collectionCount++;
            _reclaimedBytes += reclaimedBytes;
            _reclaimedObjects += reclaimedObjects;
            _allocationsSinceCollection = 0U;
            _bytesSinceCollection = 0UL;
            _lastFailure = NativeAotGcFailure.None;
            return true;
        }
    }

    internal static Boolean TrackAllocationUnderHeapGate(void* address, UInt64 size, void* methodTable)
    {
        if (address == null || size == 0UL || methodTable == null) return false;

        fixed (GcTables* tables = &_tables)
        {
            UInt32 index = _trackedHighWater;
            if (index >= MaximumTrackedAllocations)
            {
                _lastFailure = NativeAotGcFailure.MetadataExhausted;
                return false;
            }

            tables->Addresses[index] = (UInt64)(nuint)address;
            tables->Sizes[index] = size;
            tables->MethodTables[index] = (UInt64)(nuint)methodTable;
            tables->States[index] = RecordLive;
            tables->Marks[index] = 0;
            UInt32 flags = *(UInt32*)methodTable;
            Boolean isFinalizable = (flags & HasFinalizerFlag) != 0U;
            tables->FinalizerStates[index] = isFinalizable ? FinalizerRegistered : FinalizerNone;
            if (isFinalizable)
            {
                NativeAotExceptionRuntime.TraceStage(0x130UL); // finalizable allocation registered
                NativeAotExceptionRuntime.TraceValue(0x131UL, index);
                NativeAotExceptionRuntime.TraceValue(0x132UL, (UInt64)(nuint)address);
                NativeAotExceptionRuntime.TraceValue(0x133UL, (UInt64)(nuint)methodTable);
                NativeAotExceptionRuntime.TraceValue(0x134UL, flags);
            }
            _trackedHighWater = index + 1U;
            return true;
        }
    }

    internal static Boolean TryReuseUnderHeapGate(UInt64 requestedSize, void* methodTable, out void* address)
    {
        address = null;
        fixed (GcTables* tables = &_tables)
        {
            UInt32 best = UInt32.MaxValue;
            UInt64 bestSize = UInt64.MaxValue;
            for (UInt32 i = 0U; i < _trackedHighWater; i++)
            {
                if (tables->States[i] != RecordFree) continue;
                UInt64 blockSize = tables->Sizes[i];
                if (blockSize < requestedSize || blockSize >= bestSize) continue;
                best = i;
                bestSize = blockSize;
            }

            if (best == UInt32.MaxValue) return false;

            UInt64 blockAddress = tables->Addresses[best];
            NativeAotRuntime.Zero((void*)(nuint)blockAddress, bestSize);
            tables->States[best] = RecordLive;
            tables->MethodTables[best] = (UInt64)(nuint)methodTable;
            tables->Marks[best] = 0;
            UInt32 flags = *(UInt32*)methodTable;
            tables->FinalizerStates[best] = (flags & HasFinalizerFlag) != 0U ? FinalizerRegistered : FinalizerNone;
            address = (void*)(nuint)blockAddress;
            _reusedObjects++;
            _reusedBytes += bestSize;
            return true;
        }
    }

    public static Boolean SuppressFinalize(Object value)
    {
        if (value == null) return false;
        UInt64 raw = System.Runtime.CompilerServices.Unsafe.As<Object, UInt64>(ref value);
        if (!NativeAotRuntime.EnterHeapGate()) return false;
        Boolean result = false;
        fixed (GcTables* tables = &_tables)
        {
            for (UInt32 i = 0U; i < _trackedHighWater; i++)
            {
                if (tables->States[i] != RecordLive || tables->Addresses[i] != raw) continue;
                tables->FinalizerStates[i] = FinalizerRan;
                result = true;
                break;
            }
        }
        NativeAotRuntime.ExitHeapGate();
        return result;
    }

    public static Boolean ReRegisterForFinalize(Object value)
    {
        if (value == null) return false;
        UInt64 raw = System.Runtime.CompilerServices.Unsafe.As<Object, UInt64>(ref value);
        if (!NativeAotRuntime.EnterHeapGate()) return false;
        Boolean result = false;
        fixed (GcTables* tables = &_tables)
        {
            for (UInt32 i = 0U; i < _trackedHighWater; i++)
            {
                if (tables->States[i] != RecordLive || tables->Addresses[i] != raw) continue;
                UInt64 methodTable = tables->MethodTables[i];
                if (methodTable != 0UL && (*(UInt32*)(nuint)methodTable & HasFinalizerFlag) != 0U)
                {
                    Byte state = tables->FinalizerStates[i];
                    tables->FinalizerStates[i] = (state == FinalizerPending || state == FinalizerPendingReregister) ? FinalizerPendingReregister : FinalizerRegistered;
                    result = true;
                }
                break;
            }
        }
        NativeAotRuntime.ExitHeapGate();
        return result;
    }

    private static Boolean EnqueueFinalizerUnderHeapGate(GcTables* tables, UInt32 index)
    {
        if (_finalizerQueueCount >= MaximumTrackedAllocations)
        {
            _lastFailure = NativeAotGcFailure.FinalizerQueueExhausted;
            return false;
        }
        UInt32 tail = (_finalizerQueueHead + _finalizerQueueCount) % MaximumTrackedAllocations;
        tables->FinalizerQueue[tail] = index;
        tables->FinalizerStates[index] = FinalizerPending;
        _finalizerQueueCount++;
        return true;
    }

    /// <summary>Returns the exact allocation record whose finalizer is currently executing, or UInt32.MaxValue.</summary>
    public static UInt32 GetExecutingFinalizerRecord() => _executingFinalizerRecord;

    /// <summary>Runs every queued finalizer after stop-the-world has been released.</summary>
    public static UInt32 DrainFinalizerQueue()
    {
        NativeAotExceptionRuntime.TraceStage(0xF0UL); // drain enter
        NativeAotExceptionRuntime.TraceValue(0xF1UL, _finalizerQueueCount);
        UInt32 completed = 0U;
        for (;;)
        {
            UInt32 index;
            UInt64 address;
            UInt64 methodTable;
            Byte queuedState;
            NativeAotExceptionRuntime.TraceStage(0xF2UL); // acquire queue gate
            if (!NativeAotRuntime.EnterHeapGate())
            {
                NativeAotExceptionRuntime.TraceStage(0xFEUL); // gate failure
                break;
            }
            fixed (GcTables* tables = &_tables)
            {
                if (_finalizerQueueCount == 0U)
                {
                    NativeAotRuntime.ExitHeapGate();
                    NativeAotExceptionRuntime.TraceStage(0xF3UL); // queue empty
                    break;
                }
                index = tables->FinalizerQueue[_finalizerQueueHead];
                tables->FinalizerQueue[_finalizerQueueHead] = 0U;
                _finalizerQueueHead = (_finalizerQueueHead + 1U) % MaximumTrackedAllocations;
                _finalizerQueueCount--;
                queuedState = index < _trackedHighWater ? tables->FinalizerStates[index] : FinalizerNone;
                address = (queuedState == FinalizerPending || queuedState == FinalizerPendingReregister) && index < _trackedHighWater ? tables->Addresses[index] : 0UL;
                methodTable = address != 0UL ? tables->MethodTables[index] : 0UL;
            }
            NativeAotRuntime.ExitHeapGate();

            NativeAotExceptionRuntime.TraceStage(0xF4UL); // item dequeued
            NativeAotExceptionRuntime.TraceValue(0xF5UL, index);
            NativeAotExceptionRuntime.TraceValue(0xF6UL, queuedState);
            NativeAotExceptionRuntime.TraceValue(0xF7UL, address);
            NativeAotExceptionRuntime.TraceValue(0xF8UL, methodTable);

            if (address != 0UL && methodTable != 0UL)
            {
                NativeAotExceptionRuntime.TraceStage(0xF9UL); // invoke begin
                _executingFinalizerRecord = index;
                NativeAotExceptionRuntime.TraceValue(0x107UL, index);
                InvokeFinalizer(address, methodTable);
                _executingFinalizerRecord = UInt32.MaxValue;
                NativeAotExceptionRuntime.TraceStage(0xFAUL); // invoke returned
            }
            else
            {
                NativeAotExceptionRuntime.TraceStage(0xFBUL); // skipped invalid item
            }

            if (!NativeAotRuntime.EnterHeapGate())
            {
                NativeAotExceptionRuntime.TraceStage(0xFEUL);
                break;
            }
            fixed (GcTables* tables = &_tables)
            {
                if (index < _trackedHighWater && tables->States[index] == RecordLive && tables->Addresses[index] == address && address != 0UL)
                    tables->FinalizerStates[index] = queuedState == FinalizerPendingReregister ? FinalizerRegistered : FinalizerRan;
            }
            NativeAotRuntime.ExitHeapGate();
            NativeAotExceptionRuntime.TraceStage(0xFCUL); // state committed
            if (address != 0UL) { _finalizersRun++; completed++; }
        }
        NativeAotExceptionRuntime.TraceValue(0xFDUL, completed);
        return completed;
    }

    private static void InvokeFinalizer(UInt64 address, UInt64 methodTable)
    {
        UInt64 raw = address;
        Object instance = System.Runtime.CompilerServices.Unsafe.As<UInt64, Object>(ref raw);
        NativeAotExceptionRuntime.TraceValue(0x100UL, address);
        NativeAotExceptionRuntime.TraceValue(0x101UL, methodTable);

        // NativeAOT does NOT store the physical finalizer entry at MethodTable+24.
        // EETypeNode emits it as an optional field after TypeManager, writable-data,
        // and optional dispatch-map fields. CoreLib owns that ABI decoder because it
        // has the compiler-intrinsic SupportsRelativePointers contract.
        UInt64 entry = Inu.Runtime.RuntimeDiagnostics.GetFinalizerAddress(instance);
        NativeAotExceptionRuntime.TraceValue(0x102UL, entry);
        if (entry == 0UL)
        {
            NativeAotExceptionRuntime.TraceStage(0x103UL);
            return;
        }

        delegate*<Object, void> finalizer = (delegate*<Object, void>)(void*)(nuint)entry;
        NativeAotExceptionRuntime.TraceStage(0x104UL); // immediately before managed finalizer
        try
        {
            finalizer(instance);
            NativeAotExceptionRuntime.TraceStage(0x105UL); // normal return
        }
        catch (Exception)
        {
            NativeAotExceptionRuntime.TraceStage(0x106UL); // finalizer threw, queue continues
        }
    }

    /// <summary>Emits root-registry counts so GC-static startup coverage can be diagnosed without changing reachability.</summary>
    public static void TraceRootRegistrationState(UInt64 stage)
    {
        NativeAotExceptionRuntime.TraceStage(stage);
        NativeAotExceptionRuntime.TraceValue(stage + 1UL, _rootSlotCount);
        NativeAotExceptionRuntime.TraceValue(stage + 2UL, _staticBaseCount);
        NativeAotExceptionRuntime.TraceValue(stage + 3UL, _pinnedRootCount);
        NativeAotExceptionRuntime.TraceValue(stage + 4UL, _rootMapReady ? 1UL : 0UL);
    }

    /// <summary>Resolves a managed object to its current live GC allocation record without dereferencing object fields.</summary>
    public static Boolean TryGetLiveRecord(Object instance, out UInt32 recordIndex)
    {
        recordIndex = UInt32.MaxValue;
        if (instance == null) return false;
        Object local = instance;
        UInt64 address = System.Runtime.CompilerServices.Unsafe.As<Object, UInt64>(ref local);
        if (address == 0UL) return false;
        fixed (GcTables* tables = &_tables)
        {
            for (UInt32 i = 0U; i < _trackedHighWater; i++)
            {
                if (tables->States[i] == RecordLive && tables->Addresses[i] == address)
                {
                    recordIndex = i;
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Returns whether an allocation record is still live. Used by deterministic GC conformance probes.</summary>
    public static Boolean IsRecordLive(UInt32 recordIndex)
    {
        fixed (GcTables* tables = &_tables)
            return recordIndex < _trackedHighWater && tables->States[recordIndex] == RecordLive;
    }

    public static NativeAotGcStatistics GetStatistics()
    {
        UInt32 liveObjects = 0U;
        UInt64 liveBytes = 0UL;

        fixed (GcTables* tables = &_tables)
        {
            for (UInt32 i = 0U; i < _trackedHighWater; i++)
            {
                if (tables->States[i] != RecordLive) continue;
                liveObjects++;
                liveBytes += tables->Sizes[i];
            }
        }

        return new NativeAotGcStatistics(
            _collectionCount,
            _reclaimedObjects,
            _reclaimedBytes,
            _reusedObjects,
            _reusedBytes,
            _writeBarrierStores,
            liveObjects,
            liveBytes,
            _finalizerQueueCount,
            _finalizersRun,
            _pressureCollections,
            _allocationsSinceCollection,
            _bytesSinceCollection,
            _lastFailure);
    }

    /// <summary>Registers a write-barrier destination as a precise image-lifetime
    /// GC root when the destination is a slot inside the loaded NativeAOT PE image.
    /// Heap/object-field destinations never reach this path: the x64 leaf barrier
    /// fast-path calls InuGcReferenceWrite only for low-address destinations.
    /// This avoids treating the collector's own address tables as roots, which was
    /// the flaw in 0.0.75's whole-writable-image conservative scan.</summary>
    public static Boolean RegisterImageStaticWrite(void* destination)
    {
        if (destination == null) return true;
        UInt64 imageBase = NativeAotRuntime.RegisteredOsModuleBase;
        UInt64 slot = (UInt64)(nuint)destination;
        if (imageBase == 0UL || slot < imageBase) return true;

        Byte* image = (Byte*)(nuint)imageBase;
        if (*(UInt16*)image != 0x5A4DU) return true; // MZ
        UInt32 ntOffset = *(UInt32*)(image + 0x3CUL);
        if (ntOffset < 0x40U || ntOffset > 16U * 1024U * 1024U) return true;
        Byte* nt = image + ntOffset;
        if (*(UInt32*)nt != 0x00004550U) return true; // PE\0\0
        UInt16 optionalHeaderSize = *(UInt16*)(nt + 20);
        if (optionalHeaderSize < 64U || optionalHeaderSize > 4096U) return true;
        Byte* optional = nt + 24;
        UInt16 magic = *(UInt16*)optional;
        if (magic != 0x20BU && magic != 0x10BU) return true;
        UInt32 sizeOfImage = *(UInt32*)(optional + 56);
        if (sizeOfImage < 4096U || sizeOfImage > 1024U * 1024U * 1024U) return true;
        UInt64 imageEnd = imageBase + sizeOfImage;
        if (imageEnd < imageBase || slot > imageEnd - (UInt64)sizeof(void*)) return true;

        return RegisterStaticRoot((void**)destination);
    }

    private static Boolean IsRegisteredStaticBase(GcTables* tables, UInt64 address)
    {
        for (UInt32 i = 0U; i < _staticBaseCount; i++)
            if (tables->StaticBases[i] == address) return true;
        return false;
    }

    private static Boolean MarkRegisteredStaticBase(GcTables* tables, UInt64 staticBase, ref UInt32 stackCount)
    {
        // The registry itself is the lifetime authority for NativeAOT GC-static bases.
        // Mark the base allocation when present in the managed allocation table.
        if (!MarkCandidate(tables, staticBase, 1UL, staticBase, ref stackCount)) return false;

        // Scan the base directly as module-owned static storage. This deliberately does
        // not depend on finding the base in the allocation table: the compiler accesses
        // GC statics through this object and every pointer-sized payload word is therefore
        // a conservative root in Inu's non-moving collector.
        UInt64 methodTable = *(UInt64*)(nuint)staticBase;
        if (methodTable == 0UL) return true;
        UInt32 baseSize = *(UInt32*)(nuint)(methodTable + 4UL);
        if (baseSize < 16U || baseSize > 1024U * 1024U) return true;

        for (UInt64 offset = 8UL; offset + 8UL <= (UInt64)baseSize; offset += 8UL)
        {
            if ((offset & 0x7FFUL) == 0UL && CollectionAbortRequested()) return false;
            UInt64 nested = *(UInt64*)(nuint)(staticBase + offset);
            if (!MarkCandidate(tables, nested, 1UL, staticBase + offset, ref stackCount)) return false;
        }
        return true;
    }

    private static Boolean MarkStaticRootCandidate(GcTables* tables, UInt64 candidate, UInt64 sourceAddress, ref UInt32 stackCount)
    {
        if (candidate == 0UL) return true;
        UInt32 i = FindContainingRecord(tables, candidate);
        if (i == UInt32.MaxValue) return true;
        if (!MarkRecord(tables, i, 1UL, sourceAddress, ref stackCount)) return false;

        // A registered NativeAOT GC-static cell points at a synthetic static-base
        // object. Scan its payload conservatively regardless of HasPointers so every
        // managed static stored in that base becomes an exact root of its referent.
        UInt64 start = tables->Addresses[i];
        UInt64 size = tables->Sizes[i];
        for (UInt64 offset = 8UL; offset + 8UL <= size; offset += 8UL)
        {
            if ((offset & 0x7FFUL) == 0UL && CollectionAbortRequested()) return false;
            UInt64 nested = *(UInt64*)(nuint)(start + offset);
            if (!MarkCandidate(tables, nested, 1UL, start + offset, ref stackCount)) return false;
        }
        return true;
    }

    private static Boolean MarkCandidate(GcTables* tables, UInt64 candidate, UInt64 sourceKind, UInt64 sourceAddress, ref UInt32 stackCount)
    {
        if (candidate == 0UL) return true;
        UInt32 index = FindContainingRecord(tables, candidate);
        return index == UInt32.MaxValue || MarkRecord(tables, index, sourceKind, sourceAddress, ref stackCount);
    }

    /// <summary>Builds a collection-local ascending index of live allocation records by start address.</summary>
    private static Boolean BuildAddressOrder(GcTables* tables)
    {
        UInt32 count = 0U;
        for (UInt32 i = 0U; i < _trackedHighWater; i++)
        {
            if ((i & 0xFFU) == 0U && CollectionAbortRequested()) return false;
            if (tables->States[i] == RecordLive) tables->AddressOrder[count++] = i;
        }

        // In-place heapsort: bounded memory, no managed allocation, no recursion.
        if (count > 1U)
        {
            UInt32 root = count / 2U;
            while (root != 0U) { root--; SiftAddressOrderDown(tables, root, count); }
            UInt32 end = count;
            while (end > 1U)
            {
                end--;
                UInt32 temporary = tables->AddressOrder[0];
                tables->AddressOrder[0] = tables->AddressOrder[end];
                tables->AddressOrder[end] = temporary;
                SiftAddressOrderDown(tables, 0U, end);
                if ((end & 0xFFU) == 0U && CollectionAbortRequested()) return false;
            }
        }
        _addressOrderCount = count;
        return true;
    }

    private static void SiftAddressOrderDown(GcTables* tables, UInt32 root, UInt32 count)
    {
        for (;;)
        {
            UInt32 child = root * 2U + 1U;
            if (child >= count) return;
            UInt32 candidate = root;
            if (AddressOrderGreater(tables, child, candidate)) candidate = child;
            UInt32 right = child + 1U;
            if (right < count && AddressOrderGreater(tables, right, candidate)) candidate = right;
            if (candidate == root) return;
            UInt32 temporary = tables->AddressOrder[root];
            tables->AddressOrder[root] = tables->AddressOrder[candidate];
            tables->AddressOrder[candidate] = temporary;
            root = candidate;
        }
    }

    private static Boolean AddressOrderGreater(GcTables* tables, UInt32 leftPosition, UInt32 rightPosition)
    {
        UInt32 left = tables->AddressOrder[leftPosition], right = tables->AddressOrder[rightPosition];
        UInt64 leftAddress = tables->Addresses[left], rightAddress = tables->Addresses[right];
        return leftAddress > rightAddress || (leftAddress == rightAddress && left > right);
    }

    /// <summary>Finds the unique live allocation containing a conservative/interior pointer.</summary>
    private static UInt32 FindContainingRecord(GcTables* tables, UInt64 candidate)
    {
        UInt32 count = _addressOrderCount;
        if (count == 0U) return UInt32.MaxValue;
        UInt32 low = 0U, high = count;
        while (low < high)
        {
            UInt32 middle = low + (high - low) / 2U;
            UInt32 index = tables->AddressOrder[middle];
            if (tables->Addresses[index] <= candidate) low = middle + 1U;
            else high = middle;
        }
        if (low == 0U) return UInt32.MaxValue;
        UInt32 result = tables->AddressOrder[low - 1U];
        UInt64 start = tables->Addresses[result], size = tables->Sizes[result];
        return candidate >= start && candidate - start < size ? result : UInt32.MaxValue;
    }

    private static Boolean MarkRecord(GcTables* tables, UInt32 index, UInt64 sourceKind, UInt64 sourceAddress, ref UInt32 stackCount)
    {
        if (tables->Marks[index] != 0) return true;
        if (tables->FinalizerStates[index] != FinalizerNone)
        {
            NativeAotExceptionRuntime.TraceStage(0x13AUL); // first mark of a finalizable record
            NativeAotExceptionRuntime.TraceValue(0x13BUL, index);
            NativeAotExceptionRuntime.TraceValue(0x13CUL, tables->Addresses[index]);
            NativeAotExceptionRuntime.TraceValue(0x13DUL, sourceKind);
            NativeAotExceptionRuntime.TraceValue(0x13EUL, sourceAddress);
            NativeAotExceptionRuntime.TraceValue(0x13FUL, tables->FinalizerStates[index]);
        }
        if (stackCount >= MaximumTrackedAllocations)
        {
            _lastFailure = NativeAotGcFailure.MarkStackExhausted;
            return false;
        }
        tables->Marks[index] = 1;
        tables->MarkStack[stackCount++] = index;
        return true;
    }

    private static void TraceFinalizerRecords(GcTables* tables, UInt64 stage)
    {
        NativeAotExceptionRuntime.TraceStage(stage);
        for (UInt32 i = 0U; i < _trackedHighWater; i++)
        {
            Byte finalizerState = tables->FinalizerStates[i];
            if (tables->States[i] != RecordLive || finalizerState == FinalizerNone) continue;
            NativeAotExceptionRuntime.TraceValue(0x147UL, i);
            NativeAotExceptionRuntime.TraceValue(0x148UL, finalizerState);
            NativeAotExceptionRuntime.TraceValue(0x149UL, tables->Marks[i]);
            NativeAotExceptionRuntime.TraceValue(0x14AUL, tables->Addresses[i]);
            NativeAotExceptionRuntime.TraceValue(0x14BUL, tables->MethodTables[i]);
        }
    }
}
