using Internal.Runtime;
using System.Runtime.InteropServices;

namespace System
{
    // CONTRACT with .NET 10 NativeAOT Runtime.Base.
    // Data contract: Object contains exactly one MethodTable* field at object offset zero.
    // VTable contract: Object's finalizer is the first virtual method / vtable slot.
    public unsafe class Object
    {
#pragma warning disable CS0649 // Written by the NativeAOT allocator before the object reference is published.
        private MethodTable* m_pEEType;
#pragma warning restore CS0649

        public Object() { }

        // CONTRACT with NativeAOT: do not add a virtual method ahead of this finalizer.
        ~Object() { }

        internal MethodTable* MethodTable => m_pEEType;
        internal MethodTable* GetMethodTable() => m_pEEType;

        // NativeAOT object type identity is the MethodTable pointer stored at object
        // offset zero. Keep GetType non-virtual: System.Object's vtable ABI must remain
        // Finalize, ToString, Equals, GetHashCode in slots 0..3.
        [Runtime.CompilerServices.Intrinsic]
        public Type GetType() => Type.GetTypeFromMethodTable(m_pEEType);

        [StructLayout(LayoutKind.Sequential)]
        private class RawData
        {
            public Byte Data;
        }

        // CONTRACT with Runtime.Base: raw object data starts immediately after
        // the MethodTable pointer. The size includes Array/String length fields.
        internal ref Byte GetRawData() => ref Runtime.CompilerServices.Unsafe.As<RawData>(this).Data;

        internal UInt32 GetRawDataSize()
            => m_pEEType->BaseSize - (UInt32)sizeof(ObjHeader) - (UInt32)sizeof(MethodTable*);

        /// <summary>Returns the freestanding type name used when no more specific value formatter is available.</summary>
        public virtual String ToString() => "System.Object";

        /// <summary>Implements the normal Object identity default until a derived type overrides equality.</summary>
        public virtual Boolean Equals(Object obj) => ReferenceEquals(this, obj);

        /// <summary>Provides a legal, allocation-free default hash. Identity hashing is supplied by a later GC/type-system phase.</summary>
        public virtual Int32 GetHashCode() => 0;

        /// <summary>Determines whether two object references identify the same managed object.</summary>
        public static Boolean ReferenceEquals(Object first, Object second) => first == second;
    }

    // CONTRACT with .NET 10.0.10 DynamicInvokeMethodThunk. Once usage-based
    // NativeAOT runtime mappings are enabled, ILC can generate reflection-invoke
    // thunks for methods that are present in InvokeMap. Those thunks resolve the
    // exact System.ByReference type and its Value field from System.Private.CoreLib.
    // This is a tracked managed byref container only; it does not add a managed
    // reflection implementation to Inu.
    [Runtime.Versioning.NonVersionable]
    internal readonly ref struct ByReference
    {
        public readonly ref Byte Value;

        public ByReference(ref Byte value)
            => Value = ref value;

        public static ByReference Create<T>(ref T value)
            => new ByReference(ref Runtime.CompilerServices.Unsafe.As<T, Byte>(ref value));
    }

    namespace Diagnostics.CodeAnalysis
    {
        // CONTRACT with the NativeAOT trimmer/dataflow engine. The exact full names
        // and PublicParameterlessConstructor flag are compiler contracts: Activator's
        // generic parameter annotation causes a `new()`-constrained T to keep its public
        // parameterless constructor in InvokeMap for DefaultConstructor dictionary lookup.
        [Flags]
        public enum DynamicallyAccessedMemberTypes
        {
            None = 0,
            PublicParameterlessConstructor = 0x0001,
            PublicConstructors = 0x0002 | PublicParameterlessConstructor,
            NonPublicConstructors = 0x0004,
            PublicMethods = 0x0008,
            NonPublicMethods = 0x0010,
            PublicFields = 0x0020,
            NonPublicFields = 0x0040,
            PublicNestedTypes = 0x0080,
            NonPublicNestedTypes = 0x0100,
            PublicProperties = 0x0200,
            NonPublicProperties = 0x0400,
            PublicEvents = 0x0800,
            NonPublicEvents = 0x1000,
            Interfaces = 0x2000,
            All = ~None
        }

        [Runtime.CompilerServices.CompilerLoweringPreserve]
        [AttributeUsage(AttributeTargets.Field | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, Inherited = false)]
        public sealed class DynamicallyAccessedMembersAttribute : Attribute
        {
            public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes)
            {
                MemberTypes = memberTypes;
            }

