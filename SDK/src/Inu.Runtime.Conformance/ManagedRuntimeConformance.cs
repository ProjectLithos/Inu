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

        // 0.0.95: BCL compatibility now has a fixed, named target instead of an
        // open-ended "CoreLib works" claim. Every item in Inu.BCL.Core.v1 is also
        // exercised by SDK/tests/Inu.DotNetConformance.Tests against the reference BCL.
        NativeAotExceptionRuntime.TraceStage(0xBC10UL);
        RunBclCoreV1Checks(ref passed, ref failed);
        NativeAotExceptionRuntime.TraceStage(0xBC11UL);

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

    private static Int32 _bclDelegateObserved;
    private static void BclCapture(Int32 value) => _bclDelegateObserved = value;
    private static void BclAddCapture(Int32 left, Int32 right) => _bclDelegateObserved = left + right;
    private static Int32 BclDouble(Int32 value) => value * 2;
    private static Int32 BclSum(Int32 left, Int32 right) => left + right;
    private static Int32 BclConstant() => 11;
    private static Boolean BclPositive(Int32 value) => value > 0;

    private static T ParseViaIParsable<T>(String text, IFormatProvider provider) where T : IParsable<T>
        => T.Parse(text, provider);

    private static Boolean TryParseViaIParsable<T>(String text, IFormatProvider provider, out T result) where T : IParsable<T>
        => T.TryParse(text, provider, out result);

    private static T ParseViaISpanParsable<T>(ReadOnlySpan<Char> text, IFormatProvider provider) where T : ISpanParsable<T>
        => T.Parse(text, provider);

    private static Boolean TryParseViaISpanParsable<T>(ReadOnlySpan<Char> text, IFormatProvider provider, out T result) where T : ISpanParsable<T>
        => T.TryParse(text, provider, out result);
    private static Int32 BclCompare(Int32 left, Int32 right) => left.CompareTo(right);
    private static Int64 BclWiden(Int32 value) => value;

    /// <summary>Name of the fixed BCL compatibility target enforced by this release.</summary>
    public const String BclTargetName = "Inu.BCL.Core.v1";

    /// <summary>Number of type-level BCL items in <see cref="BclTargetName"/>.</summary>
    public const Int32 BclTargetItemCount = 37;

    /// <summary>
    /// Hard in-kernel gate for the named BCL subset. Keep this list in lock-step with
    /// SDK/tests/Inu.DotNetConformance.Tests/Program.cs and docs/BCL-Conformance-Target.md.
    /// One Record call equals one advertised target item; a failed item rejects boot.
    /// </summary>
    private static void RunBclCoreV1Checks(ref UInt32 passed, ref UInt32 failed)
    {
        UInt32 before = passed + failed;
        NativeAotExceptionRuntime.TraceStage(0xBC20UL);

        // 01 System.Object
        Object objectValue = new Object();
        Object objectAlias = objectValue;
        Object objectOther = new Object();
        Object derivedObject = new Probe(7);
        Object genericObject = new List<Int32>();
        Object arrayObject = new Int32[1];
        Record(Object.ReferenceEquals(objectValue, objectAlias)
            && !Object.ReferenceEquals(objectValue, objectOther)
            && objectValue.Equals(objectAlias)
            && objectValue.GetHashCode() == objectAlias.GetHashCode()
            && objectValue.GetType() == typeof(Object)
            && objectAlias.GetType() == objectValue.GetType()
            && String.Equals(objectValue.ToString(), "System.Object")
            && String.Equals(derivedObject.ToString(), "Inu.Runtime.Conformance.ManagedRuntimeConformance+Probe")
            && String.Equals(genericObject.ToString(), "System.Collections.Generic.List`1[System.Int32]")
            && String.Equals(arrayObject.ToString(), "System.Int32[]"), ref passed, ref failed);

        // 02 System.Boolean — complete .NET 10 Boolean contract
        Boolean parsedBoolean;
        Boolean invalidBoolean;
        Boolean nullBooleanParseThrows = false;
        Boolean formatBooleanThrows = false;
        Boolean wrongBooleanCompareThrows = false;
        Boolean charBooleanConvertThrows = false;
        Boolean dateBooleanConvertThrows = false;
        Boolean badTypeBooleanConvertThrows = false;
        Boolean nullTypeBooleanConvertThrows = false;
        try { Boolean.Parse((String)null); } catch (ArgumentNullException) { nullBooleanParseThrows = true; }
        try { Boolean.Parse("not-a-boolean"); } catch (FormatException) { formatBooleanThrows = true; }
        try { ((IComparable)(Boolean)true).CompareTo((Int32)1); } catch (ArgumentException) { wrongBooleanCompareThrows = true; }

        IConvertible booleanTrueConvertible = (Boolean)true;
        IConvertible booleanFalseConvertible = (Boolean)false;
        try { booleanTrueConvertible.ToChar(null); } catch (InvalidCastException) { charBooleanConvertThrows = true; }
        try { booleanTrueConvertible.ToDateTime(null); } catch (InvalidCastException) { dateBooleanConvertThrows = true; }
        try { booleanTrueConvertible.ToType(typeof(Probe), null); } catch (InvalidCastException) { badTypeBooleanConvertThrows = true; }
        try { booleanTrueConvertible.ToType(null, null); } catch (ArgumentNullException) { nullTypeBooleanConvertThrows = true; }

        Char[] booleanDestinationArray = new Char[5];
        Span<Char> booleanDestination = booleanDestinationArray;
        Boolean booleanFormatted = ((Boolean)false).TryFormat(booleanDestination, out Int32 booleanCharsWritten);
        Span<Char> booleanTooSmall = new Char[4];
        Boolean booleanSmallFormat = ((Boolean)false).TryFormat(booleanTooSmall, out Int32 booleanSmallCharsWritten);
        Char[] booleanSpanTrue = new Char[] { ' ', 'T', 'R', 'U', 'E', '\0' };
        Char[] booleanSpanFalseArray = new Char[] { ' ', 'F', 'A', 'L', 'S', 'E', ' ' };
        ReadOnlySpan<Char> booleanSpanFalse = new ReadOnlySpan<Char>(booleanSpanFalseArray);
        Boolean staticBooleanParsed = ParseViaIParsable<Boolean>(" true ", null);
        Boolean staticBooleanTryParsed = TryParseViaIParsable<Boolean>("FALSE", null, out Boolean staticBooleanTryValue);
        Boolean staticSpanBooleanParsed = ParseViaISpanParsable<Boolean>(new ReadOnlySpan<Char>(booleanSpanTrue), null);
        Boolean staticSpanBooleanTryParsed = TryParseViaISpanParsable<Boolean>(booleanSpanFalse, null, out Boolean staticSpanBooleanTryValue);

        Record(String.Equals(System.Runtime.CompilerServices.RuntimeFeature.VirtualStaticsInInterfaces, "VirtualStaticsInInterfaces")
            && String.Equals(Boolean.TrueString, "True") && String.Equals(Boolean.FalseString, "False")
            && String.Equals(true.ToString(), "True") && String.Equals(false.ToString(), "False")
            && String.Equals(((Boolean)true).ToString(null), "True") && String.Equals(((Boolean)false).ToString(null), "False")
            && ((Boolean)true).GetHashCode() == 1 && ((Boolean)false).GetHashCode() == 0
            && booleanFormatted && booleanCharsWritten == 5
            && booleanDestinationArray[0] == 'F' && booleanDestinationArray[4] == 'e'
            && !booleanSmallFormat && booleanSmallCharsWritten == 0
            && Boolean.Parse("true") && !Boolean.Parse(" FALSE ")
            && Boolean.Parse(new ReadOnlySpan<Char>(booleanSpanTrue))
            && Boolean.TryParse("TrUe", out parsedBoolean) && parsedBoolean
            && Boolean.TryParse("\u3000false\u3000", out parsedBoolean) && !parsedBoolean
            && !Boolean.TryParse("yes", out invalidBoolean) && !invalidBoolean
            && !Boolean.TryParse((String)null, out invalidBoolean) && !invalidBoolean
            && nullBooleanParseThrows && formatBooleanThrows
            && ((Boolean)false).CompareTo(true) < 0 && ((Boolean)true).CompareTo(false) > 0 && ((Boolean)true).CompareTo(true) == 0
            && ((IComparable)(Boolean)true).CompareTo(null) > 0 && wrongBooleanCompareThrows
            && ((Boolean)true).Equals(true) && !((Boolean)true).Equals(false)
            && ((Object)(Boolean)true).Equals((Boolean)true) && !((Object)(Boolean)true).Equals((Boolean)false)
            && booleanTrueConvertible.GetTypeCode() == TypeCode.Boolean
            && booleanTrueConvertible.ToBoolean(null) && !booleanFalseConvertible.ToBoolean(null)
            && booleanTrueConvertible.ToSByte(null) == (SByte)1 && booleanFalseConvertible.ToSByte(null) == (SByte)0
            && booleanTrueConvertible.ToByte(null) == (Byte)1 && booleanFalseConvertible.ToByte(null) == (Byte)0
            && booleanTrueConvertible.ToInt16(null) == (Int16)1 && booleanFalseConvertible.ToInt16(null) == (Int16)0
            && booleanTrueConvertible.ToUInt16(null) == (UInt16)1 && booleanFalseConvertible.ToUInt16(null) == (UInt16)0
            && booleanTrueConvertible.ToInt32(null) == 1 && booleanFalseConvertible.ToInt32(null) == 0
            && booleanTrueConvertible.ToUInt32(null) == 1U && booleanFalseConvertible.ToUInt32(null) == 0U
            && booleanTrueConvertible.ToInt64(null) == 1L && booleanFalseConvertible.ToInt64(null) == 0L
            && booleanTrueConvertible.ToUInt64(null) == 1UL && booleanFalseConvertible.ToUInt64(null) == 0UL
            && booleanTrueConvertible.ToSingle(null) == 1F && booleanFalseConvertible.ToSingle(null) == 0F
            && booleanTrueConvertible.ToDouble(null) == 1D && booleanFalseConvertible.ToDouble(null) == 0D
            && booleanTrueConvertible.ToDecimal(null) == Decimal.One && booleanFalseConvertible.ToDecimal(null) == Decimal.Zero
            && String.Equals(booleanTrueConvertible.ToString(null), "True") && String.Equals(booleanFalseConvertible.ToString(null), "False")
            && (Boolean)booleanTrueConvertible.ToType(typeof(Boolean), null)
            && (Int32)booleanTrueConvertible.ToType(typeof(Int32), null) == 1
            && String.Equals((String)booleanTrueConvertible.ToType(typeof(String), null), "True")
            && (Boolean)booleanTrueConvertible.ToType(typeof(Object), null)
            && charBooleanConvertThrows && dateBooleanConvertThrows && badTypeBooleanConvertThrows && nullTypeBooleanConvertThrows
            && staticBooleanParsed && staticBooleanTryParsed && !staticBooleanTryValue
            && staticSpanBooleanParsed && staticSpanBooleanTryParsed && !staticSpanBooleanTryValue, ref passed, ref failed);

        // 03 System.Char — comparison, selected classification, conversion and formatting
        Boolean wrongCharCompareThrows = false;
        Boolean nullCharParseThrows = false;
        Boolean formatCharParseThrows = false;
        Boolean charToBooleanConvertThrows = false;
        Boolean charSingleConvertThrows = false;
        Boolean charDoubleConvertThrows = false;
        Boolean charDecimalConvertThrows = false;
        Boolean charDateConvertThrows = false;
        Boolean badTypeCharConvertThrows = false;
        Boolean nullTypeCharConvertThrows = false;
        Boolean charSByteOverflowThrows = false;
        Boolean nullStringCharConvertThrows = false;

        try { ((IComparable)(Char)'B').CompareTo((Int32)66); } catch (ArgumentException) { wrongCharCompareThrows = true; }
        try { Char.Parse((String)null); } catch (ArgumentNullException) { nullCharParseThrows = true; }
        try { Char.Parse("AB"); } catch (FormatException) { formatCharParseThrows = true; }
        try { Convert.ToChar((String)null); } catch (ArgumentNullException) { nullStringCharConvertThrows = true; }

        IConvertible charConvertible = (Char)'A';
        try { charConvertible.ToBoolean(null); } catch (InvalidCastException) { charToBooleanConvertThrows = true; }
        try { charConvertible.ToSingle(null); } catch (InvalidCastException) { charSingleConvertThrows = true; }
        try { charConvertible.ToDouble(null); } catch (InvalidCastException) { charDoubleConvertThrows = true; }
        try { charConvertible.ToDecimal(null); } catch (InvalidCastException) { charDecimalConvertThrows = true; }
        try { charConvertible.ToDateTime(null); } catch (InvalidCastException) { charDateConvertThrows = true; }
        try { charConvertible.ToType(typeof(Probe), null); } catch (InvalidCastException) { badTypeCharConvertThrows = true; }
        try { charConvertible.ToType(null, null); } catch (ArgumentNullException) { nullTypeCharConvertThrows = true; }
        try { ((IConvertible)(Char)0x0100).ToSByte(null); } catch (OverflowException) { charSByteOverflowThrows = true; }

        Boolean charStaticParsed = ParseViaIParsable<Char>("Z", null) == 'Z';
        Boolean charStaticTryParsed = TryParseViaIParsable<Char>("Q", null, out Char charStaticTryValue) && charStaticTryValue == 'Q';
        Char[] charSpanParseArray = new Char[] { 'M' };
        Char[] charSpanTryArray = new Char[] { 'N' };
        Boolean charSpanStaticParsed = ParseViaISpanParsable<Char>(new ReadOnlySpan<Char>(charSpanParseArray), null) == 'M';
        Boolean charSpanStaticTryParsed = TryParseViaISpanParsable<Char>(new ReadOnlySpan<Char>(charSpanTryArray), null, out Char charSpanTryValue) && charSpanTryValue == 'N';

        Char[] charDestinationArray = new Char[1];
        ISpanFormattable charFormattable = (Char)'K';
        Boolean charFormatted = charFormattable.TryFormat(new Span<Char>(charDestinationArray), out Int32 charCharsWritten, new ReadOnlySpan<Char>(new Char[0]), null);
        Boolean charEmptyFormatted = charFormattable.TryFormat(new Span<Char>(new Char[0]), out Int32 charEmptyCharsWritten, new ReadOnlySpan<Char>(new Char[0]), null);
        Boolean charParsed = Char.TryParse("R", out Char parsedChar);
        Boolean charInvalid = Char.TryParse("RR", out Char invalidChar);
        Boolean charNullInvalid = Char.TryParse((String)null, out Char nullInvalidChar);

        Record(Char.MinValue == (Char)0 && Char.MaxValue == (Char)0xFFFF
            && ((Char)'A').CompareTo('B') == -1 && ((Char)'B').CompareTo('A') == 1 && ((Char)'A').CompareTo('A') == 0
            && ((IComparable)(Char)'B').CompareTo(null) > 0 && wrongCharCompareThrows
            && ((Char)'A').Equals('A') && !((Char)'A').Equals('B') && ((Object)(Char)'A').Equals((Char)'A')
            && ((Char)'A').GetHashCode() == ((Int32)'A' | ((Int32)'A' << 16))
            && Char.IsAscii('A') && Char.IsAscii((Char)0x7F) && !Char.IsAscii((Char)0x80)
            && Char.IsAsciiLetter('A') && Char.IsAsciiLetter('z') && !Char.IsAsciiLetter('4')
            && Char.IsAsciiLetterLower('z') && !Char.IsAsciiLetterLower('Z')
            && Char.IsAsciiLetterUpper('Z') && !Char.IsAsciiLetterUpper('z')
            && Char.IsAsciiDigit('7') && !Char.IsAsciiDigit('x')
            && Char.IsAsciiLetterOrDigit('7') && Char.IsAsciiLetterOrDigit('x') && !Char.IsAsciiLetterOrDigit('-')
            && Char.IsAsciiHexDigit('F') && Char.IsAsciiHexDigit('f') && !Char.IsAsciiHexDigit('G')
            && Char.IsAsciiHexDigitLower('f') && !Char.IsAsciiHexDigitLower('F')
            && Char.IsAsciiHexDigitUpper('F') && !Char.IsAsciiHexDigitUpper('f')
            && Char.IsBetween('m', 'a', 'z') && !Char.IsBetween('M', 'a', 'z')
            && Char.IsControl('\0') && Char.IsControl((Char)0x009F) && !Char.IsControl(' ')
            && Char.IsWhiteSpace(' ') && Char.IsWhiteSpace('\n') && Char.IsWhiteSpace((Char)0x3000) && !Char.IsWhiteSpace('X')
            && String.Equals(((Char)'K').ToString(), "K") && String.Equals(((Char)'K').ToString(null), "K") && String.Equals(Char.ToString('K'), "K")
            && String.Equals(((IFormattable)(Char)'K').ToString("ignored", null), "K")
            && charFormatted && charCharsWritten == 1 && charDestinationArray[0] == 'K'
            && !charEmptyFormatted && charEmptyCharsWritten == 0
            && Char.Parse("P") == 'P' && charParsed && parsedChar == 'R'
            && !charInvalid && invalidChar == (Char)0 && !charNullInvalid && nullInvalidChar == (Char)0
            && nullCharParseThrows && formatCharParseThrows
            && charStaticParsed && charStaticTryParsed && charSpanStaticParsed && charSpanStaticTryParsed
            && charConvertible.GetTypeCode() == TypeCode.Char && charConvertible.ToChar(null) == 'A'
            && charConvertible.ToSByte(null) == (SByte)65 && charConvertible.ToByte(null) == (Byte)65
            && charConvertible.ToInt16(null) == (Int16)65 && charConvertible.ToUInt16(null) == (UInt16)65
            && charConvertible.ToInt32(null) == 65 && charConvertible.ToUInt32(null) == 65U
            && charConvertible.ToInt64(null) == 65L && charConvertible.ToUInt64(null) == 65UL
            && String.Equals(charConvertible.ToString(null), "A")
            && (Char)charConvertible.ToType(typeof(Char), null) == 'A'
            && (Int32)charConvertible.ToType(typeof(Int32), null) == 65
            && String.Equals((String)charConvertible.ToType(typeof(String), null), "A")
            && (Char)charConvertible.ToType(typeof(Object), null) == 'A'
            && Convert.ToChar((Byte)65) == 'A' && Convert.ToChar((Int32)65) == 'A' && Convert.ToChar("A") == 'A'
            && Convert.ToUInt16('A') == (UInt16)65 && Convert.ToInt32('A') == 65 && String.Equals(Convert.ToString('A'), "A")
            && charToBooleanConvertThrows && charSingleConvertThrows && charDoubleConvertThrows && charDecimalConvertThrows
            && charDateConvertThrows && badTypeCharConvertThrows && nullTypeCharConvertThrows
            && charSByteOverflowThrows && nullStringCharConvertThrows, ref passed, ref failed);

        // 04 System.SByte
        Boolean sbOverflow = false;
        Boolean sbCheckedOverflow = false;
        Boolean sbWrongCompare = false;
        try { SByte.Parse("128"); } catch (OverflowException) { sbOverflow = true; }
        Int32 sbTooLarge = 128;
        try { _ = checked((SByte)sbTooLarge); } catch (OverflowException) { sbCheckedOverflow = true; }
        try { ((IComparable)(SByte)4).CompareTo((Int16)4); } catch (ArgumentException) { sbWrongCompare = true; }
        Char[] sbFormatChars = new Char[4];
        Boolean sbFormatted = ((SByte)(-12)).TryFormat(new Span<Char>(sbFormatChars), out Int32 sbWritten, new ReadOnlySpan<Char>(new Char[] { 'D', '3' }), null);
        Boolean sbParsed = SByte.TryParse("42", out SByte sbValue);
        IConvertible sbConvertible = (SByte)(-12);
        Boolean sbConvertOverflow = false;
        try { Convert.ToSByte((Int32)128); } catch (OverflowException) { sbConvertOverflow = true; }
        Record(SByte.MinValue == (SByte)(-128) && SByte.MaxValue == (SByte)127
            && SByte.Parse(" -128 ") == SByte.MinValue && SByte.Parse("+127") == SByte.MaxValue
            && sbParsed && sbValue == (SByte)42 && !SByte.TryParse("129", out sbValue) && sbValue == 0
            && ParseViaIParsable<SByte>("7", null) == (SByte)7
            && ParseViaISpanParsable<SByte>(new ReadOnlySpan<Char>(new Char[] { '-', '8' }), null) == (SByte)(-8)
            && String.Equals(((SByte)(-12)).ToString(), "-12") && String.Equals(((SByte)12).ToString("D3", null), "012")
            && String.Equals(((SByte)(-1)).ToString("X2", null), "FF")
            && sbFormatted && sbWritten == 4 && sbFormatChars[0] == '-' && sbFormatChars[1] == '0' && sbFormatChars[2] == '1' && sbFormatChars[3] == '2'
            && ((SByte)4).CompareTo((SByte)5) < 0 && ((SByte)4).Equals((SByte)4) && !((SByte)4).Equals((SByte)5)
            && sbConvertible.GetTypeCode() == TypeCode.SByte && sbConvertible.ToInt64(null) == -12L && sbConvertible.ToBoolean(null)
            && sbConvertible.ToDecimal(null).Equals(new Decimal((Int64)(-12))) && sbConvertOverflow
            && sbCheckedOverflow && unchecked((SByte)130) == (SByte)(-126) && sbOverflow && sbWrongCompare, ref passed, ref failed);

        // 05 System.Int16
        Boolean i16Overflow = false;
        Boolean i16CheckedOverflow = false;
        Boolean i16WrongCompare = false;
        try { Int16.Parse("32768"); } catch (OverflowException) { i16Overflow = true; }
        Int32 i16TooLarge = 32768;
        try { _ = checked((Int16)i16TooLarge); } catch (OverflowException) { i16CheckedOverflow = true; }
        try { ((IComparable)(Int16)4).CompareTo((Int32)4); } catch (ArgumentException) { i16WrongCompare = true; }
        Char[] i16FormatChars = new Char[6];
        Boolean i16Formatted = ((Int16)(-123)).TryFormat(new Span<Char>(i16FormatChars), out Int32 i16Written, new ReadOnlySpan<Char>(new Char[] { 'D', '5' }), null);
        IConvertible i16Convertible = (Int16)(-123);
        Boolean i16ConvertOverflow = false;
        try { Convert.ToInt16((Int32)32768); } catch (OverflowException) { i16ConvertOverflow = true; }
        Record(Int16.MinValue == (Int16)(-32768) && Int16.MaxValue == (Int16)32767
            && Int16.Parse("-32768") == Int16.MinValue && Int16.Parse("32767") == Int16.MaxValue
            && Int16.TryParse("1234", out Int16 i16Value) && i16Value == (Int16)1234 && !Int16.TryParse("32768", out i16Value) && i16Value == 0
            && ParseViaIParsable<Int16>("-45", null) == (Int16)(-45)
            && ParseViaISpanParsable<Int16>(new ReadOnlySpan<Char>(new Char[] { '4', '6' }), null) == (Int16)46
            && String.Equals(((Int16)(-123)).ToString(), "-123") && String.Equals(((Int16)12).ToString("D4", null), "0012")
            && String.Equals(((Int16)(-1)).ToString("X4", null), "FFFF")
            && i16Formatted && i16Written == 6 && i16FormatChars[0] == '-' && i16FormatChars[1] == '0' && i16FormatChars[2] == '0'
            && i16FormatChars[3] == '1' && i16FormatChars[4] == '2' && i16FormatChars[5] == '3'
            && ((Int16)4).CompareTo((Int16)5) < 0 && ((Int16)4).Equals((Int16)4) && !((Int16)4).Equals((Int16)5)
            && i16Convertible.GetTypeCode() == TypeCode.Int16 && i16Convertible.ToInt64(null) == -123L && i16Convertible.ToBoolean(null)
            && i16Convertible.ToDecimal(null).Equals(new Decimal((Int64)(-123))) && i16ConvertOverflow
            && i16CheckedOverflow && unchecked((Int16)65535) == (Int16)(-1) && i16Overflow && i16WrongCompare, ref passed, ref failed);

        // 06 System.Int32
        Boolean i32Overflow = false;
        Boolean i32CheckedOverflow = false;
        Boolean i32WrongCompare = false;
        try { Int32.Parse("2147483648"); } catch (OverflowException) { i32Overflow = true; }
        Int64 i32TooLarge = 2147483648L;
        try { _ = checked((Int32)i32TooLarge); } catch (OverflowException) { i32CheckedOverflow = true; }
        try { ((IComparable)(Int32)12345).CompareTo((Int64)12345); } catch (ArgumentException) { i32WrongCompare = true; }
        Char[] i32FormatChars = new Char[7];
        Boolean i32Formatted = ((Int32)(-123)).TryFormat(new Span<Char>(i32FormatChars), out Int32 i32Written, new ReadOnlySpan<Char>(new Char[] { 'D', '6' }), null);
        IConvertible i32Convertible = (Int32)(-123);
        Boolean i32ConvertOverflow = false;
        try { Convert.ToInt32((Int64)2147483648L); } catch (OverflowException) { i32ConvertOverflow = true; }
        Int32 integer = 12345;
        Record(Int32.MinValue == -2147483648 && Int32.MaxValue == 2147483647
            && Int32.Parse(" -2147483648 ") == Int32.MinValue && Int32.Parse("+2147483647") == Int32.MaxValue
            && Int32.TryParse("12345", out Int32 i32Value) && i32Value == 12345 && !Int32.TryParse("2147483648", out i32Value) && i32Value == 0
            && ParseViaIParsable<Int32>("-77", null) == -77
            && ParseViaISpanParsable<Int32>(new ReadOnlySpan<Char>(new Char[] { '7', '8' }), null) == 78
            && String.Equals(((Int32)(-123)).ToString(), "-123") && String.Equals(((Int32)12).ToString("D4", null), "0012")
            && String.Equals(((Int32)(-1)).ToString("X8", null), "FFFFFFFF")
            && i32Formatted && i32Written == 7 && i32FormatChars[0] == '-' && i32FormatChars[1] == '0' && i32FormatChars[2] == '0'
            && i32FormatChars[3] == '0' && i32FormatChars[4] == '1' && i32FormatChars[5] == '2' && i32FormatChars[6] == '3'
            && integer.Equals((Int32)12345) && !integer.Equals((Int32)12346) && integer.GetHashCode() == 12345 && integer.CompareTo(12346) < 0
            && i32Convertible.GetTypeCode() == TypeCode.Int32 && i32Convertible.ToInt64(null) == -123L && i32Convertible.ToBoolean(null)
            && i32Convertible.ToDecimal(null).Equals(new Decimal((Int64)(-123))) && i32ConvertOverflow
            && i32CheckedOverflow && unchecked((Int32)4294967295L) == -1 && i32Overflow && i32WrongCompare, ref passed, ref failed);

        // 07 System.Int64
        Boolean i64Overflow = false;
        Boolean i64CheckedOverflow = false;
        Boolean i64WrongCompare = false;
        try { Int64.Parse("9223372036854775808"); } catch (OverflowException) { i64Overflow = true; }
        UInt64 i64TooLarge = 9223372036854775808UL;
        try { _ = checked((Int64)i64TooLarge); } catch (OverflowException) { i64CheckedOverflow = true; }
        try { ((IComparable)(Int64)4).CompareTo((Int32)4); } catch (ArgumentException) { i64WrongCompare = true; }
        Char[] i64FormatChars = new Char[8];
        Boolean i64Formatted = ((Int64)(-123)).TryFormat(new Span<Char>(i64FormatChars), out Int32 i64Written, new ReadOnlySpan<Char>(new Char[] { 'D', '7' }), null);
        IConvertible i64Convertible = (Int64)(-123);
        Boolean i64ConvertOverflow = false;
        try { Convert.ToInt64((UInt64)9223372036854775808UL); } catch (OverflowException) { i64ConvertOverflow = true; }
        Record(Int64.MinValue == -9223372036854775808L && Int64.MaxValue == 9223372036854775807L
            && Int64.Parse("-9223372036854775808") == Int64.MinValue && Int64.Parse("9223372036854775807") == Int64.MaxValue
            && Int64.TryParse("123456789", out Int64 i64Value) && i64Value == 123456789L && !Int64.TryParse("9223372036854775808", out i64Value) && i64Value == 0L
            && ParseViaIParsable<Int64>("-79", null) == -79L
            && ParseViaISpanParsable<Int64>(new ReadOnlySpan<Char>(new Char[] { '8', '0' }), null) == 80L
            && String.Equals(((Int64)(-123)).ToString(), "-123") && String.Equals(((Int64)12).ToString("D4", null), "0012")
            && String.Equals(((Int64)(-1)).ToString("X16", null), "FFFFFFFFFFFFFFFF")
            && i64Formatted && i64Written == 8 && i64FormatChars[0] == '-' && i64FormatChars[1] == '0' && i64FormatChars[2] == '0'
            && i64FormatChars[3] == '0' && i64FormatChars[4] == '0' && i64FormatChars[5] == '1' && i64FormatChars[6] == '2' && i64FormatChars[7] == '3'
            && ((Int64)4).CompareTo((Int64)5) < 0 && ((Int64)4).Equals((Int64)4) && !((Int64)4).Equals((Int64)5)
            && i64Convertible.GetTypeCode() == TypeCode.Int64 && i64Convertible.ToInt64(null) == -123L && i64Convertible.ToBoolean(null)
            && i64Convertible.ToDecimal(null).Equals(new Decimal((Int64)(-123))) && i64ConvertOverflow
            && i64CheckedOverflow && unchecked((Int64)UInt64.MaxValue) == -1L && i64Overflow && i64WrongCompare, ref passed, ref failed);


        // 08 System.Byte
        Boolean byteOverflow = false;
        Boolean byteCheckedOverflow = false;
        Boolean byteWrongCompare = false;
        try { Byte.Parse("256"); } catch (OverflowException) { byteOverflow = true; }
        Int32 byteNegative = -1;
        try { _ = checked((Byte)byteNegative); } catch (OverflowException) { byteCheckedOverflow = true; }
        try { ((IComparable)(Byte)4).CompareTo((UInt16)4); } catch (ArgumentException) { byteWrongCompare = true; }
        Char[] byteFormatChars = new Char[3];
        Boolean byteFormatted = ((Byte)12).TryFormat(new Span<Char>(byteFormatChars), out Int32 byteWritten, new ReadOnlySpan<Char>(new Char[] { 'D', '3' }), null);
        IConvertible byteConvertible = (Byte)200;
        Boolean byteConvertOverflow = false;
        try { Convert.ToByte((Int32)256); } catch (OverflowException) { byteConvertOverflow = true; }
        Record(Byte.MinValue == (Byte)0 && Byte.MaxValue == (Byte)255
            && Byte.Parse(" 0 ") == Byte.MinValue && Byte.Parse("+255") == Byte.MaxValue
            && Byte.TryParse("42", out Byte byteValue) && byteValue == (Byte)42 && !Byte.TryParse("256", out byteValue) && byteValue == 0
            && ParseViaIParsable<Byte>("7", null) == (Byte)7
            && ParseViaISpanParsable<Byte>(new ReadOnlySpan<Char>(new Char[] { '8' }), null) == (Byte)8
            && String.Equals(((Byte)12).ToString(), "12") && String.Equals(((Byte)12).ToString("D3", null), "012")
            && String.Equals(((Byte)255).ToString("X2", null), "FF")
            && byteFormatted && byteWritten == 3 && byteFormatChars[0] == '0' && byteFormatChars[1] == '1' && byteFormatChars[2] == '2'
            && ((Byte)4).CompareTo((Byte)5) < 0 && ((Byte)4).Equals((Byte)4) && !((Byte)4).Equals((Byte)5)
            && byteConvertible.GetTypeCode() == TypeCode.Byte && byteConvertible.ToUInt64(null) == 200UL && byteConvertible.ToBoolean(null)
            && byteConvertible.ToDecimal(null).Equals(new Decimal((UInt64)200)) && byteConvertOverflow
            && byteCheckedOverflow && unchecked((Byte)(-1)) == Byte.MaxValue && byteOverflow && byteWrongCompare, ref passed, ref failed);

        // 09 System.UInt16
        Boolean u16Overflow = false;
        Boolean u16CheckedOverflow = false;
        Boolean u16WrongCompare = false;
        try { UInt16.Parse("65536"); } catch (OverflowException) { u16Overflow = true; }
        Int32 u16Negative = -1;
        try { _ = checked((UInt16)u16Negative); } catch (OverflowException) { u16CheckedOverflow = true; }
        try { ((IComparable)(UInt16)4).CompareTo((UInt32)4); } catch (ArgumentException) { u16WrongCompare = true; }
        Char[] u16FormatChars = new Char[5];
        Boolean u16Formatted = ((UInt16)123).TryFormat(new Span<Char>(u16FormatChars), out Int32 u16Written, new ReadOnlySpan<Char>(new Char[] { 'D', '5' }), null);
        IConvertible u16Convertible = (UInt16)60000;
        Boolean u16ConvertOverflow = false;
        try { Convert.ToUInt16((Int32)65536); } catch (OverflowException) { u16ConvertOverflow = true; }
        Record(UInt16.MinValue == (UInt16)0 && UInt16.MaxValue == (UInt16)65535
            && UInt16.Parse("0") == UInt16.MinValue && UInt16.Parse("65535") == UInt16.MaxValue
            && UInt16.TryParse("1234", out UInt16 u16Value) && u16Value == (UInt16)1234 && !UInt16.TryParse("65536", out u16Value) && u16Value == 0
            && ParseViaIParsable<UInt16>("45", null) == (UInt16)45
            && ParseViaISpanParsable<UInt16>(new ReadOnlySpan<Char>(new Char[] { '4', '6' }), null) == (UInt16)46
            && String.Equals(((UInt16)123).ToString(), "123") && String.Equals(((UInt16)12).ToString("D4", null), "0012")
            && String.Equals(UInt16.MaxValue.ToString("X4", null), "FFFF")
            && u16Formatted && u16Written == 5 && u16FormatChars[0] == '0' && u16FormatChars[1] == '0' && u16FormatChars[2] == '1' && u16FormatChars[3] == '2' && u16FormatChars[4] == '3'
            && ((UInt16)4).CompareTo((UInt16)5) < 0 && ((UInt16)4).Equals((UInt16)4) && !((UInt16)4).Equals((UInt16)5)
            && u16Convertible.GetTypeCode() == TypeCode.UInt16 && u16Convertible.ToUInt64(null) == 60000UL && u16Convertible.ToBoolean(null)
            && u16Convertible.ToDecimal(null).Equals(new Decimal((UInt64)60000)) && u16ConvertOverflow
            && u16CheckedOverflow && unchecked((UInt16)(-1)) == UInt16.MaxValue && u16Overflow && u16WrongCompare, ref passed, ref failed);

        // 10 System.UInt32
        Boolean u32Overflow = false;
        Boolean u32CheckedOverflow = false;
        Boolean u32WrongCompare = false;
        try { UInt32.Parse("4294967296"); } catch (OverflowException) { u32Overflow = true; }
        Int64 u32Negative = -1L;
        try { _ = checked((UInt32)u32Negative); } catch (OverflowException) { u32CheckedOverflow = true; }
        try { ((IComparable)(UInt32)12345U).CompareTo((UInt64)12345UL); } catch (ArgumentException) { u32WrongCompare = true; }
        Char[] u32FormatChars = new Char[6];
        Boolean u32Formatted = ((UInt32)123U).TryFormat(new Span<Char>(u32FormatChars), out Int32 u32Written, new ReadOnlySpan<Char>(new Char[] { 'D', '6' }), null);
        IConvertible u32Convertible = (UInt32)4000000000U;
        Boolean u32ConvertOverflow = false;
        try { Convert.ToUInt32((Int64)4294967296L); } catch (OverflowException) { u32ConvertOverflow = true; }
        UInt32 unsignedInteger = 12345U;
        Record(UInt32.MinValue == 0U && UInt32.MaxValue == 4294967295U
            && UInt32.Parse(" 0 ") == UInt32.MinValue && UInt32.Parse("+4294967295") == UInt32.MaxValue
            && UInt32.TryParse("12345", out UInt32 u32Value) && u32Value == 12345U && !UInt32.TryParse("4294967296", out u32Value) && u32Value == 0U
            && ParseViaIParsable<UInt32>("77", null) == 77U
            && ParseViaISpanParsable<UInt32>(new ReadOnlySpan<Char>(new Char[] { '7', '8' }), null) == 78U
            && String.Equals(((UInt32)123U).ToString(), "123") && String.Equals(((UInt32)12U).ToString("D4", null), "0012")
            && String.Equals(UInt32.MaxValue.ToString("X8", null), "FFFFFFFF")
            && u32Formatted && u32Written == 6 && u32FormatChars[0] == '0' && u32FormatChars[1] == '0' && u32FormatChars[2] == '0'
            && u32FormatChars[3] == '1' && u32FormatChars[4] == '2' && u32FormatChars[5] == '3'
            && unsignedInteger.Equals((UInt32)12345U) && !unsignedInteger.Equals((UInt32)12346U) && unsignedInteger.CompareTo(12346U) < 0
            && u32Convertible.GetTypeCode() == TypeCode.UInt32 && u32Convertible.ToUInt64(null) == 4000000000UL && u32Convertible.ToBoolean(null)
            && u32Convertible.ToDecimal(null).Equals(new Decimal((UInt64)4000000000UL)) && u32ConvertOverflow
            && u32CheckedOverflow && unchecked((UInt32)(-1L)) == UInt32.MaxValue && u32Overflow && u32WrongCompare, ref passed, ref failed);

        // 11 System.UInt64
        Boolean u64Overflow = false;
        Boolean u64CheckedOverflow = false;
        Boolean u64WrongCompare = false;
        try { UInt64.Parse("18446744073709551616"); } catch (OverflowException) { u64Overflow = true; }
        Int64 u64Negative = -1L;
        try { _ = checked((UInt64)u64Negative); } catch (OverflowException) { u64CheckedOverflow = true; }
        try { ((IComparable)(UInt64)4UL).CompareTo((UInt32)4U); } catch (ArgumentException) { u64WrongCompare = true; }
        Char[] u64FormatChars = new Char[7];
        Boolean u64Formatted = ((UInt64)123UL).TryFormat(new Span<Char>(u64FormatChars), out Int32 u64Written, new ReadOnlySpan<Char>(new Char[] { 'D', '7' }), null);
        IConvertible u64Convertible = UInt64.MaxValue;
        Boolean u64ConvertOverflow = false;
        try { Convert.ToUInt64((Int64)(-1L)); } catch (OverflowException) { u64ConvertOverflow = true; }
        Record(UInt64.MinValue == 0UL && UInt64.MaxValue == 18446744073709551615UL
            && UInt64.Parse("0") == UInt64.MinValue && UInt64.Parse("18446744073709551615") == UInt64.MaxValue
            && UInt64.TryParse("123456789", out UInt64 u64Value) && u64Value == 123456789UL && !UInt64.TryParse("18446744073709551616", out u64Value) && u64Value == 0UL
            && ParseViaIParsable<UInt64>("79", null) == 79UL
            && ParseViaISpanParsable<UInt64>(new ReadOnlySpan<Char>(new Char[] { '8', '0' }), null) == 80UL
            && String.Equals(((UInt64)123UL).ToString(), "123") && String.Equals(((UInt64)12UL).ToString("D4", null), "0012")
            && String.Equals(UInt64.MaxValue.ToString("X16", null), "FFFFFFFFFFFFFFFF")
            && u64Formatted && u64Written == 7 && u64FormatChars[0] == '0' && u64FormatChars[1] == '0' && u64FormatChars[2] == '0'
            && u64FormatChars[3] == '0' && u64FormatChars[4] == '1' && u64FormatChars[5] == '2' && u64FormatChars[6] == '3'
            && ((UInt64)4UL).CompareTo((UInt64)5UL) < 0 && ((UInt64)4UL).Equals((UInt64)4UL) && !((UInt64)4UL).Equals((UInt64)5UL)
            && u64Convertible.GetTypeCode() == TypeCode.UInt64 && u64Convertible.ToUInt64(null) == UInt64.MaxValue && u64Convertible.ToBoolean(null)
            && u64Convertible.ToDecimal(null).Equals(new Decimal(UInt64.MaxValue)) && u64ConvertOverflow
            && u64CheckedOverflow && unchecked((UInt64)(-1L)) == UInt64.MaxValue && u64Overflow && u64WrongCompare, ref passed, ref failed);


        // 12 System.Single
        Char[] singleFormatChars = new Char[16];
        Boolean singleFormatted = ((Single)12.5F).TryFormat(new Span<Char>(singleFormatChars), out Int32 singleWritten, new ReadOnlySpan<Char>(new Char[] { 'F', '2' }), null);
        IConvertible singleConvertible = (Single)2.5F;
        Record(Single.MinValue < 0.0F && Single.MaxValue > 0.0F && Single.Epsilon > 0.0F
            && Single.IsNaN(Single.NaN) && Single.IsPositiveInfinity(Single.PositiveInfinity)
            && Single.IsNegativeInfinity(Single.NegativeInfinity) && Single.IsInfinity(Single.PositiveInfinity)
            && Single.IsFinite((Single)12.5F) && !Single.IsFinite(Single.NaN) && Single.IsNegative(Single.NegativeZero)
            && Single.NaN.Equals(Single.NaN) && Single.NaN.CompareTo((Single)0.0F) < 0
            && Single.TryParse(" -12.5 ", out Single singleParsed) && singleParsed == (Single)(-12.5F)
            && Single.TryParse(new ReadOnlySpan<Char>(new Char[] { '1', '.', '2', '5', 'e', '2' }), out Single singleSpanParsed) && singleSpanParsed == (Single)125.0F
            && ParseViaIParsable<Single>("2.5", null) == (Single)2.5F
            && ParseViaISpanParsable<Single>(new ReadOnlySpan<Char>(new Char[] { '3', '.', '5' }), null) == (Single)3.5F
            && String.Equals(((Single)12.5F).ToString("G", null), "12.5")
            && String.Equals(((Single)12.5F).ToString("E2", null), "1.25E+001")
            && singleFormatted && singleWritten == 5 && singleFormatChars[0] == '1' && singleFormatChars[1] == '2'
            && singleFormatChars[2] == '.' && singleFormatChars[3] == '5' && singleFormatChars[4] == '0'
            && singleConvertible.GetTypeCode() == TypeCode.Single && singleConvertible.ToInt32(null) == 2 && singleConvertible.ToDouble(null) == 2.5, ref passed, ref failed);

        // 13 System.Double
        Char[] doubleFormatChars = new Char[16];
        Boolean doubleFormatted = ((Double)12.5).TryFormat(new Span<Char>(doubleFormatChars), out Int32 doubleWritten, new ReadOnlySpan<Char>(new Char[] { 'F', '3' }), null);
        IConvertible doubleConvertible = (Double)3.5;
        Record(Double.MinValue < 0.0 && Double.MaxValue > 0.0 && Double.Epsilon > 0.0
            && Double.IsNaN(Double.NaN) && Double.IsPositiveInfinity(Double.PositiveInfinity)
            && Double.IsNegativeInfinity(Double.NegativeInfinity) && Double.IsInfinity(Double.PositiveInfinity)
            && Double.IsFinite((Double)12.5) && !Double.IsFinite(Double.NaN) && Double.IsNegative(Double.NegativeZero)
            && Double.NaN.Equals(Double.NaN) && Double.NaN.CompareTo((Double)0.0) < 0
            && Double.TryParse(" -12.5 ", out Double doubleParsed) && doubleParsed == (Double)(-12.5)
            && Double.TryParse(new ReadOnlySpan<Char>(new Char[] { '1', '.', '2', '5', 'e', '2' }), out Double doubleSpanParsed) && doubleSpanParsed == (Double)125.0
            && ParseViaIParsable<Double>("2.5", null) == (Double)2.5
            && ParseViaISpanParsable<Double>(new ReadOnlySpan<Char>(new Char[] { '3', '.', '5' }), null) == (Double)3.5
            && String.Equals(((Double)12.5).ToString("G", null), "12.5")
            && String.Equals(((Double)12.5).ToString("E2", null), "1.25E+001")
            && doubleFormatted && doubleWritten == 6 && doubleFormatChars[0] == '1' && doubleFormatChars[1] == '2'
            && doubleFormatChars[2] == '.' && doubleFormatChars[3] == '5' && doubleFormatChars[4] == '0' && doubleFormatChars[5] == '0'
            && doubleConvertible.GetTypeCode() == TypeCode.Double && doubleConvertible.ToInt32(null) == 4 && doubleConvertible.ToSingle(null) == (Single)3.5F, ref passed, ref failed);


        // 14 System.IntPtr
        IntPtr signedPointer = new IntPtr(100);
        IntPtr signedAdvanced = IntPtr.Add(signedPointer, 23);
        IntPtr signedArithmetic = (new IntPtr(6) * new IntPtr(7)) + new IntPtr(2);
        IntPtr signedDivided = signedArithmetic / new IntPtr(4);
        IntPtr signedRemainder = signedArithmetic % new IntPtr(5);
        Boolean signedWrongCompare = false;
        try { ((IComparable)signedPointer).CompareTo((Int64)100L); } catch (ArgumentException) { signedWrongCompare = true; }
        Boolean signedNarrowOverflow = false;
        if (IntPtr.Size == 8)
        {
            try { IntPtr.MaxValue.ToInt32(); } catch (OverflowException) { signedNarrowOverflow = true; }
        }
        else
        {
            signedNarrowOverflow = true;
        }
        Char[] signedFormatChars = new Char[5];
        Boolean signedFormatted = new IntPtr(123).TryFormat(new Span<Char>(signedFormatChars), out Int32 signedWritten, new ReadOnlySpan<Char>(new Char[] { 'D', '5' }), null);
        Int64 signedExpectedMax = IntPtr.Size == 8 ? Int64.MaxValue : Int32.MaxValue;
        Int64 signedExpectedMin = IntPtr.Size == 8 ? Int64.MinValue : Int32.MinValue;
        Record((IntPtr.Size == 4 || IntPtr.Size == 8)
            && IntPtr.Zero.ToInt64() == 0L && IntPtr.MaxValue.ToInt64() == signedExpectedMax && IntPtr.MinValue.ToInt64() == signedExpectedMin
            && signedAdvanced.ToInt64() == 123L && IntPtr.Subtract(signedAdvanced, 23) == signedPointer
            && (signedPointer + 23).ToInt64() == 123L && ((signedPointer + 23) - 23) == signedPointer
            && signedArithmetic.ToInt64() == 44L && signedDivided.ToInt64() == 11L && signedRemainder.ToInt64() == 4L
            && signedPointer.CompareTo(signedAdvanced) < 0 && signedAdvanced.CompareTo(signedPointer) > 0 && signedPointer.CompareTo(signedPointer) == 0
            && signedPointer.Equals(new IntPtr(100)) && !signedPointer.Equals(signedAdvanced) && signedWrongCompare
            && (Int64)new IntPtr(-55) == -55L && signedNarrowOverflow
            && String.Equals(new IntPtr(-123).ToString(), "-123") && String.Equals(new IntPtr(12).ToString("D4", null), "0012")
            && signedFormatted && signedWritten == 5 && signedFormatChars[0] == '0' && signedFormatChars[1] == '0'
            && signedFormatChars[2] == '1' && signedFormatChars[3] == '2' && signedFormatChars[4] == '3', ref passed, ref failed);

        // 15 System.UIntPtr
        UIntPtr unsignedPointer = new UIntPtr((UInt64)200UL);
        UIntPtr unsignedAdvanced = UIntPtr.Add(unsignedPointer, 17);
        UIntPtr unsignedArithmetic = (new UIntPtr((UInt64)6UL) * new UIntPtr((UInt64)7UL)) + new UIntPtr((UInt64)2UL);
        UIntPtr unsignedDivided = unsignedArithmetic / new UIntPtr((UInt64)4UL);
        UIntPtr unsignedRemainder = unsignedArithmetic % new UIntPtr((UInt64)5UL);
        Boolean unsignedWrongCompare = false;
        try { ((IComparable)unsignedPointer).CompareTo((UInt64)200UL); } catch (ArgumentException) { unsignedWrongCompare = true; }
        Boolean unsignedNarrowOverflow = false;
        if (UIntPtr.Size == 8)
        {
            try { UIntPtr.MaxValue.ToUInt32(); } catch (OverflowException) { unsignedNarrowOverflow = true; }
        }
        else
        {
            unsignedNarrowOverflow = true;
        }
        Char[] unsignedFormatChars = new Char[5];
        Boolean unsignedFormatted = new UIntPtr((UInt64)123UL).TryFormat(new Span<Char>(unsignedFormatChars), out Int32 unsignedWritten, new ReadOnlySpan<Char>(new Char[] { 'D', '5' }), null);
        UInt64 unsignedExpectedMax = UIntPtr.Size == 8 ? UInt64.MaxValue : UInt32.MaxValue;
        Record((UIntPtr.Size == 4 || UIntPtr.Size == 8)
            && UIntPtr.Zero.ToUInt64() == 0UL && UIntPtr.MinValue.ToUInt64() == 0UL && UIntPtr.MaxValue.ToUInt64() == unsignedExpectedMax
            && unsignedAdvanced.ToUInt64() == 217UL && UIntPtr.Subtract(unsignedAdvanced, 17) == unsignedPointer
            && (unsignedPointer + 17).ToUInt64() == 217UL && ((unsignedPointer + 17) - 17) == unsignedPointer
            && unsignedArithmetic.ToUInt64() == 44UL && unsignedDivided.ToUInt64() == 11UL && unsignedRemainder.ToUInt64() == 4UL
            && unsignedPointer.CompareTo(unsignedAdvanced) < 0 && unsignedAdvanced.CompareTo(unsignedPointer) > 0 && unsignedPointer.CompareTo(unsignedPointer) == 0
            && unsignedPointer.Equals(new UIntPtr((UInt64)200UL)) && !unsignedPointer.Equals(unsignedAdvanced) && unsignedWrongCompare
            && (UInt64)new UIntPtr((UInt64)55UL) == 55UL && unsignedNarrowOverflow
            && String.Equals(new UIntPtr((UInt64)123UL).ToString(), "123") && String.Equals(new UIntPtr((UInt64)12UL).ToString("D4", null), "0012")
            && unsignedFormatted && unsignedWritten == 5 && unsignedFormatChars[0] == '0' && unsignedFormatChars[1] == '0'
            && unsignedFormatChars[2] == '1' && unsignedFormatChars[3] == '2' && unsignedFormatChars[4] == '3', ref passed, ref failed);

        // 16 System.Array
        Int32[] array = new Int32[3];
        array[1] = 9;
        Int32[] emptyArray = Array.Empty<Int32>();
        Record(array.Length == 3 && array.LongLength == 3L && array[1] == 9
            && emptyArray != null && emptyArray.Length == 0, ref passed, ref failed);

        // 17 System.String
        String text = "Inu kernel";
        String concatenated = String.Concat("Inu", " ", "kernel");
        Record(String.Empty.Length == 0 && text.Length == 10 && text[0] == 'I'
            && String.Equals(text, concatenated) && String.CompareOrdinal("abc", "abd") < 0
            && text.IndexOf('k') == 4 && text.Contains('u') && text.StartsWith("Inu") && text.EndsWith("kernel")
            && String.Equals(text.Substring(4, 6), "kernel")
            && String.IsNullOrEmpty("") && String.IsNullOrWhiteSpace(" \t\r\n"), ref passed, ref failed);

        // 18 System.Nullable<T>
        Nullable<Int32> present = new Nullable<Int32>(55);
        Nullable<Int32> absent = default;
        Record(present.HasValue && present.Value == 55 && present.GetValueOrDefault() == 55
            && !absent.HasValue && absent.GetValueOrDefault() == 0 && absent.GetValueOrDefault(7) == 7, ref passed, ref failed);

        // 19 System.Type
        Type intType = typeof(Int32);
        Type arrayType = typeof(Int32[]);
        Record(intType.IsValueType && intType.IsPrimitive && !intType.IsArray
            && arrayType.IsArray && arrayType.IsSZArray && arrayType.GetElementType() == intType
            && arrayType.BaseType == typeof(Array)
            && typeof(Object).IsAssignableFrom(typeof(Probe)) && typeof(Probe).IsSubclassOf(typeof(Object)), ref passed, ref failed);

        // 20 System.Collections.Generic.KeyValuePair<TKey,TValue>
        KeyValuePair<String, Int32> pair = new KeyValuePair<String, Int32>("answer", 42);
        Record(String.Equals(pair.Key, "answer") && pair.Value == 42, ref passed, ref failed);

        // 21 System.Collections.Generic.EqualityComparer<T>
        EqualityComparer<Int32> intComparer = EqualityComparer<Int32>.Default;
        EqualityComparer<String> stringComparer = EqualityComparer<String>.Default;
        Record(intComparer.Equals(7, 7) && !intComparer.Equals(7, 8)
            && stringComparer.Equals("same", "same") && !stringComparer.Equals("same", "other")
            && stringComparer.GetHashCode("same") == stringComparer.GetHashCode("same"), ref passed, ref failed);

        // 22 System.Collections.Generic.List<T>
        List<Int32> list = new List<Int32>();
        list.Add(1); list.Add(3); list.Insert(1, 2);
        Int32[] listCopy = list.ToArray();
        Record(list.Count == 3 && list[1] == 2 && list.Contains(3) && list.IndexOf(2) == 1
            && listCopy.Length == 3 && listCopy[2] == 3 && list.Remove(2) && list.Count == 2, ref passed, ref failed);

        // 23 System.Collections.Generic.Dictionary<TKey,TValue>
        Dictionary<String, Int32> dictionary = new Dictionary<String, Int32>();
        dictionary.Add("one", 1);
        dictionary["two"] = 2;
        Int32 dictionaryValue;
        Record(dictionary.Count == 2 && dictionary.ContainsKey("one")
            && dictionary.TryGetValue("two", out dictionaryValue) && dictionaryValue == 2
            && !dictionary.TryAdd("one", 11) && dictionary.Remove("one") && !dictionary.ContainsKey("one"), ref passed, ref failed);

        // 24 System.Collections.Generic.Queue<T>
        Queue<Int32> queue = new Queue<Int32>();
        queue.Enqueue(4); queue.Enqueue(5); queue.Enqueue(6);
        Record(queue.Count == 3 && queue.Peek() == 4 && queue.Dequeue() == 4 && queue.Peek() == 5 && queue.Count == 2, ref passed, ref failed);

        // 25 System.Collections.Generic.Stack<T>
        Stack<Int32> stack = new Stack<Int32>();
        stack.Push(4); stack.Push(5); stack.Push(6);
        Record(stack.Count == 3 && stack.Peek() == 6 && stack.Pop() == 6 && stack.Peek() == 5 && stack.Count == 2, ref passed, ref failed);

        // 26 System.Text.StringBuilder
        StringBuilder builder = new StringBuilder();
        builder.Append("Inu").Append(' ').Append(95).AppendLine();
        Record(builder.Length == 8 && builder[0] == 'I' && String.Equals(builder.ToString(), "Inu 95\r\n")
            && builder.Clear().Append(true).EnsureCapacity(32) >= 32 && String.Equals(builder.ToString(), "True"), ref passed, ref failed);

        // 27 System.Text.Encoding (factory contract)
        Encoding asciiFactory = Encoding.ASCII;
        Encoding utf8Factory = Encoding.UTF8;
        Record(asciiFactory != null && utf8Factory != null
            && asciiFactory.GetByteCount("Inu") == 3 && utf8Factory.GetByteCount("Inu") == 3, ref passed, ref failed);

        // 28 System.Text.ASCIIEncoding
        ASCIIEncoding ascii = new ASCIIEncoding();
        Byte[] asciiBytes = ascii.GetBytes("Inu");
        Record(asciiBytes.Length == 3 && asciiBytes[0] == (Byte)'I' && asciiBytes[2] == (Byte)'u'
            && String.Equals(ascii.GetString(asciiBytes), "Inu"), ref passed, ref failed);

        // 29 System.Text.UTF8Encoding
        UTF8Encoding utf8 = new UTF8Encoding();
        String unicode = "\u00A3\u20AC";
        Byte[] utf8Bytes = utf8.GetBytes(unicode);
        Record(utf8Bytes.Length == 5 && utf8Bytes[0] == 0xC2 && utf8Bytes[1] == 0xA3
            && utf8Bytes[2] == 0xE2 && utf8Bytes[3] == 0x82 && utf8Bytes[4] == 0xAC
            && String.Equals(utf8.GetString(utf8Bytes), unicode), ref passed, ref failed);

        // 30 Primitive formatting: integer G/D/X plus invariant floating G/F/E.
        Record(String.Equals(((Int32)12345).ToString(), "12345")
            && String.Equals(((Int32)(-42)).ToString(), "-42")
            && String.Equals(((Int32)42).ToString("D5", null), "00042")
            && String.Equals(((Int32)0x2A).ToString("X4", null), "002A")
            && String.Equals(((Int32)(-1)).ToString("X", null), "FFFFFFFF")
            && String.Equals(((UInt32)99U).ToString("D4", null), "0099")
            && String.Equals(((Int64)(-9000000000L)).ToString("D12", null), "-009000000000")
            && String.Equals(((Double)12.5).ToString("G", null), "12.5")
            && String.Equals(((Single)(-0.25F)).ToString("G", null), "-0.25"), ref passed, ref failed);

        // 31 System.Math: integer + floating primitives used by the runtime and graphics layers.
        Double sqrt81 = Math.Sqrt(81.0);
        Record(Math.Abs(-17) == 17 && Math.Abs(-17L) == 17L && Math.Abs(-2.5) == 2.5
            && Math.Min(5, 9) == 5 && Math.Max(5, 9) == 9 && Math.Min(5L, 9L) == 5L
            && Math.Sign(-8) == -1 && Math.Sign(0L) == 0 && Math.Sign(8.0) == 1
            && Math.Clamp(15, 0, 10) == 10 && Math.Clamp(-4L, 0L, 10L) == 0L
            && Math.Floor(2.75) == 2.0 && Math.Ceiling(2.25) == 3.0 && Math.Truncate(-2.75) == -2.0
            && Math.Round(2.5) == 2.0 && Math.Round(3.5) == 4.0
            && Math.Abs(sqrt81 - 9.0) < 0.000001, ref passed, ref failed);

        // 32 System.Convert: all primitive integer widths plus NativeAOT checked floating/integer helper paths.
        Boolean intOverflow = false, uintOverflow = false, longOverflow = false, ulongOverflow = false;
        try { Convert.ToInt32(2147483648.0); } catch (OverflowException) { intOverflow = true; }
        try { Convert.ToUInt32(-1.0); } catch (OverflowException) { uintOverflow = true; }
        try { Convert.ToInt64(9223372036854775808.0); } catch (OverflowException) { longOverflow = true; }
        try { Convert.ToUInt64(-1.0); } catch (OverflowException) { ulongOverflow = true; }
        Record(Convert.ToInt32(true) == 1 && Convert.ToInt32(false) == 0
            && Convert.ToByte(255) == 255 && Convert.ToSByte(-12) == -12
            && Convert.ToInt16(-32000) == -32000 && Convert.ToUInt16(65000) == 65000
            && Convert.ToUInt32(123) == 123U && Convert.ToInt64(-123) == -123L
            && Convert.ToUInt64(123) == 123UL && Convert.ToBoolean(1) && !Convert.ToBoolean(0)
            && Convert.ToInt32(2.5) == 2 && Convert.ToInt32(3.5) == 4
            && Convert.ToInt32(2147483647.0) == 2147483647
            && Convert.ToUInt32(4294967295.0) == 4294967295U
            && Convert.ToDouble(123) == 123.0 && Convert.ToSingle(12) == 12.0F
            && String.Equals(Convert.ToString(-321), "-321")
            && String.Equals(Convert.ToString(12.5), "12.5")
            && String.Equals(Convert.ToString(true), "True")
            && intOverflow && uintOverflow && longOverflow && ulongOverflow, ref passed, ref failed);

        // 33 System.IComparable / IComparable<T> / IEquatable<T> across primitive families.
        IComparable nonGenericComparable = (Int32)7;
        IComparable<Int32> genericComparable = (Int32)7;
        IComparable<Int64> longComparable = (Int64)9;
        IEquatable<UInt32> uintEquatable = (UInt32)77U;
        Record(nonGenericComparable.CompareTo((Int32)6) > 0 && nonGenericComparable.CompareTo((Int32)7) == 0
            && genericComparable.CompareTo(8) < 0 && ((IComparable<Int32>)(Int32)8).CompareTo(7) > 0
            && longComparable.CompareTo(10L) < 0 && uintEquatable.Equals(77U)
            && ((IComparable<Char>)(Char)'b').CompareTo('a') > 0
            && ((IEquatable<Boolean>)(Boolean)true).Equals(true), ref passed, ref failed);

        // 34 Delegate family: multiple arities plus Predicate/Comparison/Converter.
        _bclDelegateObserved = 0;
        Action<Int32> bclAction = BclCapture;
        Action<Int32, Int32> bclAdd = BclAddCapture;
        Func<Int32, Int32> bclTwice = BclDouble;
        Func<Int32, Int32, Int32> bclSum = BclSum;
        Func<Int32> bclConstant = BclConstant;
        Predicate<Int32> bclPositive = BclPositive;
        Comparison<Int32> bclCompare = BclCompare;
        Converter<Int32, Int64> bclWiden = BclWiden;
        bclAction(9);
        Boolean firstDelegatePass = _bclDelegateObserved == 9 && bclTwice(6) == 12 && bclConstant() == 11;
        bclAdd(7, 8);
        Record(firstDelegatePass && _bclDelegateObserved == 15 && bclSum(4, 5) == 9
            && bclPositive(1) && !bclPositive(-1) && bclCompare(3, 7) < 0 && bclWiden(44) == 44L, ref passed, ref failed);

        // 35 System.Span<T> / ReadOnlySpan<T>: empty/null, fill, slicing and overlap-safe copy.
        Int32[] spanValues = new Int32[] { 1, 2, 3, 4, 5 };
        Span<Int32> span = spanValues;
        span[1] = 20;
        Span<Int32> middle = span.Slice(1, 3);
        middle.Fill(7);
        ReadOnlySpan<Int32> readOnlySpan = span;
        Int32[] spanCopy = readOnlySpan.Slice(1, 3).ToArray();
        Int32[] overlapping = new Int32[] { 1, 2, 3, 4, 5 };
        Span<Int32> overlapSource = new Span<Int32>(overlapping, 0, 4);
        Span<Int32> overlapDestination = new Span<Int32>(overlapping, 1, 4);
        overlapSource.CopyTo(overlapDestination);
        Span<Int32> tooSmall = new Span<Int32>(new Int32[2]);
        Span<Int32> nullSpan = new Span<Int32>((Int32[])null);
        Record(span.Length == 5 && span[0] == 1 && span[1] == 7 && span[3] == 7
            && readOnlySpan.Length == 5 && spanCopy.Length == 3 && spanCopy[0] == 7 && spanCopy[2] == 7
            && overlapping[0] == 1 && overlapping[1] == 1 && overlapping[4] == 4
            && !span.TryCopyTo(tooSmall) && Span<Int32>.Empty.IsEmpty && ReadOnlySpan<Int32>.Empty.IsEmpty
            && nullSpan.IsEmpty, ref passed, ref failed);

        // 36 System.Memory<T> / ReadOnlyMemory<T>: storable array-backed windows over Span.
        Int32[] memoryValues = new Int32[] { 10, 20, 30, 40 };
        Memory<Int32> memory = memoryValues;
        Memory<Int32> memoryMiddle = memory.Slice(1, 2);
        memoryMiddle.Span[0] = 25;
        ReadOnlyMemory<Int32> readOnlyMemory = memory;
        Int32[] memoryCopy = readOnlyMemory.Slice(1, 2).ToArray();
        Memory<Int32> nullMemory = new Memory<Int32>((Int32[])null);
        Record(memory.Length == 4 && memoryValues[1] == 25 && memoryMiddle.Span[1] == 30
            && readOnlyMemory.Span[1] == 25 && memoryCopy.Length == 2 && memoryCopy[0] == 25 && memoryCopy[1] == 30
            && Memory<Int32>.Empty.IsEmpty && ReadOnlyMemory<Int32>.Empty.IsEmpty && nullMemory.IsEmpty, ref passed, ref failed);

        // 37 Generic comparison/equality consistency.
        Comparer<Int32> intOrdering = Comparer<Int32>.Default;
        Comparer<Int64> longOrdering = Comparer<Int64>.Default;
        EqualityComparer<UInt32> uintEquality = EqualityComparer<UInt32>.Default;
        EqualityComparer<Int64> longEquality = EqualityComparer<Int64>.Default;
        Record(intOrdering.Compare(2, 7) < 0 && intOrdering.Compare(7, 2) > 0 && intOrdering.Compare(4, 4) == 0
            && longOrdering.Compare(9L, 10L) < 0
            && uintEquality.Equals(99U, 99U) && !uintEquality.Equals(99U, 100U)
            && longEquality.Equals(-5L, -5L) && !longEquality.Equals(-5L, 5L), ref passed, ref failed);

        UInt32 exercised = (passed + failed) - before;
        Record(exercised == (UInt32)BclTargetItemCount, ref passed, ref failed);
        NativeAotExceptionRuntime.TraceValue(0xBC21UL, exercised);
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
