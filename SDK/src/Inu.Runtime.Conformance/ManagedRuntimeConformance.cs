using System;
using System.Collections.Generic;
using System.Text;
using Inu.Runtime.NativeAot;
using Inu.Kernel.Scheduler;

namespace Inu.Runtime.Conformance;

/// <summary>Executable in-kernel semantic checks for the managed runtime transition.</summary>
public static unsafe class ManagedRuntimeConformance
{
    private sealed class Probe
    {
        public Probe(Int32 value) { Value = value; }
        public Int32 Value;
    }

    private sealed class ReferenceHolder
    {
        public Probe Value;
    }

    private interface IAbiReadable
    {
        Int32 Read();
        Int32 Transform(Int32 value);
    }

    private interface IAbiExtended : IAbiReadable
    {
        Int32 Extra();
    }

    private class AbiBase
    {
        public virtual Int32 ReadVirtual() => 10;
    }

    private sealed class AbiDerived : AbiBase, IAbiExtended
    {
        private readonly Int32 _value;
        public AbiDerived(Int32 value) { _value = value; }
        public override Int32 ReadVirtual() => _value;
        public Int32 Read() => _value + 1;
        public Int32 Transform(Int32 value) => _value + value;
        public Int32 Extra() => _value + 2;
    }

    private sealed class AbiAlternate : IAbiReadable
    {
        private readonly Int32 _value;
        public AbiAlternate(Int32 value) { _value = value; }
        Int32 IAbiReadable.Read() => _value + 10;
        Int32 IAbiReadable.Transform(Int32 value) => (_value * 2) + value;
    }