            public DynamicallyAccessedMemberTypes MemberTypes { get; }
        }
    }

    // CONTRACT with Roslyn and .NET 10 NativeAOT. Generic `new T()` is lowered by
    // the C# compiler through System.Activator.CreateInstance<T>(). NativeAOT then
    // recognizes DefaultConstructorOf<T>/AllocatorOf<T> below as runtime generic
    // lookup intrinsics, which is what emits the NativeLayout DefaultConstructor and
    // ObjectAllocator dictionary cells for shared generic code.
    public static unsafe class Activator
    {
        [Runtime.CompilerServices.Intrinsic]
        public static T CreateInstance<[Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>()
        {
            Runtime.ActivationRuntimeDiagnostics.TraceStage(0x1A00UL);
            Runtime.ActivationRuntimeDiagnostics.TraceValue(0x1A01UL, (UInt64)(nuint)global::Internal.Runtime.MethodTable.Of<T>());
            IntPtr defaultConstructor = DefaultConstructorOf<T>();
            Runtime.ActivationRuntimeDiagnostics.TraceValue(0x1A02UL, (UInt64)(nuint)(void*)defaultConstructor);
            IntPtr missingConstructor = global::Internal.Runtime.CompilerServices.FunctionPointerOps.MissingDefaultConstructorMarker();
            Runtime.ActivationRuntimeDiagnostics.TraceValue(0x1A03UL, (UInt64)(nuint)(void*)missingConstructor);
            Runtime.ActivationRuntimeDiagnostics.TraceValue(0x1A04UL, typeof(T).IsValueType ? 1UL : 0UL);

            if (typeof(T).IsValueType)
            {
                T value = default(T);
                if (defaultConstructor != missingConstructor)
                {
                    Runtime.ActivationRuntimeDiagnostics.TraceStage(0x1A05UL);
                    Runtime.RawCalliHelper.CallDefaultStructConstructor(
                        defaultConstructor,
                        ref Runtime.CompilerServices.Unsafe.As<T, Byte>(ref value));
                    Runtime.ActivationRuntimeDiagnostics.TraceStage(0x1A06UL);
                }
                Runtime.ActivationRuntimeDiagnostics.TraceStage(0x1A07UL);
                return value;
            }

            if (defaultConstructor == missingConstructor)
            {
                Runtime.ActivationRuntimeDiagnostics.TraceStage(0x1A08UL);
                throw new NotSupportedException("Type has no parameterless constructor.");
            }

            Runtime.ActivationRuntimeDiagnostics.TraceStage(0x1A09UL);
            IntPtr allocator = AllocatorOf<T>();
            Runtime.ActivationRuntimeDiagnostics.TraceValue(0x1A0AUL, (UInt64)(nuint)(void*)allocator);
            Runtime.ActivationRuntimeDiagnostics.TraceStage(0x1A0BUL);
            T instance = Runtime.RawCalliHelper.Call<T>(allocator, (IntPtr)global::Internal.Runtime.MethodTable.Of<T>());
            Runtime.ActivationRuntimeDiagnostics.TraceValue(0x1A0CUL, (UInt64)(nuint)(void*)global::Internal.Runtime.MethodTable.Of<T>());
            Runtime.ActivationRuntimeDiagnostics.TraceStage(0x1A0DUL);
            Runtime.ActivationRuntimeDiagnostics.TraceStage(0x1A0EUL);
            Runtime.RawCalliHelper.Call(defaultConstructor, instance);
            Runtime.ActivationRuntimeDiagnostics.TraceStage(0x1A0FUL);
            return instance;
        }

        [Runtime.CompilerServices.Intrinsic]
        private static IntPtr DefaultConstructorOf<T>()
        {
            throw new NotSupportedException("NativeAOT must replace Activator.DefaultConstructorOf<T>().");
        }

        [Runtime.CompilerServices.Intrinsic]
        private static IntPtr AllocatorOf<T>()
        {
            throw new NotSupportedException("NativeAOT must replace Activator.AllocatorOf<T>().");
        }
    }

    /// <summary>Freestanding non-moving tracing collector surface.</summary>
    public static class GC
    {
#pragma warning disable CS0626
        [Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [Runtime.RuntimeImport("*", "InuGcCollect")]
        private static extern Boolean CollectCore();

        [Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [Runtime.RuntimeImport("*", "InuGcWaitForPendingFinalizers")]
        private static extern void WaitForPendingFinalizersCore();

        [Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [Runtime.RuntimeImport("*", "RhSuppressFinalize")]
        private static extern void SuppressFinalizeCore(Object value);

        [Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [Runtime.RuntimeImport("*", "RhReRegisterForFinalize")]
        private static extern Boolean ReRegisterForFinalizeCore(Object value);
#pragma warning restore CS0626

        public static void Collect() { CollectCore(); }
        public static void WaitForPendingFinalizers() => WaitForPendingFinalizersCore();
        public static void SuppressFinalize(Object value) { if (value == null) throw new ArgumentNullException(); SuppressFinalizeCore(value); }
        public static void ReRegisterForFinalize(Object value) { if (value == null) throw new ArgumentNullException(); ReRegisterForFinalizeCore(value); }
    }

    // Minimal NativeAOT-compatible exception object hierarchy. The runtime owns
    // dispatch/unwinding; CoreLib owns the managed objects and normal throw semantics.
    public class Exception : Object
    {
        private readonly String _message;
        public Exception() { }
        public Exception(String message) { _message = message; }
        public virtual String Message => _message ?? "Exception of type System.Exception was thrown.";
        public override String ToString() => Message;
    }
    public class SystemException : Exception { public SystemException() { } public SystemException(String message) : base(message) { } }
    public class ArithmeticException : SystemException { public ArithmeticException() { } public ArithmeticException(String message) : base(message) { } }
    public class DivideByZeroException : ArithmeticException { public DivideByZeroException() { } }
    public class OverflowException : ArithmeticException { public OverflowException() { } }
    public class NullReferenceException : SystemException { public NullReferenceException() { } }
    public class IndexOutOfRangeException : SystemException { public IndexOutOfRangeException() { } }
    public class InvalidCastException : SystemException { public InvalidCastException() { } }
    public class ArrayTypeMismatchException : SystemException { public ArrayTypeMismatchException() { } }
    public class ArgumentException : SystemException { public ArgumentException() { } public ArgumentException(String message) : base(message) { } }
    public class ArgumentNullException : ArgumentException { public ArgumentNullException() { } public ArgumentNullException(String message) : base(message) { } }
    public class ArgumentOutOfRangeException : ArgumentException { public ArgumentOutOfRangeException() { } }
    public class NotSupportedException : SystemException { public NotSupportedException() { } public NotSupportedException(String message) : base(message) { } }
    public class PlatformNotSupportedException : NotSupportedException { public PlatformNotSupportedException() { } }
    public class NotImplementedException : SystemException { public NotImplementedException() { } }
    public class OutOfMemoryException : SystemException { public OutOfMemoryException() { } }
    public class VerificationException : SystemException { public VerificationException() { } }
    public class InvalidProgramException : SystemException { public InvalidProgramException() { } public InvalidProgramException(String message) : base(message) { } }
    // 0.0.84: required by the freestanding NativeFormat/interface-dispatch decoder.
    public class BadImageFormatException : SystemException { public BadImageFormatException() { } public BadImageFormatException(String message) : base(message) { } }
    public class FormatException : SystemException { public FormatException() { } }
    public class InvalidOperationException : SystemException { public InvalidOperationException() { } public InvalidOperationException(String message) : base(message) { } }
    public class KeyNotFoundException : SystemException { public KeyNotFoundException() { } public KeyNotFoundException(String message) : base(message) { } }

    public struct Void { }

    /// <summary><inu.api/>Non-generic ordering contract used by primitive and SDK value types.</summary>
    public interface IComparable { Int32 CompareTo(Object obj); }
    /// <summary><inu.api/>Strongly typed ordering contract.</summary>
    public interface IComparable<T> { Int32 CompareTo(T other); }
    /// <summary><inu.api/>Strongly typed equality contract.</summary>
    public interface IEquatable<T> { Boolean Equals(T other); }
    /// <summary><inu.api/>Minimal formatting contract used by primitive values.</summary>
    public interface IFormattable { String ToString(String format, IFormatProvider formatProvider); }
    public interface IFormatProvider { Object GetFormat(Type formatType); }

    // CONTRACT with .NET 10 NativeAOT Runtime.Base Primitives.cs. These are not
    // decorative fields: ILC/runtime layout, boxing and generic value-type layout
    // are permitted to rely on each primitive's canonical one-field data contract.
#pragma warning disable CS0169, CS0649 // Primitive backing fields are consumed by compiler/runtime ABI.
    public struct Boolean : IComparable, IComparable<Boolean>, IEquatable<Boolean>
    {
        private bool _value;

        /// <summary>Returns the normal .NET Boolean text without allocating a new string.</summary>
        public override String ToString() => this ? "True" : "False";
        public Boolean Equals(Boolean other) => _value == other._value;
        public override Boolean Equals(Object obj) => obj is Boolean && Equals((Boolean)obj);
        public override Int32 GetHashCode() => this ? 1 : 0;
        public Int32 CompareTo(Boolean other) => this == other ? 0 : (this ? 1 : -1);
        public Int32 CompareTo(Object obj) { if (obj == null) return 1; if (!(obj is Boolean)) throw new ArgumentException(); return CompareTo((Boolean)obj); }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Char : IComparable, IComparable<Char>, IEquatable<Char>
    {
        private char _value;
        public const Char MaxValue = (Char)0xFFFF;
        public const Char MinValue = (Char)0x0000;

        /// <summary>Returns whether the character is one of the ASCII whitespace characters supported during freestanding bootstrap.</summary>
        public static Boolean IsWhiteSpace(Char value)
            => value == ' ' || value == '\t' || value == '\r' || value == '\n' || value == '\f' || value == '\v';
        public Boolean Equals(Char other) => _value == other._value;
        public override Boolean Equals(Object obj) => obj is Char && Equals((Char)obj);
        public override Int32 GetHashCode() => _value;
        public Int32 CompareTo(Char other) => _value < other._value ? -1 : (_value > other._value ? 1 : 0);
        public Int32 CompareTo(Object obj) { if (obj == null) return 1; if (!(obj is Char)) throw new ArgumentException(); return CompareTo((Char)obj); }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SByte : IComparable, IComparable<SByte>, IEquatable<SByte>, IFormattable
    {
        private sbyte _value; public const SByte MinValue = -128; public const SByte MaxValue = 127;
        public Boolean Equals(SByte other) => _value == other._value;
        public override Boolean Equals(Object obj) => obj is SByte && Equals((SByte)obj);
        public override Int32 GetHashCode() => _value;
        public Int32 CompareTo(SByte other) => _value < other._value ? -1 : (_value > other._value ? 1 : 0);
        public Int32 CompareTo(Object obj) { if (obj == null) return 1; if (!(obj is SByte)) throw new ArgumentException(); return CompareTo((SByte)obj); }
        public override String ToString() => NumberFormatting.FormatInt64(_value);
        public String ToString(String format, IFormatProvider formatProvider) => NumberFormatting.FormatSigned(_value, 8, format);
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct Byte : IComparable, IComparable<Byte>, IEquatable<Byte>, IFormattable
    {
        private byte _value; public const Byte MinValue = 0; public const Byte MaxValue = 255;
        public Boolean Equals(Byte other) => _value == other._value;
        public override Boolean Equals(Object obj) => obj is Byte && Equals((Byte)obj);
        public override Int32 GetHashCode() => _value;
        public Int32 CompareTo(Byte other) => _value < other._value ? -1 : (_value > other._value ? 1 : 0);
        public Int32 CompareTo(Object obj) { if (obj == null) return 1; if (!(obj is Byte)) throw new ArgumentException(); return CompareTo((Byte)obj); }
        public override String ToString() => NumberFormatting.FormatUInt64(_value);
        public String ToString(String format, IFormatProvider formatProvider) => NumberFormatting.FormatUnsigned(_value, 8, format);
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct Int16 : IComparable, IComparable<Int16>, IEquatable<Int16>, IFormattable
    {
        private short _value; public const Int16 MinValue = -32768; public const Int16 MaxValue = 32767;
        public Boolean Equals(Int16 other) => _value == other._value;
        public override Boolean Equals(Object obj) => obj is Int16 && Equals((Int16)obj);
        public override Int32 GetHashCode() => _value;
        public Int32 CompareTo(Int16 other) => _value < other._value ? -1 : (_value > other._value ? 1 : 0);
        public Int32 CompareTo(Object obj) { if (obj == null) return 1; if (!(obj is Int16)) throw new ArgumentException(); return CompareTo((Int16)obj); }
        public override String ToString() => NumberFormatting.FormatInt64(_value);
        public String ToString(String format, IFormatProvider formatProvider) => NumberFormatting.FormatSigned(_value, 16, format);
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct UInt16 : IComparable, IComparable<UInt16>, IEquatable<UInt16>, IFormattable
    {
        private ushort _value; public const UInt16 MinValue = 0; public const UInt16 MaxValue = 65535;
        public Boolean Equals(UInt16 other) => _value == other._value;
        public override Boolean Equals(Object obj) => obj is UInt16 && Equals((UInt16)obj);
        public override Int32 GetHashCode() => _value;
        public Int32 CompareTo(UInt16 other) => _value < other._value ? -1 : (_value > other._value ? 1 : 0);
        public Int32 CompareTo(Object obj) { if (obj == null) return 1; if (!(obj is UInt16)) throw new ArgumentException(); return CompareTo((UInt16)obj); }
        public override String ToString() => NumberFormatting.FormatUInt64(_value);
        public String ToString(String format, IFormatProvider formatProvider) => NumberFormatting.FormatUnsigned(_value, 16, format);
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct Int32 : IComparable, IComparable<Int32>, IEquatable<Int32>, IFormattable
    {
        private int _value;
        public const Int32 MinValue = -2147483648;
        public const Int32 MaxValue = 2147483647;
        public Boolean Equals(Int32 other) => _value == other._value;
        public override Boolean Equals(Object obj) => obj is Int32 && Equals((Int32)obj);
        public override Int32 GetHashCode() => _value;
        public Int32 CompareTo(Int32 other) => _value < other._value ? -1 : (_value > other._value ? 1 : 0);
        public Int32 CompareTo(Object obj) { if (obj == null) return 1; if (!(obj is Int32)) throw new ArgumentException(); return CompareTo((Int32)obj); }
        public override String ToString() => NumberFormatting.FormatInt64(_value);
        public String ToString(String format, IFormatProvider formatProvider) => NumberFormatting.FormatSigned(_value, 32, format);
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct UInt32 : IComparable, IComparable<UInt32>, IEquatable<UInt32>, IFormattable
    {
        private uint _value; public const UInt32 MinValue = 0U; public const UInt32 MaxValue = 0xFFFFFFFFU;
        public Boolean Equals(UInt32 other) => _value == other._value;
        public override Boolean Equals(Object obj) => obj is UInt32 && Equals((UInt32)obj);
        public override Int32 GetHashCode() => unchecked((Int32)_value);
        public Int32 CompareTo(UInt32 other) => _value < other._value ? -1 : (_value > other._value ? 1 : 0);
        public Int32 CompareTo(Object obj) { if (obj == null) return 1; if (!(obj is UInt32)) throw new ArgumentException(); return CompareTo((UInt32)obj); }
        public override String ToString() => NumberFormatting.FormatUInt64(_value);
        public String ToString(String format, IFormatProvider formatProvider) => NumberFormatting.FormatUnsigned(_value, 32, format);
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct Int64 : IComparable, IComparable<Int64>, IEquatable<Int64>, IFormattable
    {
        private long _value; public const Int64 MinValue = -9223372036854775808L; public const Int64 MaxValue = 9223372036854775807L;
        public Boolean Equals(Int64 other) => _value == other._value;
        public override Boolean Equals(Object obj) => obj is Int64 && Equals((Int64)obj);
        public override Int32 GetHashCode() => unchecked((Int32)_value ^ (Int32)(_value >> 32));
        public Int32 CompareTo(Int64 other) => _value < other._value ? -1 : (_value > other._value ? 1 : 0);
        public Int32 CompareTo(Object obj) { if (obj == null) return 1; if (!(obj is Int64)) throw new ArgumentException(); return CompareTo((Int64)obj); }
        public override String ToString() => NumberFormatting.FormatInt64(_value);
        public String ToString(String format, IFormatProvider formatProvider) => NumberFormatting.FormatSigned(_value, 64, format);
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct UInt64 : IComparable, IComparable<UInt64>, IEquatable<UInt64>, IFormattable
    {
        private ulong _value; public const UInt64 MinValue = 0UL; public const UInt64 MaxValue = 0xFFFFFFFFFFFFFFFFUL;
        public Boolean Equals(UInt64 other) => _value == other._value;
        public override Boolean Equals(Object obj) => obj is UInt64 && Equals((UInt64)obj);
        public override Int32 GetHashCode() => unchecked((Int32)_value ^ (Int32)(_value >> 32));
        public Int32 CompareTo(UInt64 other) => _value < other._value ? -1 : (_value > other._value ? 1 : 0);
        public Int32 CompareTo(Object obj) { if (obj == null) return 1; if (!(obj is UInt64)) throw new ArgumentException(); return CompareTo((UInt64)obj); }
        public override String ToString() => NumberFormatting.FormatUInt64(_value);
        public String ToString(String format, IFormatProvider formatProvider) => NumberFormatting.FormatUnsigned(_value, 64, format);
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct Single : IComparable, IComparable<Single>, IEquatable<Single>, IFormattable
    {
        private float _value;
        private static Boolean IsNaN(float value) => !(value < 0.0f || value >= 0.0f);
        public Boolean Equals(Single other) => _value == other._value || (IsNaN(_value) && IsNaN(other._value));
        public override Boolean Equals(Object obj) => obj is Single && Equals((Single)obj);
        public override Int32 GetHashCode() => _value == 0 ? 0 : (Int32)_value;
        public Int32 CompareTo(Single other) { if (_value < other._value) return -1; if (_value > other._value) return 1; if (_value == other._value) return 0; return IsNaN(_value) ? (IsNaN(other._value) ? 0 : -1) : 1; }
        public Int32 CompareTo(Object obj) { if (obj == null) return 1; if (!(obj is Single)) throw new ArgumentException(); return CompareTo((Single)obj); }
        public override String ToString() => NumberFormatting.FormatDouble(_value);
        public String ToString(String format, IFormatProvider formatProvider)
        {
            if (String.IsNullOrEmpty(format) || String.Equals(format, "G")) return ToString();
            throw new FormatException();
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct Double : IComparable, IComparable<Double>, IEquatable<Double>, IFormattable
    {
        private double _value;
        private static Boolean IsNaN(double value) => !(value < 0.0 || value >= 0.0);
        public Boolean Equals(Double other) => _value == other._value || (IsNaN(_value) && IsNaN(other._value));
        public override Boolean Equals(Object obj) => obj is Double && Equals((Double)obj);
        public override Int32 GetHashCode() => _value == 0 ? 0 : (Int32)_value;
        public Int32 CompareTo(Double other) { if (_value < other._value) return -1; if (_value > other._value) return 1; if (_value == other._value) return 0; return IsNaN(_value) ? (IsNaN(other._value) ? 0 : -1) : 1; }
        public Int32 CompareTo(Object obj) { if (obj == null) return 1; if (!(obj is Double)) throw new ArgumentException(); return CompareTo((Double)obj); }
        public override String ToString() => NumberFormatting.FormatDouble(_value);
        public String ToString(String format, IFormatProvider formatProvider)
        {
            if (String.IsNullOrEmpty(format) || String.Equals(format, "G")) return ToString();
            throw new FormatException();
        }
    }

    internal static class NumberFormatting
    {
        internal static String FormatInt64(Int64 value)
        {
            if (value >= 0) return FormatUInt64((UInt64)value);
            UInt64 magnitude = unchecked(0UL - (UInt64)value);
            Char[] digits = FormatUInt64Chars(magnitude, true, 0);
            return String.CreateFromChars(digits, digits.Length);
        }

        internal static String FormatUInt64(UInt64 value)
        {
            Char[] digits = FormatUInt64Chars(value, false, 0);
            return String.CreateFromChars(digits, digits.Length);
        }

        internal static String FormatSigned(Int64 value, Int32 bitWidth, String format)
        {
            Char specifier; Int32 precision;
            ParseFormat(format, out specifier, out precision);
            if (specifier == 'G' || specifier == 'g') return FormatInt64(value);
            if (specifier == 'D' || specifier == 'd')
            {
                Boolean negative = value < 0;
                UInt64 magnitude = negative ? unchecked(0UL - (UInt64)value) : (UInt64)value;
                Char[] digits = FormatUInt64Chars(magnitude, negative, precision);
                return String.CreateFromChars(digits, digits.Length);
            }
            if (specifier == 'X' || specifier == 'x')
            {
                UInt64 bits = (UInt64)value;
                if (bitWidth < 64) bits &= (1UL << bitWidth) - 1UL;
                return FormatHex(bits, precision, specifier == 'x');
            }
            throw new FormatException();
        }

        internal static String FormatUnsigned(UInt64 value, Int32 bitWidth, String format)
        {
            Char specifier; Int32 precision;
            ParseFormat(format, out specifier, out precision);
            if (specifier == 'G' || specifier == 'g') return FormatUInt64(value);
            if (specifier == 'D' || specifier == 'd')
            {
                Char[] digits = FormatUInt64Chars(value, false, precision);
                return String.CreateFromChars(digits, digits.Length);
            }
            if (specifier == 'X' || specifier == 'x') return FormatHex(value, precision, specifier == 'x');
            throw new FormatException();
        }

        // Deliberately invariant and allocation-small. Core v1 promises a bounded general
        // floating format, not culture-aware/custom numeric formatting. Six fractional
        // places are enough for the bootstrap/runtime diagnostics this surface targets.
        internal static String FormatDouble(Double value)
        {
            if (value == 0.0) return "0";
            Boolean negative = value < 0.0;
            if (negative) value = -value;
            UInt64 whole = (UInt64)value;
            Double fraction = value - (Double)whole;
            Char[] wholeChars = FormatUInt64Chars(whole, negative, 0);
            if (fraction == 0.0) return String.CreateFromChars(wholeChars, wholeChars.Length);

            Char[] result = new Char[wholeChars.Length + 1 + 6];
            Int32 pos = 0;
            for (Int32 i = 0; i < wholeChars.Length; i++) result[pos++] = wholeChars[i];
            result[pos++] = '.';
            Int32 fractionalStart = pos;
            for (Int32 i = 0; i < 6; i++)
            {
                fraction *= 10.0;
                Int32 digit = (Int32)fraction;
                if (digit < 0) digit = 0; else if (digit > 9) digit = 9;
                result[pos++] = (Char)('0' + digit);
                fraction -= digit;
            }
            while (pos > fractionalStart + 1 && result[pos - 1] == '0') pos--;
            Char[] trimmed = new Char[pos];
            for (Int32 i = 0; i < pos; i++) trimmed[i] = result[i];
            return String.CreateFromChars(trimmed, trimmed.Length);
        }

        private static void ParseFormat(String format, out Char specifier, out Int32 precision)
        {
            if (String.IsNullOrEmpty(format)) { specifier = 'G'; precision = 0; return; }
            specifier = format[0]; precision = 0;
            for (Int32 i = 1; i < format.Length; i++)
            {
                Char c = format[i];
                if (c < '0' || c > '9') throw new FormatException();
                precision = precision * 10 + (c - '0');
                if (precision > 99) throw new FormatException();
            }
        }

        private static Char[] FormatUInt64Chars(UInt64 value, Boolean negative, Int32 minimumDigits)
        {
            Char[] reverse = new Char[20];
            Int32 count = 0;
            do { reverse[count++] = (Char)('0' + (Char)(value % 10UL)); value /= 10UL; } while (value != 0UL);
            while (count < minimumDigits && count < reverse.Length) reverse[count++] = '0';
            Char[] result = new Char[count + (negative ? 1 : 0)];
            Int32 output = 0;
            if (negative) result[output++] = '-';
            while (count != 0) result[output++] = reverse[--count];
            return result;
        }

        private static String FormatHex(UInt64 value, Int32 minimumDigits, Boolean lower)
        {
            Char[] reverse = new Char[16]; Int32 count = 0;
            do
            {
                Int32 nibble = (Int32)(value & 0xFUL);
                reverse[count++] = (Char)(nibble < 10 ? '0' + nibble : (lower ? 'a' : 'A') + nibble - 10);
                value >>= 4;
            } while (value != 0UL);
            while (count < minimumDigits && count < reverse.Length) reverse[count++] = '0';
            Char[] result = new Char[count]; Int32 output = 0;
            while (count != 0) result[output++] = reverse[--count];
            return String.CreateFromChars(result, result.Length);
        }
    }

    /// <summary><inu.api/>Primitive mathematical operations used by the Core v1 freestanding runtime.</summary>
    public static class Math
    {
        public static Int32 Abs(Int32 value) { if (value == Int32.MinValue) throw new OverflowException(); return value < 0 ? -value : value; }
        public static Int64 Abs(Int64 value) { if (value == Int64.MinValue) throw new OverflowException(); return value < 0 ? -value : value; }
        public static Single Abs(Single value) => value < 0 ? -value : value;
        public static Double Abs(Double value) => value < 0 ? -value : value;
        public static Int32 Min(Int32 left, Int32 right) => left < right ? left : right;
        public static Int64 Min(Int64 left, Int64 right) => left < right ? left : right;
        public static Single Min(Single left, Single right) => left < right ? left : right;
        public static Double Min(Double left, Double right) => left < right ? left : right;
        public static Int32 Max(Int32 left, Int32 right) => left > right ? left : right;
        public static Int64 Max(Int64 left, Int64 right) => left > right ? left : right;
        public static Single Max(Single left, Single right) => left > right ? left : right;
        public static Double Max(Double left, Double right) => left > right ? left : right;
        public static Int32 Sign(Int32 value) => value < 0 ? -1 : (value > 0 ? 1 : 0);
        public static Int32 Sign(Int64 value) => value < 0 ? -1 : (value > 0 ? 1 : 0);
        public static Int32 Sign(Single value) => value < 0 ? -1 : (value > 0 ? 1 : 0);
        public static Int32 Sign(Double value) => value < 0 ? -1 : (value > 0 ? 1 : 0);
        public static Int32 Clamp(Int32 value, Int32 min, Int32 max) { if (min > max) throw new ArgumentException(); return value < min ? min : (value > max ? max : value); }
        public static Int64 Clamp(Int64 value, Int64 min, Int64 max) { if (min > max) throw new ArgumentException(); return value < min ? min : (value > max ? max : value); }
        public static Single Clamp(Single value, Single min, Single max) { if (min > max) throw new ArgumentException(); return value < min ? min : (value > max ? max : value); }
        public static Double Clamp(Double value, Double min, Double max) { if (min > max) throw new ArgumentException(); return value < min ? min : (value > max ? max : value); }
        public static Double Truncate(Double value) => value >= 0 ? (Double)(Int64)value : (Double)(Int64)value;
        public static Double Floor(Double value) { Int64 truncated = (Int64)value; return value < truncated ? truncated - 1 : truncated; }
        public static Double Ceiling(Double value) { Int64 truncated = (Int64)value; return value > truncated ? truncated + 1 : truncated; }
        public static Double Round(Double value)
        {
            Int64 truncated = (Int64)value; Double fraction = value - truncated;
            if (fraction > 0.5 || (fraction == 0.5 && (truncated & 1L) != 0)) return truncated + 1;
            if (fraction < -0.5 || (fraction == -0.5 && (truncated & 1L) != 0)) return truncated - 1;
            return truncated;
        }
        public static Double Sqrt(Double value)
        {
            if (value < 0.0) return 0.0 / 0.0;
            if (value == 0.0) return 0.0;
            Double guess = value >= 1.0 ? value : 1.0;
            for (Int32 i = 0; i < 24; i++) guess = (guess + value / guess) * 0.5;
            return guess;
        }
    }

    /// <summary><inu.api/>Invariant primitive conversions for the Core v1 freestanding runtime.</summary>
    public static class Convert
    {
        public static Boolean ToBoolean(Boolean value) => value;
        public static Boolean ToBoolean(SByte value) => value != 0;
        public static Boolean ToBoolean(Byte value) => value != 0;
        public static Boolean ToBoolean(Int16 value) => value != 0;
        public static Boolean ToBoolean(UInt16 value) => value != 0;
        public static Boolean ToBoolean(Int32 value) => value != 0;
        public static Boolean ToBoolean(UInt32 value) => value != 0;
        public static Boolean ToBoolean(Int64 value) => value != 0;
        public static Boolean ToBoolean(UInt64 value) => value != 0;
        public static Boolean ToBoolean(Single value) => value != 0;
        public static Boolean ToBoolean(Double value) => value != 0;

        public static SByte ToSByte(Boolean value) => value ? (SByte)1 : (SByte)0;
        public static SByte ToSByte(SByte value) => value;
        public static SByte ToSByte(Byte value) => checked((SByte)value);
        public static SByte ToSByte(Int16 value) => checked((SByte)value);
        public static SByte ToSByte(UInt16 value) => checked((SByte)value);
        public static SByte ToSByte(Int32 value) => checked((SByte)value);
        public static SByte ToSByte(UInt32 value) => checked((SByte)value);
        public static SByte ToSByte(Int64 value) => checked((SByte)value);
        public static SByte ToSByte(UInt64 value) => checked((SByte)value);
        public static SByte ToSByte(Single value) => checked((SByte)Math.Round(value));
        public static SByte ToSByte(Double value) => checked((SByte)Math.Round(value));

        public static Byte ToByte(Boolean value) => value ? (Byte)1 : (Byte)0;
        public static Byte ToByte(SByte value) => checked((Byte)value);
        public static Byte ToByte(Byte value) => value;
        public static Byte ToByte(Int16 value) => checked((Byte)value);
        public static Byte ToByte(UInt16 value) => checked((Byte)value);
        public static Byte ToByte(Int32 value) => checked((Byte)value);
        public static Byte ToByte(UInt32 value) => checked((Byte)value);
        public static Byte ToByte(Int64 value) => checked((Byte)value);
        public static Byte ToByte(UInt64 value) => checked((Byte)value);
        public static Byte ToByte(Single value) => checked((Byte)Math.Round(value));
        public static Byte ToByte(Double value) => checked((Byte)Math.Round(value));

        public static Int16 ToInt16(Boolean value) => value ? (Int16)1 : (Int16)0;
        public static Int16 ToInt16(SByte value) => checked((Int16)value);
        public static Int16 ToInt16(Byte value) => checked((Int16)value);
        public static Int16 ToInt16(Int16 value) => value;
        public static Int16 ToInt16(UInt16 value) => checked((Int16)value);
        public static Int16 ToInt16(Int32 value) => checked((Int16)value);
        public static Int16 ToInt16(UInt32 value) => checked((Int16)value);
        public static Int16 ToInt16(Int64 value) => checked((Int16)value);
        public static Int16 ToInt16(UInt64 value) => checked((Int16)value);
        public static Int16 ToInt16(Single value) => checked((Int16)Math.Round(value));
        public static Int16 ToInt16(Double value) => checked((Int16)Math.Round(value));

        public static UInt16 ToUInt16(Boolean value) => value ? (UInt16)1 : (UInt16)0;
        public static UInt16 ToUInt16(SByte value) => checked((UInt16)value);
        public static UInt16 ToUInt16(Byte value) => checked((UInt16)value);
        public static UInt16 ToUInt16(Int16 value) => checked((UInt16)value);
        public static UInt16 ToUInt16(UInt16 value) => value;
        public static UInt16 ToUInt16(Int32 value) => checked((UInt16)value);
        public static UInt16 ToUInt16(UInt32 value) => checked((UInt16)value);
        public static UInt16 ToUInt16(Int64 value) => checked((UInt16)value);
        public static UInt16 ToUInt16(UInt64 value) => checked((UInt16)value);
        public static UInt16 ToUInt16(Single value) => checked((UInt16)Math.Round(value));
        public static UInt16 ToUInt16(Double value) => checked((UInt16)Math.Round(value));

        public static Int32 ToInt32(Boolean value) => value ? (Int32)1 : (Int32)0;
        public static Int32 ToInt32(SByte value) => checked((Int32)value);
        public static Int32 ToInt32(Byte value) => checked((Int32)value);
        public static Int32 ToInt32(Int16 value) => checked((Int32)value);
        public static Int32 ToInt32(UInt16 value) => checked((Int32)value);
        public static Int32 ToInt32(Int32 value) => value;
        public static Int32 ToInt32(UInt32 value) => checked((Int32)value);
        public static Int32 ToInt32(Int64 value) => checked((Int32)value);
        public static Int32 ToInt32(UInt64 value) => checked((Int32)value);
        public static Int32 ToInt32(Single value) => checked((Int32)Math.Round(value));
        public static Int32 ToInt32(Double value) => checked((Int32)Math.Round(value));

        public static UInt32 ToUInt32(Boolean value) => value ? (UInt32)1 : (UInt32)0;
        public static UInt32 ToUInt32(SByte value) => checked((UInt32)value);
        public static UInt32 ToUInt32(Byte value) => checked((UInt32)value);
        public static UInt32 ToUInt32(Int16 value) => checked((UInt32)value);
        public static UInt32 ToUInt32(UInt16 value) => checked((UInt32)value);
        public static UInt32 ToUInt32(Int32 value) => checked((UInt32)value);
        public static UInt32 ToUInt32(UInt32 value) => value;
        public static UInt32 ToUInt32(Int64 value) => checked((UInt32)value);
        public static UInt32 ToUInt32(UInt64 value) => checked((UInt32)value);
        public static UInt32 ToUInt32(Single value) => checked((UInt32)Math.Round(value));
        public static UInt32 ToUInt32(Double value) => checked((UInt32)Math.Round(value));

        public static Int64 ToInt64(Boolean value) => value ? (Int64)1 : (Int64)0;
        public static Int64 ToInt64(SByte value) => checked((Int64)value);
        public static Int64 ToInt64(Byte value) => checked((Int64)value);
        public static Int64 ToInt64(Int16 value) => checked((Int64)value);
        public static Int64 ToInt64(UInt16 value) => checked((Int64)value);
        public static Int64 ToInt64(Int32 value) => checked((Int64)value);
        public static Int64 ToInt64(UInt32 value) => checked((Int64)value);
        public static Int64 ToInt64(Int64 value) => value;
        public static Int64 ToInt64(UInt64 value) => checked((Int64)value);
        public static Int64 ToInt64(Single value) => checked((Int64)Math.Round(value));
        public static Int64 ToInt64(Double value) => checked((Int64)Math.Round(value));

        public static UInt64 ToUInt64(Boolean value) => value ? (UInt64)1 : (UInt64)0;
        public static UInt64 ToUInt64(SByte value) => checked((UInt64)value);
        public static UInt64 ToUInt64(Byte value) => checked((UInt64)value);
        public static UInt64 ToUInt64(Int16 value) => checked((UInt64)value);
        public static UInt64 ToUInt64(UInt16 value) => checked((UInt64)value);
        public static UInt64 ToUInt64(Int32 value) => checked((UInt64)value);
        public static UInt64 ToUInt64(UInt32 value) => checked((UInt64)value);
        public static UInt64 ToUInt64(Int64 value) => checked((UInt64)value);
        public static UInt64 ToUInt64(UInt64 value) => value;
        public static UInt64 ToUInt64(Single value) => checked((UInt64)Math.Round(value));
        public static UInt64 ToUInt64(Double value) => checked((UInt64)Math.Round(value));

        public static Single ToSingle(Boolean value) => value ? 1F : 0F;
        public static Single ToSingle(SByte value) => (Single)value;
        public static Single ToSingle(Byte value) => (Single)value;
        public static Single ToSingle(Int16 value) => (Single)value;
        public static Single ToSingle(UInt16 value) => (Single)value;
        public static Single ToSingle(Int32 value) => (Single)value;
        public static Single ToSingle(UInt32 value) => (Single)value;
        public static Single ToSingle(Int64 value) => (Single)value;
        public static Single ToSingle(UInt64 value) => (Single)value;
        public static Single ToSingle(Single value) => value;
        public static Single ToSingle(Double value) => (Single)value;

        public static Double ToDouble(Boolean value) => value ? 1D : 0D;
        public static Double ToDouble(SByte value) => (Double)value;
        public static Double ToDouble(Byte value) => (Double)value;
        public static Double ToDouble(Int16 value) => (Double)value;
        public static Double ToDouble(UInt16 value) => (Double)value;
        public static Double ToDouble(Int32 value) => (Double)value;
        public static Double ToDouble(UInt32 value) => (Double)value;
        public static Double ToDouble(Int64 value) => (Double)value;
        public static Double ToDouble(UInt64 value) => (Double)value;
        public static Double ToDouble(Single value) => (Double)value;
        public static Double ToDouble(Double value) => value;

        public static String ToString(SByte value) => value.ToString();
        public static String ToString(Byte value) => value.ToString();
        public static String ToString(Int16 value) => value.ToString();
        public static String ToString(UInt16 value) => value.ToString();
        public static String ToString(Int32 value) => value.ToString();
        public static String ToString(UInt32 value) => value.ToString();
        public static String ToString(Int64 value) => value.ToString();
        public static String ToString(UInt64 value) => value.ToString();
        public static String ToString(Single value) => value.ToString();
        public static String ToString(Double value) => value.ToString();
        public static String ToString(Boolean value) => value.ToString();
    }

    // CONTRACT with Roslyn / .NET 10 NativeAOT:
    // IntPtr and UIntPtr are compiler-known primitive structs.  Keep each as exactly one
    // pointer-sized field and provide the conversion/operator surface Roslyn binds for
    // nint/nuint constants, casts, pointer arithmetic and interop lowering.
    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct IntPtr
    {
        private readonly void* _value;

        public IntPtr(Int32 value) { _value = (void*)(Int64)value; }
        public IntPtr(Int64 value) { _value = (void*)value; }
        public IntPtr(void* value) { _value = value; }

        [Runtime.CompilerServices.Intrinsic]
        public static readonly IntPtr Zero;

        public static Int32 Size => sizeof(void*);
        public static IntPtr MaxValue => new IntPtr(Int64.MaxValue);
        public static IntPtr MinValue => new IntPtr(Int64.MinValue);

        public Int32 ToInt32() => checked((Int32)(Int64)_value);
        public Int64 ToInt64() => (Int64)_value;
        public void* ToPointer() => _value;

        public static explicit operator IntPtr(Int32 value) => new IntPtr(value);
        public static explicit operator IntPtr(Int64 value) => new IntPtr(value);
        public static explicit operator IntPtr(void* value) => new IntPtr(value);
        public static explicit operator void*(IntPtr value) => value._value;
        public static explicit operator Int32(IntPtr value) => value.ToInt32();
        public static explicit operator Int64(IntPtr value) => value.ToInt64();

        public static IntPtr Add(IntPtr pointer, Int32 offset)
            => new IntPtr((Byte*)pointer._value + offset);
        public static IntPtr Subtract(IntPtr pointer, Int32 offset)
            => new IntPtr((Byte*)pointer._value - offset);
        public static IntPtr operator +(IntPtr pointer, Int32 offset) => Add(pointer, offset);
        public static IntPtr operator -(IntPtr pointer, Int32 offset) => Subtract(pointer, offset);

        public static Boolean operator ==(IntPtr left, IntPtr right) => left._value == right._value;
        public static Boolean operator !=(IntPtr left, IntPtr right) => left._value != right._value;
        public override Boolean Equals(Object obj) => obj is IntPtr && this == (IntPtr)obj;
        public override Int32 GetHashCode()
            => unchecked((Int32)(UInt64)_value) ^ unchecked((Int32)((UInt64)_value >> 32));
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly unsafe struct UIntPtr
    {
        private readonly void* _value;

        public UIntPtr(UInt32 value) { _value = (void*)(UInt64)value; }
        public UIntPtr(UInt64 value) { _value = (void*)value; }
        public UIntPtr(void* value) { _value = value; }

        [Runtime.CompilerServices.Intrinsic]
        public static readonly UIntPtr Zero;

        public static Int32 Size => sizeof(void*);
        public static UIntPtr MaxValue => new UIntPtr(UInt64.MaxValue);
        public static UIntPtr MinValue => new UIntPtr(UInt64.MinValue);

        public UInt32 ToUInt32() => checked((UInt32)(UInt64)_value);
        public UInt64 ToUInt64() => (UInt64)_value;
        public void* ToPointer() => _value;

        // These op_Explicit members are compiler-required for nuint/UIntPtr casts.
        public static explicit operator UIntPtr(UInt32 value) => new UIntPtr(value);
        public static explicit operator UIntPtr(UInt64 value) => new UIntPtr(value);
        public static explicit operator UIntPtr(void* value) => new UIntPtr(value);
        public static explicit operator void*(UIntPtr value) => value._value;
        public static explicit operator UInt32(UIntPtr value) => value.ToUInt32();
        public static explicit operator UInt64(UIntPtr value) => value.ToUInt64();

        public static UIntPtr Add(UIntPtr pointer, Int32 offset)
            => new UIntPtr((Byte*)pointer._value + offset);
        public static UIntPtr Subtract(UIntPtr pointer, Int32 offset)
            => new UIntPtr((Byte*)pointer._value - offset);
        public static UIntPtr operator +(UIntPtr pointer, Int32 offset) => Add(pointer, offset);
        public static UIntPtr operator -(UIntPtr pointer, Int32 offset) => Subtract(pointer, offset);

        public static Boolean operator ==(UIntPtr left, UIntPtr right) => left._value == right._value;
        public static Boolean operator !=(UIntPtr left, UIntPtr right) => left._value != right._value;
        public override Boolean Equals(Object obj) => obj is UIntPtr && this == (UIntPtr)obj;
        public override Int32 GetHashCode()
            => unchecked((Int32)(UInt64)_value) ^ unchecked((Int32)((UInt64)_value >> 32));
    }
#pragma warning restore CS0169, CS0649
    public abstract class ValueType { }
    public abstract class Enum : ValueType { }
    public abstract class Array
    {
        // NativeAOT SZ-array layout is [method table][number of components][data...].
        // NativeAotRuntime writes this field immediately after the object header. Keeping
        // the field on System.Array gives Roslyn/ILC the normal managed Array.Length contract
        // while preserving the runtime layout expected by RhpNewArray.
        #pragma warning disable CS0649 // Populated by RhpNewArray before the array reference is published.
        private readonly Int32 _numComponents;
#pragma warning restore CS0649, CS0169

        /// <summary>Gets the total number of elements in this one-dimensional managed array.</summary>
        public Int32 Length => _numComponents;

        /// <summary>Gets the total number of elements as a 64-bit value.</summary>
        public Int64 LongLength => _numComponents;

        /// <summary>Returns an empty array of the requested element type.</summary>
        public static T[] Empty<T>() => new T[0];

        // .NET 10 NativeAOT class-library callback. The runtime asks CoreLib for
        // the canonical System.Array MethodTable when resolving array base-type
        // relationships and array type-system helpers.
        [System.Runtime.RuntimeExport("GetSystemArrayEEType")]
        private static unsafe Internal.Runtime.MethodTable* GetSystemArrayEEType()
            => Internal.Runtime.MethodTable.Of<Array>();
    }
    internal class Array<T> : Array { }

    [StructLayout(LayoutKind.Sequential)]
    internal class RawArrayData
    {
        public UInt32 Length;
        public UInt32 Padding;
        public Byte Data;
    }

    // .NET 10 ILC recognizes this CoreLib type as the bulk-reference-copy helper owner.
    // Inu 0.42.0 uses a non-moving, non-generational managed heap after the
    // runtime transition, so a correct overlap-safe copy is the required barrier.
    public static class Buffer
    {
        internal static void BulkMoveWithWriteBarrier(ref Byte destination, ref Byte source, UIntPtr byteCount)
            => SpanHelpers.Memmove(ref destination, ref source, byteCount);
    }

    // .NET 10 ILC resolves unmanaged value-type block clears and copies through System.SpanHelpers.
    // These freestanding implementations deliberately avoid the GC and any external runtime library.
    internal static unsafe class SpanHelpers
    {
        internal static void ClearWithoutReferences(ref Byte destination, UIntPtr byteCount)
        {
            fixed (Byte* pointer = &destination)
            {
                UInt64 length = (UInt64)(nuint)byteCount;
                for (UInt64 index = 0; index < length; index++) pointer[index] = 0;
            }
        }

        internal static void Memmove(ref Byte destination, ref Byte source, UIntPtr byteCount)
        {
            fixed (Byte* destinationPointer = &destination)
            fixed (Byte* sourcePointer = &source)
            {
                UInt64 length = (UInt64)(nuint)byteCount;
                if (length == 0UL || destinationPointer == sourcePointer) return;

                UInt64 destinationAddress = (UInt64)(nuint)destinationPointer;
                UInt64 sourceAddress = (UInt64)(nuint)sourcePointer;
                if (destinationAddress < sourceAddress || destinationAddress - sourceAddress >= length)
                {
                    for (UInt64 index = 0; index < length; index++) destinationPointer[index] = sourcePointer[index];
                    return;
                }

                for (UInt64 index = length; index != 0UL; index--) destinationPointer[index - 1UL] = sourcePointer[index - 1UL];
            }
        }
    }
    #pragma warning disable CS0660, CS0661 // Freestanding String supplies ordinal operators; Object equality/hash expansion is staged separately.
    [Serializable]
    [Runtime.Versioning.NonVersionable] // Matches System.Private.CoreLib: this contract applies to String field layout.
    public sealed unsafe class String
    {
        // NativeAOT/CLR x64 string ABI is fixed: [EEType*][Int32 length][UTF-16 data...].
        // Inu links no Microsoft NativeAOT runtime library, so CoreLib must own this
        // ABI explicitly instead of relying on a VM field-layout service that is absent.
        internal const Int32 MaxLength = 0x3FFFFFDF;

        private static Byte* RuntimeObject(String value)
        {
            if (Object.ReferenceEquals(value, null)) return null;
            String local = value;
            UInt64 address = Runtime.CompilerServices.Unsafe.As<String, UInt64>(ref local);
            return (Byte*)address;
        }

        internal static Int32 ReadRuntimeLength(String value)
        {
            Byte* objectAddress = RuntimeObject(value);
            if (objectAddress == null) return 0;
            return *(Int32*)(objectAddress + 8);
        }

        private static Char ReadRuntimeChar(String value, Int32 index)
        {
            Byte* objectAddress = RuntimeObject(value);
            if (objectAddress == null) return (Char)0;
            return *(Char*)(objectAddress + 12 + ((UInt32)index * 2U));
        }

#pragma warning disable CS0649, CS0169 // NativeAOT materializes string objects and requires these canonical runtime layout fields.
        [NonSerialized]
        private readonly Int32 _stringLength;
        [NonSerialized]
        private Char _firstChar;
#pragma warning restore CS0649

        /// <summary>Represents the empty string. This is the canonical zero-length string literal and requires no managed allocation.</summary>
        public static String Empty => "";

        public Int32 Length
        {
            // Keep the intrinsic marker for normal NativeAOT optimization, but source the
            // value from the fixed CLR ABI because Inu does not link the stock VM.
            [Runtime.CompilerServices.Intrinsic]
            get { return ReadRuntimeLength(this); }
        }

        /// <summary>Returns a readonly reference to the first UTF-16 code unit in the runtime string payload.</summary>
        public ref readonly Char GetPinnableReference() => ref _firstChar;

        /// <summary>Returns the first UTF-16 code unit by reference for freestanding runtime helpers.</summary>
        internal ref Char GetRawStringData() => ref _firstChar;

        /// <summary>Returns this string instance.</summary>
        public override String ToString() => this;

        /// <summary>Returns true when the value is null or has zero characters.</summary>
        public static Boolean IsNullOrEmpty(String value)
            => Object.ReferenceEquals(value, null) || value.Length == 0;

        /// <summary>Returns true when the value is null, empty, or contains only bootstrap-safe ASCII whitespace.</summary>
        public static Boolean IsNullOrWhiteSpace(String value)
        {
            if (Object.ReferenceEquals(value, null) || value.Length == 0) return true;
            for (Int32 index = 0; index < value.Length; index++)
                if (!Char.IsWhiteSpace(value[index])) return false;
            return true;
        }

        /// <summary>Performs ordinal string equality without allocation or culture services.</summary>
        public static Boolean Equals(String first, String second)
        {
            if (Object.ReferenceEquals(first, second)) return true;
            if (Object.ReferenceEquals(first, null) || Object.ReferenceEquals(second, null)) return false;
            if (first.Length != second.Length) return false;
            for (Int32 index = 0; index < first.Length; index++)
                if (first[index] != second[index]) return false;
            return true;
        }

        /// <summary>Performs ordinal string equality.</summary>
        public Boolean Equals(String other) => Equals(this, other);

        public static Boolean operator ==(String first, String second) => Equals(first, second);
        public static Boolean operator !=(String first, String second) => !Equals(first, second);

        /// <summary>Returns an ordinal comparison result compatible with the sign semantics of System.String.CompareOrdinal.</summary>
        public static Int32 CompareOrdinal(String first, String second)
        {
            if (Object.ReferenceEquals(first, second)) return 0;
            if (Object.ReferenceEquals(first, null)) return -1;
            if (Object.ReferenceEquals(second, null)) return 1;
            Int32 length = first.Length < second.Length ? first.Length : second.Length;
            for (Int32 index = 0; index < length; index++)
            {
                Int32 difference = (Int32)first[index] - (Int32)second[index];
                if (difference != 0) return difference;
            }
            return first.Length - second.Length;
        }

        /// <summary>Finds a character using ordinal comparison.</summary>
        public Int32 IndexOf(Char value)
        {
            for (Int32 index = 0; index < Length; index++)
                if (this[index] == value) return index;
            return -1;
        }

        /// <summary>Returns whether this string contains the specified character.</summary>
        public Boolean Contains(Char value) => IndexOf(value) >= 0;

        /// <summary>Returns whether this string starts with the supplied value using ordinal comparison.</summary>
        public Boolean StartsWith(String value)
        {
            if (Object.ReferenceEquals(value, null) || value.Length > Length) return false;
            for (Int32 index = 0; index < value.Length; index++)
                if (this[index] != value[index]) return false;
            return true;
        }

        /// <summary>Returns whether this string ends with the supplied value using ordinal comparison.</summary>
        public Boolean EndsWith(String value)
        {
            if (Object.ReferenceEquals(value, null) || value.Length > Length) return false;
            Int32 offset = Length - value.Length;
            for (Int32 index = 0; index < value.Length; index++)
                if (this[offset + index] != value[index]) return false;
            return true;
        }

        /// <summary>Returns the substring beginning at the supplied character index.</summary>
        public String Substring(Int32 startIndex)
        {
            Int32 sourceLength = Length;
            if ((UInt32)startIndex > (UInt32)sourceLength) throw new ArgumentOutOfRangeException();
            return Substring(startIndex, sourceLength - startIndex);
        }

        /// <summary>Returns a substring of the supplied character length.</summary>
        public String Substring(Int32 startIndex, Int32 length)
        {
            Int32 sourceLength = Length;
            if ((UInt32)startIndex > (UInt32)sourceLength) throw new ArgumentOutOfRangeException();
            if (length < 0 || length > sourceLength - startIndex) throw new ArgumentOutOfRangeException();
            if (length == 0) return Empty;
            if (startIndex == 0 && length == sourceLength) return this;

            void* address = AllocateRuntimeString(global::Internal.Runtime.MethodTable.Of<String>(), length);
            if (address == null) throw new OutOfMemoryException();
            UInt64 raw = (UInt64)(nuint)address;
            String result = Runtime.CompilerServices.Unsafe.As<UInt64, String>(ref raw);
            ref Char destination = ref result.GetRawStringData();
            Char* destinationPointer = (Char*)Runtime.CompilerServices.Unsafe.AsPointer(ref destination);
            for (Int32 index = 0; index < length; index++)
                destinationPointer[index] = ReadRuntimeChar(this, startIndex + index);
            return result;
        }

        /// <summary>Concatenates two strings, treating null as an empty string.</summary>
        public static String Concat(String first, String second)
        {
            Int32 firstLength = Object.ReferenceEquals(first, null) ? 0 : first.Length;
            Int32 secondLength = Object.ReferenceEquals(second, null) ? 0 : second.Length;
            Int32 totalLength = firstLength + secondLength;
            if (totalLength == 0) return Empty;
            if (totalLength < firstLength || totalLength > MaxLength) throw new OutOfMemoryException();

            void* address = AllocateRuntimeString(global::Internal.Runtime.MethodTable.Of<String>(), totalLength);
            if (address == null) throw new OutOfMemoryException();
            UInt64 raw = (UInt64)(nuint)address;
            String result = Runtime.CompilerServices.Unsafe.As<UInt64, String>(ref raw);
            ref Char destination = ref result.GetRawStringData();
            Char* destinationPointer = (Char*)Runtime.CompilerServices.Unsafe.AsPointer(ref destination);
            Int32 offset = 0;
            for (Int32 index = 0; index < firstLength; index++) destinationPointer[offset++] = ReadRuntimeChar(first, index);
            for (Int32 index = 0; index < secondLength; index++) destinationPointer[offset++] = ReadRuntimeChar(second, index);
            return result;
        }

        /// <summary>Concatenates three strings, treating null as an empty string.</summary>
        public static String Concat(String first, String second, String third)
            => Concat(Concat(first, second), third);

        /// <summary>Concatenates four strings, treating null as an empty string.</summary>
        public static String Concat(String first, String second, String third, String fourth)
            => Concat(Concat(first, second, third), fourth);

        public Char this[Int32 index]
        {
            [Runtime.CompilerServices.Intrinsic]
            get
            {
                Int32 length = ReadRuntimeLength(this);
                if ((UInt32)index >= (UInt32)length) return (Char)0;
                return ReadRuntimeChar(this, index);
            }
        }

        [Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [Runtime.RuntimeImport("*", "RhNewString")]
        private static extern void* AllocateRuntimeString(global::Internal.Runtime.MethodTable* methodTable, Int32 characterCount);

        // Freestanding string materialisation used by CoreLib helpers such as
        // System.Text.StringBuilder and Encoding. RhNewString owns the NativeAOT
        // variable-sized string allocation contract; callers only fill UTF-16 data.
        internal static String CreateFromChars(Char[] characters, Int32 length)
        {
            if (characters == null) throw new ArgumentNullException();
            if (length < 0 || length > characters.Length || length > MaxLength) throw new ArgumentOutOfRangeException();
            if (length == 0) return Empty;

            void* address = AllocateRuntimeString(global::Internal.Runtime.MethodTable.Of<String>(), length);
            if (address == null) throw new OutOfMemoryException();
            UInt64 raw = (UInt64)(nuint)address;
            String value = Runtime.CompilerServices.Unsafe.As<UInt64, String>(ref raw);
            ref Char destination = ref value.GetRawStringData();
            Char* pointer = (Char*)Runtime.CompilerServices.Unsafe.AsPointer(ref destination);
            for (Int32 index = 0; index < length; index++) pointer[index] = characters[index];
            return value;
        }
    }
    #pragma warning restore CS0660, CS0661
    // CONTRACT with .NET 10 NativeAOT System.Private.CoreLib. ILC recognizes
    // both this four-field order and the initializer method names below.
    public class Delegate
    {
        private Object _firstParameter;
        private Object _helperObject;
        private nint _extraFunctionPointerOrData;
        private IntPtr _functionPointer;

        // NativeAOT's generated multicast thunk looks this nested type up by
        // name. It has no impact on a single-cast delegate's object layout.
        private struct Wrapper
        {
            public Wrapper(Delegate value) { Value = value; }
            public Delegate Value;
        }

        private protected const Int32 MulticastThunk = 0;
        private protected const Int32 ClosedStaticThunk = 1;
        private protected const Int32 OpenStaticThunk = 2;
        private protected const Int32 ClosedInstanceThunkOverGenericMethod = 3;
        private protected const Int32 OpenInstanceThunk = 4;
        private protected const Int32 ObjectArrayThunk = 5;

        private protected virtual IntPtr GetThunk(Int32 whichThunk) => default;

        private void InitializeClosedInstance(Object firstParameter, IntPtr functionPointer)
        {
            if (Object.ReferenceEquals(firstParameter, null)) throw new ArgumentException();
            _functionPointer = functionPointer;
            _firstParameter = firstParameter;
        }

        private void InitializeClosedInstanceSlow(Object firstParameter, IntPtr functionPointer)
            => InitializeClosedInstance(firstParameter, functionPointer);

        private void InitializeClosedInstanceWithoutNullCheck(Object firstParameter, IntPtr functionPointer)
        {
            _functionPointer = functionPointer;
            _firstParameter = firstParameter;
        }

        private void InitializeClosedStaticThunk(Object firstParameter, IntPtr functionPointer, IntPtr functionPointerThunk)
        {
            _extraFunctionPointerOrData = functionPointer;
            _helperObject = firstParameter;
            _functionPointer = functionPointerThunk;
            _firstParameter = this;
        }

        private void InitializeOpenStaticThunk(Object firstParameter, IntPtr functionPointer, IntPtr functionPointerThunk)
        {
            _firstParameter = this;
            _functionPointer = functionPointerThunk;
            _extraFunctionPointerOrData = functionPointer;
        }

        private void InitializeOpenInstanceThunkDynamic(IntPtr functionPointer, IntPtr functionPointerThunk)
        {
            _firstParameter = this;
            _functionPointer = functionPointerThunk;
            _extraFunctionPointerOrData = functionPointer;
        }
    }
    public class MulticastDelegate : Delegate { }
    public delegate void Action();
    public delegate void Action<in T>(T obj);
    public delegate void Action<in T1, in T2>(T1 arg1, T2 arg2);
    public delegate void Action<in T1, in T2, in T3>(T1 arg1, T2 arg2, T3 arg3);
    public delegate void Action<in T1, in T2, in T3, in T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    public delegate TResult Func<out TResult>();
    public delegate TResult Func<in T, out TResult>(T arg);
    public delegate TResult Func<in T1, in T2, out TResult>(T1 arg1, T2 arg2);
    public delegate TResult Func<in T1, in T2, in T3, out TResult>(T1 arg1, T2 arg2, T3 arg3);
    public delegate TResult Func<in T1, in T2, in T3, in T4, out TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    public delegate Boolean Predicate<in T>(T obj);
    public delegate Int32 Comparison<in T>(T x, T y);
    public delegate TOutput Converter<in TInput, out TOutput>(TInput input);

    /// <summary><inu.api/>Array-backed mutable span for the Core v1 freestanding target.</summary>
    public ref struct Span<T>
    {
        private T[] _array;
        private Int32 _start;
        private Int32 _length;

        public Span(T[] array)
        {
            if (array == null) { _array = null; _start = 0; _length = 0; return; }
            _array = array; _start = 0; _length = array.Length;
        }

        public Span(T[] array, Int32 start, Int32 length)
        {
            if (array == null)
            {
                if (start != 0 || length != 0) throw new ArgumentOutOfRangeException();
                _array = null; _start = 0; _length = 0; return;
            }
            if (start < 0 || length < 0 || start > array.Length - length) throw new ArgumentOutOfRangeException();
            _array = array; _start = start; _length = length;
        }

        internal Span(T[] array, Int32 start, Int32 length, Boolean trusted)
        { _array = array; _start = start; _length = length; }

        public static Span<T> Empty => default;
        public Int32 Length => _length;
        public Boolean IsEmpty => _length == 0;
        public ref T this[Int32 index]
        {
            get { if ((UInt32)index >= (UInt32)_length) throw new IndexOutOfRangeException(); return ref _array[_start + index]; }
        }
        public Span<T> Slice(Int32 start) => Slice(start, _length - start);
        public Span<T> Slice(Int32 start, Int32 length)
        {
            if (start < 0 || length < 0 || start > _length - length) throw new ArgumentOutOfRangeException();
            return new Span<T>(_array, _start + start, length, true);
        }
        public T[] ToArray() { T[] copy = new T[_length]; for (Int32 i = 0; i < _length; i++) copy[i] = _array[_start + i]; return copy; }
        public void Clear() { for (Int32 i = 0; i < _length; i++) _array[_start + i] = default; }
        public void Fill(T value) { for (Int32 i = 0; i < _length; i++) _array[_start + i] = value; }
        public void CopyTo(Span<T> destination)
        {
            if (!TryCopyTo(destination)) throw new ArgumentException();
        }
        public Boolean TryCopyTo(Span<T> destination)
        {
            if (destination._length < _length) return false;
            if (_length == 0) return true;
            if (Object.ReferenceEquals(_array, destination._array)
                && destination._start > _start && destination._start < _start + _length)
            {
                for (Int32 i = _length - 1; i >= 0; i--) destination._array[destination._start + i] = _array[_start + i];
            }
            else
            {
                for (Int32 i = 0; i < _length; i++) destination._array[destination._start + i] = _array[_start + i];
            }
            return true;
        }
        public static implicit operator Span<T>(T[] array) => new Span<T>(array);
        public static implicit operator ReadOnlySpan<T>(Span<T> span) => new ReadOnlySpan<T>(span._array, span._start, span._length, true);
    }

    /// <summary><inu.api/>Array-backed read-only span for the Core v1 freestanding target.</summary>
    public readonly ref struct ReadOnlySpan<T>
    {
        private readonly T[] _array;
        private readonly Int32 _start;
        private readonly Int32 _length;

        public ReadOnlySpan(T[] array)
        {
            if (array == null) { _array = null; _start = 0; _length = 0; return; }
            _array = array; _start = 0; _length = array.Length;
        }

        public ReadOnlySpan(T[] array, Int32 start, Int32 length)
        {
            if (array == null)
            {
                if (start != 0 || length != 0) throw new ArgumentOutOfRangeException();
                _array = null; _start = 0; _length = 0; return;
            }
            if (start < 0 || length < 0 || start > array.Length - length) throw new ArgumentOutOfRangeException();
            _array = array; _start = start; _length = length;
        }

        internal ReadOnlySpan(T[] array, Int32 start, Int32 length, Boolean trusted)
        { _array = array; _start = start; _length = length; }

        public static ReadOnlySpan<T> Empty => default;
        public Int32 Length => _length;
        public Boolean IsEmpty => _length == 0;
        public ref readonly T this[Int32 index]
        {
            get { if ((UInt32)index >= (UInt32)_length) throw new IndexOutOfRangeException(); return ref _array[_start + index]; }
        }
        public ReadOnlySpan<T> Slice(Int32 start) => Slice(start, _length - start);
        public ReadOnlySpan<T> Slice(Int32 start, Int32 length)
        {
            if (start < 0 || length < 0 || start > _length - length) throw new ArgumentOutOfRangeException();
            return new ReadOnlySpan<T>(_array, _start + start, length, true);
        }
        public T[] ToArray() { T[] copy = new T[_length]; for (Int32 i = 0; i < _length; i++) copy[i] = _array[_start + i]; return copy; }
        public void CopyTo(Span<T> destination)
        {
            if (!TryCopyTo(destination)) throw new ArgumentException();
        }
        public Boolean TryCopyTo(Span<T> destination)
        {
            if (destination.Length < _length) return false;
            for (Int32 i = 0; i < _length; i++) destination[i] = _array[_start + i];
            return true;
        }
        public static implicit operator ReadOnlySpan<T>(T[] array) => new ReadOnlySpan<T>(array);
    }

    /// <summary><inu.api/>Array-backed storable memory window whose Span property provides temporary byref access.</summary>
    public readonly struct Memory<T>
    {
        private readonly T[] _array;
        private readonly Int32 _start;
        private readonly Int32 _length;

        public Memory(T[] array)
        {
            if (array == null) { _array = null; _start = 0; _length = 0; return; }
            _array = array; _start = 0; _length = array.Length;
        }
        public Memory(T[] array, Int32 start, Int32 length)
        {
            if (array == null)
            {
                if (start != 0 || length != 0) throw new ArgumentOutOfRangeException();
                _array = null; _start = 0; _length = 0; return;
            }
            if (start < 0 || length < 0 || start > array.Length - length) throw new ArgumentOutOfRangeException();
            _array = array; _start = start; _length = length;
        }
        internal Memory(T[] array, Int32 start, Int32 length, Boolean trusted) { _array = array; _start = start; _length = length; }
        public static Memory<T> Empty => default;
        public Int32 Length => _length;
        public Boolean IsEmpty => _length == 0;
        public Span<T> Span => new Span<T>(_array, _start, _length, true);
        public Memory<T> Slice(Int32 start) => Slice(start, _length - start);
        public Memory<T> Slice(Int32 start, Int32 length)
        {
            if (start < 0 || length < 0 || start > _length - length) throw new ArgumentOutOfRangeException();
            return new Memory<T>(_array, _start + start, length, true);
        }
        public T[] ToArray() => Span.ToArray();
        public static implicit operator Memory<T>(T[] array) => new Memory<T>(array);
        public static implicit operator ReadOnlyMemory<T>(Memory<T> memory) => new ReadOnlyMemory<T>(memory._array, memory._start, memory._length, true);
    }

    /// <summary><inu.api/>Array-backed storable read-only memory window.</summary>
    public readonly struct ReadOnlyMemory<T>
    {
        private readonly T[] _array;
        private readonly Int32 _start;
        private readonly Int32 _length;
        public ReadOnlyMemory(T[] array)
        {
            if (array == null) { _array = null; _start = 0; _length = 0; return; }
            _array = array; _start = 0; _length = array.Length;
        }
        public ReadOnlyMemory(T[] array, Int32 start, Int32 length)
        {
            if (array == null)
            {
                if (start != 0 || length != 0) throw new ArgumentOutOfRangeException();
                _array = null; _start = 0; _length = 0; return;
            }
            if (start < 0 || length < 0 || start > array.Length - length) throw new ArgumentOutOfRangeException();
            _array = array; _start = start; _length = length;
        }
        internal ReadOnlyMemory(T[] array, Int32 start, Int32 length, Boolean trusted) { _array = array; _start = start; _length = length; }
        public static ReadOnlyMemory<T> Empty => default;
        public Int32 Length => _length;
        public Boolean IsEmpty => _length == 0;
        public ReadOnlySpan<T> Span => new ReadOnlySpan<T>(_array, _start, _length, true);
        public ReadOnlyMemory<T> Slice(Int32 start) => Slice(start, _length - start);
        public ReadOnlyMemory<T> Slice(Int32 start, Int32 length)
        {
            if (start < 0 || length < 0 || start > _length - length) throw new ArgumentOutOfRangeException();
            return new ReadOnlyMemory<T>(_array, _start + start, length, true);
        }
        public T[] ToArray() => Span.ToArray();
        public static implicit operator ReadOnlyMemory<T>(T[] array) => new ReadOnlyMemory<T>(array);
    }

    public class Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Delegate, Inherited = false)]
    public sealed class SerializableAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public sealed class NonSerializedAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public sealed class ThreadStaticAttribute : Attribute { }

    [Flags]
    public enum AttributeTargets
    {
        Assembly = 1,
        Module = 2,
        Class = 4,
        Struct = 8,
        Enum = 16,
        Constructor = 32,
        Method = 64,
        Property = 128,
        Field = 256,
        Event = 512,
        Interface = 1024,
        Parameter = 2048,
        Delegate = 4096,
        ReturnValue = 8192,
        GenericParameter = 16384,
        All = 32767
    }

    [AttributeUsage(AttributeTargets.Enum, Inherited = false)]
    public sealed class FlagsAttribute : Attribute { }

    public sealed class AttributeUsageAttribute : Attribute
    {
        public AttributeUsageAttribute(AttributeTargets targets) { }
        public Boolean AllowMultiple { get; set; }
        public Boolean Inherited { get; set; }
    }
    // CONTRACT with .NET 10 NativeAOT Runtime.Base. RuntimeTypeHandle is a
    // pointer-sized wrapper around the emitted MethodTable/EEType address used by
    // ldtoken, typeof and Object.GetType lowering.
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RuntimeTypeHandle
    {
        private IntPtr _value;

        internal RuntimeTypeHandle(IntPtr value) { _value = value; }
        internal RuntimeTypeHandle(MethodTable* value) : this((IntPtr)value) { }

        public IntPtr Value => _value;
        internal Boolean IsNull => _value == IntPtr.Zero;

        public static RuntimeTypeHandle FromIntPtr(IntPtr value) => new RuntimeTypeHandle(value);

        [Runtime.CompilerServices.Intrinsic]
        public static IntPtr ToIntPtr(RuntimeTypeHandle handle) => handle._value;

        [Runtime.CompilerServices.Intrinsic]
        internal MethodTable* ToMethodTable() => (MethodTable*)(void*)_value;

        public override Boolean Equals(Object value)
            => value is RuntimeTypeHandle && Equals((RuntimeTypeHandle)value);
        public Boolean Equals(RuntimeTypeHandle handle) => _value == handle._value;
        public override Int32 GetHashCode()
        {
            if (IsNull) return 0;
            return unchecked((Int32)ToMethodTable()->HashCode);
        }
        public static Boolean operator ==(RuntimeTypeHandle left, RuntimeTypeHandle right) => left._value == right._value;
        public static Boolean operator !=(RuntimeTypeHandle left, RuntimeTypeHandle right) => left._value != right._value;
    }

    // Minimal NativeAOT type object. Inu deliberately does not implement broad
    // reflection here; this surface is the compiler/runtime contract needed by
    // typeof, ldtoken, Object.GetType, base-type walking and parameterized types.
    public unsafe class Type
    {
        private readonly RuntimeTypeHandle _typeHandle;

        private Type(RuntimeTypeHandle typeHandle) { _typeHandle = typeHandle; }

        public RuntimeTypeHandle TypeHandle => _typeHandle;

        [Runtime.CompilerServices.Intrinsic]
        public static Type GetTypeFromHandle(RuntimeTypeHandle handle)
            => GetTypeFromMethodTable(handle.ToMethodTable());

        // CONTRACT with .NET 10 NativeAOT: Object.GetType and the RyuJIT
        // GetRuntimeType helper bind to this exact compiler-known entry point.
        [Runtime.CompilerServices.Intrinsic]
        internal static Type GetTypeFromMethodTable(MethodTable* pMT)
            => pMT == null ? null : new Type(new RuntimeTypeHandle(pMT));

        internal MethodTable* EEType => _typeHandle.ToMethodTable();

        public Boolean IsValueType => EEType != null && EEType->IsValueType;
        public Boolean IsPrimitive => EEType != null && EEType->IsPrimitive;
        public Boolean IsArray => EEType != null && EEType->IsArray;
        public Boolean IsSZArray => EEType != null && EEType->IsSzArray;
        public Boolean IsInterface => EEType != null && EEType->IsInterface;
        public Boolean IsPointer => EEType != null && EEType->IsPointer;
        public Boolean IsByRef => EEType != null && EEType->IsByRef;
        public Boolean IsGenericType => EEType != null && EEType->IsGeneric;
        public Boolean IsGenericTypeDefinition => EEType != null && EEType->IsGenericTypeDefinition;

        public Type BaseType
        {
            get
            {
                MethodTable* mt = EEType;
                if (mt == null) return null;
                MethodTable* baseType = mt->BaseType;
                return baseType == null ? null : GetTypeFromHandle(new RuntimeTypeHandle(baseType));
            }
        }

        public Type GetElementType()
        {
            MethodTable* mt = EEType;
            if (mt == null || (!mt->IsArray && !mt->IsPointer && !mt->IsByRef)) return null;
            MethodTable* related = mt->RelatedParameterType;
            return related == null ? null : GetTypeFromHandle(new RuntimeTypeHandle(related));
        }

        public Boolean IsAssignableFrom(Type candidate)
        {
            if (Object.ReferenceEquals(candidate, null)) return false;
            MethodTable* target = EEType;
            MethodTable* source = candidate.EEType;
            return Runtime.TypeCast.IsAssignable(source, target);
        }

        public Boolean IsAssignableTo(Type targetType)
            => !Object.ReferenceEquals(targetType, null) && targetType.IsAssignableFrom(this);

        public Boolean IsSubclassOf(Type candidateBase)
        {
            if (Object.ReferenceEquals(candidateBase, null)) return false;
            MethodTable* target = candidateBase.EEType;
            MethodTable* current = EEType;
            if (current == null || target == null || current == target || current->IsArray) return false;
            current = current->NonArrayBaseType;
            for (Int32 depth = 0; current != null && depth < 256; depth++)
            {
                if (current == target) return true;
                MethodTable* next = current->NonArrayBaseType;
                if (next == current) return false;
                current = next;
            }
            return false;
        }

        [Runtime.CompilerServices.Intrinsic]
        public static Boolean operator ==(Type left, Type right)
        {
            if (Object.ReferenceEquals(left, right)) return true;
            if (Object.ReferenceEquals(left, null) || Object.ReferenceEquals(right, null)) return false;
            return left._typeHandle == right._typeHandle;
        }

        [Runtime.CompilerServices.Intrinsic]
        public static Boolean operator !=(Type left, Type right) => !(left == right);

        public override Boolean Equals(Object value) => value is Type && this == (Type)value;
        public override Int32 GetHashCode()
        {
            MethodTable* mt = EEType;
            return mt == null ? 0 : unchecked((Int32)mt->HashCode);
        }
    }

    // CONTRACT with .NET 10 NativeAOT System.RuntimeMethodHandle.cs. The handle is
    // exactly one pointer to a MethodHandleInfo record emitted by ILC.
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RuntimeMethodHandle
    {
        private IntPtr _value;
        private RuntimeMethodHandle(IntPtr value) { _value = value; }
        public IntPtr Value => _value;
        public static RuntimeMethodHandle FromIntPtr(IntPtr value) => new RuntimeMethodHandle(value);
        public static IntPtr ToIntPtr(RuntimeMethodHandle value) => value._value;
        internal MethodHandleInfo* ToMethodHandleInfo() => (MethodHandleInfo*)(void*)_value;

        public override Boolean Equals(Object value)
            => value is RuntimeMethodHandle && Equals((RuntimeMethodHandle)value);

        public Boolean Equals(RuntimeMethodHandle handle)
        {
            if (_value == handle._value) return true;
            if (_value == IntPtr.Zero || handle._value == IntPtr.Zero) return false;
            MethodHandleInfo* left = ToMethodHandleInfo();
            MethodHandleInfo* right = handle.ToMethodHandleInfo();
            if (left->DeclaringType != right->DeclaringType || left->Handle != right->Handle || left->NumGenericArgs != right->NumGenericArgs) return false;
            RuntimeTypeHandle* leftArg = &left->FirstArgument;
            RuntimeTypeHandle* rightArg = &right->FirstArgument;
            for (Int32 i = 0; i < left->NumGenericArgs; i++)
                if (leftArg[i] != rightArg[i]) return false;
            return true;
        }

        public override Int32 GetHashCode()
        {
            if (_value == IntPtr.Zero) return 0;
            MethodHandleInfo* info = ToMethodHandleInfo();
            Int32 hash = info->DeclaringType.GetHashCode();
            Int32 rotated = (hash << 13) | (Int32)((UInt32)hash >> 19);
            hash = unchecked(hash + rotated) ^ info->Handle.GetHashCode();
            RuntimeTypeHandle* arg = &info->FirstArgument;
            for (Int32 i = 0; i < info->NumGenericArgs; i++)
            {
                Int32 argumentHashCode = arg[i].GetHashCode();
                rotated = (hash << 13) | (Int32)((UInt32)hash >> 19);
                hash = unchecked(hash + rotated) ^ argumentHashCode;
            }
            return hash;
        }

        public static Boolean operator ==(RuntimeMethodHandle left, RuntimeMethodHandle right) => left.Equals(right);
        public static Boolean operator !=(RuntimeMethodHandle left, RuntimeMethodHandle right) => !left.Equals(right);
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RuntimeFieldHandle
    {
        private IntPtr _value;
        private RuntimeFieldHandle(IntPtr value) { _value = value; }
        public IntPtr Value => _value;
        public static RuntimeFieldHandle FromIntPtr(IntPtr value) => new RuntimeFieldHandle(value);
        public static IntPtr ToIntPtr(RuntimeFieldHandle value) => value._value;
        internal FieldHandleInfo* ToFieldHandleInfo() => (FieldHandleInfo*)(void*)_value;

        public override Boolean Equals(Object value)
            => value is RuntimeFieldHandle && Equals((RuntimeFieldHandle)value);

        public Boolean Equals(RuntimeFieldHandle handle)
        {
            if (_value == handle._value) return true;
            if (_value == IntPtr.Zero || handle._value == IntPtr.Zero) return false;
            FieldHandleInfo* left = ToFieldHandleInfo();
            FieldHandleInfo* right = handle.ToFieldHandleInfo();
            return left->DeclaringType == right->DeclaringType && left->Handle == right->Handle;
        }

        public override Int32 GetHashCode()
        {
            if (_value == IntPtr.Zero) return 0;
            FieldHandleInfo* info = ToFieldHandleInfo();
            return info->DeclaringType.GetHashCode() ^ info->Handle.GetHashCode();
        }

        public static Boolean operator ==(RuntimeFieldHandle left, RuntimeFieldHandle right) => left.Equals(right);
        public static Boolean operator !=(RuntimeFieldHandle left, RuntimeFieldHandle right) => !left.Equals(right);
    }

    // Field order matches the normal Nullable<T> contract used by Roslyn/ILC.
    // Exception throwing from Value when HasValue is false remains part of the
    // later exception-runtime milestone; present values already preserve normal layout.
    public struct Nullable<T> where T : struct
    {
        private readonly Boolean hasValue;
        internal T value;

        public Nullable(T value)
        {
            this.value = value;
            hasValue = true;
        }

        public Boolean HasValue => hasValue;
        public T Value
        {
            get
            {
                if (!hasValue) throw new InvalidOperationException("Nullable object must have a value.");
                return value;
            }
        }
        public T GetValueOrDefault() => value;
        public T GetValueOrDefault(T defaultValue) => hasValue ? value : defaultValue;
        public static implicit operator Nullable<T>(T value) => new Nullable<T>(value);
        public static explicit operator T(Nullable<T> value) => value.Value;
    }

    namespace Reflection
    {
        public sealed class DefaultMemberAttribute : Attribute
        {
            public DefaultMemberAttribute(String memberName) { }
        }
    }

    namespace Runtime
    {
        internal static class ActivationRuntimeDiagnostics
        {
#pragma warning disable CS0626
            [CompilerServices.MethodImpl(CompilerServices.MethodImplOptions.InternalCall)]
            [RuntimeImport("*", "InuEhTrace")]
            private static extern void TraceStageNative(UInt64 code);

            [CompilerServices.MethodImpl(CompilerServices.MethodImplOptions.InternalCall)]
            [RuntimeImport("*", "InuEhTraceValue")]
            private static extern void TraceValueNative(UInt64 tag, UInt64 value);
#pragma warning restore CS0626

            internal static void TraceStage(UInt64 code) => TraceStageNative(code);
            internal static void TraceValue(UInt64 tag, UInt64 value) => TraceValueNative(tag, value);
        }

        // CONTRACT with .NET 10 NativeAOT. The compiler treats a type named
        // RawCalliHelper specially and suppresses fat-call adaptation for calls made
        // from these methods. This is required when invoking allocator/default-ctor
        // entry points obtained from generic dictionaries.
        internal static unsafe class RawCalliHelper
        {
            [CompilerServices.MethodImpl(CompilerServices.MethodImplOptions.AggressiveInlining)]
            public static void CallDefaultStructConstructor(IntPtr functionPointer, ref Byte data)
            {
                if (global::Internal.Runtime.CompilerServices.FunctionPointerOps.IsGenericMethodPointer(functionPointer))
                {
                    global::Internal.Runtime.CompilerServices.GenericMethodDescriptor* descriptor =
                        global::Internal.Runtime.CompilerServices.FunctionPointerOps.ConvertToGenericDescriptor(functionPointer);
                    ((delegate*<ref Byte, IntPtr, void>)descriptor->MethodFunctionPointer)(ref data, descriptor->InstantiationArgument);
                }
                else
                {
                    ((delegate*<ref Byte, void>)functionPointer)(ref data);
                }
            }

            [CompilerServices.MethodImpl(CompilerServices.MethodImplOptions.AggressiveInlining)]
            public static T Call<T>(IntPtr functionPointer, IntPtr argument)
                => ((delegate*<IntPtr, T>)functionPointer)(argument);

            [CompilerServices.MethodImpl(CompilerServices.MethodImplOptions.AggressiveInlining)]
            public static void Call(IntPtr functionPointer, Object argument)
                => ((delegate*<Object, void>)functionPointer)(argument);
        }

        public sealed class RuntimeExportAttribute : Attribute
        {
            public RuntimeExportAttribute(String name) { }
        }

        // CONTRACT with .NET 10 NativeAOT Runtime.Base. RuntimeImport is the
        // compiler-known direct runtime-call marker. Unlike DllImport it does not
        // invoke the managed/native marshaller, which is essential for freestanding
        // runtime shims that pass managed object references directly.
        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited = false)]
        public sealed class RuntimeImportAttribute : Attribute
        {
            public String DllName { get; }
            public String EntryPoint { get; }

            public RuntimeImportAttribute(String entry) { EntryPoint = entry; }
            public RuntimeImportAttribute(String dllName, String entry)
            {
                DllName = dllName;
                EntryPoint = entry;
            }
        }
    }

    namespace Runtime.CompilerServices
    {
        public sealed class CompilerGeneratedAttribute : Attribute { }
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Field | AttributeTargets.Interface, Inherited = false)]
        public sealed class IntrinsicAttribute : Attribute { }

        // .NET 10 CoreLib contract. Roslyn emits this attribute for extension methods;
        // NativeAOT RuntimeHelpers.GetMethodTable is intentionally declared as an extension
        // method in the upstream class library. Keeping the real attribute lets Inu use
        // the compiler-known source/metadata shape without referencing System.Core.
        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
        public sealed class ExtensionAttribute : Attribute { }
        public sealed class IsReadOnlyAttribute : Attribute { }
        public sealed class IsByRefLikeAttribute : Attribute { }
        // Required by Roslyn when the C# volatile modifier is emitted. In the
        // freestanding CoreLib this is a marker type only; the modifier itself
        // is carried in metadata as modreq(IsVolatile).
        public sealed class IsVolatile { }

        // 0.0.88: CONTRACT with .NET 10.0.10 NativeAOT GenericUnboxingThunk.
        // The compiler looks this type up by its exact fully-qualified name and
        // takes a managed byref to Data when adapting a boxed generic value-type
        // receiver to the underlying generic method implementation.
        [StructLayout(LayoutKind.Sequential)]
        internal sealed class RawData
        {
            public Byte Data;
        }

        // CONTRACT with .NET 10 NativeAOT Runtime.Base. Roslyn encodes this
        // compiler-known attribute into method implementation flags; RuntimeImport
        // extern methods must also carry InternalCall to produce valid IL metadata.
        public enum MethodImplOptions
        {
            Unmanaged = 0x0004,
            NoInlining = 0x0008,
            NoOptimization = 0x0040,
            AggressiveInlining = 0x0100,
            AggressiveOptimization = 0x0200,
            InternalCall = 0x1000,
        }

        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, Inherited = false)]
        public sealed class MethodImplAttribute : Attribute
        {
            internal MethodImplOptions _val;
            public MethodImplAttribute(MethodImplOptions methodImplOptions) { _val = methodImplOptions; }
            public MethodImplAttribute(Int16 value) { _val = (MethodImplOptions)value; }
            public MethodImplAttribute() { }
            public MethodImplOptions Value => _val;
        }

        /// <summary>Minimal compiler-recognized Unsafe surface required by the freestanding runtime ABI.</summary>
        public static class Unsafe
        {
            [Intrinsic]
            [Runtime.Versioning.NonVersionable]
            public static T As<T>(Object value) where T : class
            {
                // NativeAOT replaces this method body with a reference reinterpretation.
                while (true) { }
            }

            [Intrinsic]
            [Runtime.Versioning.NonVersionable]
            public static ref TTo As<TFrom, TTo>(ref TFrom source)
            {
                // NativeAOT replaces this method body with a byref reinterpretation.
                while (true) { }
            }

            [Intrinsic]
            [Runtime.Versioning.NonVersionable]
            public static unsafe void* AsPointer<T>(ref T value)
            {
                // NativeAOT replaces this method body with a managed-byref to raw-pointer conversion.
                while (true) { }
            }
        }

        [AttributeUsage(AttributeTargets.Class, Inherited = false)]
        public sealed class CompilerLoweringPreserveAttribute : Attribute
        {
            public CompilerLoweringPreserveAttribute() { }
        }

        public static class RuntimeFeature
        {
            // CONTRACT with Roslyn C# ref-field lowering. The compiler probes this
            // exact member before it will emit the managed-byref field used by
            // System.ByReference / NativeAOT DynamicInvokeMethodThunk.
            public const String ByRefFields = nameof(ByRefFields);
            public const String UnmanagedSignatureCallingConvention = nameof(UnmanagedSignatureCallingConvention);
        }
        public static class RuntimeHelpers
        {
            public static unsafe Int32 OffsetToStringData => sizeof(IntPtr) + sizeof(Int32);

            [Intrinsic]
            public static unsafe Boolean IsReferenceOrContainsReferences<T>()
            {
                global::Internal.Runtime.MethodTable* mt = global::Internal.Runtime.MethodTable.Of<T>();
                return mt != null && (!mt->IsValueType || mt->ContainsGCPointers);
            }

            // CONTRACT with .NET 10 NativeAOT. The compiler recognizes this helper
            // and lowers MethodTable reads for compiler-special managed references
            // (including delegates) without relying on an ordinary instance call.
            [Intrinsic]
            internal static unsafe global::Internal.Runtime.MethodTable* GetMethodTable(this Object obj)
                => obj.MethodTable;
        }
    }

    namespace Runtime.Versioning
    {
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
        public sealed class NonVersionableAttribute : Attribute { }
    }

    namespace Runtime.InteropServices
    {
        public enum CallingConvention { Winapi = 1, Cdecl = 2, StdCall = 3, ThisCall = 4, FastCall = 5 }
        public enum CharSet { None = 1, Ansi = 2, Unicode = 3, Auto = 4 }
        public enum LayoutKind { Sequential = 0, Explicit = 2, Auto = 3 }
        public sealed class InAttribute : Attribute { }
        [AttributeUsage(AttributeTargets.Field, Inherited = false)]
        public sealed class FieldOffsetAttribute : Attribute
        {
            public FieldOffsetAttribute(Int32 offset) { Value = offset; }
            public Int32 Value { get; }
        }
        public sealed class StructLayoutAttribute : Attribute
        {
            public StructLayoutAttribute(LayoutKind kind) { Value = kind; }
            public CharSet CharSet;
            public Int32 Pack;
            public Int32 Size;
            public LayoutKind Value { get; }
        }
        public sealed class DllImportAttribute : Attribute
        {
            public DllImportAttribute(String libraryName) { }
            public String EntryPoint { get; set; }
            public CallingConvention CallingConvention { get; set; }
            public Boolean ExactSpelling { get; set; }
        }
    }
}

namespace Internal.TypeSystem
{
    // CONTRACT with .NET 10 NativeAOT TypeSystem. ILC passes these numeric IDs
    // to compiler-known ThrowHelpers when it substitutes invalid-program bodies.
    public enum ExceptionStringID
    {
        ClassLoadGeneral, ClassLoadExplicitGeneric, ClassLoadBadFormat, ClassLoadExplicitLayout,
        ClassLoadValueClassTooLarge, ClassLoadRankTooLarge, ClassLoadInlineArrayFieldCount,
        ClassLoadInlineArrayLength, ClassLoadInlineArrayExplicit, ClassLoadInlineArrayExplicitSize,
        MissingMethod, MissingField, FileLoadErrorGeneric, InvalidProgramDefault,
        InvalidProgramSpecific, InvalidProgramVararg, InvalidProgramCallVirtFinalize,
        InvalidProgramNonStaticMethod, InvalidProgramGenericMethod, InvalidProgramNonBlittableTypes,
        InvalidProgramMultipleCallConv, BadImageFormatGeneric, MarshalDirectiveGeneric,
        AmbiguousMatchUnsafeAccessor,
    }
}

namespace Internal.Runtime
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct ObjHeader
    {
        private System.IntPtr _objHeaderContents;
    }


    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal unsafe struct DispatchMap
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct DispatchMapEntry
        {
            internal System.UInt16 InterfaceIndex;
            internal System.UInt16 InterfaceMethodSlot;
            internal System.UInt16 ImplMethodSlot;
        }

        // .NET 10 static-interface dispatch entries extend the ordinary 6-byte
        // map entry with the generic-context source used by static abstract/
        // virtual interface calls.
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct StaticDispatchMapEntry
        {
            internal DispatchMapEntry Entry;
            internal System.UInt16 ContextMapSource;
        }

        private System.UInt16 _standardEntryCount;
        private System.UInt16 _defaultEntryCount;
        private System.UInt16 _standardStaticEntryCount;
        private System.UInt16 _defaultStaticEntryCount;
        private DispatchMapEntry _firstEntry;

        internal System.UInt16 StandardEntryCount => _standardEntryCount;
        internal System.UInt16 DefaultEntryCount => _defaultEntryCount;
        internal System.UInt16 StandardStaticEntryCount => _standardStaticEntryCount;
        internal System.UInt16 DefaultStaticEntryCount => _defaultStaticEntryCount;

        internal DispatchMapEntry* GetEntry(System.Int32 index)
            => (DispatchMapEntry*)((System.Byte*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref _firstEntry) + index * sizeof(DispatchMapEntry));

        internal StaticDispatchMapEntry* GetStaticEntry(System.Int32 index)
        {
            System.Byte* staticBase = (System.Byte*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref _firstEntry)
                + (_standardEntryCount + _defaultEntryCount) * sizeof(DispatchMapEntry);
            return ((StaticDispatchMapEntry*)staticBase) + index;
        }
    }

    // .NET 10 NativeAOT MethodTable/EEType contract used by Inu. The fixed
    // 24-byte header is ABI-identical to Runtime.Base; emitted vtable slots and the
    // interface map immediately follow it. Optional metadata remains owned by ILC.
    [System.Flags]
    internal enum EETypeFlags : System.UInt32
    {
        EETypeKindMask = 0x00030000U,
        HasDispatchMap = 0x00040000U,
        IsDynamicTypeFlag = 0x00080000U,
        HasFinalizerFlag = 0x00100000U,
        HasSealedVTableEntriesFlag = 0x00400000U,
        GenericVarianceFlag = 0x00800000U,
        HasPointersFlag = 0x01000000U,
        IsGenericFlag = 0x02000000U,
        ElementTypeMask = 0x7C000000U,
        ElementTypeShift = 26,
        HasComponentSizeFlag = 0x80000000U
    }

    [System.Flags]
    internal enum EETypeFlagsEx : System.UInt16
    {
        HasEagerFinalizerFlag = 0x0001,
        HasCriticalFinalizerFlag = 0x0002,
        IsTrackedReferenceWithFinalizerFlag = 0x0004,
        IDynamicInterfaceCastableFlag = 0x0008,
        IsByRefLikeFlag = 0x0010,
        ValueTypeFieldPaddingMask = 0x00E0,
        NullableValueOffsetMask = 0x0700,
        RequiresAlign8Flag = 0x1000
    }

    internal enum EETypeKind : System.UInt32
    {
        CanonicalEEType = 0x00000000U,
        FunctionPointerEEType = 0x00010000U,
        ParameterizedEEType = 0x00020000U,
        GenericTypeDefEEType = 0x00030000U
    }

    internal enum EETypeElementType
    {
        Unknown = 0x00, Void = 0x01, Boolean = 0x02, Char = 0x03, SByte = 0x04, Byte = 0x05,
        Int16 = 0x06, UInt16 = 0x07, Int32 = 0x08, UInt32 = 0x09, Int64 = 0x0A, UInt64 = 0x0B,
        IntPtr = 0x0C, UIntPtr = 0x0D, Single = 0x0E, Double = 0x0F, ValueType = 0x10,
        Nullable = 0x12, Class = 0x14, Interface = 0x15, SystemArray = 0x16, Array = 0x17,
        SzArray = 0x18, ByRef = 0x19, Pointer = 0x1A, FunctionPointer = 0x1B
    }

    // .NET 10 NativeAOT generic variance encoding. Kept in lock-step with
    // MethodTable.Constants.cs / rhbinder.h so freestanding cast and dispatch
    // logic can consume compiler-emitted generic composition metadata.
    internal enum GenericVariance : System.Byte
    {
        NonVariant = 0,
        Covariant = 1,
        Contravariant = 2,
        ArrayCovariant = 0x20
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal unsafe struct MethodTable
    {
        // .NET 10 NativeAOT x64 MethodTable constants. The runtime and ILC both
        // assume the SZArray base size includes ObjHeader, MethodTable*, length and
        // x64 padding before the first element.
        private const System.Int32 POINTER_SIZE = 8;
        private const System.Int32 PADDING = 1;
        internal const System.Int32 SZARRAY_BASE_SIZE = POINTER_SIZE + POINTER_SIZE + (1 + PADDING) * 4;

        internal static System.Boolean SupportsRelativePointers
        {
            [System.Runtime.CompilerServices.Intrinsic]
            get { throw new System.NotImplementedException(); }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        private unsafe struct RelatedTypeUnion
        {
            [System.Runtime.InteropServices.FieldOffset(0)] internal MethodTable* _pBaseType;
            [System.Runtime.InteropServices.FieldOffset(0)] internal MethodTable* _pRelatedParameterType;
        }

#pragma warning disable CS0626
        [System.Runtime.CompilerServices.Intrinsic]
        internal static extern MethodTable* Of<T>();
#pragma warning restore CS0626

        private System.UInt32 _uFlags;
        private System.UInt32 _uBaseSize;
        private RelatedTypeUnion _relatedType;
        private System.UInt16 _usNumVtableSlots;
        private System.UInt16 _usNumInterfaces;
        private System.UInt32 _uHashCode;

        internal System.UInt32 Flags => _uFlags;
        internal EETypeKind Kind => (EETypeKind)(_uFlags & (System.UInt32)EETypeFlags.EETypeKindMask);
        internal System.Boolean HasComponentSize => (System.Int32)_uFlags < 0;
        internal System.UInt16 ComponentSize => HasComponentSize ? (System.UInt16)_uFlags : (System.UInt16)0;
        internal System.UInt16 ExtendedFlags => HasComponentSize ? (System.UInt16)0 : (System.UInt16)_uFlags;
        internal System.UInt32 RawBaseSize => _uBaseSize;
        internal System.UInt32 BaseSize => _uBaseSize;
        internal System.UInt16 GenericParameterCount => IsGenericTypeDefinition ? (System.UInt16)_uBaseSize : (System.UInt16)0;
        internal System.UInt16 NumVtableSlots => _usNumVtableSlots;
        internal System.UInt16 NumInterfaces => _usNumInterfaces;
        internal System.UInt32 HashCode => _uHashCode;

        internal EETypeElementType ElementType
            => (EETypeElementType)((_uFlags & (System.UInt32)EETypeFlags.ElementTypeMask) >> (System.Int32)EETypeFlags.ElementTypeShift);

        internal System.Boolean IsCanonical => Kind == EETypeKind.CanonicalEEType;
        internal System.Boolean IsDefType => Kind == EETypeKind.CanonicalEEType || Kind == EETypeKind.GenericTypeDefEEType;
        internal System.Boolean IsParameterizedType => Kind == EETypeKind.ParameterizedEEType;
        internal System.Boolean IsFunctionPointer => Kind == EETypeKind.FunctionPointerEEType;
        internal System.Boolean IsGenericTypeDefinition => Kind == EETypeKind.GenericTypeDefEEType;
        internal System.Boolean IsGeneric => (_uFlags & (System.UInt32)EETypeFlags.IsGenericFlag) != 0U;
        internal System.Boolean IsDynamicType => (_uFlags & (System.UInt32)EETypeFlags.IsDynamicTypeFlag) != 0U;
        internal System.Boolean HasGenericVariance => (_uFlags & (System.UInt32)EETypeFlags.GenericVarianceFlag) != 0U;
        internal System.Boolean IsFinalizable => (_uFlags & (System.UInt32)EETypeFlags.HasFinalizerFlag) != 0U;
        internal System.Boolean ContainsGCPointers => (_uFlags & (System.UInt32)EETypeFlags.HasPointersFlag) != 0U;
        internal System.Boolean HasDispatchMap => (_uFlags & (System.UInt32)EETypeFlags.HasDispatchMap) != 0U;
        internal System.Boolean HasSealedVTableEntries => (_uFlags & (System.UInt32)EETypeFlags.HasSealedVTableEntriesFlag) != 0U;
        internal System.Boolean IsByRefLike => IsValueType && (ExtendedFlags & (System.UInt16)EETypeFlagsEx.IsByRefLikeFlag) != 0U;
        internal System.Boolean IsNullable => ElementType == EETypeElementType.Nullable;
        internal System.Boolean IsInterface => ElementType == EETypeElementType.Interface;
        internal System.Boolean IsPointer => ElementType == EETypeElementType.Pointer;
        internal System.Boolean IsByRef => ElementType == EETypeElementType.ByRef;
        internal System.Boolean IsArray => ElementType == EETypeElementType.Array || ElementType == EETypeElementType.SzArray;
        internal System.Boolean IsSzArray => IsArray && BaseSize == (System.UInt32)SZARRAY_BASE_SIZE;
        internal System.Boolean IsMultiDimensionalArray => IsArray && BaseSize > (System.UInt32)(3 * POINTER_SIZE);
        internal System.Int32 ArrayRank
        {
            get
            {
                if (!IsArray) return 0;
                System.Int32 boundsSize = (System.Int32)BaseSize - SZARRAY_BASE_SIZE;
                return boundsSize > 0 ? boundsSize / (2 * sizeof(System.Int32)) : 1;
            }
        }
        internal System.UInt32 NumFunctionPointerParameters => IsFunctionPointer ? (_uBaseSize & 0x7FFFFFFFU) : 0U;
        internal System.Boolean IsUnmanagedFunctionPointer => IsFunctionPointer && (_uBaseSize & 0x80000000U) != 0U;
        internal MethodTable* FunctionPointerReturnType => IsFunctionPointer ? _relatedType._pRelatedParameterType : null;
        internal System.Boolean IsString => HasComponentSize && ComponentSize == sizeof(System.Char) && IsCanonical;
        internal System.Boolean IsValueType => ElementType < EETypeElementType.Class;
        internal System.Boolean IsPrimitive => ElementType < EETypeElementType.ValueType;

        internal System.UInt32 ValueTypeFieldPadding
            => (System.UInt32)((ExtendedFlags & (System.UInt16)EETypeFlagsEx.ValueTypeFieldPaddingMask) >> 5);
        internal System.UInt32 ValueTypeSize
            => IsValueType ? BaseSize - (System.UInt32)sizeof(ObjHeader) - (System.UInt32)sizeof(MethodTable*) - ValueTypeFieldPadding : 0U;

        internal MethodTable* BaseType
        {
            get
            {
                if (IsArray) return global::Internal.Runtime.MethodTable.Of<System.Array>();
                return IsCanonical ? _relatedType._pBaseType : null;
            }
        }

        internal MethodTable* NonArrayBaseType => IsArray ? null : BaseType;
        internal MethodTable* RelatedParameterType
            => (IsArray || IsParameterizedType) ? _relatedType._pRelatedParameterType : null;

        internal System.IntPtr* VTable
            => (System.IntPtr*)((System.Byte*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref this) + sizeof(MethodTable));

        internal MethodTable** InterfaceMap
            => (MethodTable**)((System.Byte*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref this) + sizeof(MethodTable) + sizeof(System.IntPtr) * _usNumVtableSlots);

        internal System.IntPtr GetVTableSlot(System.UInt16 slot)
            => slot < _usNumVtableSlots ? VTable[slot] : System.IntPtr.Zero;

        private System.UInt32 OptionalFieldBaseOffset
        {
            get
            {
                System.UInt32 offset = (System.UInt32)(sizeof(MethodTable) + sizeof(System.IntPtr) * _usNumVtableSlots);
                offset += (System.UInt32)(sizeof(MethodTable*) * _usNumInterfaces);
                return offset;
            }
        }

        private System.UInt32 OptionalPointerSize
            => IsDynamicType || !SupportsRelativePointers ? (System.UInt32)sizeof(System.IntPtr) : 4U;

        private void* ReadOptionalPointer(System.UInt32 offset)
        {
            System.Byte* optionalLocation = (System.Byte*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref this) + offset;
            if (IsDynamicType || !SupportsRelativePointers) return *(void**)optionalLocation;
            System.Int32 relative = *(System.Int32*)optionalLocation;
            return relative == 0 ? null : optionalLocation + relative;
        }


        private System.UInt32 GetGenericDefinitionOffset()
        {
            System.UInt32 offset = OptionalFieldBaseOffset + OptionalPointerSize + OptionalPointerSize;
            if (HasDispatchMap) offset += OptionalPointerSize;
            if (IsFinalizable) offset += OptionalPointerSize;
            if (HasSealedVTableEntries) offset += OptionalPointerSize;
            return offset;
        }

        private System.UInt32 GetGenericCompositionOffset()
        {
            System.UInt32 offset = GetGenericDefinitionOffset();
            if (IsGeneric) offset += OptionalPointerSize;
            return offset;
        }

        internal MethodTable* GenericDefinition
        {
            get
            {
                if (!IsGeneric) return null;
                return (MethodTable*)ReadOptionalPointer(GetGenericDefinitionOffset());
            }
        }

        internal System.UInt32 GenericArity
        {
            get
            {
                MethodTable* definition = GenericDefinition;
                return definition == null ? 0U : definition->GenericParameterCount;
            }
        }

        private static MethodTable* FollowRelativeMethodTable(System.Byte* location)
        {
            System.Int32 relative = *(System.Int32*)location;
            return relative == 0 ? null : (MethodTable*)(location + relative);
        }

        private System.UInt32 GetFunctionPointerParametersOffset()
        {
            System.UInt32 offset = OptionalFieldBaseOffset + OptionalPointerSize + OptionalPointerSize;
            if (HasDispatchMap) offset += OptionalPointerSize;
            if (IsFinalizable) offset += OptionalPointerSize;
            if (HasSealedVTableEntries) offset += OptionalPointerSize;
            if (IsGeneric) offset += OptionalPointerSize + OptionalPointerSize;
            return offset;
        }

        internal MethodTable* GetFunctionPointerParameter(System.UInt32 index)
        {
            if (!IsFunctionPointer || index >= NumFunctionPointerParameters) return null;
            System.UInt32 offset = GetFunctionPointerParametersOffset() + index * OptionalPointerSize;
            return (MethodTable*)ReadOptionalPointer(offset);
        }

        internal MethodTable* GetGenericArgument(System.UInt32 index)
        {
            System.UInt32 arity = GenericArity;
            if (!IsGeneric || index >= arity || arity == 0U) return null;

            System.Byte* genericField = (System.Byte*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref this) + GetGenericCompositionOffset();
            if (IsDynamicType || !SupportsRelativePointers)
            {
                if (arity == 1U) return *(MethodTable**)genericField;
                MethodTable** list = *(MethodTable***)genericField;
                return list == null ? null : list[index];
            }

            if (arity == 1U) return FollowRelativeMethodTable(genericField);
            System.Int32 listRelative = *(System.Int32*)genericField;
            if (listRelative == 0) return null;
            System.Byte* listBase = genericField + listRelative;
            return FollowRelativeMethodTable(listBase + (index * 4U));
        }

        internal GenericVariance GetGenericVariance(System.UInt32 index)
        {
            if (!HasGenericVariance) return GenericVariance.NonVariant;

            MethodTable* definition = IsGeneric ? GenericDefinition : (MethodTable*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref this);
            if (definition == null || !definition->HasGenericVariance || index >= definition->GenericParameterCount)
                return GenericVariance.NonVariant;

            System.Byte* varianceField = (System.Byte*)definition + definition->GetGenericCompositionOffset();
            System.Byte* variance;
            if (definition->IsDynamicType || !SupportsRelativePointers)
                variance = *(System.Byte**)varianceField;
            else
            {
                System.Int32 relative = *(System.Int32*)varianceField;
                variance = relative == 0 ? null : varianceField + relative;
            }
            return variance == null ? GenericVariance.NonVariant : (GenericVariance)variance[index];
        }

        internal DispatchMap* DispatchMap
        {
            get
            {
                if (!HasDispatchMap) return null;
                System.UInt32 offset = OptionalFieldBaseOffset + OptionalPointerSize + OptionalPointerSize;
                return (DispatchMap*)ReadOptionalPointer(offset);
            }
        }

        private void* GetSealedVirtualTable()
        {
            if (!HasSealedVTableEntries) return null;
            System.UInt32 offset = OptionalFieldBaseOffset + OptionalPointerSize + OptionalPointerSize;
            if (HasDispatchMap) offset += OptionalPointerSize;
            if (IsFinalizable) offset += OptionalPointerSize;
            return ReadOptionalPointer(offset);
        }

        internal System.IntPtr GetSealedVirtualSlot(System.UInt16 slotNumber)
        {
            void* table = GetSealedVirtualTable();
            if (table == null) return System.IntPtr.Zero;
            if (!SupportsRelativePointers) return ((System.IntPtr*)table)[slotNumber];
            System.Int32* slot = &((System.Int32*)table)[slotNumber];
            return *slot == 0 ? System.IntPtr.Zero : (System.IntPtr)((System.Byte*)slot + *slot);
        }

        // CONTRACT with .NET 10 NativeAOT EETypeNode/MethodTable.cs. The finalizer is
        // an optional MethodTable field, not vtable slot zero in the physical EEType.
        // Static Windows/x64 EETypes use a 32-bit relative pointer for optional fields.
        internal System.IntPtr FinalizerMethod
        {
            get
            {
                if (!IsFinalizable) return System.IntPtr.Zero;

                System.UInt32 offset = (System.UInt32)(sizeof(MethodTable) + sizeof(System.IntPtr) * _usNumVtableSlots);
                offset += (System.UInt32)(sizeof(MethodTable*) * _usNumInterfaces);
                System.UInt32 optionalPointerSize = IsDynamicType || !SupportsRelativePointers ? (System.UInt32)sizeof(System.IntPtr) : 4U;

                // TypeManager indirection + writable data precede the optional dispatch map/finalizer.
                offset += optionalPointerSize;
                offset += optionalPointerSize;
                if (HasDispatchMap) offset += optionalPointerSize;

                System.Byte* optionalField = (System.Byte*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref this) + offset;
                if (IsDynamicType || !SupportsRelativePointers)
                    return *(System.IntPtr*)optionalField;

                System.Int32 relative = *(System.Int32*)optionalField;
                return relative == 0 ? System.IntPtr.Zero : (System.IntPtr)(optionalField + relative);
            }
        }

        internal TypeManagerHandle TypeManager
        {
            get
            {
                void* indirection = ReadOptionalPointer(OptionalFieldBaseOffset);
                return indirection == null ? default : *(TypeManagerHandle*)indirection;
            }
        }

        internal MethodTable* GetInterface(System.UInt16 index)
            => index < _usNumInterfaces ? InterfaceMap[index] : null;
    }

    // NativeAOT logical-module handle. Inu keeps the handle ABI pointer-sized
    // and lets the freestanding runtime own the backing TypeManager record.
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal readonly struct TypeManagerHandle
    {
        private readonly System.IntPtr _value;
        internal TypeManagerHandle(System.IntPtr value) { _value = value; }
        internal System.IntPtr Value => _value;
        internal System.Boolean IsNull => _value == System.IntPtr.Zero;
    }

    // IDs are ABI-stable NativeAOT ReadyToRun section identifiers.
    internal enum ReadyToRunSectionType
    {
        GCStaticRegion = 201,
        ThreadStaticRegion = 202,
        TypeManagerIndirection = 204,
        EagerCctor = 205,
        FrozenObjectRegion = 206,
        DehydratedData = 207,
        ThreadStaticOffsetRegion = 208,
        InterfaceDispatchCellInfoRegion = 209,
        InterfaceDispatchCellRegion = 210,
        ImportAddressTables = 212,
        ModuleInitializerList = 213,
        GvmDispatchCellInfoRegion = 214,
        GvmDispatchCellRegion = 215
    }
}

namespace Internal.Runtime.CompilerHelpers
{
    using System;
    using System.Runtime;

    // CONTRACT with .NET 10 NativeAOT Test.CoreLib. ILC resolves these exact
    // private helpers when lowering the IL ldtoken instruction used by typeof.
    internal static unsafe class LdTokenHelpers
    {
        private static RuntimeTypeHandle GetRuntimeTypeHandle(MethodTable* pEEType)
        {
            return new RuntimeTypeHandle(pEEType);
        }

        private static RuntimeMethodHandle GetRuntimeMethodHandle(IntPtr pMethod)
        {
            return RuntimeMethodHandle.FromIntPtr(pMethod);
        }

        private static RuntimeFieldHandle GetRuntimeFieldHandle(IntPtr pField)
        {
            return RuntimeFieldHandle.FromIntPtr(pField);
        }

        private static Type GetRuntimeType(MethodTable* pEEType)
        {
            return Type.GetTypeFromMethodTable(pEEType);
        }
    }

    // NativeAOT/RyuJIT resolves CORINFO_HELP_THROW_* through this exact class.
    // These are real managed throws now; RhpThrowEx owns the freestanding x64 EH path.
    internal static class ThrowHelpers
    {
        private static void ThrowNullReferenceException() { throw new NullReferenceException(); }
        private static void ThrowArgumentException() { throw new ArgumentException(); }
        private static void ThrowArgumentOutOfRangeException() { throw new ArgumentOutOfRangeException(); }
        private static void ThrowDivideByZeroException() { throw new DivideByZeroException(); }
        private static void ThrowIndexOutOfRangeException() { throw new IndexOutOfRangeException(); }
        private static void ThrowOverflowException() { throw new OverflowException(); }
        private static void ThrowPlatformNotSupportedException() { throw new PlatformNotSupportedException(); }
        private static void ThrowNotImplementedException() { throw new NotImplementedException(); }
        private static void ThrowArrayTypeMismatchException() { throw new ArrayTypeMismatchException(); }
        private static void ThrowNotSupportedException() { throw new NotSupportedException(); }
        private static void ThrowTypeNotSupportedException() { throw new PlatformNotSupportedException(); }
        private static void ThrowVerificationException(Int32 ilOffset) { throw new VerificationException(); }
        private static void ThrowInvalidProgramException(Internal.TypeSystem.ExceptionStringID id) { throw new InvalidProgramException(); }
        private static void ThrowInvalidProgramExceptionWithArgument(Internal.TypeSystem.ExceptionStringID id, String methodName) { throw new InvalidProgramException(methodName); }
        private static void ThrowFeatureBodyRemoved() { throw new NotSupportedException(); }
    }

    public static unsafe class StartupCodeHelpers
    {
        private const Int32 UninitializedStaticMask = 1;
        private const Int32 HasPreInitializedDataMask = 2;
        private const UInt32 PinnedObjectHeapAllocationFlag = 64U;
        private static Internal.Runtime.TypeManagerHandle[] s_modules;
        private static Int32 s_moduleCount;
        // Keep NativeAOT GC-static base objects reachable through managed references.
        // .NET 10 StartupCodeHelpers builds one spine per module; Inu also keeps
        // the image cells registered as precise roots, but the managed spine is required
        // so compiler-generated GC statics are materialized and retained as GC refs.
        private static Object[] s_moduleGcStaticSpines;

#pragma warning disable CS0626
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "RhpRegisterOsModule")]
        private static extern void RhpRegisterOsModule(IntPtr osModule);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "RhpCreateTypeManager")]
        private static extern IntPtr RhpCreateTypeManager(IntPtr osModule, IntPtr moduleHeader, IntPtr* classlibFunctions, Int32 classlibFunctionCount);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "RhGetModuleSection")]
        private static extern IntPtr RhGetModuleSection(IntPtr typeManager, Int32 sectionType, Int32* length);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "RhRegisterFrozenSegment")]
        private static extern IntPtr RhRegisterFrozenSegment(void* start, UIntPtr allocated, UIntPtr committed, UIntPtr reserved);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "RhRegisterStaticRoot")]
        private static extern Boolean RhRegisterStaticRoot(void** rootSlot);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "RhRegisterStaticBase")]
        private static extern Boolean RhRegisterStaticBase(void* objectAddress);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "RhRegisterStrongRoot")]
        private static extern Boolean RhRegisterStrongRoot(void* objectAddress);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "RhSealStaticRoots")]
        private static extern void RhSealStaticRoots();

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "RhAllocateNewObject")]
        private static extern void RhAllocateNewObject(IntPtr methodTable, UInt32 flags, void* result);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "InuEhTrace")]
        private static extern void TraceGcStaticStage(UInt64 code);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "InuEhTraceValue")]
        private static extern void TraceGcStaticValue(UInt64 tag, UInt64 value);