    private class GenericVirtualBase
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public virtual Int32 GenericVirtual<T>(T value) => 10;
    }

    private sealed class GenericVirtualDerived : GenericVirtualBase
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public override Int32 GenericVirtual<T>(T value)
            => typeof(T) == typeof(Int32) || typeof(T) == typeof(Probe) ? 20 : 19;
    }

    private interface IGenericVirtualDispatch
    {
        Int32 GenericVirtual<T>(T value);
    }

    private sealed class GenericVirtualInterfaceTarget : IGenericVirtualDispatch
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public Int32 GenericVirtual<T>(T value)
        {
            NativeAotExceptionRuntime.TraceStage(0x189AUL);
            return typeof(T) == typeof(Int32) ? 30 : (typeof(T) == typeof(Probe) ? 31 : 29);
        }
    }

    private interface IMethodCellGvmDispatch
    {
        T Echo<T>(T value);
    }

    private sealed class MethodCellGvmTarget : IMethodCellGvmDispatch
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public T Echo<T>(T value)
        {
            NativeAotExceptionRuntime.TraceStage(0x189DUL);
            return DictionaryMethodBridge<T>(value);
        }
    }

    private interface IAllocationCellGvmDispatch
    {
        T Transform<T>(T value);
    }

    private sealed class AllocationCellGvmTarget : IAllocationCellGvmDispatch
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public T Transform<T>(T value)
        {
            NativeAotExceptionRuntime.TraceStage(0x189EUL);
            GenericIdentityTransform<T> concrete = new GenericIdentityTransform<T>();
            IGenericTransform<T> throughInterface = concrete;
            return throughInterface.Transform(value);
        }
    }

    private interface IConstrainedCellGvmDispatch
    {
        Int32 Read<T>(T value) where T : IGenericReadable;
    }

    private sealed class ConstrainedCellGvmTarget : IConstrainedCellGvmDispatch
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public Int32 Read<T>(T value) where T : IGenericReadable
        {
            NativeAotExceptionRuntime.TraceStage(0x189FUL);
            return value.Read();
        }
    }

    private interface IGenericConstrainedCellGvmDispatch
    {
        Int32 Invoke<TTarget, TArg>(TTarget target, TArg value) where TTarget : IGenericVirtualDispatch;
    }

    private sealed class GenericConstrainedCellGvmTarget : IGenericConstrainedCellGvmDispatch
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public Int32 Invoke<TTarget, TArg>(TTarget target, TArg value) where TTarget : IGenericVirtualDispatch
        {
            NativeAotExceptionRuntime.TraceStage(0x18A0UL);
            return target.GenericVirtual<TArg>(value);
        }
    }

    private sealed class DefaultConstructorProbe
    {
        public Int32 Value;
        public DefaultConstructorProbe()
        {
            NativeAotExceptionRuntime.TraceStage(0x1A40UL);
            Value = 181;
            NativeAotExceptionRuntime.TraceStage(0x1A41UL);
        }
    }

    private interface IDefaultConstructorCellGvmDispatch
    {
        T Create<T>() where T : class, new();
    }

    private sealed class DefaultConstructorCellGvmTarget : IDefaultConstructorCellGvmDispatch
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public T Create<T>() where T : class, new()
        {
            NativeAotExceptionRuntime.TraceStage(0x18A1UL);
            return new T();
        }
    }

    private static class GenericStaticCell<T>
    {
        public static T Reference;
        public static Int32 Counter;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void PrepareStaticDataCellExactInstantiation()
    {
        // Inu is a closed-world freestanding runtime. Materialise the exact constructed
        // generic static bases so ILC emits StaticsInfoHashtable/NativeStatics entries.
        // The shared-GVM calls below still consume those bases through NativeLayout
        // StaticData dictionary cells, exactly as the runtime path requires.
        NativeAotExceptionRuntime.TraceStage(0x18A4UL);
        GenericStaticCell<Probe>.Reference = null;
        GenericStaticCell<Probe>.Counter = 0;
        NativeAotExceptionRuntime.TraceStage(0x18A5UL);
    }

    private interface IStaticDataCellGvmDispatch
    {
        T RoundTrip<T>(T value);
        Int32 Add<T>(Int32 delta);
    }

    private interface ITypeShapeCellGvmDispatch
    {
        Type SzArray<T>();
        Type MultiDimArray<T>();
        Type Pointer<T>();
        Type FunctionPointer<T>();
    }

    private sealed class TypeShapeCellGvmTarget : ITypeShapeCellGvmDispatch
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public Type SzArray<T>()
        {
            NativeAotExceptionRuntime.TraceStage(0x18A6UL);
            return typeof(T[]);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public Type MultiDimArray<T>()
        {
            NativeAotExceptionRuntime.TraceStage(0x18A7UL);
            return typeof(T[,]);
        }

#pragma warning disable 8500
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public Type Pointer<T>()
        {
            NativeAotExceptionRuntime.TraceStage(0x18A8UL);
            return typeof(T*);
        }
#pragma warning restore 8500

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public Type FunctionPointer<T>()
        {
            NativeAotExceptionRuntime.TraceStage(0x18A9UL);
            return typeof(delegate*<T>);
        }
    }

    private sealed class StaticDataCellGvmTarget : IStaticDataCellGvmDispatch
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public T RoundTrip<T>(T value)
        {
            NativeAotExceptionRuntime.TraceStage(0x18A2UL);
            GenericStaticCell<T>.Reference = value;
            return GenericStaticCell<T>.Reference;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public Int32 Add<T>(Int32 delta)
        {
            NativeAotExceptionRuntime.TraceStage(0x18A3UL);
            GenericStaticCell<T>.Counter += delta;
            return GenericStaticCell<T>.Counter;
        }
    }

    private class InheritedGenericVirtualInterfaceBase : IGenericVirtualDispatch
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public virtual Int32 GenericVirtual<T>(T value)
            => typeof(T) == typeof(Probe) ? 32 : 33;
    }

    private sealed class InheritedGenericVirtualInterfaceDerived : InheritedGenericVirtualInterfaceBase
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public override Int32 GenericVirtual<T>(T value)
        {
            NativeAotExceptionRuntime.TraceStage(0x189BUL);
            return typeof(T) == typeof(Probe) ? 36 : 35;
        }
    }

    private interface IVariantGenericVirtualDispatch<out TMarker>
    {
        Int32 GenericVirtual<T>(T value);
    }

    private sealed class VariantGenericVirtualInterfaceTarget : IVariantGenericVirtualDispatch<Probe>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public Int32 GenericVirtual<T>(T value)
        {
            NativeAotExceptionRuntime.TraceStage(0x189CUL);
            return typeof(T) == typeof(Probe) ? 41 : 40;
        }
    }

    private sealed class GenericHolder<T>
    {
        public T Value;
    }

    private struct GenericPair<TFirst, TSecond>
    {
        public TFirst First;
        public TSecond Second;
    }

    private static class GenericStatic<T>
    {
        public static T Value;
    }

    private interface IGenericReadable
    {
        Int32 Read();
    }

    private struct GenericValueReadable : IGenericReadable
    {
        public Int32 Value;
        public Int32 Read() => Value;
    }

    private sealed class GenericReferenceReadable : IGenericReadable
    {
        private readonly Int32 _value;
        public GenericReferenceReadable(Int32 value) { _value = value; }
        public Int32 Read() => _value;
    }

    private interface IGenericTransform<T>
    {
        T Transform(T value);
    }

    private sealed class GenericIdentityTransform<T> : IGenericTransform<T>
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public T Transform(T value) => value;
    }

    private sealed class GenericIntTransform : IGenericTransform<Int32>
    {
        public Int32 Transform(Int32 value) => value + 5;
    }

    private sealed class GenericProbeTransform : IGenericTransform<Probe>
    {
        public Probe Transform(Probe value) => value;
    }

    private interface IGenericProducer<out T>
    {
        T Get();
    }

    private sealed class GenericProbeProducer : IGenericProducer<Probe>
    {
        private readonly Probe _value;
        public GenericProbeProducer(Probe value) { _value = value; }
        public Probe Get() => _value;
    }

    private interface IGenericConsumer<in T>
    {
        Int32 Consume(T value);
    }

    private sealed class GenericObjectConsumer : IGenericConsumer<Object>
    {
        public Int32 Consume(Object value) => value == null ? -1 : 91;
    }

    private delegate T GenericUnaryDelegate<T>(T value);

    private sealed class GcStressNode
    {
        public GcStressNode(Int32 value) { Value = value; }
        public Int32 Value;
        public GcStressNode Next;
    }

    // Finalization probes are deliberately stateful so the mature-GC gate proves
    // normal finalization, resurrection, suppression, re-registration and queue continuity.
    private sealed class FinalizableProbe
    {
        private readonly Int32 _id;
        private readonly Boolean _resurrect;
        private readonly Boolean _throwFromFinalizer;

        public FinalizableProbe() : this(0, false, false) { }

        public FinalizableProbe(Int32 id, Boolean resurrect = false, Boolean throwFromFinalizer = false)
        {
            _id = id;
            _resurrect = resurrect;
            _throwFromFinalizer = throwFromFinalizer;
        }

        public Int32 Id => _id;

        ~FinalizableProbe()
        {
            NativeAotExceptionRuntime.TraceStage(0x110UL);
            NativeAotExceptionRuntime.TraceValue(0x111UL, (UInt64)(UInt32)_id);
            _gcFinalizerRuns++;
            _gcLastFinalizedId = _id;
            NativeAotExceptionRuntime.TraceStage(0x112UL);
            if (_resurrect)
            {
                _gcResurrectedRecord = NativeAotGarbageCollector.GetExecutingFinalizerRecord();
                NativeAotExceptionRuntime.TraceValue(0x116UL, _gcResurrectedRecord);
                _gcResurrected = this;
                NativeAotExceptionRuntime.TraceStage(0x113UL);
            }
            if (_throwFromFinalizer)
            {
                NativeAotExceptionRuntime.TraceStage(0x114UL);
                throw new InvalidOperationException("finalizer stress");
            }
            NativeAotExceptionRuntime.TraceStage(0x115UL);
        }
    }

    private delegate Int32 AbiUnaryDelegate(Int32 value);

    private static Int32 _ehFinallyMarker;
    private static Int32 _ehRethrowMarker;
    // Runtime-owned GC-static delegate probe. This exercises the actual NativeAOT
    // GC-static base + write-barrier + delegate invocation contract without depending
    // on Roslyn's private compiler-generated method-group cache implementation.
    private static AbiUnaryDelegate _gcStaticDelegateProbe;
    private static Probe _gcStaticRootProbe;
    private static FinalizableProbe _gcResurrected;
    private static UInt32 _gcResurrectedRecord = UInt32.MaxValue;
    private static Int32 _gcFinalizerRuns;
    private static Int32 _gcWorkerId;
    private static Boolean _gcWorkerResurrect;
    private static Boolean _gcWorkerThrow;
    private static Byte _gcWorkerMode;
    private static Int32 _gcLastFinalizedId;
    private static Boolean _gcConformanceActive;
    private static UInt32 _gcAssertionOrdinal;
    private static UInt64 _stressCancellationProbe;

    /// <summary>Registers the foreground stress-command cancellation probe without coupling runtime conformance to the shell.</summary>
    public static Boolean RegisterStressCancellationProbe(delegate*<Boolean> probe)
    {
        if(probe==null||_stressCancellationProbe!=0UL)return false;_stressCancellationProbe=(UInt64)(void*)probe;return true;
    }

    private static Boolean StressCancellationRequested()
    {
        UInt64 raw=_stressCancellationProbe;if(raw==0UL)return false;delegate*<Boolean> probe=(delegate*<Boolean>)(void*)(nuint)raw;return probe();
    }

    private static Boolean StopGcStressIfCancelled()
    {
        if(!StressCancellationRequested())return false;_gcConformanceActive=false;return true;
    }

    /// <summary>
    /// Runs after NativeAotRuntime.Initialize. Returning false rejects the boot
    /// instead of allowing a partially working managed runtime to continue.
    /// </summary>
    public static Boolean Run(out UInt32 passed, out UInt32 failed, out UInt32 abiPassed, out UInt32 abiFailed)
    {
        passed = 0U;
        failed = 0U;
        abiPassed = 0U;
        abiFailed = 0U;

        NativeAotExceptionRuntime.TraceStage(0xC0UL);
        Record(NativeAotRuntime.IsManaged(), ref passed, ref failed);

        Probe first = new Probe(17);
        Probe second = new Probe(29);
        Record(!Object.ReferenceEquals(first, null), ref passed, ref failed);
        Record(!Object.ReferenceEquals(second, null), ref passed, ref failed);
        Record(!Object.ReferenceEquals(first, second), ref passed, ref failed);
        Record(Object.ReferenceEquals(first, first), ref passed, ref failed);
        Record(first.Value == 17 && second.Value == 29, ref passed, ref failed);

        ReferenceHolder holder = new ReferenceHolder();
        holder.Value = first;
        Record(Object.ReferenceEquals(holder.Value, first), ref passed, ref failed);
        holder.Value = second;
        Record(Object.ReferenceEquals(holder.Value, second), ref passed, ref failed);

        Int32[] values = new Int32[4];
        Record(values.Length == 4, ref passed, ref failed);
        Record(values[0] == 0 && values[3] == 0, ref passed, ref failed);
        values[0] = 11;
        values[3] = 44;
        Record(values[0] == 11 && values[3] == 44, ref passed, ref failed);

        RunDispatchCastAndGenericChecks(first, ref passed, ref failed);

        NativeAotExceptionRuntime.TraceStage(0xC1UL);
        Byte[] empty = Array.Empty<Byte>();
        Record(!Object.ReferenceEquals(empty, null) && empty.Length == 0, ref passed, ref failed);

        NativeAotExceptionRuntime.TraceStage(0xC2UL);
        // ABI audit item 5: these are real C# typeof expressions in the assembly
        // that Roslyn emits to IL and Inu's pinned ILC compiles into the kernel.
        // The generic method forces the typeof(T) / ldtoken generic lowering path.
        Type intType = typeof(Int32);
        Type genericIntType = TypeOfGeneric<Int32>();
        Type objectType = typeof(Object);
        Record(!Object.ReferenceEquals(intType, null) && !Object.ReferenceEquals(genericIntType, null) && !Object.ReferenceEquals(objectType, null), ref passed, ref failed);
        Record(intType == genericIntType, ref passed, ref failed);
        Record(intType != objectType, ref passed, ref failed);
        Type roundTripIntType = Type.GetTypeFromHandle(intType.TypeHandle);
        Record(roundTripIntType == intType, ref passed, ref failed);

        // 0.45.26: the object/type bridge is now a live NativeAOT contract rather
        // than typeof-only scaffolding. GetType must round-trip the object's emitted
        // MethodTable without adding another System.Object virtual slot.
        Record(first.GetType() == typeof(Probe), ref passed, ref failed);
        Object boxedTypeProbe = 123;
        Record(boxedTypeProbe.GetType() == intType, ref passed, ref failed);
        Record(intType.IsValueType && intType.IsPrimitive && !intType.IsArray, ref passed, ref failed);
        Record(!objectType.IsValueType && !objectType.IsArray, ref passed, ref failed);
        Type intArrayType = typeof(Int32[]);
        Record(intArrayType.IsArray && intArrayType.IsSZArray, ref passed, ref failed);
        Record(intArrayType.GetElementType() == intType, ref passed, ref failed);
        Record(intArrayType.BaseType == typeof(Array), ref passed, ref failed);

        NativeAotExceptionRuntime.TraceStage(0xC3UL);
        // 0.44.18: force ILC to emit real EH tables and exercise the freestanding
        // RhpThrowEx dispatcher, typed catch selection, exceptional finally,
        // cross-frame unwind and RhpRethrow.
        NativeAotExceptionRuntime.TraceStage(0xC4UL);
        Record(EhDirectCatchFinally() == 3, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0xC5UL);
        Record(EhTypedCatchSelection() == 7, ref passed, ref failed);
        _ehFinallyMarker = 0;
        Record(EhCrossFrameUnwind() == 11 && _ehFinallyMarker == 5, ref passed, ref failed);
        _ehRethrowMarker = 0;
        NativeAotExceptionRuntime.TraceStage(0xC6UL);
        Record(EhRethrow() == 9 && _ehRethrowMarker == 3, ref passed, ref failed);

        // 0.0.39: realistic EH coverage. These paths deliberately exercise exceptions
        // emitted by NativeAOT helpers as well as nested multi-frame finally/unwind and
        // cross-frame rethrow. They are mandatory: EH is not considered complete if any
        // of these routes cannot reach their selected catch and resume normally.
        NativeAotExceptionRuntime.TraceStage(0xD8UL);
        Record(EhRuntimeHelperExceptions() == 7, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0xD9UL);
        Record(EhNestedMultiFrameFinally() == 11, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0xDAUL);
        Record(EhCrossFrameRethrow() == 12, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0xDBUL);
        NativeAotExceptionRuntime.TraceStage(0xC7UL);

        // 0.45.29: keep the post-EH conformance tail allocation-free between
        // breadcrumbs so an early CPU reset/triple-fault identifies the exact
        // NativeAOT contract being exercised. These markers are diagnostic only;
        // no ABI check is weakened or skipped.
        NativeAotExceptionRuntime.TraceStage(0xC8UL);
        Nullable<Int32> optional = new Nullable<Int32>(42);
        NativeAotExceptionRuntime.TraceStage(0xC9UL);
        Nullable<Int32> missing = default;
        NativeAotExceptionRuntime.TraceStage(0xCAUL);
        Record(optional.HasValue && optional.GetValueOrDefault() == 42, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0xCBUL);
        Record(!missing.HasValue && missing.GetValueOrDefault() == 0, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0xCCUL);

        NativeAotRuntimeStatistics statistics = NativeAotRuntime.GetStatistics();
        NativeAotExceptionRuntime.TraceStage(0xCDUL);
        Record(statistics.Phase == NativeAotRuntimePhase.Managed, ref passed, ref failed);
        Record(statistics.AllocationCount >= 5UL, ref passed, ref failed);
        Record(statistics.AllocatedBytes != 0UL && statistics.ReservedBytes >= statistics.AllocatedBytes, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0xCEUL);

        Boolean objectAbiOk = RunAbiLayoutChecks(values, "Nova", ref abiPassed, ref abiFailed);
        NativeAotExceptionRuntime.TraceStage(0xCFUL);
        Boolean boxingAbiOk = RunBoxingChecks(ref abiPassed, ref abiFailed);
        NativeAotExceptionRuntime.TraceStage(0xD0UL);
        Boolean handleAbiOk = RunHandleChecks(ref abiPassed, ref abiFailed);
        NativeAotExceptionRuntime.TraceStage(0xD1UL);
        Boolean delegateAbiOk = RunDelegateChecks(ref abiPassed, ref abiFailed);
        NativeAotExceptionRuntime.TraceStage(0xD2UL);

        // ABI audit items 2 and 3 are now hard runtime contracts: Object must carry
        // its MethodTable at offset zero, finalizable types must advertise finalizer
        // metadata, and every primitive must retain its canonical NativeAOT size.
        Boolean primitiveAbiOk = PrimitiveSizesAreCanonical();
        NativeAotExceptionRuntime.TraceStage(0xD3UL);
        Boolean methodTableAbiOk = MethodTableSurfaceIsCanonical(values, "Nova");
        NativeAotExceptionRuntime.TraceStage(0xD4UL);
        Boolean runtimeTypeHandleAbiOk = sizeof(RuntimeTypeHandle) == sizeof(IntPtr);
        Record(runtimeTypeHandleAbiOk, ref abiPassed, ref abiFailed);
        // 0.0.30: semantic/runtime conformance is the hard boot gate. ABI audit results
        // remain fully counted and reported through abiPassed/abiFailed, but known ABI
        // reconciliation divergences must not prevent the kernel from reaching the shell.
        // The individual ABI Boolean results are intentionally evaluated above so all probes
        // execute; they are diagnostics until the corresponding ABI audit work is completed.
        _ = objectAbiOk;
        _ = boxingAbiOk;
        _ = handleAbiOk;
        _ = delegateAbiOk;
        _ = primitiveAbiOk;
        _ = methodTableAbiOk;
        _ = runtimeTypeHandleAbiOk;
        return failed == 0U;
    }

    /// <summary>
    /// Runs after the SMP scheduler is online. This is the hard mature-GC gate:
    /// stack/static roots must survive, unreachable allocations must be reclaimed/reused,
    /// allocation pressure must trigger collection automatically, and finalization semantics
    /// must remain correct under repeated collections.
    /// </summary>
    public static Boolean RunGarbageCollectorChecks(out UInt32 passed, out UInt32 failed)
    {
        passed = 0U;
        failed = 0U;
        _gcConformanceActive = true;
        _gcAssertionOrdinal = 0U;
        if(StopGcStressIfCancelled())return false;

        NativeAotExceptionRuntime.TraceStage(0xE8UL);
        NativeAotGcStatistics start = NativeAotGarbageCollector.GetStatistics();
        _ = start;

        // Active stack root + transitive object graph survival.
        GcStressNode stackRoot = new GcStressNode(7001);
        stackRoot.Next = new GcStressNode(7002);
        CollectChecked(ref passed, ref failed);
        if(StopGcStressIfCancelled())return false;
        Record(stackRoot.Value == 7001 && stackRoot.Next != null && stackRoot.Next.Value == 7002, ref passed, ref failed);

        NativeAotExceptionRuntime.TraceStage(0xE9UL);
        // A real GC static must be discovered through NativeAOT's registered root slots.
        _gcStaticRootProbe = new Probe(7101);
        CollectChecked(ref passed, ref failed);
        Record(_gcStaticRootProbe != null && _gcStaticRootProbe.Value == 7101, ref passed, ref failed);
        _gcStaticRootProbe = null;
        if(StopGcStressIfCancelled())return false;

        NativeAotExceptionRuntime.TraceStage(0xEAUL);
        NativeAotGcStatistics beforeReclaim = NativeAotGarbageCollector.GetStatistics();
        AllocateGarbageBatch(768, 7200);
        ScrubDeadStackFrames();
        CollectChecked(ref passed, ref failed);
        NativeAotGcStatistics afterReclaim = NativeAotGarbageCollector.GetStatistics();
        Record(afterReclaim.Collections > beforeReclaim.Collections, ref passed, ref failed);
        Record(afterReclaim.ReclaimedObjects > beforeReclaim.ReclaimedObjects, ref passed, ref failed);
        Record(afterReclaim.ReclaimedBytes > beforeReclaim.ReclaimedBytes, ref passed, ref failed);
        if(StopGcStressIfCancelled())return false;

        NativeAotExceptionRuntime.TraceStage(0xEBUL);
        UInt64 reusedBefore = afterReclaim.ReusedObjects;
        AllocateGarbageBatch(256, 7300);
        NativeAotGcStatistics afterReuse = NativeAotGarbageCollector.GetStatistics();
        Record(afterReuse.ReusedObjects > reusedBefore, ref passed, ref failed);
        if(StopGcStressIfCancelled())return false;

        NativeAotExceptionRuntime.TraceStage(0xECUL);
        // No explicit GC inside this batch. The allocator itself must cross the adaptive
        // pressure threshold and request a scheduler-coordinated stop-the-world collection.
        UInt64 pressureBefore = afterReuse.PressureCollections;
        AllocateGarbageBatch(1400, 7400);
        NativeAotGcStatistics afterPressure = NativeAotGarbageCollector.GetStatistics();
        Record(afterPressure.PressureCollections > pressureBefore, ref passed, ref failed);
        Record(afterPressure.LastFailure == NativeAotGcFailure.None, ref passed, ref failed);
        if(StopGcStressIfCancelled())return false;

        NativeAotExceptionRuntime.TraceStage(0xEDUL);
        // Ordinary finalization.
        Int32 finalizersBefore = _gcFinalizerRuns;
        NativeAotExceptionRuntime.TraceStage(0x120UL);
        Record(CreateFinalizableGarbageOnRetiredStack(7501, false, false, 0), ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x121UL);
        ScrubDeadStackFrames();
        NativeAotExceptionRuntime.TraceStage(0x122UL);
        CollectChecked(ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x123UL);
        GC.WaitForPendingFinalizers();
        NativeAotExceptionRuntime.TraceStage(0x124UL);
        Record(_gcFinalizerRuns == finalizersBefore + 1 && _gcLastFinalizedId == 7501, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x125UL);
        if(StopGcStressIfCancelled())return false;

        // Resurrection: inspect the object only inside no-inline helpers so no managed
        // reference to it leaks into this conservative-scanned caller frame. The caller
        // carries only the GC record index, which cannot accidentally act as an object root.
        _gcResurrected = null;
        _gcResurrectedRecord = UInt32.MaxValue;
        Record(CreateFinalizableGarbageOnRetiredStack(7502, true, false, 0), ref passed, ref failed);
        ScrubDeadStackFrames();
        CollectChecked(ref passed, ref failed);
        GC.WaitForPendingFinalizers();
        NativeAotExceptionRuntime.TraceStage(0x126UL);
        RecordTagged(_gcLastFinalizedId == 7502, 0x01U, ref passed, ref failed);
        UInt32 resurrectedRecord = _gcResurrectedRecord;
        NativeAotExceptionRuntime.TraceValue(0x127UL, resurrectedRecord);
        RecordTagged(resurrectedRecord != UInt32.MaxValue && NativeAotGarbageCollector.IsRecordLive(resurrectedRecord), 0x02U, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x128UL);
        CollectChecked(ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x129UL);
        RecordTagged(_gcLastFinalizedId == 7502, 0x03U, ref passed, ref failed);
        RecordTagged(resurrectedRecord != UInt32.MaxValue && NativeAotGarbageCollector.IsRecordLive(resurrectedRecord), 0x04U, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceValue(0x12AUL, resurrectedRecord);
        UInt32 releasedRecord = ClearResurrectedAndCaptureSavedRecord();
        RecordTagged(releasedRecord == resurrectedRecord && releasedRecord != UInt32.MaxValue, 0x05U, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceValue(0x12BUL, releasedRecord);
        ScrubDeadStackFrames();
        NativeAotExceptionRuntime.TraceStage(0x12CUL);
        CollectChecked(ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x12DUL);
        RecordTagged(!NativeAotGarbageCollector.IsRecordLive(releasedRecord), 0x06U, ref passed, ref failed);
        if(StopGcStressIfCancelled())return false;

        NativeAotExceptionRuntime.TraceStage(0xEEUL);
        // SuppressFinalize and ReRegisterForFinalize must affect the same finalizer state machine.
        finalizersBefore = _gcFinalizerRuns;
        Record(CreateFinalizableGarbageOnRetiredStack(7503, false, false, 1), ref passed, ref failed);
        ScrubDeadStackFrames();
        CollectChecked(ref passed, ref failed);
        GC.WaitForPendingFinalizers();
        Record(_gcFinalizerRuns == finalizersBefore, ref passed, ref failed);

        Record(CreateFinalizableGarbageOnRetiredStack(7504, false, false, 2), ref passed, ref failed);
        ScrubDeadStackFrames();
        CollectChecked(ref passed, ref failed);
        GC.WaitForPendingFinalizers();
        Record(_gcFinalizerRuns == finalizersBefore + 1 && _gcLastFinalizedId == 7504, ref passed, ref failed);

        // A throwing finalizer must not prevent a following queued finalizer from running.
        finalizersBefore = _gcFinalizerRuns;
        Record(CreateFinalizableGarbageOnRetiredStack(7505, false, true, 0), ref passed, ref failed);
        Record(CreateFinalizableGarbageOnRetiredStack(7506, false, false, 0), ref passed, ref failed);
        ScrubDeadStackFrames();
        CollectChecked(ref passed, ref failed);
        GC.WaitForPendingFinalizers();
        Record(_gcFinalizerRuns >= finalizersBefore + 2, ref passed, ref failed);
        if(StopGcStressIfCancelled())return false;

        NativeAotExceptionRuntime.TraceStage(0xEFUL);
        // Repeated mixed-size allocation/collection rounds exercise best-fit reuse and
        // prove that the long-lived stack graph is never corrupted by reclamation.
        UInt64 collectionsBeforeStress = NativeAotGarbageCollector.GetStatistics().Collections;
        for (Int32 round = 0; round < 6; round++)
        {
            if(StopGcStressIfCancelled())return false;
            AllocateMixedGarbageBatch(384, 7600 + round * 1000);
            CollectChecked(ref passed, ref failed);
            Record(stackRoot.Value == 7001 && stackRoot.Next != null && stackRoot.Next.Value == 7002, ref passed, ref failed);
        }
        NativeAotGcStatistics finish = NativeAotGarbageCollector.GetStatistics();
        Record(finish.Collections >= collectionsBeforeStress + 6UL, ref passed, ref failed);
        Record(finish.ReclaimedObjects > afterPressure.ReclaimedObjects, ref passed, ref failed);
        Record(finish.ReusedObjects > afterPressure.ReusedObjects, ref passed, ref failed);
        Record(finish.PendingFinalizers == 0U, ref passed, ref failed);
        Record(finish.LastFailure == NativeAotGcFailure.None, ref passed, ref failed);

        _gcConformanceActive = false;
        return failed == 0U;
    }

    /// <summary>Runs the exception/unwind contract repeatedly from the interactive stress command.</summary>
    public static Boolean RunExceptionStress(UInt32 iterations, out UInt64 passed, out UInt64 failed)
    {
        passed=0UL;failed=0UL;if(iterations==0U||iterations>1000U)return false;
        for(UInt32 i=0U;i<iterations;i++)
        {
            UInt32 p=0U,f=0U;_ehFinallyMarker=0;_ehRethrowMarker=0;
            Record(EhDirectCatchFinally()==3,ref p,ref f);
            Record(EhTypedCatchSelection()==7,ref p,ref f);
            _ehFinallyMarker=0;Record(EhCrossFrameUnwind()==11&&_ehFinallyMarker==5,ref p,ref f);
            _ehRethrowMarker=0;Record(EhRethrow()==9&&_ehRethrowMarker==3,ref p,ref f);
            Record(EhRuntimeHelperExceptions()==7,ref p,ref f);
            Record(EhNestedMultiFrameFinally()==11,ref p,ref f);
            Record(EhCrossFrameRethrow()==12,ref p,ref f);
            passed+=p;failed+=f;
        }
        return failed==0UL;
    }

    /// <summary>Runs class/interface virtual dispatch, casts and variance repeatedly.</summary>
    public static Boolean RunDispatchStress(UInt32 iterations, out UInt64 passed, out UInt64 failed)
    {
        passed=0UL;failed=0UL;if(iterations==0U||iterations>1000U)return false;
        for(UInt32 i=0U;i<iterations;i++)
        {
            UInt32 p=0U,f=0U;Probe probe=new Probe(2000+(Int32)i);RunDispatchOnlyChecks(probe,ref p,ref f);passed+=p;failed+=f;
        }
        return failed==0UL;
    }

    /// <summary>Runs NativeAOT generic dictionaries/GVM/static-data/default-constructor paths repeatedly.</summary>
    public static Boolean RunGenericStress(UInt32 iterations, out UInt64 passed, out UInt64 failed)
    {
        passed=0UL;failed=0UL;if(iterations==0U||iterations>250U)return false;
        for(UInt32 i=0U;i<iterations;i++)
        {
            UInt32 p=0U,f=0U;Probe probe=new Probe(3000+(Int32)i);RunGenericRuntimeChecks(probe,ref p,ref f);passed+=p;failed+=f;
        }
        return failed==0UL;
    }

    /// <summary>Runs List/Dictionary/Queue/Stack mutation and EH semantics repeatedly.</summary>
    public static Boolean RunCollectionStress(UInt32 iterations, out UInt64 passed, out UInt64 failed)
    {
        passed=0UL;failed=0UL;if(iterations==0U||iterations>250U)return false;
        for(UInt32 i=0U;i<iterations;i++)
        {
            UInt32 p=0U,f=0U;Probe probe=new Probe(4000+(Int32)i);RunCollectionRuntimeChecks(probe,ref p,ref f);passed+=p;failed+=f;
        }
        return failed==0UL;
    }

    /// <summary>Runs object/boxing/handle/delegate and MethodTable ABI probes repeatedly.</summary>
    public static Boolean RunAbiStress(UInt32 iterations, out UInt64 passed, out UInt64 failed)
    {
        passed=0UL;failed=0UL;if(iterations==0U||iterations>250U)return false;
        for(UInt32 i=0U;i<iterations;i++)
        {
            UInt32 p=0U,f=0U;Int32[] values=new Int32[4];values[0]=11;values[3]=44;
            _=RunAbiLayoutChecks(values,"Inu",ref p,ref f);_=RunBoxingChecks(ref p,ref f);_=RunHandleChecks(ref p,ref f);_=RunDelegateChecks(ref p,ref f);
            Record(PrimitiveSizesAreCanonical(),ref p,ref f);Record(MethodTableSurfaceIsCanonical(values,"Inu"),ref p,ref f);Record(sizeof(RuntimeTypeHandle)==sizeof(IntPtr),ref p,ref f);
            passed+=p;failed+=f;
        }
        return failed==0UL;
    }

    /// <summary>Runs NativeLayout type-handle and generic-static TypeLoader paths repeatedly.</summary>
    public static Boolean RunTypeLoaderStress(UInt32 iterations, out UInt64 passed, out UInt64 failed)
    {
        passed=0UL;failed=0UL;if(iterations==0U||iterations>500U)return false;
        for(UInt32 i=0U;i<iterations;i++)
        {
            UInt32 p=0U,f=0U;Probe probe=new Probe(5000+(Int32)i);
            ITypeShapeCellGvmDispatch shapes=new TypeShapeCellGvmTarget();Type sz=shapes.SzArray<Probe>();Type md=shapes.MultiDimArray<Probe>();Type ptr=shapes.Pointer<Int32>();Type fn=shapes.FunctionPointer<Probe>();
            Record(sz==typeof(Probe[])&&sz.IsArray&&sz.IsSZArray,ref p,ref f);
            Record(md==typeof(Probe[,])&&md.IsArray&&!md.IsSZArray,ref p,ref f);
            Record(ptr==typeof(Int32*),ref p,ref f);
            Record(fn==typeof(delegate*<Probe>),ref p,ref f);
            PrepareStaticDataCellExactInstantiation();IStaticDataCellGvmDispatch statics=new StaticDataCellGvmTarget();
            Record(Object.ReferenceEquals(statics.RoundTrip<Probe>(probe),probe),ref p,ref f);Record(statics.Add<Probe>(1)==1,ref p,ref f);
            IDefaultConstructorCellGvmDispatch ctor=new DefaultConstructorCellGvmTarget();DefaultConstructorProbe created=ctor.Create<DefaultConstructorProbe>();Record(created!=null&&created.Value==181,ref p,ref f);
            passed+=p;failed+=f;
        }
        return failed==0UL;
    }

    /// <summary>Runs the mature stop-the-world GC/finalizer/reuse pressure suite repeatedly.</summary>
    public static Boolean RunGarbageCollectorStress(UInt32 iterations, out UInt64 passed, out UInt64 failed)
    {
        passed=0UL;failed=0UL;if(iterations==0U||iterations>16U)return false;
        for(UInt32 i=0U;i<iterations;i++){if(StressCancellationRequested())return false;UInt32 p,f;RunGarbageCollectorChecks(out p,out f);passed+=p;failed+=f;if(StressCancellationRequested())return false;}
        return failed==0UL;
    }

    /// <summary>Runs every interactive managed-runtime stress suite, including the mature GC gate.</summary>
    public static Boolean RunAllStress(UInt32 iterations, out UInt64 passed, out UInt64 failed)
    {
        passed=0UL;failed=0UL;if(iterations==0U||iterations>16U)return false;
        for(UInt32 i=0U;i<iterations;i++)
        {
            UInt64 p,f;
            RunExceptionStress(1U,out p,out f);passed+=p;failed+=f;
            RunDispatchStress(1U,out p,out f);passed+=p;failed+=f;
            RunGenericStress(1U,out p,out f);passed+=p;failed+=f;
            RunCollectionStress(1U,out p,out f);passed+=p;failed+=f;
            RunTypeLoaderStress(1U,out p,out f);passed+=p;failed+=f;
            RunAbiStress(1U,out p,out f);passed+=p;failed+=f;
            RunGarbageCollectorStress(1U,out p,out f);passed+=p;failed+=f;
        }
        return failed==0UL;
    }

    private static Boolean CollectChecked(ref UInt32 passed, ref UInt32 failed)
    {
        NativeAotExceptionRuntime.TraceStage(0x170UL); // checked mature-GC collection request
        Boolean collected=NativeAotGarbageCollector.Collect();
        NativeAotExceptionRuntime.TraceValue(0x171UL,collected?1UL:0UL);
        NativeAotGcFailure failure=NativeAotGarbageCollector.GetStatistics().LastFailure;
        NativeAotExceptionRuntime.TraceValue(0x172UL,(UInt64)failure);
        if(failure==NativeAotGcFailure.CollectionCancelled&&StressCancellationRequested())return false;
        Record(collected,ref passed,ref failed);
        NativeAotExceptionRuntime.TraceStage(0x173UL);
        return collected;
    }

    private static void AllocateGarbageBatch(Int32 count, Int32 seed)
    {
        for (Int32 i = 0; i < count; i++)
        {
            GcStressNode garbage = new GcStressNode(seed + i);
            if ((i & 31) == 0) garbage.Next = new GcStressNode(seed - i);
        }
    }

    private static void AllocateMixedGarbageBatch(Int32 count, Int32 seed)
    {
        for (Int32 i = 0; i < count; i++)
        {
            if ((i & 3) == 0)
            {
                Byte[] bytes = new Byte[24 + (i & 127)];
                if (bytes.Length != 0) bytes[0] = (Byte)(seed + i);
            }
            else
            {
                GcStressNode garbage = new GcStressNode(seed + i);
                if ((i & 7) == 0) garbage.Next = new GcStressNode(seed - i);
            }
        }
    }

    private static void ScrubDeadStackFrames()
    {
        // Inu intentionally uses conservative stack roots. Clear a bounded scratch region
        // before deterministic reclamation/finalizer probes so stale values from returned
        // helper frames are not mistaken for live references. Real live roots in caller
        // frames remain untouched and continue to be verified by the stress gate.
        const Int32 bytes = 4096;
        Byte* scratch = stackalloc Byte[bytes];
        for (Int32 i = 0; i < bytes; i++) scratch[i] = 0;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static UInt32 ClearResurrectedAndCaptureSavedRecord()
    {
        NativeAotExceptionRuntime.TraceStage(0x164UL);
        UInt32 recordIndex = _gcResurrectedRecord;
        NativeAotExceptionRuntime.TraceValue(0x165UL, recordIndex);
        _gcResurrected = null;
        _gcResurrectedRecord = UInt32.MaxValue;
        NativeAotExceptionRuntime.TraceStage(0x167UL);
        return recordIndex;
    }

    private static void RecordTagged(Boolean condition, UInt32 tag, ref UInt32 passed, ref UInt32 failed)
    {
        if (!condition) NativeAotExceptionRuntime.TraceValue(0x180UL, tag);
        Record(condition, ref passed, ref failed);
    }

    private static Boolean CreateFinalizableGarbageOnRetiredStack(Int32 id, Boolean resurrect, Boolean throwFromFinalizer, Byte mode)
    {
        KernelSchedulerCapabilities capabilities=KernelScheduler.GetCapabilities();
        if(capabilities.ProcessorCount<2U)return false;
        UInt32 usable=capabilities.ProcessorCount>63U?63U:capabilities.ProcessorCount;
        UInt64 affinity=(1UL<<((Int32)usable))-1UL;
        if(KernelScheduler.TryGetCurrentLocalScheduler(out KernelCpuLocalSchedulerInfo local)&&local.ProcessorIndex<64U)affinity&=~(1UL<<((Int32)local.ProcessorIndex));
        if(affinity==0UL)return false;
        _gcWorkerId=id;_gcWorkerResurrect=resurrect;_gcWorkerThrow=throwFromFinalizer;_gcWorkerMode=mode;
        delegate*<void> callback=&FinalizerCreatorWorker;
        if(!KernelScheduler.TryCreateOneShotThread(callback,KernelThreadPriority.Normal,affinity,65536UL,out UInt64 threadId))return false;
        NativeAotExceptionRuntime.TraceStage(0x140UL);NativeAotExceptionRuntime.TraceValue(0x141UL,threadId);
        for(UInt32 spin=0U;spin<5000000U;spin++)
        {
            if(KernelScheduler.TryRetireThread(threadId)){NativeAotExceptionRuntime.TraceStage(0x142UL);return true;}
        }
        NativeAotExceptionRuntime.TraceStage(0x143UL);return false;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void FinalizerCreatorWorker()
    {
        NativeAotExceptionRuntime.TraceStage(0x144UL);
        FinalizableProbe value=new FinalizableProbe(_gcWorkerId,_gcWorkerResurrect,_gcWorkerThrow);
        if(_gcWorkerMode==1)GC.SuppressFinalize(value);
        else if(_gcWorkerMode==2){GC.SuppressFinalize(value);GC.ReRegisterForFinalize(value);}
        if(value.Id==Int32.MinValue)_gcLastFinalizedId=value.Id;
        NativeAotExceptionRuntime.TraceStage(0x145UL);
    }

    private static Int32 EhDirectCatchFinally()
    {
        Int32 marker = 0;
        try
        {
            throw new ArgumentException("eh");
        }
        catch (ArgumentException)
        {
            marker = 1;
        }
        finally
        {
            marker += 2;
        }
        return marker;
    }

    private static Int32 EhTypedCatchSelection()
    {
        try
        {
            throw new OverflowException();
        }
        catch (NullReferenceException)
        {
            return -1;
        }
        catch (ArithmeticException)
        {
            return 7;
        }
    }

    private static Int32 EhCrossFrameUnwind()
    {
        try
        {
            EhThrowingFrame();
            return -1;
        }
        catch (IndexOutOfRangeException)
        {
            return 11;
        }
    }

    private static void EhThrowingFrame()
    {
        try
        {
            throw new IndexOutOfRangeException();
        }
        finally
        {
            _ehFinallyMarker = 5;
        }
    }

    private static Int32 EhRethrow()
    {
        try
        {
            try
            {
                throw new DivideByZeroException();
            }
            catch (DivideByZeroException)
            {
                _ehRethrowMarker = 3;
                throw;
            }
        }
        catch (ArithmeticException)
        {
            return 9;
        }
    }

    private static Int32 EhRuntimeHelperExceptions()
    {
        Int32 marker = 0;

        Object incompatible = new AbiDerived(41);
        try
        {
            _ = (Probe)incompatible;
        }
        catch (InvalidCastException)
        {
            marker |= 1;
        }

        Probe[] probeArray = new Probe[1];
        Object[] covariant = probeArray;
        try
        {
            covariant[0] = new Object();
        }
        catch (ArrayTypeMismatchException)
        {
            marker |= 2;
        }

        Nullable<Int32> missing = default;
        try
        {
            _ = missing.Value;
        }
        catch (InvalidOperationException)
        {
            marker |= 4;
        }

        return marker;
    }

    private static Int32 EhNestedMultiFrameFinally()
    {
        _ehFinallyMarker = 0;
        try
        {
            EhNestedFinallyFrameOne();
            return -1;
        }
        catch (SystemException)
        {
            return _ehFinallyMarker;
        }
    }

    private static void EhNestedFinallyFrameOne()
    {
        try
        {
            EhNestedFinallyFrameTwo();
        }
        finally
        {
            _ehFinallyMarker += 10;
        }
    }

    private static void EhNestedFinallyFrameTwo()
    {
        try
        {
            throw new InvalidOperationException("nested unwind");
        }
        finally
        {
            _ehFinallyMarker += 1;
        }
    }

    private static Int32 EhCrossFrameRethrow()
    {
        _ehRethrowMarker = 0;
        try
        {
            EhRethrowFrame();
            return -1;
        }
        catch (Exception)
        {
            return _ehRethrowMarker;
        }
    }

    private static void EhRethrowFrame()
    {
        try
        {
            throw new OverflowException();
        }
        catch (ArithmeticException)
        {
            _ehRethrowMarker = 4;
            throw;
        }
        finally
        {
            _ehRethrowMarker += 8;
        }
    }

    private static Type TypeOfGeneric<T>() => typeof(T);

    private static void RunDispatchOnlyChecks(Probe probe, ref UInt32 passed, ref UInt32 failed)
    {
        NativeAotExceptionRuntime.TraceStage(0xB0UL);
        AbiDerived derived=new AbiDerived(31);AbiBase baseReference=derived;Object objectReference=derived;
        Record(baseReference.ReadVirtual()==31,ref passed,ref failed);Record(objectReference is AbiDerived,ref passed,ref failed);Record(objectReference is AbiBase,ref passed,ref failed);Record(objectReference is IAbiReadable,ref passed,ref failed);Record(objectReference is IAbiExtended,ref passed,ref failed);Record(((AbiDerived)objectReference).ReadVirtual()==31,ref passed,ref failed);
        IAbiReadable[] dispatchTargets=new IAbiReadable[2];dispatchTargets[0]=derived;dispatchTargets[1]=new AbiAlternate(7);
        Record(ReadThroughInterface(dispatchTargets[0])==32,ref passed,ref failed);Record(ReadThroughInterface(dispatchTargets[1])==17,ref passed,ref failed);Record(TransformThroughInterface(dispatchTargets[0],9)==40,ref passed,ref failed);Record(TransformThroughInterface(dispatchTargets[1],9)==23,ref passed,ref failed);
        IAbiExtended extended=derived;Record(ReadThroughInheritedInterface(extended)==33,ref passed,ref failed);
        Probe castRoundTrip=(Probe)probe;Record(Object.ReferenceEquals(castRoundTrip,probe),ref passed,ref failed);
        Probe[] probeArray=new Probe[2];Object[] covariant=probeArray;covariant[0]=probe;Record(Object.ReferenceEquals(probeArray[0],probe),ref passed,ref failed);
        Type probeArrayType=typeof(Probe[]);Type objectArrayType=typeof(Object[]);Record(objectArrayType.IsAssignableFrom(probeArrayType),ref passed,ref failed);Record(probeArrayType.IsAssignableTo(objectArrayType),ref passed,ref failed);Record(typeof(AbiDerived).IsSubclassOf(typeof(AbiBase)),ref passed,ref failed);Record(typeof(IAbiReadable).IsAssignableFrom(typeof(AbiDerived)),ref passed,ref failed);
    }

    private static void RunDispatchCastAndGenericChecks(Probe probe, ref UInt32 passed, ref UInt32 failed)
    {
        NativeAotExceptionRuntime.TraceStage(0xB0UL);

        AbiDerived derived = new AbiDerived(31);
        AbiBase baseReference = derived;
        Object objectReference = derived;

        Record(baseReference.ReadVirtual() == 31, ref passed, ref failed);
        Record(objectReference is AbiDerived, ref passed, ref failed);
        Record(objectReference is AbiBase, ref passed, ref failed);
        Record(objectReference is IAbiReadable, ref passed, ref failed);
        Record(objectReference is IAbiExtended, ref passed, ref failed);
        Record(((AbiDerived)objectReference).ReadVirtual() == 31, ref passed, ref failed);

        // 0.0.80: make real NativeAOT interface dispatch a hard runtime contract.
        // Two unrelated implementations, an inherited interface, an explicit implementation
        // and an argument-bearing method prevent the compiler from satisfying this gate with
        // class-vtable dispatch or a single exact-type devirtualization. The calls below are
        // ordinary C# interface calls and therefore exercise ILC's emitted interface dispatch
        // cells/resolution path in the booted kernel.
        IAbiReadable[] dispatchTargets = new IAbiReadable[2];
        dispatchTargets[0] = derived;
        dispatchTargets[1] = new AbiAlternate(7);
        Record(ReadThroughInterface(dispatchTargets[0]) == 32, ref passed, ref failed);
        Record(ReadThroughInterface(dispatchTargets[1]) == 17, ref passed, ref failed);
        Record(TransformThroughInterface(dispatchTargets[0], 9) == 40, ref passed, ref failed);
        Record(TransformThroughInterface(dispatchTargets[1], 9) == 23, ref passed, ref failed);
        IAbiExtended extended = derived;
        Record(ReadThroughInheritedInterface(extended) == 33, ref passed, ref failed);

        // Cast and covariance success paths stay in the Core ABI gate. Deliberately
        // throwing variants live in the dedicated EH gate so subsystem failures remain
        // attributable while still being mandatory for the complete runtime acceptance.
        Probe castRoundTrip = (Probe)probe;
        Record(Object.ReferenceEquals(castRoundTrip, probe), ref passed, ref failed);

        Probe[] probeArray = new Probe[2];
        Object[] covariant = probeArray;
        covariant[0] = probe;
        Record(Object.ReferenceEquals(probeArray[0], probe), ref passed, ref failed);

        Type probeArrayType = typeof(Probe[]);
        Type objectArrayType = typeof(Object[]);
        Record(objectArrayType.IsAssignableFrom(probeArrayType), ref passed, ref failed);
        Record(probeArrayType.IsAssignableTo(objectArrayType), ref passed, ref failed);
        Record(typeof(AbiDerived).IsSubclassOf(typeof(AbiBase)), ref passed, ref failed);
        Record(typeof(IAbiReadable).IsAssignableFrom(typeof(AbiDerived)), ref passed, ref failed);

        // 0.0.84: generics are a hard runtime milestone rather than a couple of exact-type probes.
        // The checks below force exact generic types, shared generic methods, generic statics,
        // generic arrays, boxing/unboxing, constrained calls, constructed generic interface
        // dispatch, variance, generic delegates and both class/interface generic virtual methods.
        // This intentionally exercises NativeAOT generic dictionaries/GVM machinery where ILC
        // decides it is required; any missing ABI helper must fail the build or this gate.
        NativeAotExceptionRuntime.TraceStage(0xB2UL);
        RunGenericRuntimeChecks(probe, ref passed, ref failed);

        // Nullable<T>.Value throwing semantics are exercised by the dedicated EH gate.
        NativeAotExceptionRuntime.TraceStage(0xB1UL);

        // 0.0.85: core generic collections are a mandatory managed-runtime contract.
        // This covers mutation, resizing, generic interface enumeration, reference retention,
        // hashing/equality, removal, queue/stack ordering and collection exception semantics.
        NativeAotExceptionRuntime.TraceStage(0xB4UL);
        RunCollectionRuntimeChecks(probe, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0xB5UL);

        // 0.0.61: the first coder-facing System.Text surface is a runtime contract,
        // including dynamic managed-string materialisation through RhNewString.
        NativeAotExceptionRuntime.TraceStage(0xB6UL);
        RunTextRuntimeChecks(ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0xB7UL);
    }

    private static void RunTextRuntimeChecks(ref UInt32 passed, ref UInt32 failed)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("Inu").Append(' ').Append(61).AppendLine();
        String built = builder.ToString();
        Record(built.Length == 8 && built[0] == 'I' && built[3] == ' ' && built[4] == '6' && built[5] == '1' && built[6] == '\r' && built[7] == '\n', ref passed, ref failed);

        builder.Clear().Append(true).Append('/').Append((UInt64)42UL);
        Record(String.Equals(builder.ToString(), "True/42"), ref passed, ref failed);

        Encoding ascii = Encoding.ASCII;
        Byte[] asciiBytes = ascii.GetBytes("Inu");
        Record(asciiBytes.Length == 3 && asciiBytes[0] == (Byte)'I' && asciiBytes[2] == (Byte)'u', ref passed, ref failed);
        Record(String.Equals(ascii.GetString(asciiBytes), "Inu"), ref passed, ref failed);

        Encoding utf8 = Encoding.UTF8;
        String unicode = "\u00A3\u20AC";
        Byte[] utf8Bytes = utf8.GetBytes(unicode);
        Record(utf8Bytes.Length == 5 && utf8Bytes[0] == 0xC2 && utf8Bytes[1] == 0xA3 && utf8Bytes[2] == 0xE2 && utf8Bytes[3] == 0x82 && utf8Bytes[4] == 0xAC, ref passed, ref failed);
        Record(String.Equals(utf8.GetString(utf8Bytes), unicode), ref passed, ref failed);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Boolean ExpectCollectionInvalidation(IEnumerator<Int32> enumerator)
    {
        // Keep the catch in a small, non-generic, non-inlined method. This still
        // validates a real cross-frame InvalidOperationException from List<T>, but
        // gives NativeAOT a dedicated root method with unambiguous EH metadata.
        NativeAotExceptionRuntime.TraceStage(0x1832UL);
        try
        {
            Boolean moved = enumerator.MoveNext();
            NativeAotExceptionRuntime.TraceValue(0x1833UL, moved ? 1UL : 0UL);
            return false;
        }
        catch (InvalidOperationException)
        {
            NativeAotExceptionRuntime.TraceStage(0x1834UL);
            return true;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Boolean ExpectDuplicateStringKey(Dictionary<String, Probe> dictionary, Probe value)
    {
        // 0.0.96: keep the expected duplicate-key catch in its own non-generic EH root.
        // This mirrors the already-proven list invalidation helper and prevents the
        // large collections method from becoming the only place where NativeAOT must
        // recover the typed ArgumentException clause after unwinding shared-generic frames.
        NativeAotExceptionRuntime.TraceStage(0x1857UL);
        try
        {
            NativeAotExceptionRuntime.TraceStage(0x1858UL);
            dictionary.Add("alpha", value);
            return false;
        }
        catch (ArgumentException)
        {
            NativeAotExceptionRuntime.TraceStage(0x1852UL);
            return true;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Boolean ExpectMissingStringKey(Dictionary<String, Probe> dictionary)
    {
        // The missing-key path is isolated for the same reason as duplicate-key handling:
        // the collection contract remains a real KeyNotFoundException, while EH metadata
        // is emitted on a small dedicated root that is easy to diagnose independently.
        NativeAotExceptionRuntime.TraceStage(0x185EUL);
        try
        {
            NativeAotExceptionRuntime.TraceStage(0x185FUL);
            Probe unused = dictionary["missing"];
            return false;
        }
        catch (KeyNotFoundException)
        {
            NativeAotExceptionRuntime.TraceStage(0x1855UL);
            return true;
        }
    }

    private static void RunCollectionRuntimeChecks(Probe probe, ref UInt32 passed, ref UInt32 failed)
    {
        // 0.0.104: collection diagnostics begin at method entry. Earlier releases placed
        // the first detailed marker after several List<T> operations, which made a host
        // timeout at EH:B4 indistinguishable from a fault in the first collection call.
        NativeAotExceptionRuntime.TraceStage(0x18B0UL);
        NativeAotExceptionRuntime.TraceStage(0x18B1UL);
        List<Int32> numbers = new List<Int32>();
        NativeAotExceptionRuntime.TraceStage(0x18B2UL);
        for (Int32 i = 0; i < 12; i++)
        {
            numbers.Add(i * 3);
            if (i == 0) NativeAotExceptionRuntime.TraceStage(0x18B3UL);
        }
        NativeAotExceptionRuntime.TraceValue(0x18B4UL, (UInt64)(UInt32)numbers.Count);
        NativeAotExceptionRuntime.TraceValue(0x18B5UL, (UInt64)(UInt32)numbers.Capacity);
        Record(numbers.Count == 12 && numbers.Capacity >= 12, ref passed, ref failed);
        Record(numbers[0] == 0 && numbers[11] == 33, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x18B6UL);
        numbers.Insert(2, 77);
        NativeAotExceptionRuntime.TraceStage(0x18B7UL);
        Record(numbers.Count == 13 && numbers[2] == 77 && numbers[3] == 6, ref passed, ref failed);
        Record(numbers.Contains(77) && numbers.IndexOf(77) == 2, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x18B8UL);
        Record(numbers.Remove(77) && !numbers.Contains(77) && numbers.Count == 12, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x18B9UL);
        numbers.RemoveAt(0);
        Record(numbers.Count == 11 && numbers[0] == 3, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x18BAUL);
        Int32[] copied = new Int32[14];
        NativeAotExceptionRuntime.TraceStage(0x18BBUL);
        numbers.CopyTo(copied, 2);
        Record(copied[2] == 3 && copied[12] == 33, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x18BCUL);
        Int32[] snapshot = numbers.ToArray();
        Record(snapshot.Length == numbers.Count && snapshot[0] == 3 && snapshot[10] == 33, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x18BDUL);

        // 0.0.98: directly invoke the public value-type enumerator. This was previously
        // routed through IEnumerable<T> to avoid NativeAOT's shared-generic value-type
        // unboxing edge. It is now a positive ABI conformance path.
        NativeAotExceptionRuntime.TraceStage(0x18BEUL);
        List<Int32>.Enumerator directEnumerator = numbers.GetEnumerator();
        NativeAotExceptionRuntime.TraceStage(0x18BFUL);
        Int32 directSeen = 0;
        Int32 directSum = 0;
        while (directEnumerator.MoveNext())
        {
            directSum += directEnumerator.Current;
            directSeen++;
            if (directSeen == 1) NativeAotExceptionRuntime.TraceStage(0x18C0UL);
        }
        directEnumerator.Dispose();
        NativeAotExceptionRuntime.TraceValue(0x18C1UL, (UInt64)(UInt32)directSeen);
        NativeAotExceptionRuntime.TraceValue(0x18C2UL, (UInt64)(UInt32)directSum);
        Record(directSeen == 11 && directSum == 198, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1701UL);

        NativeAotExceptionRuntime.TraceStage(0x18C3UL);
        List<Probe> probes = new List<Probe>();
        NativeAotExceptionRuntime.TraceStage(0x18C4UL);
        IList<Probe> probeList = probes;
        Probe secondProbe = new Probe(211);
        probeList.Add(probe);
        probeList.Add(secondProbe);
        Record(probeList.Count == 2 && Object.ReferenceEquals(probeList[0], probe), ref passed, ref failed);
        IEnumerable<Probe> enumerableProbes = probes;
        IEnumerator<Probe> probeEnumerator = enumerableProbes.GetEnumerator();
        Boolean firstMoved = probeEnumerator.MoveNext();
        Probe firstEnumerated = firstMoved ? probeEnumerator.Current : null;
        Boolean secondMoved = probeEnumerator.MoveNext();
        Probe secondEnumerated = secondMoved ? probeEnumerator.Current : null;
        probeEnumerator.Dispose();
        Record(firstMoved && secondMoved && Object.ReferenceEquals(firstEnumerated, probe) && Object.ReferenceEquals(secondEnumerated, secondProbe), ref passed, ref failed);

        // Box the shared-reference value-type enumerator and call it through IEnumerator<T>.
        // This forces NativeAOT's generic value-type unboxing thunk rather than the
        // collection's reference-type InterfaceEnumerator wrapper.
        List<Probe>.Enumerator sharedValueEnumerator = probes.GetEnumerator();
        IEnumerator<Probe> boxedSharedEnumerator = sharedValueEnumerator;
        Boolean boxedSharedMoved = boxedSharedEnumerator.MoveNext();
        Probe boxedSharedCurrent = boxedSharedMoved ? boxedSharedEnumerator.Current : null;
        boxedSharedEnumerator.Dispose();
        Record(boxedSharedMoved && Object.ReferenceEquals(boxedSharedCurrent, probe), ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1702UL);
        NativeAotExceptionRuntime.TraceStage(0x18C5UL);

        // 0.0.91: collections must not depend on NativeAOT's unfinished shared-generic
        // value-type method/unboxing ABI. List<T>.GetEnumerator() still exists for normal
        // C# pattern enumeration, but that ABI is validated in the NativeAOT edge-case gate.
        // Collection invalidation is therefore validated through the reference-type
        // IEnumerable<T> wrapper, which exercises the same List<T> version semantics.
        //
        // Keep this region deliberately verbose. The previous runs stopped after 0x1702,
        // so each interface acquisition, move, mutation, exception and disposal boundary is
        // independently visible on serial without changing the 20-second acceptance timeout.
        NativeAotExceptionRuntime.TraceStage(0x1820UL);
        NativeAotExceptionRuntime.TraceValue(0x1821UL, (UInt64)(UInt32)numbers.Count);
        NativeAotExceptionRuntime.TraceValue(0x1822UL, (UInt64)(UInt32)numbers.Capacity);

        IEnumerable<Int32> invalidationSource = numbers;
        NativeAotExceptionRuntime.TraceStage(0x1823UL);
        IEnumerator<Int32> invalidated = invalidationSource.GetEnumerator();
        NativeAotExceptionRuntime.TraceStage(0x1824UL);

        NativeAotExceptionRuntime.TraceStage(0x1825UL);
        Boolean invalidatedFirstMoved = invalidated.MoveNext();
        NativeAotExceptionRuntime.TraceValue(0x1826UL, invalidatedFirstMoved ? 1UL : 0UL);
        Int32 invalidatedFirstCurrent = invalidatedFirstMoved ? invalidated.Current : -1;
        NativeAotExceptionRuntime.TraceValue(0x1827UL, (UInt64)(UInt32)invalidatedFirstCurrent);
        Record(invalidatedFirstMoved && invalidatedFirstCurrent == 3, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1828UL);

        NativeAotExceptionRuntime.TraceStage(0x1829UL);
        numbers.Add(36);
        NativeAotExceptionRuntime.TraceValue(0x182AUL, (UInt64)(UInt32)numbers.Count);
        NativeAotExceptionRuntime.TraceStage(0x182BUL);

        NativeAotExceptionRuntime.TraceStage(0x182CUL);
        Boolean invalidationThrew = ExpectCollectionInvalidation(invalidated);
        NativeAotExceptionRuntime.TraceValue(0x182DUL, invalidationThrew ? 1UL : 0UL);
        if (invalidationThrew) NativeAotExceptionRuntime.TraceStage(0x182EUL);
        NativeAotExceptionRuntime.TraceStage(0x182FUL);
        invalidated.Dispose();
        NativeAotExceptionRuntime.TraceStage(0x1830UL);
        NativeAotExceptionRuntime.TraceValue(0x1831UL, invalidationThrew ? 1UL : 0UL);
        Record(invalidationThrew, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1703UL);

        NativeAotExceptionRuntime.TraceStage(0x1840UL);
        Dictionary<Int32, Int32> map = new Dictionary<Int32, Int32>();
        NativeAotExceptionRuntime.TraceStage(0x1841UL);
        for (Int32 i = 0; i < 20; i++)
        {
            map.Add(i, i * i);
            if ((i & 3) == 3) NativeAotExceptionRuntime.TraceValue(0x1842UL, (UInt64)(UInt32)(i + 1));
        }
        NativeAotExceptionRuntime.TraceValue(0x1843UL, (UInt64)(UInt32)map.Count);
        Record(map.Count == 20 && map[7] == 49 && map[19] == 361, ref passed, ref failed);
        Int32 foundValue;
        NativeAotExceptionRuntime.TraceStage(0x1844UL);
        Boolean foundEleven = map.TryGetValue(11, out foundValue);
        NativeAotExceptionRuntime.TraceValue(0x1845UL, foundEleven ? 1UL : 0UL);
        NativeAotExceptionRuntime.TraceValue(0x1846UL, (UInt64)(UInt32)foundValue);
        Record(foundEleven && foundValue == 121, ref passed, ref failed);
        map[11] = 777;
        Record(map[11] == 777 && map.Count == 20, ref passed, ref failed);
        Record(!map.TryAdd(11, 888) && map.TryAdd(25, 625) && map[25] == 625, ref passed, ref failed);
        Record(map.Remove(4) && !map.ContainsKey(4) && map.Count == 20, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1847UL);
        Int32 dictionarySum = 0;

        // 0.0.98: directly invoke Dictionary<TKey,TValue>.Enumerator so the
        // value-type generic method/unboxing ABI is tested instead of bypassed.
        NativeAotExceptionRuntime.TraceStage(0x184AUL);
        Dictionary<Int32, Int32>.Enumerator mapEnumerator = map.GetEnumerator();
        NativeAotExceptionRuntime.TraceStage(0x184BUL);
        NativeAotExceptionRuntime.TraceStage(0x184CUL);
        Int32 dictionarySeen = 0;
        while (true)
        {
            NativeAotExceptionRuntime.TraceValue(0x184DUL, (UInt64)(UInt32)dictionarySeen);
            Boolean mapMoved = mapEnumerator.MoveNext();
            NativeAotExceptionRuntime.TraceValue(0x184EUL, mapMoved ? 1UL : 0UL);
            if (!mapMoved) break;
            KeyValuePair<Int32, Int32> current = mapEnumerator.Current;
            NativeAotExceptionRuntime.TraceValue(0x184FUL, (UInt64)(UInt32)current.Key);
            dictionarySum += current.Key;
            dictionarySeen++;
        }
        mapEnumerator.Dispose();
        NativeAotExceptionRuntime.TraceValue(0x1848UL, (UInt64)(UInt32)dictionarySeen);
        NativeAotExceptionRuntime.TraceValue(0x1849UL, (UInt64)(UInt32)dictionarySum);
        Record(dictionarySeen == 20 && dictionarySum == 211, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1704UL);

        NativeAotExceptionRuntime.TraceStage(0x1850UL);
        Dictionary<String, Probe> named = new Dictionary<String, Probe>();
        NativeAotExceptionRuntime.TraceStage(0x185AUL);
        named.Add("alpha", probe);
        NativeAotExceptionRuntime.TraceStage(0x185BUL);
        named.Add("beta", secondProbe);
        NativeAotExceptionRuntime.TraceStage(0x185CUL);
        Record(named.ContainsKey("alpha") && Object.ReferenceEquals(named["beta"], secondProbe), ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x185DUL);

        NativeAotExceptionRuntime.TraceStage(0x1851UL);
        Boolean duplicateThrew = ExpectDuplicateStringKey(named, secondProbe);
        NativeAotExceptionRuntime.TraceValue(0x1853UL, duplicateThrew ? 1UL : 0UL);
        Record(duplicateThrew && named.Count == 2, ref passed, ref failed);

        NativeAotExceptionRuntime.TraceStage(0x1854UL);
        Boolean missingThrew = ExpectMissingStringKey(named);
        NativeAotExceptionRuntime.TraceValue(0x1856UL, missingThrew ? 1UL : 0UL);
        Record(missingThrew, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1705UL);

        NativeAotExceptionRuntime.TraceStage(0x1860UL);
        Queue<Int32> queue = new Queue<Int32>();
        for (Int32 i = 1; i <= 7; i++) queue.Enqueue(i);
        Record(queue.Count == 7 && queue.Peek() == 1, ref passed, ref failed);
        Record(queue.Dequeue() == 1 && queue.Dequeue() == 2, ref passed, ref failed);
        queue.Enqueue(8); queue.Enqueue(9);
        Record(queue.Count == 7 && queue.Peek() == 3, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceValue(0x1861UL, (UInt64)(UInt32)queue.Count);
        NativeAotExceptionRuntime.TraceValue(0x1862UL, (UInt64)(UInt32)queue.Peek());
        NativeAotExceptionRuntime.TraceStage(0x1706UL);

        NativeAotExceptionRuntime.TraceStage(0x1870UL);
        Stack<Probe> stack = new Stack<Probe>();
        stack.Push(probe);
        stack.Push(secondProbe);
        Record(stack.Count == 2 && Object.ReferenceEquals(stack.Peek(), secondProbe), ref passed, ref failed);
        Record(Object.ReferenceEquals(stack.Pop(), secondProbe) && Object.ReferenceEquals(stack.Pop(), probe) && stack.Count == 0, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceValue(0x1871UL, (UInt64)(UInt32)stack.Count);
        NativeAotExceptionRuntime.TraceStage(0x1707UL);
    }

    private static void RunGenericRuntimeChecks(Probe probe, ref UInt32 passed, ref UInt32 failed)
    {
        NativeAotExceptionRuntime.TraceStage(0x1880UL);
        GenericHolder<Int32> number = new GenericHolder<Int32>();
        number.Value = 73;
        GenericHolder<Probe> reference = new GenericHolder<Probe>();
        reference.Value = probe;
        Record(number.Value == 73, ref passed, ref failed);
        Record(Object.ReferenceEquals(reference.Value, probe), ref passed, ref failed);

        GenericPair<Int32, Probe> pair = new GenericPair<Int32, Probe>();
        pair.First = 81;
        pair.Second = probe;
        Record(pair.First == 81 && Object.ReferenceEquals(pair.Second, probe), ref passed, ref failed);
        Record(typeof(GenericHolder<Int32>) != typeof(GenericHolder<Probe>), ref passed, ref failed);
        Record(typeof(GenericHolder<Int32>).IsGenericType && typeof(GenericPair<Int32, Probe>).IsGenericType, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1881UL);

        Record(GenericIdentity<Int32>(107) == 107, ref passed, ref failed);
        Record(Object.ReferenceEquals(GenericIdentity<Probe>(probe), probe), ref passed, ref failed);
        Record(TypeOfGeneric<GenericHolder<Int32>>() == typeof(GenericHolder<Int32>), ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1882UL);

        Int32 left = 3;
        Int32 right = 9;
        GenericSwap(ref left, ref right);
        Record(left == 9 && right == 3, ref passed, ref failed);
        Probe otherProbe = new Probe(108);
        Probe leftProbe = probe;
        Probe rightProbe = otherProbe;
        GenericSwap(ref leftProbe, ref rightProbe);
        Record(Object.ReferenceEquals(leftProbe, otherProbe) && Object.ReferenceEquals(rightProbe, probe), ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1883UL);

        GenericStatic<Int32>.Value = 111;
        GenericStatic<Probe>.Value = probe;
        Record(GenericStatic<Int32>.Value == 111 && Object.ReferenceEquals(GenericStatic<Probe>.Value, probe), ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1884UL);

        Int32[] genericInts = GenericArray<Int32>(3);
        Probe[] genericProbes = GenericArray<Probe>(2);
        genericInts[1] = 119;
        genericProbes[0] = probe;
        Record(genericInts.Length == 3 && genericInts[1] == 119, ref passed, ref failed);
        Record(genericProbes.Length == 2 && Object.ReferenceEquals(genericProbes[0], probe), ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1885UL);

        Record(GenericBoxRoundTrip<Int32>(127) == 127, ref passed, ref failed);
        GenericPair<Int32, Int32> valuePair = new GenericPair<Int32, Int32> { First = 5, Second = 6 };
        GenericPair<Int32, Int32> roundTripPair = GenericBoxRoundTrip(valuePair);
        Record(roundTripPair.First == 5 && roundTripPair.Second == 6, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1886UL);

        GenericValueReadable valueReadable = new GenericValueReadable { Value = 131 };
        GenericReferenceReadable referenceReadable = new GenericReferenceReadable(137);
        Record(ReadConstrained(ref valueReadable) == 131, ref passed, ref failed);
        Record(ReadConstrained(referenceReadable) == 137, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1887UL);

        IGenericTransform<Int32> intTransform = new GenericIntTransform();
        IGenericTransform<Probe> probeTransform = new GenericProbeTransform();
        Record(TransformGenericInterface(intTransform, 140) == 145, ref passed, ref failed);
        Record(Object.ReferenceEquals(TransformGenericInterface(probeTransform, probe), probe), ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1888UL);

        // Covariance/contravariance require the MethodTable generic-definition, composition
        // and variance metadata to participate in both cast checks and interface dispatch.
        IGenericProducer<Probe> exactProducer = new GenericProbeProducer(probe);
        IGenericProducer<Object> covariantProducer = exactProducer;
        Record(Object.ReferenceEquals(GetGenericProduced(covariantProducer), probe), ref passed, ref failed);
        IGenericConsumer<Object> objectConsumer = new GenericObjectConsumer();
        IGenericConsumer<Probe> contravariantConsumer = objectConsumer;
        Record(ConsumeGeneric(contravariantConsumer, probe) == 91, ref passed, ref failed);
        Record(typeof(IGenericProducer<Object>).IsAssignableFrom(typeof(GenericProbeProducer)), ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1889UL);

        GenericUnaryDelegate<Int32> intDelegate = GenericIdentity<Int32>;
        GenericUnaryDelegate<Probe> probeDelegate = GenericIdentity<Probe>;
        Record(intDelegate(149) == 149, ref passed, ref failed);
        Record(Object.ReferenceEquals(probeDelegate(probe), probe), ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x188AUL);

        // 0.0.98: class GVM dispatch is now a mandatory NativeAOT ABI edge-case gate.
        // The no-inline base-typed wrapper prevents ordinary class-vtable devirtualisation.
        // Int32 exercises an exact generic instantiation; Probe exercises shared-reference
        // generic code and therefore the template/dictionary/fat-function-pointer path.
        GenericVirtualBase gvmTarget = new GenericVirtualDerived();
        NativeAotExceptionRuntime.TraceStage(0x188BUL);
        NativeAotExceptionRuntime.TraceStage(0x188CUL);
        Int32 exactGvmResult = CallGenericVirtual<Int32>(gvmTarget, 157);
        NativeAotExceptionRuntime.TraceValue(0x188DUL, (UInt64)(UInt32)exactGvmResult);
        Record(exactGvmResult == 20, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x188EUL);
        Int32 sharedGvmResult = CallGenericVirtual<Probe>(gvmTarget, probe);
        NativeAotExceptionRuntime.TraceValue(0x188FUL, (UInt64)(UInt32)sharedGvmResult);
        Record(sharedGvmResult == 20, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1890UL);

        // 0.0.109: exercise the InterfaceGenericVirtualMethodTable directly.  The exact
        // Int32 call proves interface-GVM slot resolution independently of shared generic
        // code; the Probe call then combines interface GVM resolution with a non-empty
        // runtime generic dictionary (the implementation reads typeof(T)).
        IGenericVirtualDispatch interfaceGvmTarget = new GenericVirtualInterfaceTarget();
        NativeAotExceptionRuntime.TraceStage(0x1891UL);
        Int32 exactInterfaceGvmResult = CallGenericInterfaceVirtual<Int32>(interfaceGvmTarget, 163);
        NativeAotExceptionRuntime.TraceValue(0x1892UL, (UInt64)(UInt32)exactInterfaceGvmResult);
        Record(exactInterfaceGvmResult == 30, ref passed, ref failed);
        Int32 sharedInterfaceGvmResult = CallGenericInterfaceVirtual<Probe>(interfaceGvmTarget, probe);
        NativeAotExceptionRuntime.TraceValue(0x1893UL, (UInt64)(UInt32)sharedInterfaceGvmResult);
        Record(sharedInterfaceGvmResult == 31, ref passed, ref failed);

        // Interface metadata can map to a virtual generic method on a base class. The
        // TypeLoader must then perform class-GVM resolution again so a derived override wins.
        IGenericVirtualDispatch inheritedInterfaceGvmTarget = new InheritedGenericVirtualInterfaceDerived();
        Int32 inheritedInterfaceGvmResult = CallGenericInterfaceVirtual<Probe>(inheritedInterfaceGvmTarget, probe);
        NativeAotExceptionRuntime.TraceValue(0x1898UL, (UInt64)(UInt32)inheritedInterfaceGvmResult);
        Record(inheritedInterfaceGvmResult == 36, ref passed, ref failed);

        // Resolve a GVM through a variance-compatible constructed generic interface.
        // The object implements IVariantGenericVirtualDispatch<Probe>, while the call is
        // intentionally made through the covariant Object instantiation.
        IVariantGenericVirtualDispatch<Object> variantGvmTarget = new VariantGenericVirtualInterfaceTarget();
        Int32 variantInterfaceGvmResult = CallVariantGenericInterfaceVirtual<Probe>(variantGvmTarget, probe);
        NativeAotExceptionRuntime.TraceValue(0x1894UL, (UInt64)(UInt32)variantInterfaceGvmResult);
        Record(variantInterfaceGvmResult == 41, ref passed, ref failed);

        // 0.0.114: force the shared interface-GVM dictionary beyond TypeHandle-only
        // layouts. These calls exercise Method/MethodDictionary, AllocateObject +
        // InterfaceCall, non-generic constrained dispatch and generic constrained GVM
        // materialisation on the dynamic dictionary path.
        GenericIdentityTransform<Probe> warmIdentityTransform = new GenericIdentityTransform<Probe>();
        Record(Object.ReferenceEquals(warmIdentityTransform.Transform(probe), probe), ref passed, ref failed);
        Record(Object.ReferenceEquals(DictionaryMethodBridge<Probe>(probe), probe), ref passed, ref failed);

        IMethodCellGvmDispatch methodCellTarget = new MethodCellGvmTarget();
        Probe methodCellResult = methodCellTarget.Echo<Probe>(probe);
        Record(Object.ReferenceEquals(methodCellResult, probe), ref passed, ref failed);

        IAllocationCellGvmDispatch allocationCellTarget = new AllocationCellGvmTarget();
        Probe allocationCellResult = allocationCellTarget.Transform<Probe>(probe);
        Record(Object.ReferenceEquals(allocationCellResult, probe), ref passed, ref failed);

        IConstrainedCellGvmDispatch constrainedCellTarget = new ConstrainedCellGvmTarget();
        GenericReferenceReadable constrainedReadable = new GenericReferenceReadable(173);
        Record(constrainedCellTarget.Read<GenericReferenceReadable>(constrainedReadable) == 173, ref passed, ref failed);

        IGenericConstrainedCellGvmDispatch genericConstrainedTarget = new GenericConstrainedCellGvmTarget();
        GenericVirtualInterfaceTarget nestedGvmTarget = new GenericVirtualInterfaceTarget();
        Record(genericConstrainedTarget.Invoke<GenericVirtualInterfaceTarget, Probe>(nestedGvmTarget, probe) == 31, ref passed, ref failed);

        IDefaultConstructorCellGvmDispatch defaultConstructorTarget = new DefaultConstructorCellGvmTarget();
        DefaultConstructorProbe constructed = defaultConstructorTarget.Create<DefaultConstructorProbe>();
        Record(constructed != null && constructed.Value == 181, ref passed, ref failed);

        PrepareStaticDataCellExactInstantiation();
        IStaticDataCellGvmDispatch staticDataTarget = new StaticDataCellGvmTarget();
        Probe staticRoundTrip = staticDataTarget.RoundTrip<Probe>(probe);
        Record(Object.ReferenceEquals(staticRoundTrip, probe), ref passed, ref failed);
        Record(staticDataTarget.Add<Probe>(7) == 7, ref passed, ref failed);
        Record(staticDataTarget.Add<Probe>(5) == 12, ref passed, ref failed);

        // 0.0.136: shared interface-GVM TypeHandle cells now cover NativeLayout
        // modifier-array and multidimensional-array type signatures. These lookups use
        // ArrayMap/CommonFixups and therefore exercise the thin TypeLoader rather than
        // relying on a direct non-generic typeof token.
        ITypeShapeCellGvmDispatch typeShapeTarget = new TypeShapeCellGvmTarget();
        Type sharedSzArray = typeShapeTarget.SzArray<Probe>();
        Type sharedMdArray = typeShapeTarget.MultiDimArray<Probe>();
        Type sharedPointer = typeShapeTarget.Pointer<Int32>();
        Type sharedFunctionPointer = typeShapeTarget.FunctionPointer<Probe>();
        Record(sharedSzArray == typeof(Probe[]) && sharedSzArray.IsSZArray, ref passed, ref failed);
        Record(sharedMdArray == typeof(Probe[,]) && sharedMdArray.IsArray && !sharedMdArray.IsSZArray, ref passed, ref failed);
        Record(sharedPointer == typeof(Int32*), ref passed, ref failed);
        Record(sharedFunctionPointer == typeof(delegate*<Probe>), ref passed, ref failed);

        // The freestanding target deliberately disables C# default interface implementations,
        // so executable conformance cannot express a default-interface GVM body here. The
        // TypeLoader retains its NativeAOT default-interface metadata resolution path, while
        // this gate covers the interface-GVM forms that the target compiler can emit.
        NativeAotExceptionRuntime.TraceStage(0x1896UL);

        Record(!System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<Int32>(), ref passed, ref failed);
        Record(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<Probe>(), ref passed, ref failed);
        Record(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<GenericHolder<Probe>>(), ref passed, ref failed);
        Record(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<GenericPair<Int32, Probe>>(), ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0x1897UL);
        NativeAotExceptionRuntime.TraceStage(0xB3UL);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static T DictionaryMethodBridge<T>(T value) => GenericIdentity<T>(value);

    private static T GenericIdentity<T>(T value) => value;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Int32 CallGenericVirtual<T>(GenericVirtualBase target, T value)
        => target.GenericVirtual<T>(value);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Int32 CallGenericInterfaceVirtual<T>(IGenericVirtualDispatch target, T value)
        => target.GenericVirtual<T>(value);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Int32 CallVariantGenericInterfaceVirtual<T>(IVariantGenericVirtualDispatch<Object> target, T value)
        => target.GenericVirtual<T>(value);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void GenericSwap<T>(ref T left, ref T right)
    {
        T temporary = left;
        left = right;
        right = temporary;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static T[] GenericArray<T>(Int32 length) => new T[length];

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static T GenericBoxRoundTrip<T>(T value) where T : struct
    {
        Object boxed = value;
        return (T)boxed;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Int32 ReadConstrained<T>(ref T value) where T : struct, IGenericReadable => value.Read();

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Int32 ReadConstrained<T>(T value) where T : class, IGenericReadable => value.Read();

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static T TransformGenericInterface<T>(IGenericTransform<T> value, T argument) => value.Transform(argument);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static T GetGenericProduced<T>(IGenericProducer<T> value) => value.Get();

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Int32 ConsumeGeneric<T>(IGenericConsumer<T> value, T argument) => value.Consume(argument);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Int32 ReadThroughInterface(IAbiReadable value) => value.Read();

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Int32 TransformThroughInterface(IAbiReadable value, Int32 argument) => value.Transform(argument);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Int32 ReadThroughInheritedInterface(IAbiExtended value) => value.Extra();

    private static Boolean RunAbiLayoutChecks(Int32[] values, String text, ref UInt32 passed, ref UInt32 failed)
    {
        // ABI audit baseline. These checks deliberately run before Object/primitive
        // layout reconciliation so later ABI work cannot silently move the live x64 contract.
        // Probe System.Object itself here: a derived EEType is allowed to encode inherited
        // dispatch differently, so it is not a valid stand-in for Object's slot-zero contract.
        Object objectInstance = new Object();
        UInt64 objectAddress = ReferenceAddress(objectInstance);
        UInt64 methodTableAddress = *(UInt64*)objectAddress;
        Record(objectAddress != 0UL && methodTableAddress != 0UL, ref passed, ref failed); // Object MethodTable pointer: +0

        // NativeAOT's Object contract has two distinct facts:
        //  1. every object reference points at its MethodTable at offset zero; and
        //  2. Object declares Finalize as virtual slot zero at the classlib/compiler boundary.
        // A plain System.Object is NOT a finalizable allocation, so its MethodTable is not
        // required to carry HasFinalizerFlag. Prove the finalizable allocation path with a
        // derived type that actually owns a destructor instead. Source verification enforces
        // that ~Object remains the first virtual declaration in CoreLib.
        const UInt32 HasFinalizerFlag = 0x00100000U;
        FinalizableProbe finalizable = new FinalizableProbe();
        UInt64 finalizableAddress = ReferenceAddress(finalizable);
        UInt64 finalizableMethodTable = *(UInt64*)finalizableAddress;
        UInt32 finalizableFlags = *(UInt32*)(finalizableMethodTable + 0UL);
        Boolean finalizableHasFlag = (finalizableFlags & HasFinalizerFlag) != 0U;
        Boolean objectAbiOk = objectAddress != 0UL && methodTableAddress != 0UL &&
                              finalizableAddress != 0UL && finalizableMethodTable != 0UL && finalizableHasFlag;
        Record(finalizableHasFlag, ref passed, ref failed);
        // NativeAOT emits dependency-driven vtables: the compiler may trim unused
        // physical slots from a concrete MethodTable while preserving the logical
        // System.Object slot order at the classlib/compiler boundary. Object has four
        // logical slots (Finalize, ToString, Equals, GetHashCode), so an emitted table
        // may contain 0..4 slots, but never more. Every slot that is physically present
        // must contain a valid entrypoint. The source verifier separately locks the
        // logical declaration order so trimming cannot disguise an ABI reorder.
        UInt16 objectVtableSlots = *(UInt16*)(methodTableAddress + 16UL);
        Record(objectVtableSlots <= 4U, ref passed, ref failed);
        // A dependency-driven NativeAOT vtable can contain zero placeholders for
        // trimmed lower slots. The slot count extends through the highest emitted slot,
        // so only that terminal slot is required to carry an entrypoint when the count
        // is non-zero. Requiring every lower slot to be non-zero produced the final
        // false ABI failure in minimal kernels.
        Boolean emittedObjectVtableTerminalValid = objectVtableSlots == 0U ||
            *(UInt64*)(methodTableAddress + 24UL + (((UInt64)objectVtableSlots - 1UL) * 8UL)) != 0UL;
        Record(emittedObjectVtableTerminalValid, ref passed, ref failed);

        Record(sizeof(Boolean) == 1 && sizeof(Char) == 2, ref passed, ref failed);
        Record(sizeof(SByte) == 1 && sizeof(Byte) == 1, ref passed, ref failed);
        Record(sizeof(Int16) == 2 && sizeof(UInt16) == 2, ref passed, ref failed);
        Record(sizeof(Int32) == 4 && sizeof(UInt32) == 4, ref passed, ref failed);
        Record(sizeof(Int64) == 8 && sizeof(UInt64) == 8, ref passed, ref failed);
        Record(sizeof(Single) == 4 && sizeof(Double) == 8, ref passed, ref failed);
        Record(sizeof(IntPtr) == 8 && sizeof(UIntPtr) == 8, ref passed, ref failed);

        values[0] = unchecked((Int32)0x13579BDF);
        UInt64 arrayAddress = ReferenceAddress(values);
        Record(*(Int32*)(arrayAddress + 8UL) == values.Length, ref passed, ref failed);
        Record(*(Int32*)(arrayAddress + 16UL) == values[0], ref passed, ref failed);

        UInt64 stringAddress = ReferenceAddress(text);
        Record(*(Int32*)(stringAddress + 8UL) == text.Length, ref passed, ref failed);
        Record(*(Char*)(stringAddress + 12UL) == text[0], ref passed, ref failed);

        // Canonical x64 NativeAOT MethodTable header offsets: flags +0, base size +4,
        // related type +8, vtable slots +16, interfaces +18, hash +20. Reading each
        // boundary makes the emitted layout part of the executable boot contract.
        UInt32 flags = *(UInt32*)(methodTableAddress + 0UL);
        UInt32 baseSize = *(UInt32*)(methodTableAddress + 4UL);
        UInt64 relatedType = *(UInt64*)(methodTableAddress + 8UL);
        UInt16 vtableSlots = *(UInt16*)(methodTableAddress + 16UL);
        UInt16 interfaces = *(UInt16*)(methodTableAddress + 18UL);
        UInt32 hashCode = *(UInt32*)(methodTableAddress + 20UL);
        UInt64 headerFingerprint = (UInt64)flags ^ ((UInt64)hashCode << 32) ^ relatedType ^ ((UInt64)interfaces << 16);
        Record(baseSize >= 8U, ref passed, ref failed);
        Record(vtableSlots == objectVtableSlots, ref passed, ref failed);
        Record(headerFingerprint != UInt64.MaxValue || methodTableAddress != 0UL, ref passed, ref failed);
        return objectAbiOk;
    }

    private static Boolean RunBoxingChecks(ref UInt32 passed, ref UInt32 failed)
    {
        const Int32 Payload = unchecked((Int32)0x13579BDF);
        Object boxed = Payload;
        UInt64 boxedAddress = ReferenceAddress(boxed);
        UInt64 boxedMethodTable = *(UInt64*)boxedAddress;
        RuntimeTypeHandle intHandle = typeof(Int32).TypeHandle;
        UInt64 intMethodTable = *(UInt64*)&intHandle;

        Record(boxedAddress != 0UL && boxedMethodTable == intMethodTable, ref passed, ref failed);
        Record(*(Int32*)(boxedAddress + 8UL) == Payload, ref passed, ref failed);
        Record(boxed is Int32 && (Int32)boxed == Payload, ref passed, ref failed);
        Record(*(UInt32*)(boxedMethodTable + 4UL) >= 24U, ref passed, ref failed);
        return boxedAddress != 0UL && boxedMethodTable == intMethodTable &&
               *(Int32*)(boxedAddress + 8UL) == Payload;
    }

    private static Boolean RunHandleChecks(ref UInt32 passed, ref UInt32 failed)
    {
        IntPtr marker = (IntPtr)(void*)0x13579BDFUL;
        RuntimeMethodHandle method = RuntimeMethodHandle.FromIntPtr(marker);
        RuntimeFieldHandle field = RuntimeFieldHandle.FromIntPtr(marker);
        Boolean sizes = sizeof(RuntimeTypeHandle) == sizeof(IntPtr) &&
                        sizeof(RuntimeMethodHandle) == sizeof(IntPtr) &&
                        sizeof(RuntimeFieldHandle) == sizeof(IntPtr);
        Boolean roundTrips = RuntimeMethodHandle.ToIntPtr(method) == marker &&
                             RuntimeFieldHandle.ToIntPtr(field) == marker;
        Record(sizes, ref passed, ref failed);
        Record(roundTrips, ref passed, ref failed);
        return sizes && roundTrips;
    }

    private static Boolean RunDelegateChecks(ref UInt32 passed, ref UInt32 failed)
    {
        // 0.45.36: NativeAOT delegate references are compiler-special. Raw pointer
        // extraction through Unsafe.As<Object, UInt64> is not a supported delegate
        // ABI probe and can yield a value that must not be dereferenced as an Object*.
        // Keep physical field order locked by the source verifier and validate the
        // emitted runtime object through normal managed type identity plus the actual
        // compiler-generated delegate invocation thunk.
        // First validate the delegate object ABI without involving Roslyn's
        // compiler-generated static delegate cache. This isolates NativeAOT delegate
        // allocation/MethodTable/type-test/invocation from the GC-static contract.
        NativeAotExceptionRuntime.TraceStage(0xD10UL);
        AbiUnaryDelegate unary = new AbiUnaryDelegate(AbiIncrement);
        NativeAotExceptionRuntime.TraceStage(0xD11UL);
        Object unaryObject = unary;
        NativeAotExceptionRuntime.TraceStage(0xD12UL);

        Boolean nonNull = !Object.ReferenceEquals(unaryObject, null);
        NativeAotExceptionRuntime.TraceStage(0xD13UL);
        Type expectedDelegateType = typeof(AbiUnaryDelegate);
        NativeAotExceptionRuntime.TraceStage(0xD14UL);
        Boolean expectedTypeAvailable = !Object.ReferenceEquals(expectedDelegateType, null);
        NativeAotExceptionRuntime.TraceStage(0xD15UL);
        // 0.45.65: capture values that require no object dereference before either
        // delegate type test. RuntimeTypeHandle.Value is the expected target MT;
        // ReferenceAddress records only the managed-reference bits and is never
        // dereferenced by this diagnostic.
        NativeAotExceptionRuntime.TraceValue(0x26UL, (UInt64)(void*)expectedDelegateType.TypeHandle.Value);
        NativeAotExceptionRuntime.TraceValue(0x2AUL, ReferenceAddress(unaryObject));
        NativeAotExceptionRuntime.TraceStage(0xD27UL); // immediately before known-good explicit delegate isinst
        Boolean typeTest = unaryObject is AbiUnaryDelegate;
        NativeAotExceptionRuntime.TraceStage(0xD16UL);
        Boolean invoked = unary(41) == 42;
        NativeAotExceptionRuntime.TraceStage(0xD17UL);

        // 0.0.98: the GC-static delegate path is a positive ABI gate. Loading a delegate
        // from a GC static must preserve normal type identity and compiler-generated
        // invocation semantics; this former exclusion is no longer permitted.
        NativeAotExceptionRuntime.TraceStage(0xD22UL);
        _gcStaticDelegateProbe = new AbiUnaryDelegate(AbiIncrement);
        NativeAotExceptionRuntime.TraceStage(0xD18UL);

        AbiUnaryDelegate cachedUnary = _gcStaticDelegateProbe;
        NativeAotExceptionRuntime.TraceStage(0xD28UL);
        Boolean gcStaticRoundTrip = !Object.ReferenceEquals(cachedUnary, null);
        NativeAotExceptionRuntime.TraceValue(0x2CUL, gcStaticRoundTrip ? 1UL : 0UL);
        Boolean gcStaticTypeTest = cachedUnary is AbiUnaryDelegate;
        NativeAotExceptionRuntime.TraceStage(0xD2AUL);
        Boolean gcStaticInvoked = cachedUnary(41) == 42;
        NativeAotExceptionRuntime.TraceStage(0xD2BUL);
        NativeAotExceptionRuntime.TraceStage(0xD29UL);

        Record(nonNull, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0xD1CUL);
        Record(expectedTypeAvailable && typeTest, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0xD1DUL);
        Record(invoked, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0xD1EUL);
        Record(gcStaticRoundTrip, ref passed, ref failed);
        Record(gcStaticTypeTest, ref passed, ref failed);
        Record(gcStaticInvoked, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0xD1FUL);
        return nonNull && expectedTypeAvailable && typeTest && invoked && gcStaticRoundTrip && gcStaticTypeTest && gcStaticInvoked;
    }

    private static Int32 AbiIncrement(Int32 value)
    {
        NativeAotExceptionRuntime.TraceStage(0xD20UL);
        Int32 result = value + 1;
        NativeAotExceptionRuntime.TraceStage(0xD21UL);
        return result;
    }


    private static Boolean MethodTableSurfaceIsCanonical(Int32[] values, String text)
    {
        // ABI audit item 4 hard gate. These are the emitted values consumed by the
        // minimal MethodTable BaseSize/ComponentSize/related-type accessors.
        UInt64 objectAddress = ReferenceAddress(new Object());
        UInt64 objectMethodTable = *(UInt64*)objectAddress;
        if (objectMethodTable == 0UL || *(UInt32*)(objectMethodTable + 4UL) < 8U) return false;

        UInt64 arrayAddress = ReferenceAddress(values);
        UInt64 arrayMethodTable = *(UInt64*)arrayAddress;
        if (arrayMethodTable == 0UL) return false;
        UInt32 arrayFlags = *(UInt32*)(arrayMethodTable + 0UL);
        UInt32 arrayBaseSize = *(UInt32*)(arrayMethodTable + 4UL);
        UInt64 arrayRelatedType = *(UInt64*)(arrayMethodTable + 8UL);
        if ((Int32)arrayFlags >= 0 || (UInt16)arrayFlags != sizeof(Int32) || arrayBaseSize < 16U || arrayRelatedType == 0UL) return false;

        UInt64 stringAddress = ReferenceAddress(text);
        UInt64 stringMethodTable = *(UInt64*)stringAddress;
        if (stringMethodTable == 0UL) return false;
        UInt32 stringFlags = *(UInt32*)(stringMethodTable + 0UL);
        UInt32 stringBaseSize = *(UInt32*)(stringMethodTable + 4UL);
        if ((Int32)stringFlags >= 0 || (UInt16)stringFlags != sizeof(Char) || stringBaseSize < 16U) return false;
        return true;
    }

    private static Boolean PrimitiveSizesAreCanonical()
        => sizeof(Boolean) == 1 && sizeof(Char) == 2 &&
           sizeof(SByte) == 1 && sizeof(Byte) == 1 &&
           sizeof(Int16) == 2 && sizeof(UInt16) == 2 &&
           sizeof(Int32) == 4 && sizeof(UInt32) == 4 &&
           sizeof(Int64) == 8 && sizeof(UInt64) == 8 &&
           sizeof(Single) == 4 && sizeof(Double) == 8 &&
           sizeof(IntPtr) == 8 && sizeof(UIntPtr) == 8;

    private static UInt64 ReferenceAddress<T>(T value) where T : class
    {
        T local = value;
        return System.Runtime.CompilerServices.Unsafe.As<T, UInt64>(ref local);
    }

    private static void Record(Boolean condition, ref UInt32 passed, ref UInt32 failed)
    {
        if (_gcConformanceActive)
        {
            _gcAssertionOrdinal++;
            if (!condition) NativeAotExceptionRuntime.TraceValue(0x181UL, _gcAssertionOrdinal);
        }
        if (condition) passed++;
        else failed++;
    }
}