#pragma warning restore CS0626

        // 0.0.84: NativeAOT 10.0.10 interface dispatch cells are 16 bytes
        // (stub pointer + cache/interface pointer field) on x64.
        private const Int32 InterfaceDispatchCellSize = 16;

        private static unsafe IntPtr ResolveInterfaceTarget(Internal.Runtime.MethodTable* targetType, Internal.Runtime.MethodTable* interfaceType, UInt16 interfaceSlot)
        {
            Internal.Runtime.MethodTable* current = targetType;
            for (Int32 pass = 0; pass < 2; pass++)
            {
                current = targetType;
                while (current != null)
                {
                    Internal.Runtime.DispatchMap* map = current->DispatchMap;
                    if (map != null)
                    {
                        Int32 start = pass == 0 ? 0 : map->StandardEntryCount;
                        Int32 end = pass == 0 ? map->StandardEntryCount : map->StandardEntryCount + map->DefaultEntryCount;
                        for (Int32 i = start; i < end; i++)
                        {
                            Internal.Runtime.DispatchMap.DispatchMapEntry* entry = map->GetEntry(i);
                            if (entry->InterfaceMethodSlot != interfaceSlot) continue;
                            Internal.Runtime.MethodTable* candidateInterface = current->GetInterface(entry->InterfaceIndex);
                            if (!global::System.Runtime.TypeCast.InterfaceTypesCompatible(candidateInterface, interfaceType)) continue;
                            UInt16 impl = entry->ImplMethodSlot;
                            if (impl < current->NumVtableSlots) return targetType->GetVTableSlot(impl);
                            return current->GetSealedVirtualSlot((UInt16)(impl - current->NumVtableSlots));
                        }
                    }
                    current = current->NonArrayBaseType;
                }
            }
            return IntPtr.Zero;
        }

        // 0.0.84: .NET 10.0.10 ILC does not use the newer 209/210
        // InterfaceDispatchCellInfoRegion/InterfaceDispatchCellRegion metadata model.
        // Its InterfaceDispatchCellNode emits each 16-byte dispatch cell inline:
        //   +0  initial dispatch stub
        //   +8  interface pointer / relative interface pointer plus two flag bits
        // A run of cells ends with a 16-byte sentinel whose stub is zero; the
        // sentinel's low 16 bits contain the interface slot and bits 16+ identify
        // the cell kind. Decode that exact pinned-ILC ABI here.
        private const UInt64 InterfaceCachePointerMask = 0x3UL;
        private const UInt64 InterfacePointerAbsolute = 0x1UL;
        private const UInt64 InterfacePointerIndirectRelative = 0x2UL;
        private const UInt64 InterfacePointerRelative = 0x3UL;
        private const UInt64 InterfaceVTableOffsetLimit = 0x1000UL;
        private const Int32 InterfaceDispatchRunLimit = 4096;

        [RuntimeExport("InuResolveInterfaceDispatch")]
        public static unsafe IntPtr ResolveInterfaceDispatch(Object obj, IntPtr dispatchCell)
        {
            TraceGcStaticStage(0xA0UL);
            TraceGcStaticValue(0xA1UL, (UInt64)(nuint)(void*)dispatchCell);
            if (obj == null || dispatchCell == IntPtr.Zero) throw new NullReferenceException();

            Internal.Runtime.MethodTable* targetType = global::System.Runtime.CompilerServices.RuntimeHelpers.GetMethodTable(obj);
            Byte* cell = (Byte*)(void*)dispatchCell;
            UInt64 cacheValue = *(UInt64*)(cell + 8);
            TraceGcStaticValue(0xA2UL, cacheValue);

            // The full NativeAOT cache engine can rewrite this field to an aligned
            // cache pointer. Inu deliberately leaves initial cells immutable for now,
            // so an aligned non-vtable value is not expected on this path.
            if ((cacheValue & InterfaceCachePointerMask) == 0UL)
            {
                if (cacheValue < InterfaceVTableOffsetLimit)
                {
                    // NativeAOT vtable-offset cells encode a byte offset from the
                    // MethodTable. This path is included for ABI completeness even
                    // though the 0.0.84 conformance calls are interface-and-slot cells.
                    TraceGcStaticValue(0xA3UL, cacheValue);
                    return *(IntPtr*)((Byte*)targetType + (nuint)cacheValue);
                }
                TraceGcStaticStage(0xAFUL);
                return IntPtr.Zero;
            }

            // Slot and cell kind are emitted once at the terminating sentinel for
            // a contiguous run of interface dispatch cells. Bound the walk so a
            // malformed image cannot turn interface dispatch into an infinite scan.
            Byte* terminator = cell;
            Int32 scanned = 0;
            while (*(UInt64*)terminator != 0UL && scanned < InterfaceDispatchRunLimit)
            {
                terminator += InterfaceDispatchCellSize;
                scanned++;
            }
            if (scanned >= InterfaceDispatchRunLimit)
            {
                TraceGcStaticStage(0xAEUL);
                return IntPtr.Zero;
            }

            UInt64 slotAndKind = *(UInt64*)(terminator + 8);
            UInt32 cellKind = (UInt32)(slotAndKind >> 16);
            UInt16 interfaceSlot = (UInt16)slotAndKind;
            TraceGcStaticValue(0xA3UL, interfaceSlot);
            TraceGcStaticValue(0xA6UL, cellKind);

            // DispatchCellType.InterfaceAndSlot == 0 in the pinned NativeAOT ABI.
            // Metadata-token/GVM cells remain intentionally rejected here; 0.0.84 generic conformance
            // forces ILC to expose any GVM-specific ABI dependency explicitly rather than silently
            // treating ordinary interface-and-slot dispatch as generic-virtual support.
            if (cellKind != 0U)
            {
                TraceGcStaticStage(0xADUL);
                return IntPtr.Zero;
            }

            Internal.Runtime.MethodTable* interfaceType;
            UInt64 pointerKind = cacheValue & InterfaceCachePointerMask;
            if (pointerKind == InterfacePointerAbsolute)
            {
                interfaceType = (Internal.Runtime.MethodTable*)(nuint)(cacheValue & ~InterfaceCachePointerMask);
            }
            else if (pointerKind == InterfacePointerRelative || pointerKind == InterfacePointerIndirectRelative)
            {
                // ILC emits IMAGE_REL_BASED_RELPTR32 into the low four bytes of the
                // pointer-sized cache field. The relative base is the address of the
                // cache field itself and the displacement is signed 32-bit.
                Int32 displacement = *(Int32*)(cell + 8);
                Byte* resolved = (cell + 8) + displacement;
                resolved = (Byte*)((nuint)resolved & ~(nuint)InterfaceCachePointerMask);
                if (pointerKind == InterfacePointerRelative)
                    interfaceType = (Internal.Runtime.MethodTable*)resolved;
                else
                    interfaceType = *(Internal.Runtime.MethodTable**)resolved;
            }
            else
            {
                TraceGcStaticStage(0xACUL);
                return IntPtr.Zero;
            }

            TraceGcStaticValue(0xA4UL, (UInt64)(nuint)interfaceType);
            IntPtr target = ResolveInterfaceTarget(targetType, interfaceType, interfaceSlot);
            TraceGcStaticValue(0xA5UL, (UInt64)(nuint)(void*)target);
            return target;
        }
        // NativeAOT startup entry. This mirrors the Runtime.Base ordering: register
        // the OS module, create all TypeManagers, rehydrate module data before any
        // managed allocation, initialize global tables/frozen objects for every
        // module, then publish the module table and run eager class constructors.
        [RuntimeExport("InitializeModules")]
        public static void InitializeModules(IntPtr osModule, IntPtr* moduleHeaders, Int32 count, IntPtr* classlibFunctions, Int32 classlibFunctionCount)
        {
            if (count < 0 || (count != 0 && moduleHeaders == null))
                throw new ArgumentOutOfRangeException();

            RhpRegisterOsModule(osModule);

            Int32 moduleCount = 0;
            for (Int32 i = 0; i < count; i++)
                if (moduleHeaders[i] != IntPtr.Zero) moduleCount++;

            IntPtr* handles = stackalloc IntPtr[moduleCount == 0 ? 1 : moduleCount];
            Int32 moduleIndex = 0;
            for (Int32 i = 0; i < count; i++)
            {
                IntPtr header = moduleHeaders[i];
                if (header == IntPtr.Zero) continue;
                IntPtr handle = RhpCreateTypeManager(osModule, header, classlibFunctions, classlibFunctionCount);
                if (handle == IntPtr.Zero) FailFastStartup();

                Int32 dehydratedLength = 0;
                IntPtr dehydrated = RhGetModuleSection(handle, (Int32)Internal.Runtime.ReadyToRunSectionType.DehydratedData, &dehydratedLength);
                if (dehydrated != IntPtr.Zero && dehydratedLength > 0)
                    RehydrateData(dehydrated, dehydratedLength);

                handles[moduleIndex++] = handle;
            }

            // MethodTables are usable after dehydration has been expanded, so managed
            // arrays/objects are safe from this point onward.
            Internal.Runtime.TypeManagerHandle[] modules = new Internal.Runtime.TypeManagerHandle[moduleCount];
            for (Int32 i = 0; i < moduleCount; i++)
                modules[i] = new Internal.Runtime.TypeManagerHandle(handles[i]);

            Object[] gcStaticBaseSpines = new Object[count];
            for (Int32 i = 0; i < modules.Length; i++)
                InitializeGlobalTablesForModule(modules[i], i, gcStaticBaseSpines);

            // Publish the spine only after every module has initialized its global tables.
            // This mirrors NativeAOT Runtime.Base rooting semantics without requiring the
            // full CoreCLR GC-handle table in Inu's non-moving collector.
            // The official NativeAOT runtime roots this top-level GC-static spine through
            // a normal GC handle. Inu has no moving handle table yet, so register the exact
            // top-level spine object as a permanent strong root in the non-moving collector.
            // This breaks the circular dependency where the static bases were previously
            // expected to keep alive the very spine needed to keep those bases alive.
            TraceGcStaticStage(0xB1UL);
            IntPtr gcStaticSpineAddress = *(IntPtr*)global::System.Runtime.CompilerServices.Unsafe.AsPointer(ref gcStaticBaseSpines);
            if (gcStaticSpineAddress == IntPtr.Zero || !RhRegisterStrongRoot(gcStaticSpineAddress.ToPointer())) FailFastStartup();
            TraceGcStaticStage(0xB2UL);

            s_moduleGcStaticSpines = gcStaticBaseSpines;
            s_modules = modules;
            s_moduleCount = modules.Length;

            // No collection is allowed before all image static roots are known.
            RhSealStaticRoots();
            TraceGcStaticStage(0xB3UL);

            for (Int32 i = 0; i < modules.Length; i++)
                RunInitializers(modules[i], Internal.Runtime.ReadyToRunSectionType.EagerCctor);
            TraceGcStaticStage(0xB4UL);
        }

        internal static Int32 GetLoadedModules(Internal.Runtime.TypeManagerHandle[] outputModules)
        {
            if (outputModules != null)
            {
                Int32 count = s_moduleCount < outputModules.Length ? s_moduleCount : outputModules.Length;
                for (Int32 i = 0; i < count; i++) outputModules[i] = s_modules[i];
            }
            return s_moduleCount;
        }

        internal static void RunModuleInitializers()
        {
            for (Int32 i = 0; i < s_moduleCount; i++)
                RunInitializers(s_modules[i], Internal.Runtime.ReadyToRunSectionType.ModuleInitializerList);
        }

        private static void InitializeGlobalTablesForModule(Internal.Runtime.TypeManagerHandle typeManager, Int32 moduleIndex, Object[] gcStaticBaseSpines)
        {
            Int32 length = 0;
            IntPtr sectionAddress = RhGetModuleSection(typeManager.Value, (Int32)Internal.Runtime.ReadyToRunSectionType.TypeManagerIndirection, &length);
            if (sectionAddress != IntPtr.Zero)
            {
                // Match .NET 10 StartupCodeHelpers exactly: TypeManagerIndirection is a
                // pointer-only ModuleInfo row. RhGetModuleSection therefore reports one
                // pointer of length, but the section storage is a TypeManagerSlot
                // (TypeManagerHandle + module index). Do not reject it based on the
                // synthetic pointer-only length; publish both fields directly.
                Byte* section = (Byte*)(void*)sectionAddress;
                *(IntPtr*)section = typeManager.Value;
                *(Int32*)(section + sizeof(IntPtr)) = moduleIndex;
                TraceGcStaticStage(0x1990UL);
                TraceGcStaticValue(0x1991UL, (UInt64)(nuint)(void*)sectionAddress);
                TraceGcStaticValue(0x1992UL, (UInt64)(nuint)(void*)typeManager.Value);
                TraceGcStaticValue(0x1993UL, (UInt64)(UInt32)moduleIndex);
            }

            IntPtr statics = RhGetModuleSection(typeManager.Value, (Int32)Internal.Runtime.ReadyToRunSectionType.GCStaticRegion, &length);
            if (statics != IntPtr.Zero && length > 0)
            {
                Object[] spine = InitializeStatics(statics, length);
                if ((UInt32)moduleIndex >= (UInt32)gcStaticBaseSpines.Length) FailFastStartup();
                gcStaticBaseSpines[moduleIndex] = spine;
            }

            IntPtr frozen = RhGetModuleSection(typeManager.Value, (Int32)Internal.Runtime.ReadyToRunSectionType.FrozenObjectRegion, &length);
            if (frozen != IntPtr.Zero && length > 0)
            {
                if (RhRegisterFrozenSegment((void*)frozen, (UIntPtr)(void*)(nuint)(UInt32)length, (UIntPtr)(void*)(nuint)(UInt32)length, (UIntPtr)(void*)(nuint)(UInt32)length) == IntPtr.Zero)
                    FailFastStartup();
            }
        }

        // GCStaticRegion is an array of relative pointers to image-owned static-base
        // cells. Each cell initially contains a tagged relative MethodTable pointer.
        // Inu allocates the static base, copies optional preinitialized GC data,
        // replaces the cell with the object reference and registers the cell itself
        // as a precise root in the non-moving collector.
        private static Object[] InitializeStatics(IntPtr regionStart, Int32 length)
        {
            Byte* end = (Byte*)(void*)regionStart + length;
            Int32 entryCount = length / sizeof(Int32);
            Object[] spine = new Object[entryCount];
            Int32 currentBase = 0;
            for (Byte* entry = (Byte*)(void*)regionStart; entry + sizeof(Int32) <= end; entry += sizeof(Int32))
            {
                IntPtr* cell = (IntPtr*)ReadRelPtr32(entry);
                if (cell == null) continue;

                nint tagged = (nint)ReadRelPtr32(cell);
                if ((tagged & UninitializedStaticMask) == 0)
                {
                    if (!RhRegisterStaticRoot((void**)cell)) FailFastStartup();
                    UInt64 existingRaw = (UInt64)(nuint)(void*)(*cell);
                    if (existingRaw != 0UL && !RhRegisterStaticBase((void*)(nuint)existingRaw)) FailFastStartup();
                    spine[currentBase++] = existingRaw == 0UL ? null : global::System.Runtime.CompilerServices.Unsafe.As<UInt64, Object>(ref existingRaw);
                    continue;
                }

                Internal.Runtime.MethodTable* methodTable = (Internal.Runtime.MethodTable*)(tagged & ~(UninitializedStaticMask | HasPreInitializedDataMask));
                if (methodTable == null) FailFastStartup();

                // .NET 10 NativeAOT StartupCodeHelpers allocates GC-static bases through
                // RhAllocateNewObject rather than the ordinary RhpNewFast JIT helper.
                // Inu's heap is non-moving, so the pinned-GC allocation flag does not
                // change placement, but the compiler/runtime entry-point contract is exact.
                // Allocate directly into a managed Object local, matching .NET 10
                // StartupCodeHelpers.  The old Inu path allocated into void* and
                // reconstructed a managed reference through UInt64 afterwards.  Keeping
                // the allocation result as a GC reference from the instant it is published
                // removes an unnecessary raw-reference transition from GC-static startup.
                Object objRef = null;
                RhAllocateNewObject((IntPtr)methodTable, PinnedObjectHeapAllocationFlag,
                    global::System.Runtime.CompilerServices.Unsafe.AsPointer(ref objRef));
                if (objRef == null) FailFastStartup();

                UInt32 rawSize = objRef.GetRawDataSize();
                if ((tagged & HasPreInitializedDataMask) != 0)
                {
                    void* source = ReadRelPtr32((Int32*)cell + 1);
                    if (source != null && rawSize != 0U)
                    {
                        ref Byte destination = ref objRef.GetRawData();
                        ref Byte sourceData = ref *(Byte*)source;
                        Buffer.BulkMoveWithWriteBarrier(ref destination, ref sourceData, (UIntPtr)rawSize);
                    }
                }

                // Publish the same managed reference into both NativeAOT roots.
                spine[currentBase++] = objRef;
                IntPtr published = *(IntPtr*)global::System.Runtime.CompilerServices.Unsafe.AsPointer(ref objRef);
                *cell = published;
                if (!RhRegisterStaticBase(published.ToPointer())) FailFastStartup();
                if (!RhRegisterStaticRoot((void**)cell)) FailFastStartup();

                // 0.45.66: one-reference GC-static bases include Roslyn's compiler-generated
                // method-group cache classes.  Trace only these compact bases so the serial
                // log stays bounded.  EV:85 must be zero before the first method-group load;
                // a non-zero/all-ones value proves corruption during static-base creation.
                if (rawSize == (UInt32)sizeof(void*))
                {
                    TraceGcStaticStage(0x80UL);
                    TraceGcStaticValue(0x81UL, (UInt64)(nuint)cell);
                    TraceGcStaticValue(0x82UL, (UInt64)(nuint)methodTable);
                    TraceGcStaticValue(0x83UL, (UInt64)(nuint)(void*)published);
                    TraceGcStaticValue(0x84UL, methodTable->BaseSize);
                    ref Byte firstRawByte = ref objRef.GetRawData();
                    UInt64 firstSlot = *(UInt64*)global::System.Runtime.CompilerServices.Unsafe.AsPointer(ref firstRawByte);
                    TraceGcStaticValue(0x85UL, firstSlot);
                }
            }

            return spine;
        }

        private static void RunInitializers(Internal.Runtime.TypeManagerHandle typeManager, Internal.Runtime.ReadyToRunSectionType sectionType)
        {
            Int32 length = 0;
            Byte* begin = (Byte*)(void*)RhGetModuleSection(typeManager.Value, (Int32)sectionType, &length);
            if (begin == null || length <= 0) return;
            Byte* end = begin + length;
            for (Byte* current = begin; current + sizeof(Int32) <= end; current += sizeof(Int32))
            {
                delegate*<void> initializer = (delegate*<void>)ReadRelPtr32(current);
                if (initializer != null) initializer();
            }
        }

        // NativeAOT dehydration command stream. The low three bits carry the command;
        // payload is encoded in the upper five bits, with payload values 29..31
        // selecting one to three little-endian extension bytes. This matches Runtime.Base.
        private static void RehydrateData(IntPtr dehydratedData, Int32 sectionLength)
        {
            if (sectionLength < sizeof(Int32) * 2) FailFastStartup();
            Byte* baseAddress = (Byte*)(void*)dehydratedData;
            Byte* destination = (Byte*)ReadRelPtr32(baseAddress);
            Int32 commandBytes = *(Int32*)(baseAddress + sizeof(Int32));
            if (destination == null || commandBytes < sizeof(Int32) * 2 || commandBytes > sectionLength) FailFastStartup();

            Byte* current = baseAddress + sizeof(Int32) * 2;
            Byte* commandEnd = baseAddress + commandBytes;
            Int32* fixups = (Int32*)commandEnd;

            while (current < commandEnd)
            {
                Byte encoded = *current++;
                Int32 command = encoded & 0x07;
                Int32 payload = encoded >> 3;
                // Runtime.Base reserves payload values 29..31 to encode one to
                // three little-endian extension bytes.
                Int32 extraBytes = payload - 28;
                if (extraBytes > 0)
                {
                    if (extraBytes > 3 || current + extraBytes > commandEnd) FailFastStartup();
                    payload = *current++;
                    if (extraBytes > 1) payload += *current++ << 8;
                    if (extraBytes > 2) payload += *current++ << 16;
                    payload += 28;
                }

                switch (command)
                {
                    case 0: // Copy
                        if (payload == 0U || current + payload > commandEnd) FailFastStartup();
                        CopyBytes(destination, current, (UInt32)payload);
                        destination += payload;
                        current += payload;
                        break;
                    case 1: // ZeroFill - destination image is zero-initialized.
                        destination += payload;
                        break;
                    case 2: // RelPtr32Reloc
                        WriteRelPtr32(destination, ReadRelPtr32(fixups + payload));
                        destination += sizeof(Int32);
                        break;
                    case 3: // PtrReloc
                        *(void**)destination = ReadRelPtr32(fixups + payload);
                        destination += sizeof(void*);
                        break;
                    case 4: // InlineRelPtr32Reloc
                        for (Int32 i = 0; i < payload; i++)
                        {
                            if (current + sizeof(Int32) > commandEnd) FailFastStartup();
                            WriteRelPtr32(destination, ReadRelPtr32(current));
                            destination += sizeof(Int32);
                            current += sizeof(Int32);
                        }
                        break;
                    case 5: // InlinePtrReloc
                        for (Int32 i = 0; i < payload; i++)
                        {
                            if (current + sizeof(Int32) > commandEnd) FailFastStartup();
                            *(void**)destination = ReadRelPtr32(current);
                            destination += sizeof(void*);
                            current += sizeof(Int32);
                        }
                        break;
                    default:
                        FailFastStartup();
                        break;
                }
            }
        }

        private static void CopyBytes(Byte* destination, Byte* source, UInt32 count)
        {
            for (UInt32 i = 0; i < count; i++) destination[i] = source[i];
        }

        private static void* ReadRelPtr32(void* address) => (Byte*)address + *(Int32*)address;
        private static void WriteRelPtr32(void* destination, void* value) => *(Int32*)destination = (Int32)((Byte*)value - (Byte*)destination);

        private static void FailFastStartup()
        {
            while (true) { }
        }

        [RuntimeExport("RhpReversePInvoke")]
        private static void RhpReversePInvoke(IntPtr frame) { }

        [RuntimeExport("RhpReversePInvokeReturn")]
        private static void RhpReversePInvokeReturn(IntPtr frame) { }

        [RuntimeExport("RhpPInvoke")]
        private static void RhpPInvoke(IntPtr frame) { }

        [RuntimeExport("RhpPInvokeReturn")]
        private static void RhpPInvokeReturn(IntPtr frame) { }

        [RuntimeExport("RhpFallbackFailFast")]
        private static void RhpFallbackFailFast() => FailFastStartup();
    }
}

namespace System.Runtime
{
    // CONTRACT with .NET 10 NativeAOT Runtime.Base. The helper names/signatures and
    // fast-path ordering below mirror Runtime.Base TypeCast. Runtime.Base spells its object
    // MethodTable read as obj.GetMethodTable(); Inu intentionally invokes the equivalent
    // [Intrinsic] RuntimeHelpers.GetMethodTable(Object) entry point explicitly so the historical
    // Object.GetMethodTable() instance helper cannot shadow compiler-special lowering for delegates.
    // Inu still omits dynamic-type/cast-cache/variance slow-path infrastructure that
    // is not emitted by the current closed-world kernel build; canonical MethodTable, base
    // class, interface-map and array-base checks use the NativeAOT layout directly. Explicit static
    // calls are required here: System.Object also exposes a historical instance helper, and that
    // must not shadow ILC's compiler-special MethodTable lowering for delegate references.
    internal static unsafe class TypeCast
    {
#pragma warning disable CS0626
        // .NET 10 NativeAOT StelemRef ultimately publishes the reference through
        // the compiler/runtime write-barrier boundary. Inu implements that
        // boundary as the x64 RhpAssignRef leaf helper in native/x64/Runtime.asm.
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "RhpAssignRef")]
        private static extern void RhpAssignRef(ref Object destination, Object value);

        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "InuEhTrace")]
        private static extern void TraceTypeCastStage(UInt64 code);

        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "InuEhTraceValue")]
        private static extern void TraceTypeCastValue(UInt64 tag, UInt64 value);
#pragma warning restore CS0626

        private static Boolean GenericArgumentsCompatible(MethodTable* sourceType, MethodTable* targetType)
        {
            if (sourceType == null || targetType == null || !sourceType->IsGeneric || !targetType->IsGeneric) return false;
            MethodTable* sourceDefinition = sourceType->GenericDefinition;
            MethodTable* targetDefinition = targetType->GenericDefinition;
            if (sourceDefinition == null || sourceDefinition != targetDefinition) return false;

            UInt32 arity = targetType->GenericArity;
            if (arity == 0U || sourceType->GenericArity != arity) return false;
            for (UInt32 i = 0U; i < arity; i++)
            {
                MethodTable* sourceArgument = sourceType->GetGenericArgument(i);
                MethodTable* targetArgument = targetType->GetGenericArgument(i);
                if (sourceArgument == null || targetArgument == null) return false;
                if (sourceArgument == targetArgument) continue;

                GenericVariance variance = targetType->GetGenericVariance(i);
                if (sourceArgument->IsValueType || targetArgument->IsValueType) return false;
                if (variance == GenericVariance.Covariant || variance == GenericVariance.ArrayCovariant)
                {
                    if (!IsAssignableNoCache(sourceArgument, targetArgument)) return false;
                }
                else if (variance == GenericVariance.Contravariant)
                {
                    if (!IsAssignableNoCache(targetArgument, sourceArgument)) return false;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        internal static Boolean InterfaceTypesCompatible(MethodTable* sourceInterface, MethodTable* targetInterface)
        {
            if (sourceInterface == null || targetInterface == null) return false;
            if (sourceInterface == targetInterface) return true;
            if (!sourceInterface->IsInterface || !targetInterface->IsInterface || !targetInterface->HasGenericVariance) return false;
            return GenericArgumentsCompatible(sourceInterface, targetInterface);
        }

        private static Boolean ImplementsInterface(MethodTable* sourceType, MethodTable* targetType)
        {
            if (sourceType == null || targetType == null) return false;

            for (MethodTable* current = sourceType, previous = null; current != null; )
            {
                if (current == previous) return false;
                MethodTable** interfaceMap = current->InterfaceMap;
                UInt16 interfaceCount = current->NumInterfaces;
                for (UInt16 i = 0; i < interfaceCount; i++)
                    if (InterfaceTypesCompatible(interfaceMap[i], targetType)) return true;

                if (current->IsArray) break;
                MethodTable* next = current->NonArrayBaseType;
                if (next == current) return false;
                previous = current;
                current = next;
            }

            return false;
        }

        private static Boolean IsArrayBaseType(MethodTable* targetType)
            => targetType == global::Internal.Runtime.MethodTable.Of<Object>() ||
               targetType == global::Internal.Runtime.MethodTable.Of<Array>();

        private static Boolean IsAssignableNoCache(MethodTable* sourceType, MethodTable* targetType)
        {
            if (sourceType == null || targetType == null) return false;
            if (sourceType == targetType) return true;

            if (targetType->IsInterface)
            {
                if (sourceType->IsInterface && InterfaceTypesCompatible(sourceType, targetType)) return true;
                return ImplementsInterface(sourceType, targetType);
            }

            if (sourceType->IsArray)
            {
                if (IsArrayBaseType(targetType)) return true;
                if (!targetType->IsArray || sourceType->IsSzArray != targetType->IsSzArray) return false;

                MethodTable* sourceElement = sourceType->RelatedParameterType;
                MethodTable* targetElement = targetType->RelatedParameterType;
                if (sourceElement == null || targetElement == null) return false;
                if (sourceElement == targetElement) return true;
                if (sourceElement->IsValueType || targetElement->IsValueType) return false;
                return IsAssignableNoCache(sourceElement, targetElement);
            }

            MethodTable* current = sourceType->NonArrayBaseType;
            for (Int32 depth = 0; current != null && depth < 256; depth++)
            {
                if (current == targetType) return true;
                MethodTable* next = current->NonArrayBaseType;
                if (next == current) return false;
                current = next;
            }

            return false;
        }

        internal static Boolean IsAssignable(MethodTable* sourceType, MethodTable* targetType)
            => IsAssignableNoCache(sourceType, targetType);

        // Runtime.Base's unusual/general isinst helper: identity first, then the slow
        // assignment relation. The identity fast path is particularly important for
        // compiler-emitted delegate MethodTables and generic exact types.
        public static Object IsInstanceOfAny(MethodTable* pTargetType, Object obj)
        {
            // 0.45.64 diagnostic contract: this helper is the exact compiler-known path
            // reached by the delegate `isinst` that currently stalls after EH:19. Keep
            // the trace allocation-free so a known-good explicit delegate and the cached
            // delegate can be compared within the same boot.
            TraceTypeCastStage(0x70UL);
            TraceTypeCastValue(0x71UL, (UInt64)(nuint)pTargetType);
            if (obj == null)
            {
                TraceTypeCastStage(0x72UL);
                return null;
            }

            TraceTypeCastStage(0x73UL); // immediately before compiler-intrinsic MethodTable read
            MethodTable* sourceType = global::System.Runtime.CompilerServices.RuntimeHelpers.GetMethodTable(obj);
            TraceTypeCastStage(0x74UL); // proves MethodTable read returned
            TraceTypeCastValue(0x75UL, (UInt64)(nuint)sourceType);

            if (sourceType == pTargetType)
            {
                TraceTypeCastStage(0x76UL); // exact identity fast path
                return obj;
            }

            TraceTypeCastStage(0x77UL); // source and target differ; entering assignability fallback
            Boolean assignable = IsAssignableNoCache(sourceType, pTargetType);
            TraceTypeCastStage(0x78UL);
            TraceTypeCastValue(0x79UL, assignable ? 1UL : 0UL);
            return assignable ? obj : null;
        }

        public static Object IsInstanceOfInterface(MethodTable* pTargetType, Object obj)
        {
            TraceTypeCastStage(0x50UL);
            TraceTypeCastValue(0x51UL, (UInt64)(nuint)pTargetType);
            if (obj == null)
            {
                TraceTypeCastStage(0x52UL);
                return null;
            }
            TraceTypeCastStage(0x53UL);
            MethodTable* sourceType = global::System.Runtime.CompilerServices.RuntimeHelpers.GetMethodTable(obj);
            TraceTypeCastStage(0x54UL);
            TraceTypeCastValue(0x55UL, (UInt64)(nuint)sourceType);
            if (sourceType == pTargetType)
            {
                TraceTypeCastStage(0x56UL);
                return obj;
            }
            TraceTypeCastStage(0x57UL);
            Boolean implemented = ImplementsInterface(sourceType, pTargetType);
            TraceTypeCastStage(0x58UL);
            TraceTypeCastValue(0x59UL, implemented ? 1UL : 0UL);
            return implemented ? obj : null;
        }

        // Mirrors Runtime.Base IsInstanceOfClass: null/exact identity are handled before
        // walking the canonical base chain; non-canonical heap types are arrays and only
        // cast to the well-known Object/Array bases in Inu's current closed world.
        public static Object IsInstanceOfClass(MethodTable* pTargetType, Object obj)
        {
            TraceTypeCastStage(0x60UL);
            TraceTypeCastValue(0x61UL, (UInt64)(nuint)pTargetType);
            if (obj == null)
            {
                TraceTypeCastStage(0x62UL);
                return null;
            }

            TraceTypeCastStage(0x63UL);
            MethodTable* sourceType = global::System.Runtime.CompilerServices.RuntimeHelpers.GetMethodTable(obj);
            TraceTypeCastStage(0x64UL);
            TraceTypeCastValue(0x65UL, (UInt64)(nuint)sourceType);
            if (sourceType == pTargetType)
            {
                TraceTypeCastStage(0x66UL);
                return obj;
            }

            TraceTypeCastStage(0x67UL);
            Boolean canonical = sourceType->IsCanonical;
            TraceTypeCastStage(0x68UL);
            TraceTypeCastValue(0x69UL, canonical ? 1UL : 0UL);
            if (!canonical)
            {
                Boolean arrayBase = sourceType->IsArray && IsArrayBaseType(pTargetType);
                TraceTypeCastValue(0x6AUL, arrayBase ? 1UL : 0UL);
                return arrayBase ? obj : null;
            }

            TraceTypeCastStage(0x6BUL);
            MethodTable* current = sourceType->NonArrayBaseType;
            for (Int32 depth = 0; current != null && depth < 256; depth++)
            {
                TraceTypeCastValue(0x6CUL, (UInt64)(nuint)current);
                if (current == pTargetType)
                {
                    TraceTypeCastStage(0x6DUL);
                    return obj;
                }
                MethodTable* next = current->NonArrayBaseType;
                if (next == current)
                {
                    TraceTypeCastStage(0x6EUL);
                    return null;
                }
                current = next;
            }

            TraceTypeCastStage(0x6FUL);
            return null;
        }

        public static Boolean IsInstanceOfException(MethodTable* pTargetType, Object obj)
        {
            TraceTypeCastStage(0x40UL);
            TraceTypeCastValue(0x41UL, (UInt64)(nuint)pTargetType);
            if (obj == null)
            {
                TraceTypeCastStage(0x42UL);
                return false;
            }
            TraceTypeCastStage(0x43UL);
            MethodTable* sourceType = global::System.Runtime.CompilerServices.RuntimeHelpers.GetMethodTable(obj);
            TraceTypeCastStage(0x44UL);
            TraceTypeCastValue(0x45UL, (UInt64)(nuint)sourceType);
            if (sourceType == pTargetType)
            {
                TraceTypeCastStage(0x46UL);
                return true;
            }
            if (sourceType->IsArray)
            {
                Boolean arrayBase = IsArrayBaseType(pTargetType);
                TraceTypeCastValue(0x47UL, arrayBase ? 1UL : 0UL);
                return arrayBase;
            }
            TraceTypeCastStage(0x48UL);
            MethodTable* current = sourceType->NonArrayBaseType;
            for (Int32 depth = 0; current != null && depth < 256; depth++)
            {
                TraceTypeCastValue(0x49UL, (UInt64)(nuint)current);
                if (current == pTargetType)
                {
                    TraceTypeCastStage(0x4AUL);
                    return true;
                }
                MethodTable* next = current->NonArrayBaseType;
                if (next == current)
                {
                    TraceTypeCastStage(0x4BUL);
                    return false;
                }
                current = next;
            }
            TraceTypeCastStage(0x4CUL);
            return false;
        }

        // Compiler-known .NET 10 NativeAOT class-library helpers. RyuJIT/ILC
        // resolves ReadyToRunHelper.Stelem_Ref and Ldelema_Ref by these exact
        // System.Runtime.TypeCast method names. Keep their signatures stable.
        private static ref Object GetArrayElementReference(Object[] array, nint index)
        {
            ref Byte first = ref global::System.Runtime.CompilerServices.Unsafe.As<global::System.RawArrayData>(array).Data;
            Byte* address = (Byte*)global::System.Runtime.CompilerServices.Unsafe.AsPointer(ref first)
                + ((nuint)index * (nuint)sizeof(void*));
            ref Byte elementByte = ref *address;
            return ref global::System.Runtime.CompilerServices.Unsafe.As<Byte, Object>(ref elementByte);
        }

        public static void StelemRef(Object[] array, nint index, Object obj)
        {
            if (array == null) throw new NullReferenceException();
            if ((nuint)index >= (nuint)(UInt32)array.Length) throw new IndexOutOfRangeException();

            ref Object element = ref GetArrayElementReference(array, index);
            if (obj != null)
            {
                MethodTable* elementType = global::System.Runtime.CompilerServices.RuntimeHelpers.GetMethodTable(array)->RelatedParameterType;
                MethodTable* sourceType = global::System.Runtime.CompilerServices.RuntimeHelpers.GetMethodTable(obj);
                if (elementType == null || (sourceType != elementType && !IsAssignableNoCache(sourceType, elementType)))
                    throw new ArrayTypeMismatchException();
            }

            RhpAssignRef(ref element, obj);
        }

        public static ref Object LdelemaRef(Object[] array, nint index, MethodTable* elementType)
        {
            if (array == null) throw new NullReferenceException();
            if ((nuint)index >= (nuint)(UInt32)array.Length) throw new IndexOutOfRangeException();

            MethodTable* actualElementType = global::System.Runtime.CompilerServices.RuntimeHelpers.GetMethodTable(array)->RelatedParameterType;
            if (actualElementType != elementType) throw new ArrayTypeMismatchException();
            return ref GetArrayElementReference(array, index);
        }

        public static Object CheckCastAny(MethodTable* pTargetType, Object obj)
        {
            Object result = IsInstanceOfAny(pTargetType, obj);
            if (obj == null || result != null) return result;
            throw new InvalidCastException();
        }

        public static Object CheckCastInterface(MethodTable* pTargetType, Object obj)
        {
            Object result = IsInstanceOfInterface(pTargetType, obj);
            if (obj == null || result != null) return result;
            throw new InvalidCastException();
        }

        public static Object CheckCastClass(MethodTable* pTargetType, Object obj)
        {
            if (obj == null || global::System.Runtime.CompilerServices.RuntimeHelpers.GetMethodTable(obj) == pTargetType) return obj;
            return CheckCastClassSpecial(pTargetType, obj);
        }

        public static Object CheckCastClassSpecial(MethodTable* pTargetType, Object obj)
        {
            Object result = IsInstanceOfClass(pTargetType, obj);
            if (result != null) return result;
            throw new InvalidCastException();
        }
    }

    // CONTRACT with .NET 10 NativeAOT Runtime.Base. These are the class-library
    // entry points RyuJIT uses for boxing/unboxing. The freestanding runtime owns
    // allocation (RhpNewFast); CoreLib owns copying the value payload and validating
    // the MethodTable on unbox.
    internal static unsafe class RuntimeExports
    {
#pragma warning disable CS0626
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [RuntimeImport("*", "RhpNewFast")]
        private static extern void* RhpNewFast(MethodTable* methodTable);
#pragma warning restore CS0626

        private static Object ObjectFromAddress(void* address)
        {
            UInt64 raw = (UInt64)address;
            return System.Runtime.CompilerServices.Unsafe.As<UInt64, Object>(ref raw);
        }

        private static void CopyBytes(void* destination, void* source, UInt32 count)
        {
            Byte* dst = (Byte*)destination;
            Byte* src = (Byte*)source;
            for (UInt32 i = 0; i < count; i++) dst[i] = src[i];
        }

        public static Object RhBox(MethodTable* pEEType, ref Byte data)
        {
            if (pEEType == null || !pEEType->IsValueType || pEEType->IsByRefLike)
                throw new InvalidCastException();
            if (pEEType->IsNullable)
                throw new NotSupportedException();

            void* address = RhpNewFast(pEEType);
            if (address == null) throw new OutOfMemoryException();
            Object result = ObjectFromAddress(address);
            UInt32 size = pEEType->ValueTypeSize;
            CopyBytes((Byte*)address + sizeof(MethodTable*),
                      System.Runtime.CompilerServices.Unsafe.AsPointer(ref data), size);
            return result;
        }

        private static Boolean UnboxTypeMatches(MethodTable* actual, MethodTable* target)
        {
            if (actual == target) return true;
            if (actual == null || target == null || actual->ElementType != target->ElementType) return false;
            switch (target->ElementType)
            {
                case EETypeElementType.Byte:
                case EETypeElementType.SByte:
                case EETypeElementType.Int16:
                case EETypeElementType.UInt16:
                case EETypeElementType.Int32:
                case EETypeElementType.UInt32:
                case EETypeElementType.Int64:
                case EETypeElementType.UInt64:
                case EETypeElementType.IntPtr:
                case EETypeElementType.UIntPtr:
                    return true;
                default:
                    return false;
            }
        }

        public static ref Byte RhUnbox2(MethodTable* pUnboxToEEType, Object obj)
        {
            if (obj == null) throw new NullReferenceException();
            if (!UnboxTypeMatches(global::System.Runtime.CompilerServices.RuntimeHelpers.GetMethodTable(obj), pUnboxToEEType))
                throw new InvalidCastException();
            return ref obj.GetRawData();
        }

        public static void RhUnboxNullable(ref Byte data, MethodTable* pUnboxToEEType, Object obj)
        {
            // Nullable<T> has special boxing semantics. Keep the compiler-known
            // entry point present, but fail explicitly until the Nullable EEType
            // related-type/offset contract is completed.
            throw new NotSupportedException();
        }

        public static void RhUnboxTypeTest(MethodTable* pType, MethodTable* pBoxType)
        {
            if (!UnboxTypeMatches(pBoxType, pType))
                throw new InvalidCastException();
        }
    }
}

namespace System.Runtime
{
    // NativeAOT class-library/runtime exception identifiers used by the EH dispatcher contract.
    public enum ExceptionIDs
    {
        OutOfMemory = 1, Arithmetic = 2, ArrayTypeMismatch = 3, DivideByZero = 4,
        IndexOutOfRange = 5, InvalidCast = 6, Overflow = 7, NullReference = 8,
        AccessViolation = 9, DataMisaligned = 10, EntrypointNotFound = 11,
        AmbiguousImplementation = 12, IllegalInstruction = 13, PrivilegedInstruction = 14, InPageError = 15
    }

    public enum RhFailFastReason
    {
        InternalError = 1, UnhandledException = 2
    }
}

namespace System.Runtime.ExceptionServices
{
    // Minimal NativeAOT Test.CoreLib callback used by the EH runtime.
    public static class ExceptionHandling
    {
        internal static Boolean IsHandledByGlobalHandler(Exception ex) => false;
    }
}

namespace System
{
    // Classlib callbacks consumed by NativeAOT EH. They deliberately stay allocation-light
    // and expose only the exception kinds Inu can currently construct safely.
    internal static class RuntimeExceptionHelpers
    {
        [System.Runtime.RuntimeExport("GetRuntimeException")]
        public static Exception GetRuntimeException(System.Runtime.ExceptionIDs id)
        {
            switch (id)
            {
                case System.Runtime.ExceptionIDs.OutOfMemory: return new OutOfMemoryException();
                case System.Runtime.ExceptionIDs.Arithmetic: return new ArithmeticException();
                case System.Runtime.ExceptionIDs.ArrayTypeMismatch: return new ArrayTypeMismatchException();
                case System.Runtime.ExceptionIDs.DivideByZero: return new DivideByZeroException();
                case System.Runtime.ExceptionIDs.IndexOutOfRange: return new IndexOutOfRangeException();
                case System.Runtime.ExceptionIDs.InvalidCast: return new InvalidCastException();
                case System.Runtime.ExceptionIDs.Overflow: return new OverflowException();
                case System.Runtime.ExceptionIDs.NullReference: return new NullReferenceException();
                default: return new PlatformNotSupportedException();
            }
        }

        [System.Runtime.RuntimeExport("RuntimeFailFast")]
        private static void RuntimeFailFast(System.Runtime.RhFailFastReason reason, Exception exception, IntPtr address, IntPtr context)
        {
            while (true) { }
        }

        [System.Runtime.RuntimeExport("AppendExceptionStackFrame")]
        private static void AppendExceptionStackFrame(Object exceptionObj, IntPtr ip, Int32 flags) { }

        [System.Runtime.RuntimeExport("OnFirstChanceException")]
        private static void OnFirstChanceException(Object exceptionObj) { }

        [System.Runtime.RuntimeExport("OnUnhandledException")]
        private static void OnUnhandledException(Object exceptionObj) { }
    }
}

namespace Inu.Runtime
{
    /// <summary>Inu-only ABI diagnostic bridge. Keeps NativeAOT MethodTable internals inside CoreLib while exposing only the address value to conformance code.</summary>
    public static unsafe class RuntimeDiagnostics
    {
        public static global::System.UInt64 GetMethodTableAddress(global::System.Object obj)
        {
            if (obj == null) return 0UL;
            return (global::System.UInt64)(nuint)global::System.Runtime.CompilerServices.RuntimeHelpers.GetMethodTable(obj);
        }

        /// <summary>Returns the NativeAOT optional finalizer entrypoint encoded in the object's MethodTable.</summary>
        public static global::System.UInt64 GetFinalizerAddress(global::System.Object obj)
        {
            if (obj == null) return 0UL;
            global::Internal.Runtime.MethodTable* methodTable = global::System.Runtime.CompilerServices.RuntimeHelpers.GetMethodTable(obj);
            if (methodTable == null || !methodTable->IsFinalizable) return 0UL;
            return unchecked((global::System.UInt64)(global::System.Int64)methodTable->FinalizerMethod);
        }

        /// <summary>Uses CoreLib's canonical NativeAOT type relation for EH typed-catch matching.</summary>
        public static global::System.Boolean IsExceptionInstanceOf(global::System.Object obj, global::System.UInt64 targetMethodTable)
        {
            if (obj == null || targetMethodTable == 0UL) return false;
            return global::System.Runtime.TypeCast.IsInstanceOfException(
                (global::Internal.Runtime.MethodTable*)(nuint)targetMethodTable, obj);
        }
    }
}



// 0.0.91: non-generic collection throw helpers. Keeping the actual throw in a
// non-generic method avoids making collection exception semantics depend on the
// unfinished shared-generic value-type/unboxing ABI while preserving the public
// InvalidOperationException contract.
namespace System.Collections
{
    // 0.0.108: diagnostic-only breadcrumbs for NativeAOT generic collection
    // value-type enumerator return boundaries. Keep these in CoreLib so the
    // collection implementation can report progress without depending on
    // Inu.Runtime.NativeAot.
    internal static class CollectionRuntimeDiagnostics
    {
#pragma warning disable CS0626
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [global::System.Runtime.RuntimeImport("*", "InuEhTrace")]
        private static extern void TraceStageNative(global::System.UInt64 code);

        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [global::System.Runtime.RuntimeImport("*", "InuEhTraceValue")]
        private static extern void TraceValueNative(global::System.UInt64 tag, global::System.UInt64 value);
#pragma warning restore CS0626

        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static void TraceStage(global::System.UInt64 code) => TraceStageNative(code);

        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static void TraceValue(global::System.UInt64 tag, global::System.UInt64 value) => TraceValueNative(tag, value);
    }

    internal static class CollectionThrowHelper
    {
#pragma warning disable CS0626
        // CoreLib cannot reference Inu.Runtime.NativeAot because NativeAot itself depends on CoreLib.
        // Emit the collection throw breadcrumb through the same native trace import used by the
        // freestanding runtime instead of creating a managed assembly dependency cycle.
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [global::System.Runtime.RuntimeImport("*", "InuEhTrace")]
        private static extern void TraceCollectionStage(global::System.UInt64 code);
#pragma warning restore CS0626

        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static void ThrowModified()
        {
            TraceCollectionStage(0x18F0UL);
            throw new global::System.InvalidOperationException("Collection was modified during enumeration.");
        }

        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static void ThrowDuplicateKey()
        {
            TraceCollectionStage(0x18F1UL);
            throw new global::System.ArgumentException("An item with the same key has already been added.");
        }

        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static void ThrowKeyNotFound()
        {
            TraceCollectionStage(0x18F2UL);
            throw new global::System.KeyNotFoundException();
        }
    }
}

// 0.0.85: freestanding generic collection contracts. These are intentionally
// allocation-simple implementations for the Inu managed kernel: List<T> uses a
// contiguous managed array and Dictionary<TKey,TValue> uses separate chaining.
// They depend only on CoreLib/GC primitives already owned by Inu and do not pull
// in the desktop .NET collection implementation or Windows runtime libraries.
namespace System
{
    public interface IDisposable
    {
        void Dispose();
    }
}

namespace System.Collections
{
    /// <summary><inu.api/>Non-generic enumerator contract for freestanding C# collection iteration.</summary>
    public interface IEnumerator
    {
        Object Current { get; }
        Boolean MoveNext();
        void Reset();
    }

    /// <summary><inu.api/>Non-generic enumerable contract for freestanding C# collection iteration.</summary>
    public interface IEnumerable
    {
        IEnumerator GetEnumerator();
    }

    /// <summary><inu.api/>Non-generic collection contract exposing Count and enumeration.</summary>
    public interface ICollection : IEnumerable
    {
        Int32 Count { get; }
    }
}

namespace System.Collections.Generic
{
    /// <summary><inu.api/>Generic enumerator contract compatible with ordinary C# foreach.</summary>
    public interface IEnumerator<out T> : System.Collections.IEnumerator, System.IDisposable
    {
        new T Current { get; }
    }

    /// <summary><inu.api/>Generic enumerable contract compatible with ordinary C# foreach.</summary>
    public interface IEnumerable<out T> : System.Collections.IEnumerable
    {
        new IEnumerator<T> GetEnumerator();
    }

    /// <summary><inu.api/>Generic mutable collection contract supplied by the Inu freestanding CoreLib.</summary>
    public interface ICollection<T> : IEnumerable<T>
    {
        Int32 Count { get; }
        Boolean IsReadOnly { get; }
        void Add(T item);
        void Clear();
        Boolean Contains(T item);
        void CopyTo(T[] array, Int32 arrayIndex);
        Boolean Remove(T item);
    }

    /// <summary><inu.api/>Generic indexed list contract supplied by the Inu freestanding CoreLib.</summary>
    public interface IList<T> : ICollection<T>
    {
        T this[Int32 index] { get; set; }
        Int32 IndexOf(T item);
        void Insert(Int32 index, T item);
        void RemoveAt(Int32 index);
    }

    /// <summary><inu.api/>Generic read-only collection contract supplied by the Inu freestanding CoreLib.</summary>
    public interface IReadOnlyCollection<out T> : IEnumerable<T>
    {
        Int32 Count { get; }
    }

    /// <summary><inu.api/>Generic read-only indexed-list contract supplied by the Inu freestanding CoreLib.</summary>
    public interface IReadOnlyList<out T> : IReadOnlyCollection<T>
    {
        T this[Int32 index] { get; }
    }

    /// <summary><inu.api/>Stores one key/value pair for dictionary enumeration.</summary>
    public readonly struct KeyValuePair<TKey, TValue>
    {
        private readonly TKey _key;
        private readonly TValue _value;

        /// <summary><inu.api/></summary>
        public KeyValuePair(TKey key, TValue value)
        {
            _key = key;
            _value = value;
        }

        /// <summary><inu.api/></summary>
        public TKey Key => _key;
        /// <summary><inu.api/></summary>
        public TValue Value => _value;
    }

    /// <summary><inu.api/>Provides the default strongly typed ordering used by freestanding generic code.</summary>
    public abstract class Comparer<T>
    {
        private sealed class DefaultComparer : Comparer<T>
        {
            public override Int32 Compare(T x, T y)
            {
                Object left = x; Object right = y;
                if (Object.ReferenceEquals(left, right)) return 0;
                if (Object.ReferenceEquals(left, null)) return -1;
                if (Object.ReferenceEquals(right, null)) return 1;
                if (left is IComparable<T>) return ((IComparable<T>)left).CompareTo(y);
                if (left is IComparable) return ((IComparable)left).CompareTo(right);
                throw new ArgumentException();
            }
        }
        public static Comparer<T> Default => new DefaultComparer();
        public abstract Int32 Compare(T x, T y);
    }

    /// <summary><inu.api/>Provides equality and hashing used by freestanding generic collections.</summary>
    public abstract class EqualityComparer<T>
    {
        private sealed class DefaultComparer : EqualityComparer<T>
        {
            public override Boolean Equals(T x, T y)
            {
                // Primitive/string specialisations avoid relying on ValueType's much
                // larger reflection-backed Equals implementation, which Inu does not need.
                if (typeof(T) == typeof(Int32))
                    return (Int32)(Object)x == (Int32)(Object)y;
                if (typeof(T) == typeof(String))
                    return String.Equals((String)(Object)x, (String)(Object)y);

                Object left = x;
                Object right = y;
                if (Object.ReferenceEquals(left, right)) return true;
                if (Object.ReferenceEquals(left, null) || Object.ReferenceEquals(right, null)) return false;
                return left.Equals(right);
            }

            public override Int32 GetHashCode(T obj)
            {
                if (typeof(T) == typeof(Int32))
                    return ((Int32)(Object)obj).GetHashCode();
                if (typeof(T) == typeof(String))
                {
                    String value = (String)(Object)obj;
                    if (Object.ReferenceEquals(value, null)) return 0;
                    // Stable allocation-free ordinal hash; dictionary correctness does not
                    // depend on process-randomized desktop String hashing.
                    UInt32 hash = 2166136261U;
                    for (Int32 i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619U;
                    }
                    return unchecked((Int32)hash);
                }

                Object boxed = obj;
                return Object.ReferenceEquals(boxed, null) ? 0 : boxed.GetHashCode();
            }
        }

        // Keep the default comparer instance-owned by the collection that requests it.
        // A static field here creates one NativeAOT GC-static base for every closed T.
        // Core collection use can occur very early, so avoid manufacturing eager generic
        // GC-static comparer bases during module startup. Dictionary<TKey,TValue> caches
        // this result in its readonly _comparer field, so normal dictionary operations do
        // not allocate a comparer repeatedly.
        /// <summary><inu.api/></summary>
        public static EqualityComparer<T> Default => new DefaultComparer();
        /// <summary><inu.api/></summary>
        public abstract Boolean Equals(T x, T y);
        /// <summary><inu.api/></summary>
        public abstract Int32 GetHashCode(T obj);
    }

    /// <summary><inu.api/>Freestanding growable indexed collection compatible with System.Collections.Generic.List&lt;T&gt;.</summary>
    public class List<T> : IList<T>, IReadOnlyList<T>
    {
        private T[] _items;
        private Int32 _size;
        private Int32 _version;

        /// <summary><inu.api/></summary>
        public List() { _items = new T[0]; }
        /// <summary><inu.api/></summary>
        public List(Int32 capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException();
            _items = capacity == 0 ? new T[0] : new T[capacity];
        }

        /// <summary><inu.api/></summary>
        public Int32 Count => _size;
        /// <summary><inu.api/></summary>
        public Int32 Capacity
        {
            get => _items.Length;
            set
            {
                if (value < _size) throw new ArgumentOutOfRangeException();
                if (value == _items.Length) return;
                T[] replacement = value == 0 ? new T[0] : new T[value];
                for (Int32 i = 0; i < _size; i++) replacement[i] = _items[i];
                _items = replacement;
            }
        }
        /// <summary><inu.api/></summary>
        public Boolean IsReadOnly => false;

        /// <summary><inu.api/></summary>
        public T this[Int32 index]
        {
            get
            {
                if ((UInt32)index >= (UInt32)_size) throw new ArgumentOutOfRangeException();
                return _items[index];
            }
            set
            {
                if ((UInt32)index >= (UInt32)_size) throw new ArgumentOutOfRangeException();
                _items[index] = value;
                _version++;
            }
        }

        /// <summary><inu.api/></summary>
        public void Add(T item)
        {
            EnsureCapacity(_size + 1);
            _items[_size++] = item;
            _version++;
        }

        /// <summary><inu.api/></summary>
        public void AddRange(IEnumerable<T> collection)
        {
            if (collection == null) throw new ArgumentNullException();
            IEnumerator<T> iterator = collection.GetEnumerator();
            try
            {
                while (iterator.MoveNext()) Add(iterator.Current);
            }
            finally { iterator.Dispose(); }
        }

        /// <summary><inu.api/></summary>
        public void Clear()
        {
            for (Int32 i = 0; i < _size; i++) _items[i] = default(T);
            _size = 0;
            _version++;
        }

        /// <summary><inu.api/></summary>
        public Boolean Contains(T item) => IndexOf(item) >= 0;

        /// <summary><inu.api/></summary>
        public Int32 IndexOf(T item)
        {
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (Int32 i = 0; i < _size; i++)
                if (comparer.Equals(_items[i], item)) return i;
            return -1;
        }

        /// <summary><inu.api/></summary>
        public void Insert(Int32 index, T item)
        {
            if ((UInt32)index > (UInt32)_size) throw new ArgumentOutOfRangeException();
            EnsureCapacity(_size + 1);
            for (Int32 i = _size; i > index; i--) _items[i] = _items[i - 1];
            _items[index] = item;
            _size++;
            _version++;
        }

        /// <summary><inu.api/></summary>
        public Boolean Remove(T item)
        {
            Int32 index = IndexOf(item);
            if (index < 0) return false;
            RemoveAt(index);
            return true;
        }

        /// <summary><inu.api/></summary>
        public void RemoveAt(Int32 index)
        {
            if ((UInt32)index >= (UInt32)_size) throw new ArgumentOutOfRangeException();
            _size--;
            for (Int32 i = index; i < _size; i++) _items[i] = _items[i + 1];
            _items[_size] = default(T);
            _version++;
        }

        /// <summary><inu.api/></summary>
        public void CopyTo(T[] array, Int32 arrayIndex)
        {
            if (array == null) throw new ArgumentNullException();
            if (arrayIndex < 0 || arrayIndex > array.Length || array.Length - arrayIndex < _size)
                throw new ArgumentOutOfRangeException();
            for (Int32 i = 0; i < _size; i++) array[arrayIndex + i] = _items[i];
        }

        /// <summary><inu.api/></summary>
        public T[] ToArray()
        {
            T[] result = new T[_size];
            for (Int32 i = 0; i < _size; i++) result[i] = _items[i];
            return result;
        }

        private void EnsureCapacity(Int32 required)
        {
            if (_items.Length >= required) return;
            Int32 capacity = _items.Length == 0 ? 4 : _items.Length * 2;
            if (capacity < required) capacity = required;
            Capacity = capacity;
        }

        /// <summary><inu.api/></summary>
        public Enumerator GetEnumerator()
        {
            // 0.0.107: isolate the exact NativeAOT return boundary without changing
            // collection semantics. If 18D0 is absent, the fault is before the managed
            // getter body (for example while preparing its hidden return storage).
            // If 18D9 is present but the caller never reaches 18BF, construction finished
            // and the remaining fault is in propagating the returned value type.
            global::System.Collections.CollectionRuntimeDiagnostics.TraceStage(0x18D0UL);
            global::System.Collections.CollectionRuntimeDiagnostics.TraceValue(0x18D1UL, (global::System.UInt64)(global::System.UInt32)_size);
            global::System.Collections.CollectionRuntimeDiagnostics.TraceValue(0x18D2UL, (global::System.UInt64)(global::System.UInt32)_version);
            global::System.Collections.CollectionRuntimeDiagnostics.TraceValue(0x18D3UL, (global::System.UInt64)(global::System.UInt32)_items.Length);
            return new Enumerator(this);
        }
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new InterfaceEnumerator(this);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => new InterfaceEnumerator(this);

        // 0.0.88: interface enumeration deliberately uses a reference-type wrapper.
        // NativeAOT 10.0.10 otherwise boxes Enumerator and routes interface calls
        // through a shared-generic value-type unboxing thunk. That thunk belongs to
        // the remaining NativeAOT generic-unboxing ABI milestone, not collections.
        // The public pattern-based GetEnumerator still returns the normal struct.
        private sealed class InterfaceEnumerator : IEnumerator<T>
        {
            private readonly List<T> _list;
            private readonly Int32 _version;
            private Int32 _index;
            private T _current;

            internal InterfaceEnumerator(List<T> list)
            {
                _list = list;
                _version = list._version;
                _index = 0;
                _current = default(T);
            }

            public T Current => _current;
            Object System.Collections.IEnumerator.Current => _current;
            public Boolean MoveNext()
            {
                if (_version != _list._version) System.Collections.CollectionThrowHelper.ThrowModified();
                if (_index < _list._size)
                {
                    _current = _list._items[_index++];
                    return true;
                }
                _index = _list._size + 1;
                _current = default(T);
                return false;
            }
            public void Reset()
            {
                if (_version != _list._version) System.Collections.CollectionThrowHelper.ThrowModified();
                _index = 0;
                _current = default(T);
            }
            public void Dispose() { }
        }

        public struct Enumerator : IEnumerator<T>
        {
            private readonly List<T> _list;
            private readonly Int32 _version;
            private Int32 _index;
            private T _current;

            internal Enumerator(List<T> list)
            {
                // .NET 10 List<T>.Enumerator relies on the value-type return storage
                // already being zero-initialised.  Do not explicitly write _index or
                // default(T) here: in shared NativeAOT generic code that manufactures
                // an unnecessary generic value-type field initialisation on the
                // GetEnumerator return path.
                //
                // 0.0.107 diagnostics deliberately bracket each constructor write. They
                // do not add fields or alter the public Enumerator layout.
                global::System.Collections.CollectionRuntimeDiagnostics.TraceStage(0x18D4UL);
                _list = list;
                global::System.Collections.CollectionRuntimeDiagnostics.TraceStage(0x18D5UL);
                _version = list._version;
                global::System.Collections.CollectionRuntimeDiagnostics.TraceValue(0x18D6UL, (global::System.UInt64)(global::System.UInt32)_version);
                global::System.Collections.CollectionRuntimeDiagnostics.TraceValue(0x18D7UL, (global::System.UInt64)(global::System.UInt32)list._size);
                global::System.Collections.CollectionRuntimeDiagnostics.TraceValue(0x18D8UL, (global::System.UInt64)(global::System.UInt32)list._items.Length);
                global::System.Collections.CollectionRuntimeDiagnostics.TraceStage(0x18D9UL);
            }

            public T Current => _current;
            Object System.Collections.IEnumerator.Current => _current;

            public Boolean MoveNext()
            {
                if (_version != _list._version) System.Collections.CollectionThrowHelper.ThrowModified();
                if (_index < _list._size)
                {
                    _current = _list._items[_index++];
                    return true;
                }
                _index = _list._size + 1;
                _current = default(T);
                return false;
            }

            public void Reset()
            {
                if (_version != _list._version) System.Collections.CollectionThrowHelper.ThrowModified();
                _index = 0;
                _current = default(T);
            }
            public void Dispose() { }
        }
    }

    /// <summary><inu.api/>Freestanding key/value collection compatible with System.Collections.Generic.Dictionary&lt;TKey,TValue&gt;.</summary>
    public class Dictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, IReadOnlyCollection<KeyValuePair<TKey, TValue>>
    {
        private struct Entry
        {
            public Int32 HashCode;
            public Int32 Next;
            public TKey Key;
            public TValue Value;
        }

        private Int32[] _buckets;
        private Entry[] _entries;
        private Int32 _used;
        private Int32 _count;
        private Int32 _version;
        private readonly EqualityComparer<TKey> _comparer;

        /// <summary><inu.api/></summary>
        public Dictionary() : this(0, null) { }
        /// <summary><inu.api/></summary>
        public Dictionary(Int32 capacity) : this(capacity, null) { }
        /// <summary><inu.api/></summary>
        public Dictionary(EqualityComparer<TKey> comparer) : this(0, comparer) { }
        /// <summary><inu.api/></summary>
        public Dictionary(Int32 capacity, EqualityComparer<TKey> comparer)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException();
            _comparer = comparer ?? EqualityComparer<TKey>.Default;
            Int32 size = capacity <= 0 ? 4 : capacity;
            _buckets = new Int32[size];
            _entries = new Entry[size];
        }

        /// <summary><inu.api/></summary>
        public Int32 Count => _count;

        /// <summary><inu.api/></summary>
        public TValue this[TKey key]
        {
            get
            {
                TValue value;
                if (!TryGetValue(key, out value)) System.Collections.CollectionThrowHelper.ThrowKeyNotFound();
                return value;
            }
            set { Insert(key, value, false); }
        }

        /// <summary><inu.api/></summary>
        public void Add(TKey key, TValue value) => Insert(key, value, true);
        /// <summary><inu.api/></summary>
        public Boolean TryAdd(TKey key, TValue value)
        {
            if (FindEntry(key) >= 0) return false;
            Insert(key, value, true);
            return true;
        }
        /// <summary><inu.api/></summary>
        public Boolean ContainsKey(TKey key) => FindEntry(key) >= 0;

        /// <summary><inu.api/></summary>
        public Boolean TryGetValue(TKey key, out TValue value)
        {
            Int32 index = FindEntry(key);
            if (index >= 0)
            {
                value = _entries[index].Value;
                return true;
            }
            value = default(TValue);
            return false;
        }

        /// <summary><inu.api/></summary>
        public Boolean Remove(TKey key)
        {
            if (IsNullKey(key)) throw new ArgumentNullException();
            Int32 hash = GetHash(key);
            Int32 bucket = hash % _buckets.Length;
            Int32 previous = -1;
            for (Int32 i = _buckets[bucket] - 1; i >= 0; previous = i, i = _entries[i].Next)
            {
                if (_entries[i].HashCode == hash && _comparer.Equals(_entries[i].Key, key))
                {
                    if (previous < 0) _buckets[bucket] = _entries[i].Next + 1;
                    else _entries[previous].Next = _entries[i].Next;
                    _entries[i].HashCode = -1;
                    _entries[i].Next = -1;
                    _entries[i].Key = default(TKey);
                    _entries[i].Value = default(TValue);
                    _count--;
                    _version++;
                    return true;
                }
            }
            return false;
        }

        /// <summary><inu.api/></summary>
        public void Clear()
        {
            for (Int32 i = 0; i < _buckets.Length; i++) _buckets[i] = 0;
            for (Int32 i = 0; i < _used; i++)
            {
                _entries[i].HashCode = -1;
                _entries[i].Next = -1;
                _entries[i].Key = default(TKey);
                _entries[i].Value = default(TValue);
            }
            _used = 0;
            _count = 0;
            _version++;
        }

        private void Insert(TKey key, TValue value, Boolean throwOnExisting)
        {
            if (IsNullKey(key)) throw new ArgumentNullException();
            if (_used == _entries.Length) Resize();

            Int32 hash = GetHash(key);
            Int32 bucket = hash % _buckets.Length;
            for (Int32 i = _buckets[bucket] - 1; i >= 0; i = _entries[i].Next)
            {
                if (_entries[i].HashCode == hash && _comparer.Equals(_entries[i].Key, key))
                {
                    if (throwOnExisting)
                    {
                        System.Collections.CollectionThrowHelper.ThrowDuplicateKey();
                        return;
                    }
                    _entries[i].Value = value;
                    _version++;
                    return;
                }
            }

            Int32 index = _used++;
            _entries[index].HashCode = hash;
            _entries[index].Next = _buckets[bucket] - 1;
            _entries[index].Key = key;
            _entries[index].Value = value;
            _buckets[bucket] = index + 1;
            _count++;
            _version++;
        }

        private Int32 FindEntry(TKey key)
        {
            if (IsNullKey(key)) throw new ArgumentNullException();
            Int32 hash = GetHash(key);
            Int32 bucket = hash % _buckets.Length;
            for (Int32 i = _buckets[bucket] - 1; i >= 0; i = _entries[i].Next)
                if (_entries[i].HashCode == hash && _comparer.Equals(_entries[i].Key, key)) return i;
            return -1;
        }

        private Int32 GetHash(TKey key) => _comparer.GetHashCode(key) & 0x7FFFFFFF;
        private static Boolean IsNullKey(TKey key)
        {
            Object boxed = key;
            return Object.ReferenceEquals(boxed, null);
        }

        private void Resize()
        {
            Int32 newSize = _entries.Length < 4 ? 4 : _entries.Length * 2 + 1;
            Int32[] newBuckets = new Int32[newSize];
            Entry[] newEntries = new Entry[newSize];
            for (Int32 i = 0; i < _used; i++) newEntries[i] = _entries[i];
            for (Int32 i = 0; i < _used; i++)
            {
                if (newEntries[i].HashCode < 0) continue;
                Int32 bucket = newEntries[i].HashCode % newSize;
                newEntries[i].Next = newBuckets[bucket] - 1;
                newBuckets[bucket] = i + 1;
            }
            _buckets = newBuckets;
            _entries = newEntries;
        }

        /// <summary><inu.api/></summary>
        public Enumerator GetEnumerator()
        {
            // 0.0.108: mirror the List<T> return-boundary diagnostics for
            // Dictionary<TKey,TValue>. If 18E0 is absent after conformance emits
            // 184A, the fault is before the managed getter body. If 18EE is present
            // but 184B is absent, construction completed and the remaining fault is
            // propagating the returned value type to the caller.
            global::System.Collections.CollectionRuntimeDiagnostics.TraceStage(0x18E0UL);
            global::System.Collections.CollectionRuntimeDiagnostics.TraceValue(0x18E1UL, (global::System.UInt64)(global::System.UInt32)_count);
            global::System.Collections.CollectionRuntimeDiagnostics.TraceValue(0x18E2UL, (global::System.UInt64)(global::System.UInt32)_used);
            global::System.Collections.CollectionRuntimeDiagnostics.TraceValue(0x18E3UL, (global::System.UInt64)(global::System.UInt32)_version);
            global::System.Collections.CollectionRuntimeDiagnostics.TraceValue(0x18E4UL, (global::System.UInt64)(global::System.UInt32)_entries.Length);
            global::System.Collections.CollectionRuntimeDiagnostics.TraceValue(0x18E5UL, (global::System.UInt64)(global::System.UInt32)_buckets.Length);
            return new Enumerator(this);
        }
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => new InterfaceEnumerator(this);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => new InterfaceEnumerator(this);

        private sealed class InterfaceEnumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private readonly Dictionary<TKey, TValue> _dictionary;
            private readonly Int32 _version;
            private Int32 _index;
            private KeyValuePair<TKey, TValue> _current;

            internal InterfaceEnumerator(Dictionary<TKey, TValue> dictionary)
            {
                _dictionary = dictionary;
                _version = dictionary._version;
                _index = 0;
                _current = default(KeyValuePair<TKey, TValue>);
            }

            public KeyValuePair<TKey, TValue> Current => _current;
            Object System.Collections.IEnumerator.Current => _current;
            public Boolean MoveNext()
            {
                if (_version != _dictionary._version) System.Collections.CollectionThrowHelper.ThrowModified();
                while (_index < _dictionary._used)
                {
                    Int32 i = _index++;
                    if (_dictionary._entries[i].HashCode >= 0)
                    {
                        _current = new KeyValuePair<TKey, TValue>(_dictionary._entries[i].Key, _dictionary._entries[i].Value);
                        return true;
                    }
                }
                _current = default(KeyValuePair<TKey, TValue>);
                return false;
            }
            public void Reset()
            {
                if (_version != _dictionary._version) System.Collections.CollectionThrowHelper.ThrowModified();
                _index = 0;
                _current = default(KeyValuePair<TKey, TValue>);
            }
            public void Dispose() { }
        }

        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private readonly Dictionary<TKey, TValue> _dictionary;
            private readonly Int32 _version;
            private Int32 _index;
            private KeyValuePair<TKey, TValue> _current;

            internal Enumerator(Dictionary<TKey, TValue> dictionary)
            {
                // 0.0.108: bracket each write in the generic value-type
                // Dictionary<TKey,TValue>.Enumerator constructor. This does not
                // add fields or alter the public enumerator layout.
                global::System.Collections.CollectionRuntimeDiagnostics.TraceStage(0x18E6UL);
                _dictionary = dictionary;
                global::System.Collections.CollectionRuntimeDiagnostics.TraceStage(0x18E7UL);
                _version = dictionary._version;
                global::System.Collections.CollectionRuntimeDiagnostics.TraceValue(0x18E8UL, (global::System.UInt64)(global::System.UInt32)_version);
                _index = 0;
                global::System.Collections.CollectionRuntimeDiagnostics.TraceStage(0x18E9UL);
                global::System.Collections.CollectionRuntimeDiagnostics.TraceStage(0x18EAUL);
                _current = default(KeyValuePair<TKey, TValue>);
                global::System.Collections.CollectionRuntimeDiagnostics.TraceStage(0x18EBUL);
                global::System.Collections.CollectionRuntimeDiagnostics.TraceValue(0x18ECUL, (global::System.UInt64)(global::System.UInt32)dictionary._count);
                global::System.Collections.CollectionRuntimeDiagnostics.TraceValue(0x18EDUL, (global::System.UInt64)(global::System.UInt32)dictionary._used);
                global::System.Collections.CollectionRuntimeDiagnostics.TraceStage(0x18EEUL);
            }

            public KeyValuePair<TKey, TValue> Current => _current;
            Object System.Collections.IEnumerator.Current => _current;

            public Boolean MoveNext()
            {
                if (_version != _dictionary._version) System.Collections.CollectionThrowHelper.ThrowModified();
                while (_index < _dictionary._used)
                {
                    Int32 i = _index++;
                    if (_dictionary._entries[i].HashCode >= 0)
                    {
                        _current = new KeyValuePair<TKey, TValue>(_dictionary._entries[i].Key, _dictionary._entries[i].Value);
                        return true;
                    }
                }
                _current = default(KeyValuePair<TKey, TValue>);
                return false;
            }

            public void Reset()
            {
                if (_version != _dictionary._version) System.Collections.CollectionThrowHelper.ThrowModified();
                _index = 0;
                _current = default(KeyValuePair<TKey, TValue>);
            }
            public void Dispose() { }
        }
    }

    /// <summary><inu.api/>Freestanding first-in/first-out collection compatible with System.Collections.Generic.Queue&lt;T&gt;.</summary>
    public class Queue<T> : IEnumerable<T>, IReadOnlyCollection<T>
    {
        private T[] _array = new T[4];
        private Int32 _head;
        private Int32 _tail;
        private Int32 _size;
        /// <summary><inu.api/></summary>
        public Int32 Count => _size;

        /// <summary><inu.api/></summary>
        public void Enqueue(T item)
        {
            if (_size == _array.Length) Grow();
            _array[_tail] = item;
            _tail = (_tail + 1) % _array.Length;
            _size++;
        }
        /// <summary><inu.api/></summary>
        public T Dequeue()
        {
            if (_size == 0) throw new InvalidOperationException("Queue empty.");
            T item = _array[_head];
            _array[_head] = default(T);
            _head = (_head + 1) % _array.Length;
            _size--;
            return item;
        }
        /// <summary><inu.api/></summary>
        public T Peek()
        {
            if (_size == 0) throw new InvalidOperationException("Queue empty.");
            return _array[_head];
        }
        /// <summary><inu.api/></summary>
        public void Clear()
        {
            while (_size != 0) Dequeue();
        }
        private void Grow()
        {
            T[] replacement = new T[_array.Length * 2];
            for (Int32 i = 0; i < _size; i++) replacement[i] = _array[(_head + i) % _array.Length];
            _array = replacement;
            _head = 0;
            _tail = _size;
        }
        /// <summary><inu.api/></summary>
        public Enumerator GetEnumerator() => new Enumerator(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new InterfaceEnumerator(this);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => new InterfaceEnumerator(this);
        private sealed class InterfaceEnumerator : IEnumerator<T>
        {
            private readonly Queue<T> _queue;
            private Int32 _index;
            internal InterfaceEnumerator(Queue<T> queue) { _queue = queue; _index = -1; }
            public T Current => _queue._array[(_queue._head + _index) % _queue._array.Length];
            Object System.Collections.IEnumerator.Current => Current;
            public Boolean MoveNext() { if (_index + 1 >= _queue._size) return false; _index++; return true; }
            public void Reset() { _index = -1; }
            public void Dispose() { }
        }
        public struct Enumerator : IEnumerator<T>
        {
            private readonly Queue<T> _queue;
            private Int32 _index;
            internal Enumerator(Queue<T> queue) { _queue = queue; _index = -1; }
            public T Current => _queue._array[(_queue._head + _index) % _queue._array.Length];
            Object System.Collections.IEnumerator.Current => Current;
            public Boolean MoveNext() { if (_index + 1 >= _queue._size) return false; _index++; return true; }
            public void Reset() { _index = -1; }
            public void Dispose() { }
        }
    }

    /// <summary><inu.api/>Freestanding last-in/first-out collection compatible with System.Collections.Generic.Stack&lt;T&gt;.</summary>
    public class Stack<T> : IEnumerable<T>, IReadOnlyCollection<T>
    {
        private T[] _array = new T[4];
        private Int32 _size;
        /// <summary><inu.api/></summary>
        public Int32 Count => _size;
        /// <summary><inu.api/></summary>
        public void Push(T item)
        {
            if (_size == _array.Length)
            {
                T[] replacement = new T[_array.Length * 2];
                for (Int32 i = 0; i < _size; i++) replacement[i] = _array[i];
                _array = replacement;
            }
            _array[_size++] = item;
        }
        /// <summary><inu.api/></summary>
        public T Pop()
        {
            if (_size == 0) throw new InvalidOperationException("Stack empty.");
            T result = _array[--_size];
            _array[_size] = default(T);
            return result;
        }
        /// <summary><inu.api/></summary>
        public T Peek()
        {
            if (_size == 0) throw new InvalidOperationException("Stack empty.");
            return _array[_size - 1];
        }
        /// <summary><inu.api/></summary>
        public void Clear() { while (_size != 0) _array[--_size] = default(T); }
        /// <summary><inu.api/></summary>
        public Enumerator GetEnumerator() => new Enumerator(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new InterfaceEnumerator(this);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => new InterfaceEnumerator(this);
        private sealed class InterfaceEnumerator : IEnumerator<T>
        {
            private readonly Stack<T> _stack;
            private Int32 _index;
            internal InterfaceEnumerator(Stack<T> stack) { _stack = stack; _index = stack._size; }
            public T Current => _stack._array[_index];
            Object System.Collections.IEnumerator.Current => Current;
            public Boolean MoveNext() { if (_index <= 0) return false; _index--; return true; }
            public void Reset() { _index = _stack._size; }
            public void Dispose() { }
        }
        public struct Enumerator : IEnumerator<T>
        {
            private readonly Stack<T> _stack;
            private Int32 _index;
            internal Enumerator(Stack<T> stack) { _stack = stack; _index = stack._size; }
            public T Current => _stack._array[_index];
            Object System.Collections.IEnumerator.Current => Current;
            public Boolean MoveNext() { if (_index <= 0) return false; _index--; return true; }
            public void Reset() { _index = _stack._size; }
            public void Dispose() { }
        }
    }
}


namespace System.Text
{
    /// <summary><inu.api/>Freestanding growable UTF-16 text builder compatible with the ordinary System.Text.StringBuilder programming model.</summary>
    public sealed class StringBuilder
    {
        private Char[] _buffer;
        private Int32 _length;

        /// <summary><inu.api/>Creates an empty builder with the default initial capacity.</summary>
        public StringBuilder() : this(16) { }

        /// <summary><inu.api/>Creates an empty builder with at least the requested initial capacity.</summary>
        public StringBuilder(Int32 capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException();
            _buffer = new Char[capacity == 0 ? 16 : capacity];
        }

        /// <summary><inu.api/>Creates a builder initialized from the supplied string.</summary>
        public StringBuilder(String value)
        {
            Int32 length = value == null ? 0 : value.Length;
            _buffer = new Char[length < 16 ? 16 : length];
            if (value != null)
            {
                for (Int32 index = 0; index < length; index++) _buffer[index] = value[index];
                _length = length;
            }
        }

        /// <summary><inu.api/>Gets or sets the number of characters currently held by the builder.</summary>
        public Int32 Length
        {
            get => _length;
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException();
                EnsureCapacity(value);
                if (value > _length)
                    for (Int32 index = _length; index < value; index++) _buffer[index] = '\0';
                _length = value;
            }
        }

        /// <summary><inu.api/>Gets or sets the current character capacity.</summary>
        public Int32 Capacity
        {
            get => _buffer.Length;
            set
            {
                if (value < _length || value < 0) throw new ArgumentOutOfRangeException();
                if (value == _buffer.Length) return;
                Char[] replacement = new Char[value];
                for (Int32 index = 0; index < _length; index++) replacement[index] = _buffer[index];
                _buffer = replacement;
            }
        }

        /// <summary><inu.api/>Gets or replaces one character already present in the builder.</summary>
        public Char this[Int32 index]
        {
            get
            {
                if ((UInt32)index >= (UInt32)_length) throw new IndexOutOfRangeException();
                return _buffer[index];
            }
            set
            {
                if ((UInt32)index >= (UInt32)_length) throw new IndexOutOfRangeException();
                _buffer[index] = value;
            }
        }

        /// <summary><inu.api/>Removes all characters while retaining the allocated buffer.</summary>
        public StringBuilder Clear()
        {
            _length = 0;
            return this;
        }

        /// <summary><inu.api/>Ensures that at least the requested number of characters fit without another growth allocation.</summary>
        public Int32 EnsureCapacity(Int32 capacity)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException();
            if (capacity <= _buffer.Length) return _buffer.Length;
            Int32 next = _buffer.Length < 16 ? 16 : _buffer.Length;
            while (next < capacity)
            {
                Int32 grown = next <= (Int32.MaxValue / 2) ? next * 2 : Int32.MaxValue;
                if (grown <= next) { next = capacity; break; }
                next = grown;
            }
            if (next < capacity) next = capacity;
            Capacity = next;
            return _buffer.Length;
        }

        /// <summary><inu.api/>Appends a string value.</summary>
        public StringBuilder Append(String value)
        {
            if (value == null || value.Length == 0) return this;
            Int32 start = _length;
            EnsureCapacity(start + value.Length);
            for (Int32 index = 0; index < value.Length; index++) _buffer[start + index] = value[index];
            _length += value.Length;
            return this;
        }

        /// <summary><inu.api/>Appends one UTF-16 character.</summary>
        public StringBuilder Append(Char value)
        {
            EnsureCapacity(_length + 1);
            _buffer[_length++] = value;
            return this;
        }

        /// <summary><inu.api/>Appends a Boolean using normal .NET True/False spelling.</summary>
        public StringBuilder Append(Boolean value) => Append(value ? "True" : "False");

        /// <summary><inu.api/>Appends a signed 32-bit integer in decimal form.</summary>
        public StringBuilder Append(Int32 value) => AppendSigned(value);

        /// <summary><inu.api/>Appends an unsigned 32-bit integer in decimal form.</summary>
        public StringBuilder Append(UInt32 value) => AppendUnsigned(value);

        /// <summary><inu.api/>Appends a signed 64-bit integer in decimal form.</summary>
        public StringBuilder Append(Int64 value) => AppendSigned(value);

        /// <summary><inu.api/>Appends an unsigned 64-bit integer in decimal form.</summary>
        public StringBuilder Append(UInt64 value) => AppendUnsigned(value);

        /// <summary><inu.api/>Appends a CR/LF line terminator.</summary>
        public StringBuilder AppendLine()
        {
            Append('\r');
            Append('\n');
            return this;
        }

        /// <summary><inu.api/>Appends a string followed by a CR/LF line terminator.</summary>
        public StringBuilder AppendLine(String value)
        {
            Append(value);
            return AppendLine();
        }

        /// <summary><inu.api/>Materializes the current UTF-16 contents as a managed string.</summary>
        public override String ToString() => String.CreateFromChars(_buffer, _length);

        private StringBuilder AppendSigned(Int64 value)
        {
            if (value < 0)
            {
                Append('-');
                UInt64 magnitude = unchecked(0UL - (UInt64)value);
                return AppendUnsigned(magnitude);
            }
            return AppendUnsigned((UInt64)value);
        }

        private StringBuilder AppendUnsigned(UInt64 value)
        {
            Char[] digits = new Char[20];
            Int32 count = 0;
            do
            {
                digits[count++] = (Char)('0' + (Char)(value % 10UL));
                value /= 10UL;
            }
            while (value != 0UL);

            EnsureCapacity(_length + count);
            while (count != 0) _buffer[_length++] = digits[--count];
            return this;
        }
    }

    /// <summary><inu.api/>Freestanding text encoding base for the initial ASCII and UTF-8 SDK surface.</summary>
    public abstract class Encoding
    {
        /// <summary><inu.api/>Returns an Inu-owned ASCII encoding instance.</summary>
        public static Encoding ASCII => new ASCIIEncoding();

        /// <summary><inu.api/>Returns an Inu-owned UTF-8 encoding instance.</summary>
        public static Encoding UTF8 => new UTF8Encoding();

        /// <summary><inu.api/>Returns the number of bytes required to encode the supplied string.</summary>
        public abstract Int32 GetByteCount(String value);

        /// <summary><inu.api/>Encodes the supplied string into a newly allocated byte array.</summary>
        public abstract Byte[] GetBytes(String value);

        /// <summary><inu.api/>Decodes a complete byte array into a newly allocated managed string.</summary>
        public abstract String GetString(Byte[] bytes);
    }

    /// <summary><inu.api/>Freestanding 7-bit ASCII encoding with '?' replacement for characters outside ASCII.</summary>
    public sealed class ASCIIEncoding : Encoding
    {
        /// <summary><inu.api/>Creates an ASCII encoding instance.</summary>
        public ASCIIEncoding() { }

        /// <summary><inu.api/></summary>
        public override Int32 GetByteCount(String value)
        {
            if (value == null) throw new ArgumentNullException();
            return value.Length;
        }

        /// <summary><inu.api/></summary>
        public override Byte[] GetBytes(String value)
        {
            if (value == null) throw new ArgumentNullException();
            Byte[] bytes = new Byte[value.Length];
            for (Int32 index = 0; index < value.Length; index++)
            {
                Char character = value[index];
                bytes[index] = character <= 0x7F ? (Byte)character : (Byte)'?';
            }
            return bytes;
        }

        /// <summary><inu.api/></summary>
        public override String GetString(Byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException();
            Char[] characters = new Char[bytes.Length];
            for (Int32 index = 0; index < bytes.Length; index++)
                characters[index] = bytes[index] <= 0x7F ? (Char)bytes[index] : '?';
            return String.CreateFromChars(characters, characters.Length);
        }
    }

    /// <summary><inu.api/>Freestanding UTF-8 encoder/decoder for ordinary Unicode scalar values represented by UTF-16 strings.</summary>
    public sealed class UTF8Encoding : Encoding
    {
        /// <summary><inu.api/>Creates a UTF-8 encoding instance.</summary>
        public UTF8Encoding() { }

        /// <summary><inu.api/></summary>
        public override Int32 GetByteCount(String value)
        {
            if (value == null) throw new ArgumentNullException();
            Int32 count = 0;
            for (Int32 index = 0; index < value.Length; index++)
            {
                UInt32 scalar = value[index];
                if (scalar < 0x80U) count += 1;
                else if (scalar < 0x800U) count += 2;
                else if (scalar >= 0xD800U && scalar <= 0xDBFFU && index + 1 < value.Length)
                {
                    UInt32 low = value[index + 1];
                    if (low >= 0xDC00U && low <= 0xDFFFU) { count += 4; index++; }
                    else count += 3; // replacement character
                }
                else count += 3;
            }
            return count;
        }

        /// <summary><inu.api/></summary>
        public override Byte[] GetBytes(String value)
        {
            if (value == null) throw new ArgumentNullException();
            Byte[] bytes = new Byte[GetByteCount(value)];
            Int32 destination = 0;
            for (Int32 index = 0; index < value.Length; index++)
            {
                UInt32 scalar = value[index];
                if (scalar >= 0xD800U && scalar <= 0xDBFFU && index + 1 < value.Length)
                {
                    UInt32 low = value[index + 1];
                    if (low >= 0xDC00U && low <= 0xDFFFU)
                    {
                        scalar = 0x10000U + ((scalar - 0xD800U) << 10) + (low - 0xDC00U);
                        index++;
                    }
                    else scalar = 0xFFFDU;
                }
                else if (scalar >= 0xD800U && scalar <= 0xDFFFU) scalar = 0xFFFDU;

                if (scalar < 0x80U)
                    bytes[destination++] = (Byte)scalar;
                else if (scalar < 0x800U)
                {
                    bytes[destination++] = (Byte)(0xC0U | (scalar >> 6));
                    bytes[destination++] = (Byte)(0x80U | (scalar & 0x3FU));
                }
                else if (scalar < 0x10000U)
                {
                    bytes[destination++] = (Byte)(0xE0U | (scalar >> 12));
                    bytes[destination++] = (Byte)(0x80U | ((scalar >> 6) & 0x3FU));
                    bytes[destination++] = (Byte)(0x80U | (scalar & 0x3FU));
                }
                else
                {
                    bytes[destination++] = (Byte)(0xF0U | (scalar >> 18));
                    bytes[destination++] = (Byte)(0x80U | ((scalar >> 12) & 0x3FU));
                    bytes[destination++] = (Byte)(0x80U | ((scalar >> 6) & 0x3FU));
                    bytes[destination++] = (Byte)(0x80U | (scalar & 0x3FU));
                }
            }
            return bytes;
        }

        /// <summary><inu.api/></summary>
        public override String GetString(Byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException();
            Char[] characters = new Char[bytes.Length];
            Int32 output = 0;
            Int32 index = 0;
            while (index < bytes.Length)
            {
                UInt32 first = bytes[index++];
                UInt32 scalar;
                Int32 continuationCount;
                UInt32 minimum;

                if (first < 0x80U) { scalar = first; continuationCount = 0; minimum = 0U; }
                else if ((first & 0xE0U) == 0xC0U) { scalar = first & 0x1FU; continuationCount = 1; minimum = 0x80U; }
                else if ((first & 0xF0U) == 0xE0U) { scalar = first & 0x0FU; continuationCount = 2; minimum = 0x800U; }
                else if ((first & 0xF8U) == 0xF0U) { scalar = first & 0x07U; continuationCount = 3; minimum = 0x10000U; }
                else { characters[output++] = (Char)0xFFFD; continue; }

                Boolean valid = true;
                for (Int32 continuation = 0; continuation < continuationCount; continuation++)
                {
                    if (index >= bytes.Length || (bytes[index] & 0xC0U) != 0x80U) { valid = false; break; }
                    scalar = (scalar << 6) | (UInt32)(bytes[index++] & 0x3FU);
                }

                if (!valid || scalar < minimum || scalar > 0x10FFFFU || (scalar >= 0xD800U && scalar <= 0xDFFFU))
                {
                    characters[output++] = (Char)0xFFFD;
                    continue;
                }

                if (scalar < 0x10000U)
                {
                    characters[output++] = (Char)scalar;
                }
                else
                {
                    scalar -= 0x10000U;
                    characters[output++] = (Char)(0xD800U + (scalar >> 10));
                    characters[output++] = (Char)(0xDC00U + (scalar & 0x3FFU));
                }
            }
            return String.CreateFromChars(characters, output);
        }
    }
}
