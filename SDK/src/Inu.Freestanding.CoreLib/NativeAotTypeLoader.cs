// Portions of this file are derived from the .NET 10.0.10 NativeAOT runtime and
// NativeFormat sources from dotnet/runtime and are used under the MIT licence.
// Inu keeps the emitted NativeAOT ABI/data formats intact and adapts only the
// platform substrate (module enumeration and native allocation) to the kernel.

using System;
using System.Runtime.InteropServices;

namespace Internal.Metadata.NativeFormat
{
    // .NET 10 NativeFormat metadata handles are one encoded 32-bit value.
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct MethodHandle
    {
        internal readonly Int32 _value;
        internal MethodHandle(Int32 value) { _value = value; }
        internal Int32 Value => _value;
        public override Int32 GetHashCode() => _value;
        public override Boolean Equals(Object value)
            => value is MethodHandle && _value == ((MethodHandle)value)._value;
        public static Boolean operator ==(MethodHandle left, MethodHandle right) => left._value == right._value;
        public static Boolean operator !=(MethodHandle left, MethodHandle right) => left._value != right._value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct FieldHandle
    {
        internal readonly Int32 _value;
        internal FieldHandle(Int32 value) { _value = value; }
        internal Int32 Value => _value;
        public override Int32 GetHashCode() => _value;
        public override Boolean Equals(Object value)
            => value is FieldHandle && _value == ((FieldHandle)value)._value;
        public static Boolean operator ==(FieldHandle left, FieldHandle right) => left._value == right._value;
        public static Boolean operator !=(FieldHandle left, FieldHandle right) => left._value != right._value;
    }

    // .NET 10 ILC's usage-based metadata dependency graph resolves these
    // NativeFormat API marker types from System.Private.CoreLib even when the
    // application never exposes managed reflection.  Inu only consumes the
    // generated runtime mapping blobs (InvokeMap, FieldAccessMap, etc.), so the
    // reflection object model itself is intentionally not implemented.  These
    // tiny compatibility anchors give ILC the conditional dependency methods it
    // requires without importing the Microsoft reflection stack.  They must not
    // be called by the freestanding kernel.
    internal sealed class Method
    {
        internal Object get_CustomAttributes() => null;
    }

    internal sealed class Parameter
    {
        internal Object get_CustomAttributes() => null;
    }

    internal sealed class GenericParameter
    {
        internal Object get_CustomAttributes() => null;
    }

    internal sealed class Property
    {
        internal Object get_CustomAttributes() => null;
    }

    internal sealed class Event
    {
        internal Object get_CustomAttributes() => null;
    }

    internal sealed class TypeDefinition
    {
        internal Object get_CustomAttributes() => null;
    }

    internal sealed class Field
    {
        internal Object get_CustomAttributes() => null;
    }

    internal sealed class ScopeDefinition
    {
        internal Object get_CustomAttributes() => null;
        internal Object get_ModuleCustomAttributes() => null;
    }
}

namespace System
{
    // CONTRACT with .NET 10 NativeAOT System.RuntimeMethodHandle.cs. RuntimeMethodHandle
    // points at this variable-length record; FirstArgument is followed by any remaining
    // generic RuntimeTypeHandle arguments.
    [StructLayout(LayoutKind.Sequential)]
    public struct MethodHandleInfo
    {
        public RuntimeTypeHandle DeclaringType;
        public Internal.Metadata.NativeFormat.MethodHandle Handle;
        public Int32 NumGenericArgs;
        public RuntimeTypeHandle FirstArgument;
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct FieldHandleInfo
    {
        public RuntimeTypeHandle DeclaringType;
        public Internal.Metadata.NativeFormat.FieldHandle Handle;
    }
}

namespace Internal.Runtime
{
    // Exact .NET 10 metadata blob identifiers from Internal/Runtime/MetadataBlob.cs.
    internal enum ReflectionMapBlob
    {
        TypeMap = 1,
        ArrayMap = 2,
        PointerTypeMap = 3,
        FunctionPointerTypeMap = 4,
        InvokeMap = 6,
        VirtualInvokeMap = 7,
        CommonFixupsTable = 8,
        FieldAccessMap = 9,
        CCtorContextMap = 10,
        ByRefTypeMap = 11,
        EmbeddedMetadata = 13,
        UnboxingAndInstantiatingStubMap = 15,
        StructMarshallingStubMap = 16,
        DelegateMarshallingStubMap = 17,
        GenericVirtualMethodTable = 18,
        InterfaceGenericVirtualMethodTable = 19,
        TypeTemplateMap = 21,
        GenericMethodsTemplateMap = 22,
        NativeLayoutInfo = 30,
        NativeReferences = 31,
        GenericsHashtable = 32,
        NativeStatics = 33,
        StaticsInfoHashtable = 34,
        GenericMethodsHashtable = 35,
        ExactMethodInstantiationsHashtable = 36,
        ExternalTypeMap = 40,
        ProxyTypeMap = 41
    }

    [Flags]
    internal enum InvokeTableFlags : UInt32
    {
        IsDefaultConstructor = 0x00000008U
    }
}

namespace Internal.NativeFormat
{
    // Exact .NET 10 NativeFormat flag values.
    [Flags]
    internal enum MethodFlags : UInt32
    {
        HasInstantiation = 0x1U,
        IsUnboxingStub = 0x2U,
        HasFunctionPointer = 0x4U
    }

    internal enum TypeSignatureKind : UInt32
    {
        Null = 0x0U,
        Lookback = 0x1U,
        Modifier = 0x2U,
        Instantiation = 0x3U,
        Variable = 0x4U,
        BuiltIn = 0x5U,
        External = 0x6U,
        MultiDimArray = 0xAU,
        FunctionPointer = 0xBU
    }

    internal enum TypeModifierKind : UInt32
    {
        Array = 0x1U,
        ByRef = 0x2U,
        Pointer = 0x3U
    }

    // Exact .NET 10 NativeFormat bag/fixup identifiers used by TypeBuilder.
    internal enum BagElementKind : UInt32
    {
        End = 0x00U,
        DictionaryLayout = 0x40U,
        NonGcStaticData = 0x42U,
        GcStaticData = 0x43U,
        ThreadStaticIndex = 0x49U
    }

    internal enum StaticDataKind : UInt32
    {
        Gc = 0x01U,
        NonGc = 0x02U
    }

    internal enum FixupSignatureKind : UInt32
    {
        Null = 0x00U,
        TypeHandle = 0x01U,
        InterfaceCall = 0x02U,
        MethodDictionary = 0x04U,
        StaticData = 0x05U,
        UnwrapNullableType = 0x06U,
        FieldLdToken = 0x07U,
        MethodLdToken = 0x08U,
        AllocateObject = 0x09U,
        DefaultConstructor = 0x0AU,
        ThreadStaticIndex = 0x0BU,
        Method = 0x0DU,
        NonGenericInstanceConstrainedMethod = 0x20U,
        NonGenericStaticConstrainedMethod = 0x21U,
        GenericConstrainedMethod = 0x22U
    }

    // Port of NativeFormatReader.Primitives.cs / NativeFormatReader.cs from .NET 10.0.10.
    internal unsafe partial struct NativePrimitiveDecoder
    {
        public static Byte ReadUInt8(ref Byte* stream)
        {
            Byte result = *stream;
            stream++;
            return result;
        }

        public static UInt16 ReadUInt16(ref Byte* stream)
        {
            UInt16 result = *(UInt16*)stream;
            stream += 2;
            return result;
        }

        public static UInt32 ReadUInt32(ref Byte* stream)
        {
            UInt32 result = *(UInt32*)stream;
            stream += 4;
            return result;
        }

        public static UInt64 ReadUInt64(ref Byte* stream)
        {
            UInt64 result = *(UInt64*)stream;
            stream += 8;
            return result;
        }

        public static Single ReadFloat(ref Byte* stream)
        {
            UInt32 value = ReadUInt32(ref stream);
            return *(Single*)&value;
        }

        public static Double ReadDouble(ref Byte* stream)
        {
            UInt64 value = ReadUInt64(ref stream);
            return *(Double*)&value;
        }

        public static UInt32 GetUnsignedEncodingSize(UInt32 value)
        {
            if (value < 128U) return 1U;
            if (value < 128U * 128U) return 2U;
            if (value < 128U * 128U * 128U) return 3U;
            if (value < 128U * 128U * 128U * 128U) return 4U;
            return 5U;
        }

        public static UInt32 DecodeUnsigned(ref Byte* stream, Byte* streamEnd)
        {
            if (stream >= streamEnd) ThrowBadImageFormatException();

            UInt32 value;
            UInt32 val = *stream;
            if ((val & 1U) == 0U)
            {
                value = val >> 1;
                stream += 1;
            }
            else if ((val & 2U) == 0U)
            {
                if (stream + 1 >= streamEnd) ThrowBadImageFormatException();
                value = (val >> 2) | ((UInt32)(*(stream + 1)) << 6);
                stream += 2;
            }
            else if ((val & 4U) == 0U)
            {
                if (stream + 2 >= streamEnd) ThrowBadImageFormatException();
                value = (val >> 3) | ((UInt32)(*(stream + 1)) << 5) | ((UInt32)(*(stream + 2)) << 13);
                stream += 3;
            }
            else if ((val & 8U) == 0U)
            {
                if (stream + 3 >= streamEnd) ThrowBadImageFormatException();
                value = (val >> 4) | ((UInt32)(*(stream + 1)) << 4) | ((UInt32)(*(stream + 2)) << 12) | ((UInt32)(*(stream + 3)) << 20);
                stream += 4;
            }
            else if ((val & 16U) == 0U)
            {
                if (stream + 4 >= streamEnd) ThrowBadImageFormatException();
                stream += 1;
                value = ReadUInt32(ref stream);
            }
            else
            {
                ThrowBadImageFormatException();
                value = 0U;
            }
            return value;
        }

        public static Int32 DecodeSigned(ref Byte* stream, Byte* streamEnd)
        {
            if (stream >= streamEnd) ThrowBadImageFormatException();

            Int32 value;
            Int32 val = *stream;
            if ((val & 1) == 0)
            {
                value = ((SByte)val) >> 1;
                stream += 1;
            }
            else if ((val & 2) == 0)
            {
                if (stream + 1 >= streamEnd) ThrowBadImageFormatException();
                value = (val >> 2) | ((Int32)(*(SByte*)(stream + 1)) << 6);
                stream += 2;
            }
            else if ((val & 4) == 0)
            {
                if (stream + 2 >= streamEnd) ThrowBadImageFormatException();
                value = (val >> 3) | ((Int32)(*(stream + 1)) << 5) | ((Int32)(*(SByte*)(stream + 2)) << 13);
                stream += 3;
            }
            else if ((val & 8) == 0)
            {
                if (stream + 3 >= streamEnd) ThrowBadImageFormatException();
                value = (val >> 4) | ((Int32)(*(stream + 1)) << 4) | ((Int32)(*(stream + 2)) << 12) | ((Int32)(*(SByte*)(stream + 3)) << 20);
                stream += 4;
            }
            else if ((val & 16) == 0)
            {
                if (stream + 4 >= streamEnd) ThrowBadImageFormatException();
                stream += 1;
                value = (Int32)ReadUInt32(ref stream);
            }
            else
            {
                ThrowBadImageFormatException();
                value = 0;
            }
            return value;
        }

        public static void SkipInteger(ref Byte* stream)
        {
            Byte val = *stream;
            if ((val & 1) == 0) stream += 1;
            else if ((val & 2) == 0) stream += 2;
            else if ((val & 4) == 0) stream += 3;
            else if ((val & 8) == 0) stream += 4;
            else if ((val & 16) == 0) stream += 5;
            else if ((val & 32) == 0) stream += 9;
            else ThrowBadImageFormatException();
        }

        private static void ThrowBadImageFormatException() => throw new BadImageFormatException();
    }

    // Inu freestanding adaptation of the .NET 10 NativeFormat NativeReader.
    // The upstream reader is a sealed class, but allocating it from the GVM helper
    // crosses Inu's managed allocator at exactly the point runtime metadata lookup
    // must remain allocation-free. Keep the same fields, decoding rules and public
    // surface used by NativeParser/NativeHashtable, but store the reader inline.
    internal unsafe struct NativeReader
    {
        private readonly Byte* _base;
        private readonly UInt32 _size;

        public NativeReader(Byte* baseAddress, UInt32 size)
        {
            if (size >= UInt32.MaxValue / 4U) throw new BadImageFormatException();
            _base = baseAddress;
            _size = size;
        }

        public UInt32 Size => _size;
        public Boolean IsNull => _base == null;

        private void EnsureOffsetInRange(UInt32 offset, UInt32 lookAhead)
        {
            if (offset >= _size || lookAhead >= _size || offset > _size - 1U - lookAhead)
                throw new BadImageFormatException();
        }

        public Byte ReadUInt8(UInt32 offset)
        {
            EnsureOffsetInRange(offset, 0U);
            Byte* data = _base + offset;
            return NativePrimitiveDecoder.ReadUInt8(ref data);
        }

        public UInt16 ReadUInt16(UInt32 offset)
        {
            EnsureOffsetInRange(offset, 1U);
            Byte* data = _base + offset;
            return NativePrimitiveDecoder.ReadUInt16(ref data);
        }

        public UInt32 ReadUInt32(UInt32 offset)
        {
            EnsureOffsetInRange(offset, 3U);
            Byte* data = _base + offset;
            return NativePrimitiveDecoder.ReadUInt32(ref data);
        }

        public UInt32 DecodeUnsigned(UInt32 offset, out UInt32 value)
        {
            EnsureOffsetInRange(offset, 0U);
            Byte* data = _base + offset;
            value = NativePrimitiveDecoder.DecodeUnsigned(ref data, _base + _size);
            return (UInt32)(data - _base);
        }

        public UInt32 DecodeSigned(UInt32 offset, out Int32 value)
        {
            EnsureOffsetInRange(offset, 0U);
            Byte* data = _base + offset;
            value = NativePrimitiveDecoder.DecodeSigned(ref data, _base + _size);
            return (UInt32)(data - _base);
        }

        public UInt32 SkipInteger(UInt32 offset)
        {
            EnsureOffsetInRange(offset, 0U);
            Byte* data = _base + offset;
            NativePrimitiveDecoder.SkipInteger(ref data);
            if (data > _base + _size) throw new BadImageFormatException();
            return (UInt32)(data - _base);
        }
    }

    internal struct NativeParser
    {
        private readonly NativeReader _reader;
        private UInt32 _offset;

        public NativeParser(NativeReader reader, UInt32 offset)
        {
            _reader = reader;
            _offset = offset;
        }

        public Boolean IsNull => _reader.IsNull;
        public NativeReader Reader => _reader;
        public UInt32 Offset { get => _offset; set => _offset = value; }
        public Byte GetUInt8() { Byte value = _reader.ReadUInt8(_offset); _offset++; return value; }
        public UInt32 GetUnsigned() { UInt32 value; _offset = _reader.DecodeUnsigned(_offset, out value); return value; }
        public Int32 GetSigned() { Int32 value; _offset = _reader.DecodeSigned(_offset, out value); return value; }
        public UInt32 GetRelativeOffset() { UInt32 pos = _offset; Int32 delta; _offset = _reader.DecodeSigned(_offset, out delta); return pos + (UInt32)delta; }
        public void SkipInteger() { _offset = _reader.SkipInteger(_offset); }
        public NativeParser GetParserFromRelativeOffset() => new NativeParser(_reader, GetRelativeOffset());
        public UInt32 GetSequenceCount() => GetUnsigned();
        public BagElementKind GetBagElementKind() => (BagElementKind)GetUnsigned();
        public UInt32? GetUnsignedForBagElementKind(BagElementKind kindToFind)
        {
            NativeParser parser = this;
            BagElementKind kind;
            while ((kind = parser.GetBagElementKind()) != BagElementKind.End)
            {
                if (kind == kindToFind) return parser.GetUnsigned();
                parser.SkipInteger();
            }
            return null;
        }
        public FixupSignatureKind GetFixupSignatureKind() => (FixupSignatureKind)GetUnsigned();
        public TypeSignatureKind GetTypeSignatureKind(out UInt32 data)
        {
            UInt32 value = GetUnsigned();
            data = value >> 4;
            return (TypeSignatureKind)(value & 0xFU);
        }
        public NativeParser GetLookbackParser(UInt32 lookback)
        {
            UInt32 adjusted = lookback + NativePrimitiveDecoder.GetUnsignedEncodingSize(lookback << 4) + 2U;
            if (adjusted > _offset) throw new BadImageFormatException();
            return new NativeParser(_reader, _offset - adjusted);
        }
    }

    internal struct NativeHashtable
    {
        private NativeReader _reader;
        private UInt32 _baseOffset;
        private UInt32 _bucketMask;
        private Byte _entryIndexSize;

        public NativeHashtable(NativeParser parser)
        {
            UInt32 header = parser.GetUInt8();
            _reader = parser.Reader;
            _baseOffset = parser.Offset;
            Int32 numberOfBucketsShift = (Int32)(header >> 2);
            if (numberOfBucketsShift > 31) throw new BadImageFormatException();
            _bucketMask = (UInt32)((1U << numberOfBucketsShift) - 1U);
            _entryIndexSize = (Byte)(header & 3U);
            if (_entryIndexSize > 2) throw new BadImageFormatException();
        }

        public Boolean IsNull => _reader.IsNull;

        public struct Enumerator
        {
            private NativeParser _parser;
            private UInt32 _endOffset;
            private readonly Byte _lowHashcode;

            internal Enumerator(NativeParser parser, UInt32 endOffset, Byte lowHashcode)
            {
                _parser = parser;
                _endOffset = endOffset;
                _lowHashcode = lowHashcode;
            }

            public NativeParser GetNext()
            {
                while (_parser.Offset < _endOffset)
                {
                    Byte lowHashcode = _parser.GetUInt8();
                    if (lowHashcode == _lowHashcode) return _parser.GetParserFromRelativeOffset();
                    if (lowHashcode > _lowHashcode)
                    {
                        _endOffset = _parser.Offset;
                        break;
                    }
                    _parser.SkipInteger();
                }
                return default;
            }
        }

        public struct AllEntriesEnumerator
        {
            private NativeHashtable _table;
            private NativeParser _parser;
            private UInt32 _currentBucket;
            private UInt32 _endOffset;

            internal AllEntriesEnumerator(NativeHashtable table)
            {
                _table = table;
                _currentBucket = 0U;
                _parser = _table.GetParserForBucket(0U, out _endOffset);
            }

            public NativeParser GetNext()
            {
                for (;;)
                {
                    while (_parser.Offset < _endOffset)
                    {
                        _parser.GetUInt8();
                        return _parser.GetParserFromRelativeOffset();
                    }
                    if (_currentBucket >= _table._bucketMask) return default;
                    _currentBucket++;
                    _parser = _table.GetParserForBucket(_currentBucket, out _endOffset);
                }
            }
        }

        private void GetBucketBounds(UInt32 bucket, out UInt32 startOffset, out UInt32 endOffset)
        {
            UInt32 start;
            UInt32 end;
            if (_entryIndexSize == 0)
            {
                UInt32 bucketOffset = _baseOffset + bucket;
                start = _reader.ReadUInt8(bucketOffset);
                end = _reader.ReadUInt8(bucketOffset + 1U);
            }
            else if (_entryIndexSize == 1)
            {
                UInt32 bucketOffset = _baseOffset + 2U * bucket;
                start = _reader.ReadUInt16(bucketOffset);
                end = _reader.ReadUInt16(bucketOffset + 2U);
            }
            else
            {
                UInt32 bucketOffset = _baseOffset + 4U * bucket;
                start = _reader.ReadUInt32(bucketOffset);
                end = _reader.ReadUInt32(bucketOffset + 4U);
            }

            startOffset = _baseOffset + start;
            endOffset = _baseOffset + end;
            if (startOffset > endOffset || endOffset > _reader.Size)
                throw new BadImageFormatException();
        }

        private NativeParser GetParserForBucket(UInt32 bucket, out UInt32 endOffset)
        {
            UInt32 startOffset;
            GetBucketBounds(bucket, out startOffset, out endOffset);
            return new NativeParser(_reader, startOffset);
        }

        public void GetLookupBounds(Int32 hashcode, out UInt32 bucket, out UInt32 startOffset, out UInt32 endOffset)
        {
            bucket = ((UInt32)hashcode >> 8) & _bucketMask;
            GetBucketBounds(bucket, out startOffset, out endOffset);
        }

        public Enumerator Lookup(Int32 hashcode)
        {
            UInt32 endOffset;
            UInt32 bucket = ((UInt32)hashcode >> 8) & _bucketMask;
            NativeParser parser = GetParserForBucket(bucket, out endOffset);
            return new Enumerator(parser, endOffset, (Byte)hashcode);
        }

        public AllEntriesEnumerator EnumerateAllEntries() => new AllEntriesEnumerator(this);
    }
}

namespace Internal.Runtime.CompilerServices
{
    // Exact two-pointer .NET 10 NativeAOT generic function descriptor ABI.
    [StructLayout(LayoutKind.Sequential)]
    public struct GenericMethodDescriptor
    {
        public readonly IntPtr MethodFunctionPointer;
        public readonly IntPtr InstantiationArgument;

        public GenericMethodDescriptor(IntPtr methodFunctionPointer, IntPtr instantiationArgument)
        {
            MethodFunctionPointer = methodFunctionPointer;
            InstantiationArgument = instantiationArgument;
        }
    }

    public static unsafe class FunctionPointerOps
    {
        // Exact NativeAOT x64 tag. Bit 1 identifies a pointer to GenericMethodDescriptor + 2.
        private const Int32 FatFunctionPointerOffset = 2;

#pragma warning disable CS0626
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [System.Runtime.RuntimeImport("*", "InuNativeAlloc")]
        private static extern void* NativeAlloc(UInt64 byteCount, UInt64 alignment);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [System.Runtime.RuntimeImport("*", "InuGetInitialInterfaceDispatch")]
        private static extern IntPtr GetInitialInterfaceDispatchStub();

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [System.Runtime.RuntimeImport("*", "InuGetRhpNewFast")]
        private static extern IntPtr GetRhpNewFast();

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [System.Runtime.RuntimeImport("*", "InuGetRhpNewFinalizable")]
        private static extern IntPtr GetRhpNewFinalizable();

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [System.Runtime.RuntimeImport("*", "InuGetMissingDefaultConstructor")]
        private static extern IntPtr GetMissingDefaultConstructorMarker();
#pragma warning restore CS0626

        public static IntPtr GetGenericMethodFunctionPointer(IntPtr canonFunctionPointer, IntPtr instantiationArgument)
        {
            if (canonFunctionPointer == IntPtr.Zero) throw new BadImageFormatException();
            if (instantiationArgument == IntPtr.Zero) return canonFunctionPointer;

            GenericMethodDescriptor* descriptor = (GenericMethodDescriptor*)NativeAlloc((UInt64)sizeof(GenericMethodDescriptor), 8UL);
            if (descriptor == null) throw new OutOfMemoryException();
            *descriptor = new GenericMethodDescriptor(canonFunctionPointer, instantiationArgument);
            return (IntPtr)((Byte*)descriptor + FatFunctionPointerOffset);
        }

        // .NET 10 System.Private.TypeLoader GenericMethodDictionary.Allocate contract.
        // Method dictionaries always reserve one pointer-sized header before the first
        // dictionary cell. A zero-cell dictionary therefore still has a stable non-zero
        // instantiation argument, which is required by shared generic method entry points.
        internal static IntPtr AllocateGenericMethodDictionary(Int32 cellCount)
        {
            if (cellCount < 0) throw new BadImageFormatException();
            UInt64 bytes = checked(((UInt64)(UInt32)cellCount + 1UL) * (UInt64)IntPtr.Size);
            Byte* dictionaryWithHeader = (Byte*)NativeAlloc(bytes, (UInt64)IntPtr.Size);
            if (dictionaryWithHeader == null) throw new OutOfMemoryException();

            // Same dynamic-method-dictionary marker used by .NET 10 TypeLoader.
            *(Int32*)dictionaryWithHeader = 0x0D1CC0DE;
            return (IntPtr)(dictionaryWithHeader + IntPtr.Size);
        }

        // Stable freestanding equivalent of .NET 10 TypeLoader's
        // GetRuntimeMethodHandleForComponents. RuntimeMethodHandle is one pointer to a
        // MethodHandleInfo record, so dynamic ldtoken cells can materialise the exact
        // declaring type + metadata handle + generic argument tuple without reflection.
        internal static RuntimeMethodHandle AllocateRuntimeMethodHandle(RuntimeTypeHandle declaringType, UInt32 methodToken, RuntimeTypeHandle[] methodArguments)
        {
            if (declaringType.IsNull) throw new BadImageFormatException();
            Int32 argumentCount = methodArguments == null ? 0 : methodArguments.Length;
            UInt64 bytes = (UInt64)sizeof(MethodHandleInfo);
            if (argumentCount > 1)
                bytes = checked(bytes + (UInt64)(argumentCount - 1) * (UInt64)sizeof(RuntimeTypeHandle));

            MethodHandleInfo* info = (MethodHandleInfo*)NativeAlloc(bytes, (UInt64)IntPtr.Size);
            if (info == null) throw new OutOfMemoryException();
            info->DeclaringType = declaringType;
            info->Handle = new global::Internal.Metadata.NativeFormat.MethodHandle((Int32)methodToken);
            info->NumGenericArgs = argumentCount;
            info->FirstArgument = default;
            if (argumentCount > 0)
            {
                RuntimeTypeHandle* target = &info->FirstArgument;
                for (Int32 i = 0; i < argumentCount; i++) target[i] = methodArguments[i];
            }
            return RuntimeMethodHandle.FromIntPtr((IntPtr)info);
        }


        internal static RuntimeFieldHandle AllocateRuntimeFieldHandle(RuntimeTypeHandle declaringType, UInt32 fieldToken)
        {
            if (declaringType.IsNull) throw new BadImageFormatException();
            FieldHandleInfo* info = (FieldHandleInfo*)NativeAlloc((UInt64)sizeof(FieldHandleInfo), (UInt64)IntPtr.Size);
            if (info == null) throw new OutOfMemoryException();
            info->DeclaringType = declaringType;
            info->Handle = new global::Internal.Metadata.NativeFormat.FieldHandle((Int32)fieldToken);
            return RuntimeFieldHandle.FromIntPtr((IntPtr)info);
        }

        internal static IntPtr MissingDefaultConstructorMarker() => GetMissingDefaultConstructorMarker();

        // Dynamic equivalent of RuntimeAugments.NewInterfaceDispatchCell for Inu's
        // pinned x64 NativeAOT dispatch-cell ABI. One real cell is followed by the
        // sentinel that carries InterfaceAndSlot metadata.
        internal static IntPtr CreateInterfaceDispatchCell(RuntimeTypeHandle interfaceType, Int32 slot)
        {
            global::Internal.Runtime.MethodTable* interfaceTable = interfaceType.ToMethodTable();
            if (interfaceTable == null || !interfaceTable->IsInterface || (UInt32)slot > UInt16.MaxValue)
                throw new BadImageFormatException();

            IntPtr* cells = (IntPtr*)NativeAlloc(4UL * (UInt64)IntPtr.Size, (UInt64)IntPtr.Size);
            if (cells == null) throw new OutOfMemoryException();
            IntPtr stub = GetInitialInterfaceDispatchStub();
            if (stub == IntPtr.Zero) throw new BadImageFormatException();

            cells[0] = stub;
            cells[1] = (IntPtr)(nint)(((nuint)interfaceTable) | (nuint)1); // absolute interface pointer
            cells[2] = IntPtr.Zero;                                   // terminating sentinel
            cells[3] = new IntPtr((Int32)(UInt16)slot);                    // InterfaceAndSlot == 0
            return (IntPtr)cells;
        }

        internal static IntPtr GetAllocateObjectHelper(RuntimeTypeHandle type)
        {
            global::Internal.Runtime.MethodTable* table = type.ToMethodTable();
            if (table == null || table->IsInterface || table->IsArray || table->IsPointer || table->IsByRef || table->IsFunctionPointer)
                return IntPtr.Zero;
            return table->IsFinalizable ? GetRhpNewFinalizable() : GetRhpNewFast();
        }

        public static Boolean IsGenericMethodPointer(IntPtr functionPointer)
            => (((UInt64)(void*)functionPointer) & (UInt64)FatFunctionPointerOffset) == (UInt64)FatFunctionPointerOffset;

        public static GenericMethodDescriptor* ConvertToGenericDescriptor(IntPtr functionPointer)
            => (GenericMethodDescriptor*)((Byte*)(void*)functionPointer - FatFunctionPointerOffset);

        public static Boolean Compare(IntPtr functionPointerA, IntPtr functionPointerB)
        {
            if (!IsGenericMethodPointer(functionPointerA)) return functionPointerA == functionPointerB;
            if (!IsGenericMethodPointer(functionPointerB)) return false;
            GenericMethodDescriptor* a = ConvertToGenericDescriptor(functionPointerA);
            GenericMethodDescriptor* b = ConvertToGenericDescriptor(functionPointerB);
            return a->InstantiationArgument == b->InstantiationArgument && a->MethodFunctionPointer == b->MethodFunctionPointer;
        }

        public static Int32 GetHashCode(IntPtr functionPointer)
        {
            if (!IsGenericMethodPointer(functionPointer)) return functionPointer.GetHashCode();
            GenericMethodDescriptor* descriptor = ConvertToGenericDescriptor(functionPointer);
            return descriptor->MethodFunctionPointer.GetHashCode() ^ descriptor->InstantiationArgument.GetHashCode();
        }
    }
}

namespace Internal.Runtime.TypeLoader
{
    using Internal.NativeFormat;
    using Internal.Runtime.CompilerServices;

    // Port of .NET 10 ExternalReferencesTable with Inu module lookup as the only platform adaptation.
    internal partial struct ExternalReferencesTable
    {
        private IntPtr _elements;
        private UInt32 _elementsCount;

        private unsafe Boolean Initialize(TypeManagerHandle typeManager, ReflectionMapBlob blobId)
        {
            Byte* pBlob;
            UInt32 cbBlob;
            if (!TypeLoaderEnvironment.TryFindBlob(typeManager, blobId, out pBlob, out cbBlob))
            {
                _elements = IntPtr.Zero;
                _elementsCount = 0U;
                return false;
            }
            _elements = (IntPtr)pBlob;
            _elementsCount = cbBlob / sizeof(UInt32);
            return true;
        }

        public Boolean InitializeCommonFixupsTable(TypeManagerHandle module) => Initialize(module, ReflectionMapBlob.CommonFixupsTable);
        public Boolean InitializeNativeReferences(TypeManagerHandle module) => Initialize(module, ReflectionMapBlob.NativeReferences);
        public Boolean InitializeNativeStatics(TypeManagerHandle module) => Initialize(module, ReflectionMapBlob.NativeStatics);
        public Boolean IsInitialized() => _elements != IntPtr.Zero;

        public unsafe IntPtr GetAddressFromIndex(UInt32 index)
        {
            if (index >= _elementsCount) throw new BadImageFormatException();
            if (global::Internal.Runtime.MethodTable.SupportsRelativePointers)
            {
                Int32* pRelPtr32 = &((Int32*)_elements)[index];
                return (IntPtr)((Byte*)pRelPtr32 + *pRelPtr32);
            }
            return (IntPtr)(((void**)_elements)[index]);
        }

        public IntPtr GetIntPtrFromIndex(UInt32 index) => GetAddressFromIndex(index);
        public IntPtr GetFunctionPointerFromIndex(UInt32 index) => GetAddressFromIndex(index);
        public RuntimeTypeHandle GetRuntimeTypeHandleFromIndex(UInt32 index)
            => RuntimeTypeHandle.FromIntPtr(GetAddressFromIndex(index));
    }

    // Closed-world static TypeLoader slice. The binary formats and resolution order below
    // are the .NET 10 NativeAOT GVM/exact-method/generic-dictionary/template tables. Inu
    // substitutes only module discovery; dynamic type construction remains a later extension.
    internal static unsafe class TypeLoaderEnvironment
    {
        private const Int32 ReadonlyBlobRegionStart = 300;

#pragma warning disable CS0626
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [System.Runtime.RuntimeImport("*", "RhGetModuleSection")]
        private static extern IntPtr RhGetModuleSection(IntPtr typeManager, Int32 sectionType, Int32* length);

        // Reuse Inu's existing low-level serial trace boundary. These diagnostics are intentionally
        // allocation-free so GVM lookup failures remain visible even before the managed console is ready.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [System.Runtime.RuntimeImport("*", "InuEhTrace")]
        private static extern void TraceGvmStage(UInt64 code);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [System.Runtime.RuntimeImport("*", "InuEhTraceValue")]
        private static extern void TraceGvmValue(UInt64 tag, UInt64 value);
#pragma warning restore CS0626

        private static UInt64 PointerValue(IntPtr value) => (UInt64)(void*)value;
        private static UInt64 TypeValue(RuntimeTypeHandle value) => (UInt64)(void*)RuntimeTypeHandle.ToIntPtr(value);

        internal static Boolean TryFindBlob(TypeManagerHandle module, ReflectionMapBlob blobId, out Byte* blob, out UInt32 length)
        {
            Int32 sectionType = ReadonlyBlobRegionStart + (Int32)blobId;
            TraceGvmStage(0x1970UL);
            TraceGvmValue(0x1971UL, PointerValue(module.Value));
            TraceGvmValue(0x1972UL, (UInt64)(UInt32)sectionType);

            Int32 sectionLength = 0;
            IntPtr address = RhGetModuleSection(module.Value, sectionType, &sectionLength);
            TraceGvmValue(0x1973UL, PointerValue(address));
            TraceGvmValue(0x1974UL, (UInt64)(UInt32)(sectionLength < 0 ? 0 : sectionLength));
            if (address == IntPtr.Zero || sectionLength <= 0)
            {
                TraceGvmStage(0x1975UL);
                blob = null;
                length = 0U;
                return false;
            }

            blob = (Byte*)(void*)address;
            length = (UInt32)sectionLength;
            TraceGvmStage(0x1976UL);
            return true;
        }

        private static NativeHashtable LoadHashtable(TypeManagerHandle module, ReflectionMapBlob blobId)
        {
            TraceGvmStage(0x1980UL);
            TraceGvmValue(0x1981UL, (UInt64)(UInt32)blobId);
            Byte* blob;
            UInt32 size;
            if (!TryFindBlob(module, blobId, out blob, out size))
            {
                TraceGvmStage(0x1982UL);
                return default;
            }

            TraceGvmValue(0x1983UL, PointerValue((IntPtr)blob));
            TraceGvmValue(0x1984UL, size);
            if (blob == null || size == 0U)
            {
                TraceGvmStage(0x1982UL);
                return default;
            }

            // Read the NativeHashtable header directly before constructing the reader so a
            // corrupt/misaddressed reflection-map section is visible in the serial log. The
            // validation is the same contract enforced by .NET NativeFormatReader: low two
            // bits are the entry-index width and the remaining bits are the bucket shift.
            Byte header = *blob;
            TraceGvmValue(0x1985UL, header);
            UInt32 bucketShift = (UInt32)(header >> 2);
            UInt32 entryIndexSize = (UInt32)(header & 3U);
            TraceGvmValue(0x1986UL, bucketShift);
            TraceGvmValue(0x1987UL, entryIndexSize);
            if (bucketShift > 31U || entryIndexSize > 2U)
            {
                TraceGvmStage(0x198EUL);
                return default;
            }

            NativeReader reader = new NativeReader(blob, size);
            TraceGvmStage(0x1988UL);
            NativeParser parser = new NativeParser(reader, 0U);
            TraceGvmStage(0x1989UL);
            NativeHashtable table = new NativeHashtable(parser);
            TraceGvmStage(0x198AUL);
            return table;
        }

        private static RuntimeTypeHandle GetTypeDefinition(RuntimeTypeHandle handle)
        {
            MethodTable* table = handle.ToMethodTable();
            if (table == null) return default;
            MethodTable* definition = table->GenericDefinition;
            return definition == null ? handle : new RuntimeTypeHandle(definition);
        }

        private static Boolean SameType(RuntimeTypeHandle left, RuntimeTypeHandle right)
            => RuntimeTypeHandle.ToIntPtr(left) == RuntimeTypeHandle.ToIntPtr(right);

        private static RuntimeTypeHandle GetMethodArgument(MethodHandleInfo* method, Int32 index)
        {
            if (method == null || index < 0 || index >= method->NumGenericArgs) return default;
            RuntimeTypeHandle* first = &method->FirstArgument;
            return first[index];
        }

        private static Boolean MatchInstantiation(ref NativeParser entryParser, ExternalReferencesTable refs, MethodHandleInfo* slot)
        {
            UInt32 count = entryParser.GetSequenceCount();
            if (slot == null || count != (UInt32)slot->NumGenericArgs)
            {
                for (UInt32 i = 0; i < count; i++) entryParser.GetUnsigned();
                return false;
            }

            Boolean matches = true;
            for (UInt32 i = 0; i < count; i++)
            {
                RuntimeTypeHandle parsed = refs.GetRuntimeTypeHandleFromIndex(entryParser.GetUnsigned());
                if (!SameType(parsed, GetMethodArgument(slot, (Int32)i))) matches = false;
            }
            return matches;
        }

        private static Boolean TryResolveClassGvmImplementation(RuntimeTypeHandle targetType, MethodHandleInfo* slot, out RuntimeTypeHandle implementationType, out UInt32 implementationMethodToken)
        {
            implementationType = default;
            implementationMethodToken = 0U;
            TraceGvmStage(0x1920UL);
            if (slot == null || slot->NumGenericArgs <= 0)
            {
                TraceGvmStage(0x192FUL);
                return false;
            }

            RuntimeTypeHandle openCallingType = GetTypeDefinition(slot->DeclaringType);
            UInt32 callingToken = (UInt32)slot->Handle.Value;
            MethodTable* current = targetType.ToMethodTable();
            TraceGvmValue(0x1921UL, TypeValue(openCallingType));
            TraceGvmValue(0x1922UL, callingToken);

            TypeManagerHandle[] modules = new TypeManagerHandle[global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(null)];
            global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(modules);
            TraceGvmValue(0x1923UL, (UInt64)(UInt32)modules.Length);

            for (Int32 depth = 0; current != null && depth < 256; depth++)
            {
                RuntimeTypeHandle concreteTarget = new RuntimeTypeHandle(current);
                RuntimeTypeHandle openTarget = GetTypeDefinition(concreteTarget);
                Int32 hashCode = openCallingType.GetHashCode();
                hashCode = unchecked(((hashCode << 13) ^ hashCode) ^ openTarget.GetHashCode());
                TraceGvmValue(0x1924UL, (UInt64)(UInt32)depth);
                TraceGvmValue(0x1925UL, TypeValue(concreteTarget));
                TraceGvmValue(0x1926UL, TypeValue(openTarget));
                TraceGvmValue(0x1927UL, (UInt64)(UInt32)hashCode);

                for (Int32 moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
                {
                    NativeHashtable table = LoadHashtable(modules[moduleIndex], ReflectionMapBlob.GenericVirtualMethodTable);
                    if (table.IsNull) continue;
                    ExternalReferencesTable refs = default;
                    if (!refs.InitializeCommonFixupsTable(modules[moduleIndex])) continue;
                    TraceGvmValue(0x1928UL, (UInt64)(UInt32)moduleIndex);

                    // Match .NET 10 TypeLoaderEnvironment.GVMResolution exactly: use the
                    // hashtable's combined calling/target type hash rather than scanning the
                    // complete GVM table. This is both the NativeAOT contract and critical for
                    // acceptable lookup cost under QEMU TCG.
                    UInt32 lookupBucket;
                    UInt32 lookupStart;
                    UInt32 lookupEnd;
                    TraceGvmStage(0x19A0UL);
                    table.GetLookupBounds(hashCode, out lookupBucket, out lookupStart, out lookupEnd);
                    TraceGvmValue(0x19A1UL, lookupBucket);
                    TraceGvmValue(0x19A2UL, lookupStart);
                    TraceGvmValue(0x19A3UL, lookupEnd);
                    NativeHashtable.Enumerator entries = table.Lookup(hashCode);
                    TraceGvmStage(0x19A4UL);
                    NativeParser entry;
                    UInt32 candidates = 0U;
                    while (!(entry = entries.GetNext()).IsNull)
                    {
                        candidates++;
                        RuntimeTypeHandle parsedCallingType = refs.GetRuntimeTypeHandleFromIndex(entry.GetUnsigned());
                        RuntimeTypeHandle parsedTargetType = refs.GetRuntimeTypeHandleFromIndex(entry.GetUnsigned());
                        UInt32 parsedCallingToken = entry.GetUnsigned();
                        UInt32 parsedTargetToken = entry.GetUnsigned();
                        TraceGvmValue(0x1929UL, candidates);
                        TraceGvmValue(0x192AUL, TypeValue(parsedCallingType));
                        TraceGvmValue(0x192BUL, TypeValue(parsedTargetType));
                        TraceGvmValue(0x192CUL, parsedCallingToken);
                        TraceGvmValue(0x192DUL, parsedTargetToken);
                        if (SameType(parsedCallingType, openCallingType) &&
                            SameType(parsedTargetType, openTarget) &&
                            parsedCallingToken == callingToken)
                        {
                            implementationType = concreteTarget;
                            implementationMethodToken = parsedTargetToken;
                            TraceGvmStage(0x192EUL);
                            return true;
                        }
                    }
                }

                MethodTable* next = current->BaseType;
                if (next == current) break;
                current = next;
            }
            TraceGvmStage(0x192FUL);
            return false;
        }

        private static Boolean TryGetExactMethodPointer(RuntimeTypeHandle declaringType, UInt32 methodToken, MethodHandleInfo* slot, out IntPtr methodPointer)
        {
            methodPointer = IntPtr.Zero;
            TraceGvmStage(0x1930UL);
            TraceGvmValue(0x1931UL, TypeValue(declaringType));
            TraceGvmValue(0x1932UL, methodToken);
            Int32 hashCode = declaringType.GetHashCode();
            TraceGvmValue(0x1933UL, (UInt64)(UInt32)hashCode);

            TypeManagerHandle[] modules = new TypeManagerHandle[global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(null)];
            global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(modules);

            for (Int32 moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
            {
                NativeHashtable table = LoadHashtable(modules[moduleIndex], ReflectionMapBlob.ExactMethodInstantiationsHashtable);
                if (table.IsNull) continue;
                ExternalReferencesTable refs = default;
                // ExactMethodInstantiationsHashtable is emitted against NativeReferences.
                // CommonFixups is used by the GVM resolution maps, but using it here shifts
                // every type/function index and makes exact value-type instantiations look
                // like mismatches. This mirrors .NET 10 GetHashtableFromBlob.
                if (!refs.InitializeNativeReferences(modules[moduleIndex])) continue;
                TraceGvmValue(0x1934UL, (UInt64)(UInt32)moduleIndex);

                // .NET 10 TryLookupExactMethodPointer hashes this table by the owning type.
                NativeHashtable.Enumerator entries = table.Lookup(hashCode);
                NativeParser entry;
                UInt32 candidates = 0U;
                while (!(entry = entries.GetNext()).IsNull)
                {
                    candidates++;
                    RuntimeTypeHandle parsedDeclaringType = refs.GetRuntimeTypeHandleFromIndex(entry.GetUnsigned());
                    UInt32 parsedToken = entry.GetUnsigned();
                    Boolean instantiationMatches = MatchInstantiation(ref entry, refs, slot);
                    UInt32 functionPointerIndex = entry.GetUnsigned();
                    TraceGvmValue(0x1935UL, candidates);
                    TraceGvmValue(0x1936UL, TypeValue(parsedDeclaringType));
                    TraceGvmValue(0x1937UL, parsedToken);
                    TraceGvmValue(0x1938UL, instantiationMatches ? 1UL : 0UL);
                    if (SameType(parsedDeclaringType, declaringType) && parsedToken == methodToken && instantiationMatches)
                    {
                        methodPointer = refs.GetFunctionPointerFromIndex(functionPointerIndex);
                        TraceGvmValue(0x1939UL, PointerValue(methodPointer));
                        if (methodPointer != IntPtr.Zero)
                        {
                            TraceGvmStage(0x193AUL);
                            return true;
                        }
                    }
                }
            }
            TraceGvmStage(0x193FUL);
            return false;
        }

        private static Boolean TryGetGenericDictionary(RuntimeTypeHandle declaringType, UInt32 methodToken, MethodHandleInfo* slot, out IntPtr dictionaryPointer)
        {
            dictionaryPointer = IntPtr.Zero;
            TraceGvmStage(0x1950UL);
            TraceGvmValue(0x1951UL, TypeValue(declaringType));
            TraceGvmValue(0x1952UL, methodToken);
            TypeManagerHandle[] modules = new TypeManagerHandle[global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(null)];
            global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(modules);

            for (Int32 moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
            {
                NativeHashtable table = LoadHashtable(modules[moduleIndex], ReflectionMapBlob.GenericMethodsHashtable);
                if (table.IsNull) continue;
                ExternalReferencesTable refs = default;
                // GenericMethodsHashtable uses the same NativeReferences table as the exact
                // method-instantiation map. Keep CommonFixups reserved for the GVM tables.
                if (!refs.InitializeNativeReferences(modules[moduleIndex])) continue;
                TraceGvmValue(0x1953UL, (UInt64)(UInt32)moduleIndex);

                // The official table is keyed by InstantiatedMethod.GetHashCode(), which includes
                // the method-name hash. This thin closed-world loader does not yet carry the native
                // metadata name reader, so keep the semantically exact fallback scan here and make
                // its cost/results fully observable. Do not silently replace the .NET hash contract.
                NativeHashtable.AllEntriesEnumerator entries = table.EnumerateAllEntries();
                NativeParser entry;
                UInt32 candidates = 0U;
                while (!(entry = entries.GetNext()).IsNull)
                {
                    candidates++;
                    UInt32 dictionaryIndex = entry.GetUnsigned();
                    RuntimeTypeHandle parsedDeclaringType = refs.GetRuntimeTypeHandleFromIndex(entry.GetUnsigned());
                    UInt32 parsedToken = entry.GetUnsigned();
                    Boolean instantiationMatches = MatchInstantiation(ref entry, refs, slot);
                    if ((candidates & 63U) == 0U) TraceGvmValue(0x1954UL, candidates);
                    if (SameType(parsedDeclaringType, declaringType) && parsedToken == methodToken && instantiationMatches)
                    {
                        TraceGvmValue(0x1955UL, candidates);
                        dictionaryPointer = refs.GetIntPtrFromIndex(dictionaryIndex);
                        TraceGvmValue(0x1956UL, PointerValue(dictionaryPointer));
                        if (dictionaryPointer != IntPtr.Zero)
                        {
                            TraceGvmStage(0x195AUL);
                            return true;
                        }
                    }
                }
                TraceGvmValue(0x1957UL, candidates);
            }
            TraceGvmStage(0x195FUL);
            return false;
        }

        private static Boolean TryReadExternalTypeSignature(ref NativeParser parser, ExternalReferencesTable refs, out RuntimeTypeHandle type)
        {
            UInt32 encoded = parser.GetUnsigned();
            if ((encoded & 0xFU) != (UInt32)TypeSignatureKind.External)
            {
                type = default;
                return false;
            }
            type = refs.GetRuntimeTypeHandleFromIndex(encoded >> 4);
            return true;
        }

        private static Boolean TryGetTemplateMethodPointer(RuntimeTypeHandle declaringType, UInt32 methodToken, Int32 genericArgumentCount, out IntPtr methodPointer, out TypeManagerHandle nativeLayoutModule, out UInt32 nativeLayoutToken)
        {
            methodPointer = IntPtr.Zero;
            nativeLayoutModule = default;
            nativeLayoutToken = 0U;
            TraceGvmStage(0x1940UL);
            RuntimeTypeHandle declaringDefinition = GetTypeDefinition(declaringType);
            TraceGvmValue(0x1941UL, TypeValue(declaringType));
            TraceGvmValue(0x1942UL, TypeValue(declaringDefinition));
            TraceGvmValue(0x1943UL, methodToken);
            TraceGvmValue(0x1944UL, (UInt64)(UInt32)genericArgumentCount);
            TypeManagerHandle[] modules = new TypeManagerHandle[global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(null)];
            global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(modules);

            for (Int32 moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
            {
                Byte* layoutBlob;
                UInt32 layoutSize;
                if (!TryFindBlob(modules[moduleIndex], ReflectionMapBlob.NativeLayoutInfo, out layoutBlob, out layoutSize)) continue;
                NativeReader layoutReader = new NativeReader(layoutBlob, layoutSize);

                NativeHashtable templateMap = LoadHashtable(modules[moduleIndex], ReflectionMapBlob.GenericMethodsTemplateMap);
                if (templateMap.IsNull) continue;
                ExternalReferencesTable nativeRefs = default;
                if (!nativeRefs.InitializeNativeReferences(modules[moduleIndex])) continue;
                TraceGvmValue(0x1945UL, (UInt64)(UInt32)moduleIndex);
                TraceGvmValue(0x1946UL, layoutSize);

                // TemplateLocator in the full .NET TypeLoader hashes by the canonical MethodDesc.
                // Until the thin loader owns the corresponding native metadata name reader, scan
                // candidates without inventing a substitute hash and report the scan count.
                NativeHashtable.AllEntriesEnumerator entries = templateMap.EnumerateAllEntries();
                NativeParser mapEntry;
                UInt32 candidates = 0U;
                while (!(mapEntry = entries.GetNext()).IsNull)
                {
                    candidates++;
                    UInt32 methodEntryOffset = mapEntry.GetUnsigned();
                    UInt32 templateLayoutOffset = mapEntry.GetUnsigned();
                    if ((candidates & 63U) == 0U) TraceGvmValue(0x1947UL, candidates);
                    if (methodEntryOffset >= layoutSize) continue;

                    NativeParser method = new NativeParser(layoutReader, methodEntryOffset);
                    MethodFlags flags = (MethodFlags)method.GetUnsigned();
                    UInt32 functionPointerIndex = 0U;
                    if ((flags & MethodFlags.HasFunctionPointer) != 0) functionPointerIndex = method.GetUnsigned();

                    RuntimeTypeHandle parsedContainingType;
                    if (!TryReadExternalTypeSignature(ref method, nativeRefs, out parsedContainingType)) continue;
                    UInt32 parsedToken = method.GetUnsigned();

                    Int32 parsedGenericArgCount = 0;
                    if ((flags & MethodFlags.HasInstantiation) != 0)
                    {
                        parsedGenericArgCount = (Int32)method.GetUnsigned();
                        Boolean signaturesValid = true;
                        for (Int32 argIndex = 0; argIndex < parsedGenericArgCount; argIndex++)
                        {
                            RuntimeTypeHandle unused;
                            if (!TryReadExternalTypeSignature(ref method, nativeRefs, out unused)) signaturesValid = false;
                        }
                        if (!signaturesValid) continue;
                    }

                    if ((flags & MethodFlags.HasFunctionPointer) == 0 ||
                        parsedToken != methodToken ||
                        parsedGenericArgCount != genericArgumentCount ||
                        !SameType(GetTypeDefinition(parsedContainingType), declaringDefinition))
                        continue;

                    TraceGvmValue(0x1948UL, candidates);
                    TraceGvmValue(0x1949UL, methodEntryOffset);
                    TraceGvmValue(0x194AUL, templateLayoutOffset);
                    TraceGvmValue(0x194BUL, (UInt64)(UInt32)flags);
                    methodPointer = nativeRefs.GetFunctionPointerFromIndex(functionPointerIndex);
                    TraceGvmValue(0x194CUL, PointerValue(methodPointer));
                    if (methodPointer != IntPtr.Zero)
                    {
                        nativeLayoutModule = modules[moduleIndex];
                        nativeLayoutToken = templateLayoutOffset;
                        TraceGvmValue(0x194FUL, nativeLayoutToken);
                        TraceGvmStage(0x194DUL);
                        return true;
                    }
                }
                TraceGvmValue(0x194EUL, candidates);
            }
            TraceGvmStage(0x194FUL);
            return false;
        }

        private static RuntimeTypeHandle GetTypeArgument(RuntimeTypeHandle type, UInt32 index)
        {
            MethodTable* table = type.ToMethodTable();
            if (table == null || !table->IsGeneric || index >= table->GenericArity) return default;
            MethodTable* argument = table->GetGenericArgument(index);
            return argument == null ? default : new RuntimeTypeHandle(argument);
        }

        // Static constructed-generic lookup used by NativeLayout type signatures.  This is the
        // same GenericsHashtable emitted by .NET 10 ILC; scanning is deliberate here because the
        // thin loader does not carry TypeHashingAlgorithms yet and correctness matters more than
        // lookup frequency on the rare dynamic-dictionary path.
        private static Boolean TryLookupConstructedGenericType(RuntimeTypeHandle definition, RuntimeTypeHandle[] arguments, out RuntimeTypeHandle result)
        {
            result = default;
            if (definition.IsNull || arguments == null || arguments.Length == 0) return false;

            TypeManagerHandle[] modules = new TypeManagerHandle[global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(null)];
            global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(modules);
            for (Int32 moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
            {
                NativeHashtable table = LoadHashtable(modules[moduleIndex], ReflectionMapBlob.GenericsHashtable);
                if (table.IsNull) continue;
                ExternalReferencesTable refs = default;
                if (!refs.InitializeNativeReferences(modules[moduleIndex])) continue;

                NativeHashtable.AllEntriesEnumerator entries = table.EnumerateAllEntries();
                NativeParser entry;
                while (!(entry = entries.GetNext()).IsNull)
                {
                    RuntimeTypeHandle candidate = refs.GetRuntimeTypeHandleFromIndex(entry.GetUnsigned());
                    MethodTable* candidateTable = candidate.ToMethodTable();
                    if (candidateTable == null || !candidateTable->IsGeneric || candidateTable->GenericDefinition != definition.ToMethodTable()) continue;
                    if (candidateTable->GenericArity != (UInt32)arguments.Length) continue;

                    Boolean matches = true;
                    for (Int32 i = 0; i < arguments.Length; i++)
                    {
                        if (candidateTable->GetGenericArgument((UInt32)i) != arguments[i].ToMethodTable())
                        {
                            matches = false;
                            break;
                        }
                    }
                    if (matches)
                    {
                        result = candidate;
                        return true;
                    }
                }
            }
            return false;
        }

        private static Boolean TryLookupParameterizedType(RuntimeTypeHandle elementType, ReflectionMapBlob mapBlob, TypeModifierKind modifier, Int32 rank, out RuntimeTypeHandle result)
        {
            result = default;
            MethodTable* elementTable = elementType.ToMethodTable();
            if (elementTable == null) return false;

            TypeManagerHandle[] modules = new TypeManagerHandle[global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(null)];
            global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(modules);
            for (Int32 moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
            {
                NativeHashtable table = LoadHashtable(modules[moduleIndex], mapBlob);
                if (table.IsNull) continue;
                ExternalReferencesTable refs = default;
                if (!refs.InitializeCommonFixupsTable(modules[moduleIndex])) continue;

                NativeHashtable.AllEntriesEnumerator entries = table.EnumerateAllEntries();
                NativeParser entry;
                while (!(entry = entries.GetNext()).IsNull)
                {
                    RuntimeTypeHandle candidate = refs.GetRuntimeTypeHandleFromIndex(entry.GetUnsigned());
                    MethodTable* candidateTable = candidate.ToMethodTable();
                    if (candidateTable == null || candidateTable->RelatedParameterType != elementTable) continue;
                    if (modifier == TypeModifierKind.Array)
                    {
                        if (!candidateTable->IsArray) continue;
                        if (rank < 0)
                        {
                            if (!candidateTable->IsSzArray) continue;
                        }
                        else if (candidateTable->IsSzArray || candidateTable->ArrayRank != rank) continue;
                    }
                    else if (modifier == TypeModifierKind.ByRef)
                    {
                        if (!candidateTable->IsByRef) continue;
                    }
                    else if (modifier == TypeModifierKind.Pointer)
                    {
                        if (!candidateTable->IsPointer) continue;
                    }
                    else continue;

                    result = candidate;
                    return true;
                }
            }
            return false;
        }

        private static Boolean TryLookupFunctionPointerType(RuntimeTypeHandle returnType, RuntimeTypeHandle[] parameters, Boolean unmanaged, out RuntimeTypeHandle result)
        {
            result = default;
            MethodTable* returnTable = returnType.ToMethodTable();
            if (returnTable == null || parameters == null) return false;

            TypeManagerHandle[] modules = new TypeManagerHandle[global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(null)];
            global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(modules);
            for (Int32 moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
            {
                NativeHashtable table = LoadHashtable(modules[moduleIndex], ReflectionMapBlob.FunctionPointerTypeMap);
                if (table.IsNull) continue;
                ExternalReferencesTable refs = default;
                if (!refs.InitializeCommonFixupsTable(modules[moduleIndex])) continue;

                NativeHashtable.AllEntriesEnumerator entries = table.EnumerateAllEntries();
                NativeParser entry;
                while (!(entry = entries.GetNext()).IsNull)
                {
                    RuntimeTypeHandle candidate = refs.GetRuntimeTypeHandleFromIndex(entry.GetUnsigned());
                    MethodTable* candidateTable = candidate.ToMethodTable();
                    if (candidateTable == null || !candidateTable->IsFunctionPointer || candidateTable->FunctionPointerReturnType != returnTable) continue;
                    if (candidateTable->IsUnmanagedFunctionPointer != unmanaged || candidateTable->NumFunctionPointerParameters != (UInt32)parameters.Length) continue;

                    Boolean matches = true;
                    for (Int32 i = 0; i < parameters.Length; i++)
                    {
                        if (candidateTable->GetFunctionPointerParameter((UInt32)i) != parameters[i].ToMethodTable())
                        {
                            matches = false;
                            break;
                        }
                    }
                    if (!matches) continue;
                    result = candidate;
                    return true;
                }
            }
            return false;
        }

        private static Boolean TryResolveBuiltInType(UInt32 id, out RuntimeTypeHandle result)
        {
            // Internal.TypeSystem.WellKnownType numbering in .NET 10.0.10. Keep this local to
            // the NativeLayout decoder so Inu does not need to import the compiler type system.
            switch (id)
            {
                case 1U: result = typeof(void).TypeHandle; return true;
                case 2U: result = typeof(Boolean).TypeHandle; return true;
                case 3U: result = typeof(Char).TypeHandle; return true;
                case 4U: result = typeof(SByte).TypeHandle; return true;
                case 5U: result = typeof(Byte).TypeHandle; return true;
                case 6U: result = typeof(Int16).TypeHandle; return true;
                case 7U: result = typeof(UInt16).TypeHandle; return true;
                case 8U: result = typeof(Int32).TypeHandle; return true;
                case 9U: result = typeof(UInt32).TypeHandle; return true;
                case 10U: result = typeof(Int64).TypeHandle; return true;
                case 11U: result = typeof(UInt64).TypeHandle; return true;
                case 12U: result = typeof(IntPtr).TypeHandle; return true;
                case 13U: result = typeof(UIntPtr).TypeHandle; return true;
                case 14U: result = typeof(Single).TypeHandle; return true;
                case 15U: result = typeof(Double).TypeHandle; return true;
                case 16U: result = typeof(ValueType).TypeHandle; return true;
                case 17U: result = typeof(Enum).TypeHandle; return true;
                case 18U: result = typeof(Nullable<>).TypeHandle; return true;
                case 19U: result = typeof(Object).TypeHandle; return true;
                case 20U: result = typeof(String).TypeHandle; return true;
                case 21U: result = typeof(Array).TypeHandle; return true;
                case 22U: result = typeof(MulticastDelegate).TypeHandle; return true;
                case 23U: result = typeof(RuntimeTypeHandle).TypeHandle; return true;
                case 24U: result = typeof(RuntimeMethodHandle).TypeHandle; return true;
                case 25U: result = typeof(RuntimeFieldHandle).TypeHandle; return true;
                case 26U: result = typeof(Exception).TypeHandle; return true;
                default: result = default; return false;
            }
        }

        // Resolve the NativeLayout type signatures needed by dynamic generic method dictionaries
        // and interface-GVM signatures. External, built-in, type/method variable, lookback and
        // statically compiled generic-instantiation signatures preserve the .NET 10 ABI.
        private static Boolean TryResolveNativeLayoutType(ref NativeParser parser, TypeManagerHandle module, RuntimeTypeHandle declaringType, MethodHandleInfo* method, out RuntimeTypeHandle result)
        {
            result = default;
            UInt32 data;
            TypeSignatureKind kind = parser.GetTypeSignatureKind(out data);
            switch (kind)
            {
                case TypeSignatureKind.Lookback:
                    {
                        NativeParser lookback = parser.GetLookbackParser(data);
                        return TryResolveNativeLayoutType(ref lookback, module, declaringType, method, out result);
                    }

                case TypeSignatureKind.Variable:
                    {
                        UInt32 index = data >> 1;
                        if ((data & 1U) != 0U)
                        {
                            if (method == null || index >= (UInt32)method->NumGenericArgs) return false;
                            result = GetMethodArgument(method, (Int32)index);
                        }
                        else
                        {
                            result = GetTypeArgument(declaringType, index);
                        }
                        return !result.IsNull;
                    }

                case TypeSignatureKind.External:
                    {
                        ExternalReferencesTable refs = default;
                        if (!refs.InitializeNativeReferences(module)) return false;
                        result = refs.GetRuntimeTypeHandleFromIndex(data);
                        return !result.IsNull;
                    }

                case TypeSignatureKind.BuiltIn:
                    return TryResolveBuiltInType(data, out result);

                case TypeSignatureKind.Instantiation:
                    {
                        UInt32 arity = data;
                        if (arity == 0U || arity > 64U) return false;
                        RuntimeTypeHandle definition;
                        if (!TryResolveNativeLayoutType(ref parser, module, declaringType, method, out definition)) return false;
                        RuntimeTypeHandle[] arguments = new RuntimeTypeHandle[arity];
                        for (UInt32 i = 0U; i < arity; i++)
                            if (!TryResolveNativeLayoutType(ref parser, module, declaringType, method, out arguments[i])) return false;
                        return TryLookupConstructedGenericType(definition, arguments, out result);
                    }

                case TypeSignatureKind.Modifier:
                    {
                        RuntimeTypeHandle elementType;
                        if (!TryResolveNativeLayoutType(ref parser, module, declaringType, method, out elementType)) return false;
                        TypeModifierKind modifier = (TypeModifierKind)data;
                        ReflectionMapBlob blob;
                        Int32 rank = 0;
                        if (modifier == TypeModifierKind.Array) { blob = ReflectionMapBlob.ArrayMap; rank = -1; }
                        else if (modifier == TypeModifierKind.ByRef) blob = ReflectionMapBlob.ByRefTypeMap;
                        else if (modifier == TypeModifierKind.Pointer) blob = ReflectionMapBlob.PointerTypeMap;
                        else return false;
                        return TryLookupParameterizedType(elementType, blob, modifier, rank, out result);
                    }

                case TypeSignatureKind.MultiDimArray:
                    {
                        if (data == 0U || data > 32U) return false;
                        RuntimeTypeHandle elementType;
                        if (!TryResolveNativeLayoutType(ref parser, module, declaringType, method, out elementType)) return false;
                        UInt32 boundsCount = parser.GetUnsigned();
                        for (UInt32 i = 0U; i < boundsCount; i++) parser.GetUnsigned();
                        UInt32 loBoundsCount = parser.GetUnsigned();
                        for (UInt32 i = 0U; i < loBoundsCount; i++) parser.GetUnsigned();
                        return TryLookupParameterizedType(elementType, ReflectionMapBlob.ArrayMap, TypeModifierKind.Array, checked((Int32)data), out result);
                    }

                case TypeSignatureKind.FunctionPointer:
                    {
                        UInt32 callConvention = parser.GetUnsigned();
                        if ((callConvention & 0x1U) != 0U) return false; // NativeAOT NativeLayout does not encode generic function-pointer signatures here.
                        UInt32 parameterCount = parser.GetUnsigned();
                        if (parameterCount > 64U) return false;
                        RuntimeTypeHandle returnType;
                        if (!TryResolveNativeLayoutType(ref parser, module, declaringType, method, out returnType)) return false;
                        RuntimeTypeHandle[] parameters = new RuntimeTypeHandle[parameterCount];
                        for (UInt32 i = 0U; i < parameterCount; i++)
                            if (!TryResolveNativeLayoutType(ref parser, module, declaringType, method, out parameters[i])) return false;
                        Boolean unmanaged = (callConvention & 0x4U) != 0U;
                        return TryLookupFunctionPointerType(returnType, parameters, unmanaged, out result);
                    }

                default:
                    return false;
            }
        }

        private static Boolean TryResolveNativeLayoutMethod(
            ref NativeParser parser,
            TypeManagerHandle module,
            RuntimeTypeHandle declaringTypeContext,
            MethodHandleInfo* outerMethod,
            out RuntimeTypeHandle methodDeclaringType,
            out UInt32 methodToken,
            out RuntimeTypeHandle[] methodArguments,
            out IntPtr embeddedFunctionPointer,
            out Boolean isUnboxingStub)
        {
            methodDeclaringType = default;
            methodToken = 0U;
            methodArguments = Array.Empty<RuntimeTypeHandle>();
            embeddedFunctionPointer = IntPtr.Zero;
            isUnboxingStub = false;

            MethodFlags flags = (MethodFlags)parser.GetUnsigned();
            if ((flags & MethodFlags.HasFunctionPointer) != 0)
            {
                ExternalReferencesTable refs = default;
                if (!refs.InitializeNativeReferences(module)) return false;
                embeddedFunctionPointer = refs.GetFunctionPointerFromIndex(parser.GetUnsigned());
                if (embeddedFunctionPointer == IntPtr.Zero) return false;
            }

            if (!TryResolveNativeLayoutType(ref parser, module, declaringTypeContext, outerMethod, out methodDeclaringType))
                return false;
            methodToken = parser.GetUnsigned();
            isUnboxingStub = (flags & MethodFlags.IsUnboxingStub) != 0;

            if ((flags & MethodFlags.HasInstantiation) != 0)
            {
                UInt32 count = parser.GetSequenceCount();
                if (count == 0U || count > 64U) return false;
                methodArguments = new RuntimeTypeHandle[count];
                for (UInt32 i = 0U; i < count; i++)
                    if (!TryResolveNativeLayoutType(ref parser, module, declaringTypeContext, outerMethod, out methodArguments[i]))
                        return false;
            }

            return !methodDeclaringType.IsNull;
        }

        private static void FillMethodHandleInfo(MethodHandleInfo* info, RuntimeTypeHandle declaringType, UInt32 token, RuntimeTypeHandle[] arguments)
        {
            info->DeclaringType = declaringType;
            info->Handle = new global::Internal.Metadata.NativeFormat.MethodHandle((Int32)token);
            info->NumGenericArgs = arguments == null ? 0 : arguments.Length;
            info->FirstArgument = default;
            if (arguments == null || arguments.Length == 0) return;
            RuntimeTypeHandle* target = &info->FirstArgument;
            for (Int32 i = 0; i < arguments.Length; i++) target[i] = arguments[i];
        }

        private static IntPtr ResolveGenericConstrainedMethodCell(
            RuntimeTypeHandle constraintType,
            RuntimeTypeHandle constrainedDeclaringType,
            UInt32 constrainedToken,
            RuntimeTypeHandle[] constrainedArguments)
        {
            if (constrainedArguments == null || constrainedArguments.Length == 0)
                return IntPtr.Zero;

            Int32 handleBytes = checked(sizeof(MethodHandleInfo) + (constrainedArguments.Length - 1) * sizeof(RuntimeTypeHandle));
            Byte* handleStorage = stackalloc Byte[handleBytes];
            MethodHandleInfo* constrainedInfo = (MethodHandleInfo*)handleStorage;
            FillMethodHandleInfo(constrainedInfo, constrainedDeclaringType, constrainedToken, constrainedArguments);
            RuntimeMethodHandle constrainedHandle = RuntimeMethodHandle.FromIntPtr((IntPtr)constrainedInfo);
            return ResolveGenericVirtualMethodTarget(constraintType, constrainedHandle);
        }

        private static Boolean TryGetOrBuildMethodDictionary(
            RuntimeTypeHandle methodDeclaringType,
            UInt32 methodToken,
            RuntimeTypeHandle[] methodArguments,
            out IntPtr dictionaryPointer,
            Int32 recursionDepth)
        {
            dictionaryPointer = IntPtr.Zero;
            if (methodArguments == null || methodArguments.Length == 0 || recursionDepth > 24) return false;

            Int32 bytes = checked(sizeof(MethodHandleInfo) + (methodArguments.Length - 1) * sizeof(RuntimeTypeHandle));
            Byte* storage = stackalloc Byte[bytes];
            MethodHandleInfo* info = (MethodHandleInfo*)storage;
            FillMethodHandleInfo(info, methodDeclaringType, methodToken, methodArguments);

            if (TryGetGenericDictionary(methodDeclaringType, methodToken, info, out dictionaryPointer))
                return dictionaryPointer != IntPtr.Zero;

            IntPtr templatePointer;
            TypeManagerHandle templateModule;
            UInt32 templateLayoutToken;
            if (!TryGetTemplateMethodPointer(methodDeclaringType, methodToken, methodArguments.Length, out templatePointer, out templateModule, out templateLayoutToken))
                return false;

            return TryBuildGenericMethodDictionary(templateModule, templateLayoutToken, methodDeclaringType, info, out dictionaryPointer, recursionDepth + 1);
        }

        private static Boolean TryResolveMethodCell(
            RuntimeTypeHandle methodDeclaringType,
            UInt32 methodToken,
            RuntimeTypeHandle[] methodArguments,
            IntPtr embeddedFunctionPointer,
            out IntPtr result,
            Int32 recursionDepth)
        {
            result = IntPtr.Zero;
            if (recursionDepth > 24) return false;

            Int32 genericCount = methodArguments == null ? 0 : methodArguments.Length;
            IntPtr target = embeddedFunctionPointer;
            TypeManagerHandle templateModule = default;
            UInt32 templateLayoutToken = 0U;

            if (genericCount > 0)
            {
                IntPtr dictionaryPointer;
                Int32 bytes = checked(sizeof(MethodHandleInfo) + (genericCount - 1) * sizeof(RuntimeTypeHandle));
                Byte* storage = stackalloc Byte[bytes];
                MethodHandleInfo* info = (MethodHandleInfo*)storage;
                FillMethodHandleInfo(info, methodDeclaringType, methodToken, methodArguments);

                if (!TryGetGenericDictionary(methodDeclaringType, methodToken, info, out dictionaryPointer))
                {
                    IntPtr locatedTemplate;
                    if (!TryGetTemplateMethodPointer(methodDeclaringType, methodToken, genericCount, out locatedTemplate, out templateModule, out templateLayoutToken))
                        return false;
                    if (target == IntPtr.Zero) target = locatedTemplate;
                    if (!TryBuildGenericMethodDictionary(templateModule, templateLayoutToken, methodDeclaringType, info, out dictionaryPointer, recursionDepth + 1))
                        return false;
                }
                else if (target == IntPtr.Zero)
                {
                    if (!TryGetTemplateMethodPointer(methodDeclaringType, methodToken, genericCount, out target, out templateModule, out templateLayoutToken))
                        return false;
                }

                result = FunctionPointerOps.GetGenericMethodFunctionPointer(target, dictionaryPointer);
                return result != IntPtr.Zero;
            }

            // MethodCell uses the owning type as the hidden instantiation argument
            // for non-generic methods (the exact .NET 10 GenericDictionaryCell ABI).
            if (target == IntPtr.Zero) return false;
            result = FunctionPointerOps.GetGenericMethodFunctionPointer(target, RuntimeTypeHandle.ToIntPtr(methodDeclaringType));
            return result != IntPtr.Zero;
        }

        private static Boolean TryResolveConstrainedDispatch(
            RuntimeTypeHandle constraintType,
            RuntimeTypeHandle constrainedMethodType,
            UInt32 slot,
            Boolean staticDispatch,
            out IntPtr result)
        {
            result = IntPtr.Zero;
            if (slot > UInt16.MaxValue) return false;
            MethodTable* rootTarget = constraintType.ToMethodTable();
            MethodTable* interfaceType = constrainedMethodType.ToMethodTable();
            if (rootTarget == null || interfaceType == null) return false;

            for (Int32 defaultPass = 0; defaultPass < 2; defaultPass++)
            {
                for (MethodTable* current = rootTarget; current != null; current = current->NonArrayBaseType)
                {
                    UInt16 implSlot = (UInt16)0;
                    MethodTable* genericContext = null;
                    Boolean matched = false;

                    if (!interfaceType->IsInterface)
                    {
                        if (current == interfaceType)
                        {
                            implSlot = (UInt16)slot;
                            matched = true;
                        }
                    }
                    else
                    {
                        DispatchMap* map = current->DispatchMap;
                        if (map == null) continue;
                        for (Int32 variancePass = 0; variancePass < 2 && !matched; variancePass++)
                        {
                            Boolean allowVariance = variancePass != 0;
                            Int32 start;
                            Int32 end;
                            if (staticDispatch)
                            {
                                start = defaultPass == 0 ? 0 : map->StandardStaticEntryCount;
                                end = defaultPass == 0
                                    ? map->StandardStaticEntryCount
                                    : map->StandardStaticEntryCount + map->DefaultStaticEntryCount;
                                for (Int32 i = start; i < end; i++)
                                {
                                    DispatchMap.StaticDispatchMapEntry* staticEntry = map->GetStaticEntry(i);
                                    DispatchMap.DispatchMapEntry* entry = &staticEntry->Entry;
                                    if (entry->InterfaceMethodSlot != (UInt16)slot) continue;
                                    MethodTable* candidate = current->GetInterface(entry->InterfaceIndex);
                                    if (candidate == null) continue;
                                    if (candidate != interfaceType &&
                                        (!allowVariance || !global::System.Runtime.TypeCast.InterfaceTypesCompatible(candidate, interfaceType)))
                                        continue;

                                    implSlot = entry->ImplMethodSlot;
                                    UInt16 source = staticEntry->ContextMapSource;
                                    if (source == 1U) genericContext = current;
                                    else if (source >= 2U) genericContext = current->GetInterface((UInt16)(source - 2U));
                                    matched = true;
                                    break;
                                }
                            }
                            else
                            {
                                start = defaultPass == 0 ? 0 : map->StandardEntryCount;
                                end = defaultPass == 0
                                    ? map->StandardEntryCount
                                    : map->StandardEntryCount + map->DefaultEntryCount;
                                for (Int32 i = start; i < end; i++)
                                {
                                    DispatchMap.DispatchMapEntry* entry = map->GetEntry(i);
                                    if (entry->InterfaceMethodSlot != (UInt16)slot) continue;
                                    MethodTable* candidate = current->GetInterface(entry->InterfaceIndex);
                                    if (candidate == null) continue;
                                    if (candidate != interfaceType &&
                                        (!allowVariance || !global::System.Runtime.TypeCast.InterfaceTypesCompatible(candidate, interfaceType)))
                                        continue;
                                    implSlot = entry->ImplMethodSlot;
                                    matched = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!matched) continue;
                    if (implSlot == 0xFFFEU || implSlot == 0xFFFFU) return false;

                    if (implSlot < current->NumVtableSlots)
                        result = rootTarget->GetVTableSlot(implSlot);
                    else
                        result = current->GetSealedVirtualSlot((UInt16)(implSlot - current->NumVtableSlots));

                    if (result == IntPtr.Zero) return false;
                    if (staticDispatch && genericContext != null)
                        result = FunctionPointerOps.GetGenericMethodFunctionPointer(result, (IntPtr)genericContext);
                    return result != IntPtr.Zero;
                }
            }
            return false;
        }

        private static NativeParser GetStaticInfo(RuntimeTypeHandle instantiatedType, out ExternalReferencesTable staticInfoLookup)
        {
            staticInfoLookup = default;
            TraceGvmValue(0x1A50UL, TypeValue(instantiatedType));
            MethodTable* table = instantiatedType.ToMethodTable();
            if (table == null)
            {
                TraceGvmStage(0x1A51UL);
                return default;
            }
            TypeManagerHandle module = table->TypeManager;
            TraceGvmValue(0x1A52UL, PointerValue(module.Value));
            if (module.IsNull)
            {
                TraceGvmStage(0x1A53UL);
                return default;
            }

            NativeHashtable staticsInfoHashtable = LoadHashtable(module, ReflectionMapBlob.StaticsInfoHashtable);
            if (staticsInfoHashtable.IsNull)
            {
                TraceGvmStage(0x1A54UL);
                return default;
            }

            ExternalReferencesTable typeReferences = default;
            if (!typeReferences.InitializeNativeReferences(module) || !staticInfoLookup.InitializeNativeStatics(module))
            {
                TraceGvmStage(0x1A55UL);
                return default;
            }

            Int32 lookupHash = instantiatedType.GetHashCode();
            TraceGvmValue(0x1A56UL, (UInt64)(UInt32)lookupHash);
            NativeHashtable.Enumerator entries = staticsInfoHashtable.Lookup(lookupHash);
            NativeParser entry;
            UInt32 candidateCount = 0U;
            while (!(entry = entries.GetNext()).IsNull)
            {
                candidateCount++;
                RuntimeTypeHandle parsedType = typeReferences.GetRuntimeTypeHandleFromIndex(entry.GetUnsigned());
                TraceGvmValue(0x1A57UL, TypeValue(parsedType));
                if (parsedType == instantiatedType)
                {
                    TraceGvmValue(0x1A58UL, candidateCount);
                    return entry;
                }
            }
            TraceGvmValue(0x1A58UL, candidateCount);
            TraceGvmStage(0x1A59UL);
            return default;
        }

        private static Boolean TryGetStaticDataPointer(RuntimeTypeHandle instantiatedType, StaticDataKind kind, out IntPtr result)
        {
            result = IntPtr.Zero;
            TraceGvmValue(0x1A5AUL, (UInt64)(UInt32)kind);
            ExternalReferencesTable statics;
            NativeParser parser = GetStaticInfo(instantiatedType, out statics);
            if (parser.IsNull)
            {
                TraceGvmStage(0x1A5BUL);
                return false;
            }

            BagElementKind bagKind;
            switch (kind)
            {
                case StaticDataKind.Gc: bagKind = BagElementKind.GcStaticData; break;
                case StaticDataKind.NonGc: bagKind = BagElementKind.NonGcStaticData; break;
                default:
                    TraceGvmStage(0x1A5CUL);
                    return false;
            }

            UInt32? index = parser.GetUnsignedForBagElementKind(bagKind);
            if (!index.HasValue)
            {
                TraceGvmStage(0x1A5DUL);
                return false;
            }
            TraceGvmValue(0x1A5EUL, index.Value);
            result = statics.GetIntPtrFromIndex(index.Value);
            TraceGvmValue(0x1A5FUL, PointerValue(result));
            return result != IntPtr.Zero;
        }

        private static Boolean TryGetThreadStaticIndexPointer(RuntimeTypeHandle instantiatedType, out IntPtr result)
        {
            result = IntPtr.Zero;
            ExternalReferencesTable statics;
            NativeParser parser = GetStaticInfo(instantiatedType, out statics);
            if (parser.IsNull) return false;
            UInt32? index = parser.GetUnsignedForBagElementKind(BagElementKind.ThreadStaticIndex);
            if (!index.HasValue) return false;
            result = statics.GetIntPtrFromIndex(index.Value);
            return result != IntPtr.Zero;
        }

        private static Boolean DefaultConstructorTypesCompatible(RuntimeTypeHandle requestedType, RuntimeTypeHandle candidateType)
        {
            if (requestedType == candidateType) return true;
            MethodTable* requested = requestedType.ToMethodTable();
            MethodTable* candidate = candidateType.ToMethodTable();
            if (requested == null || candidate == null || !requested->IsGeneric || !candidate->IsGeneric) return false;
            if (requested->GenericDefinition != candidate->GenericDefinition) return false;
            UInt32 arity = requested->GenericArity;
            if (arity == 0U || candidate->GenericArity != arity) return false;

            // .NET 10 CanonicallyEquivalentEntryLocator uses Specific canonical-form
            // sharing. Reference-type arguments share __Canon code; value-type arguments
            // remain exact. This bounded comparison is sufficient for runtime handles
            // already materialised by the freestanding TypeLoader.
            for (UInt32 i = 0U; i < arity; i++)
            {
                MethodTable* requestedArgument = requested->GetGenericArgument(i);
                MethodTable* candidateArgument = candidate->GetGenericArgument(i);
                if (requestedArgument == candidateArgument) continue;
                if (requestedArgument == null || candidateArgument == null) return false;
                if (requestedArgument->IsValueType || candidateArgument->IsValueType) return false;
            }
            return true;
        }

        private static IntPtr TryGetDefaultConstructorPointer(RuntimeTypeHandle type)
        {
            if (type.IsNull) return IntPtr.Zero;
            TraceGvmStage(0x1A30UL);
            TraceGvmValue(0x1A31UL, TypeValue(type));

            TypeManagerHandle[] modules = new TypeManagerHandle[global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(null)];
            Int32 count = global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(modules);
            Int32 lookupHash = type.GetHashCode();
            TraceGvmValue(0x1A32UL, (UInt64)(UInt32)lookupHash);
            TraceGvmValue(0x1A3CUL, (UInt64)(UInt32)count);

            // For non-generic types .NET 10 CanonicallyEquivalentEntryLocator uses the
            // exact RuntimeTypeHandle hash. Generic types use a Specific-canonical hash;
            // if our compact freestanding hash lookup differs, the bounded all-entry scan
            // below still applies the same reference-sharing/value-type-exact equivalence.
            for (Int32 moduleIndex = 0; moduleIndex < count; moduleIndex++)
            {
                TraceGvmValue(0x1A3DUL, (UInt64)(UInt32)moduleIndex);
                NativeHashtable invokeMap = LoadHashtable(modules[moduleIndex], ReflectionMapBlob.InvokeMap);
                TraceGvmValue(0x1A3EUL, invokeMap.IsNull ? 0UL : 1UL);
                if (invokeMap.IsNull) continue;
                ExternalReferencesTable refs = default;
                Boolean refsReady = refs.InitializeCommonFixupsTable(modules[moduleIndex]);
                TraceGvmValue(0x1A3FUL, refsReady ? 1UL : 0UL);
                if (!refsReady) continue;

                UInt32 keyedSeen = 0U;
                NativeHashtable.Enumerator entries = invokeMap.Lookup(lookupHash);
                NativeParser entry;
                while (!(entry = entries.GetNext()).IsNull)
                {
                    keyedSeen++;
                    InvokeTableFlags flags = (InvokeTableFlags)entry.GetUnsigned();
                    TraceGvmValue(0x1A33UL, (UInt64)(UInt32)flags);
                    if ((flags & InvokeTableFlags.IsDefaultConstructor) == 0) continue;
                    UInt32 methodCookie = entry.GetUnsigned(); // method handle / NameAndSig cookie
                    TraceGvmValue(0x1A42UL, methodCookie);
                    RuntimeTypeHandle entryType = refs.GetRuntimeTypeHandleFromIndex(entry.GetUnsigned());
                    Boolean compatible = DefaultConstructorTypesCompatible(type, entryType);
                    TraceGvmValue(0x1A34UL, TypeValue(entryType));
                    TraceGvmValue(0x1A35UL, compatible ? 1UL : 0UL);
                    if (!compatible) continue;
                    IntPtr pointer = refs.GetFunctionPointerFromIndex(entry.GetUnsigned());
                    TraceGvmValue(0x1A36UL, PointerValue(pointer));
                    if (pointer != IntPtr.Zero)
                    {
                        TraceGvmStage(0x1A39UL);
                        return pointer;
                    }
                }
                TraceGvmValue(0x1A43UL, keyedSeen);

                TraceGvmStage(0x1A37UL);
                NativeHashtable.AllEntriesEnumerator allEntries = invokeMap.EnumerateAllEntries();
                UInt32 allSeen = 0U;
                UInt32 diagnosticSeen = 0U;
                while (!(entry = allEntries.GetNext()).IsNull)
                {
                    allSeen++;
                    InvokeTableFlags flags = (InvokeTableFlags)entry.GetUnsigned();
                    UInt32 methodCookie = entry.GetUnsigned();
                    RuntimeTypeHandle entryType = refs.GetRuntimeTypeHandleFromIndex(entry.GetUnsigned());
                    Boolean compatible = DefaultConstructorTypesCompatible(type, entryType);

                    // Report only the first sixteen raw InvokeMap entries to keep serial
                    // diagnostics useful without flooding the user's QEMU output.
                    if (diagnosticSeen < 16U)
                    {
                        TraceGvmValue(0x1A44UL, (UInt64)(UInt32)flags);
                        TraceGvmValue(0x1A45UL, methodCookie);
                        TraceGvmValue(0x1A46UL, TypeValue(entryType));
                        TraceGvmValue(0x1A47UL, compatible ? 1UL : 0UL);
                        diagnosticSeen++;
                    }

                    if ((flags & InvokeTableFlags.IsDefaultConstructor) == 0 || !compatible) continue;
                    IntPtr pointer = refs.GetFunctionPointerFromIndex(entry.GetUnsigned());
                    TraceGvmValue(0x1A36UL, PointerValue(pointer));
                    if (pointer != IntPtr.Zero)
                    {
                        TraceGvmStage(0x1A3AUL);
                        return pointer;
                    }
                }
                TraceGvmValue(0x1A48UL, allSeen);
            }

            IntPtr missing = FunctionPointerOps.MissingDefaultConstructorMarker();
            TraceGvmValue(0x1A38UL, PointerValue(missing));
            TraceGvmStage(0x1A3BUL);
            return missing;
        }

        private static Boolean TryResolveNativeLayoutInterface(TypeManagerHandle module, UInt32 signatureOffset, RuntimeTypeHandle concreteImplementationType, MethodHandleInfo* slot, Boolean allowVariance, out RuntimeTypeHandle parsedInterface)
        {
            parsedInterface = default;
            Byte* layoutBlob;
            UInt32 layoutSize;
            if (!TryFindBlob(module, ReflectionMapBlob.NativeLayoutInfo, out layoutBlob, out layoutSize) || layoutBlob == null || signatureOffset >= layoutSize)
                return false;

            NativeParser signature = new NativeParser(new NativeReader(layoutBlob, layoutSize), signatureOffset);
            if (!TryResolveNativeLayoutType(ref signature, module, concreteImplementationType, slot, out parsedInterface)) return false;
            MethodTable* source = parsedInterface.ToMethodTable();
            MethodTable* target = slot == null ? null : slot->DeclaringType.ToMethodTable();
            if (source == null || target == null) return false;
            if (source == target) return true;
            return allowVariance && global::System.Runtime.TypeCast.InterfaceTypesCompatible(source, target);
        }

        private static RuntimeTypeHandle FindConcreteHierarchyType(RuntimeTypeHandle targetType, RuntimeTypeHandle openDefinition)
        {
            MethodTable* wanted = openDefinition.ToMethodTable();
            for (MethodTable* current = targetType.ToMethodTable(), previous = null; current != null; )
            {
                if (current == previous) break;
                RuntimeTypeHandle currentHandle = new RuntimeTypeHandle(current);
                if (GetTypeDefinition(currentHandle).ToMethodTable() == wanted) return currentHandle;
                previous = current;
                current = current->BaseType;
            }
            return default;
        }

        private static RuntimeTypeHandle FindConcreteDefaultInterface(RuntimeTypeHandle targetType, RuntimeTypeHandle openDefinition, RuntimeTypeHandle matchedInterface)
        {
            MethodTable* wanted = openDefinition.ToMethodTable();
            MethodTable* matched = matchedInterface.ToMethodTable();
            if (wanted == null || matched == null) return default;

            // Default implementation on the same generic interface that supplied the slot.
            if (GetTypeDefinition(matchedInterface).ToMethodTable() == wanted) return matchedInterface;

            // Default implementation on a different interface. Match the .NET 10 TypeLoader:
            // locate the concrete instantiation of the implementation interface whose own
            // interface map contains the exact matched slot interface.
            for (MethodTable* current = targetType.ToMethodTable(), previous = null; current != null; )
            {
                if (current == previous) break;
                for (UInt16 i = 0; i < current->NumInterfaces; i++)
                {
                    MethodTable* candidate = current->GetInterface(i);
                    if (candidate == null) continue;
                    RuntimeTypeHandle candidateHandle = new RuntimeTypeHandle(candidate);
                    if (GetTypeDefinition(candidateHandle).ToMethodTable() != wanted) continue;
                    if (candidate == matched) return candidateHandle;
                    for (UInt16 nestedIndex = 0; nestedIndex < candidate->NumInterfaces; nestedIndex++)
                        if (candidate->GetInterface(nestedIndex) == matched) return candidateHandle;
                }
                previous = current;
                current = current->BaseType;
            }
            return default;
        }

        private static Boolean TryResolveMappedClassGvm(RuntimeTypeHandle targetType, RuntimeTypeHandle mappedDeclaringType, UInt32 mappedToken, MethodHandleInfo* originalSlot, out RuntimeTypeHandle implementationType, out UInt32 implementationMethodToken)
        {
            implementationType = mappedDeclaringType;
            implementationMethodToken = mappedToken;
            if (originalSlot == null || originalSlot->NumGenericArgs <= 0) return true;

            Int32 genericCount = originalSlot->NumGenericArgs;
            Int32 bytes = checked(sizeof(MethodHandleInfo) + (genericCount - 1) * sizeof(RuntimeTypeHandle));
            Byte* storage = stackalloc Byte[bytes];
            for (Int32 i = 0; i < bytes; i++) storage[i] = 0;
            MethodHandleInfo* mappedSlot = (MethodHandleInfo*)storage;
            mappedSlot->DeclaringType = mappedDeclaringType;
            mappedSlot->Handle = new global::Internal.Metadata.NativeFormat.MethodHandle((Int32)mappedToken);
            mappedSlot->NumGenericArgs = genericCount;
            RuntimeTypeHandle* mappedArguments = &mappedSlot->FirstArgument;
            for (Int32 i = 0; i < genericCount; i++) mappedArguments[i] = GetMethodArgument(originalSlot, i);

            RuntimeTypeHandle resolvedType;
            UInt32 resolvedToken;
            if (TryResolveClassGvmImplementation(targetType, mappedSlot, out resolvedType, out resolvedToken))
            {
                implementationType = resolvedType;
                implementationMethodToken = resolvedToken;
            }
            return true;
        }

        private static Boolean TryResolveInterfaceGvmImplementation(RuntimeTypeHandle targetType, MethodHandleInfo* slot, out RuntimeTypeHandle implementationType, out UInt32 implementationMethodToken)
        {
            implementationType = default;
            implementationMethodToken = 0U;
            MethodTable* slotDeclaringTable = slot == null ? null : slot->DeclaringType.ToMethodTable();
            if (slot == null || slot->NumGenericArgs <= 0 || slotDeclaringTable == null || !slotDeclaringTable->IsInterface) return false;

            TraceGvmStage(0x19C0UL);
            RuntimeTypeHandle openCallingType = GetTypeDefinition(slot->DeclaringType);
            UInt32 callingToken = (UInt32)slot->Handle.Value;
            TypeManagerHandle[] modules = new TypeManagerHandle[global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(null)];
            global::Internal.Runtime.CompilerHelpers.StartupCodeHelpers.GetLoadedModules(modules);

            // Match upstream ordering: walk the most-derived type first; for each hierarchy
            // level try an exact interface match and only then variance. Concrete class
            // implementations are exhausted before a second pass for default-interface methods.
            for (Int32 defaultPass = 0; defaultPass < 2; defaultPass++)
            {
                for (MethodTable* current = targetType.ToMethodTable(), previous = null; current != null; )
                {
                    if (current == previous) break;
                    RuntimeTypeHandle concreteTarget = new RuntimeTypeHandle(current);
                    RuntimeTypeHandle openTarget = GetTypeDefinition(concreteTarget);

                    for (Int32 variancePass = 0; variancePass < 2; variancePass++)
                    {
                        for (Int32 moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
                        {
                            NativeHashtable table = LoadHashtable(modules[moduleIndex], ReflectionMapBlob.InterfaceGenericVirtualMethodTable);
                            if (table.IsNull) continue;
                            ExternalReferencesTable refs = default;
                            if (!refs.InitializeCommonFixupsTable(modules[moduleIndex])) continue;

                            NativeHashtable.Enumerator entries = table.Lookup(openCallingType.GetHashCode());
                            NativeParser entry;
                            while (!(entry = entries.GetNext()).IsNull)
                            {
                                RuntimeTypeHandle parsedInterface = refs.GetRuntimeTypeHandleFromIndex(entry.GetUnsigned());
                                UInt32 parsedCallingToken = entry.GetUnsigned();
                                UInt32 targetCount = entry.GetUnsigned();
                                Boolean callingEntryMatches = SameType(parsedInterface, openCallingType) && parsedCallingToken == callingToken;

                                for (UInt32 targetIndex = 0U; targetIndex < targetCount; targetIndex++)
                                {
                                    UInt32 targetToken = entry.GetUnsigned();
                                    Boolean special = targetToken == 0xFFFFFFFFU || targetToken == 0xFFFFFFFEU;
                                    RuntimeTypeHandle targetDefinition = default;
                                    Boolean defaultImplementation = true;
                                    if (!special)
                                    {
                                        targetDefinition = refs.GetRuntimeTypeHandleFromIndex(entry.GetUnsigned());
                                        MethodTable* targetDefinitionTable = targetDefinition.ToMethodTable();
                                        defaultImplementation = targetDefinitionTable != null && targetDefinitionTable->IsInterface;
                                    }

                                    UInt32 implementingTypeCount = entry.GetUnsigned();
                                    for (UInt32 implementingIndex = 0U; implementingIndex < implementingTypeCount; implementingIndex++)
                                    {
                                        RuntimeTypeHandle implementingOpenType = refs.GetRuntimeTypeHandleFromIndex(entry.GetUnsigned());
                                        UInt32 signatureCount = entry.GetUnsigned();
                                        Boolean implementingTypeMatches = SameType(implementingOpenType, openTarget);
                                        Boolean signatureMatches = false;
                                        RuntimeTypeHandle matchedInterface = default;
                                        for (UInt32 signatureIndex = 0U; signatureIndex < signatureCount; signatureIndex++)
                                        {
                                            UInt32 signatureOffset = entry.GetUnsigned();
                                            RuntimeTypeHandle resolvedInterface;
                                            if (callingEntryMatches && implementingTypeMatches && !signatureMatches &&
                                                TryResolveNativeLayoutInterface(modules[moduleIndex], signatureOffset, concreteTarget, slot, variancePass != 0, out resolvedInterface))
                                            {
                                                signatureMatches = true;
                                                matchedInterface = resolvedInterface;
                                            }
                                        }

                                        if (!callingEntryMatches || !implementingTypeMatches || !signatureMatches || defaultImplementation != (defaultPass != 0))
                                            continue;
                                        if (special)
                                        {
                                            TraceGvmStage(targetToken == 0xFFFFFFFFU ? 0x19CEUL : 0x19CFUL);
                                            return false;
                                        }

                                        RuntimeTypeHandle concreteImplementation = defaultImplementation
                                            ? FindConcreteDefaultInterface(targetType, targetDefinition, matchedInterface)
                                            : FindConcreteHierarchyType(targetType, targetDefinition);
                                        MethodTable* targetDefinitionTable = targetDefinition.ToMethodTable();
                                        if (concreteImplementation.IsNull && targetDefinitionTable != null && !targetDefinitionTable->IsGenericTypeDefinition)
                                            concreteImplementation = targetDefinition;
                                        if (concreteImplementation.IsNull) continue;

                                        if (defaultImplementation)
                                        {
                                            implementationType = concreteImplementation;
                                            implementationMethodToken = targetToken;
                                        }
                                        else
                                        {
                                            // Interface GVM metadata identifies the class method that implements
                                            // the slot. If that method is virtual, resolve any more-derived class
                                            // GVM override exactly as .NET 10 GVMLookupForSlotWorker does.
                                            TryResolveMappedClassGvm(concreteTarget, concreteImplementation, targetToken, slot, out implementationType, out implementationMethodToken);
                                        }
                                        TraceGvmStage(0x19CDUL);
                                        TraceGvmValue(0x19C1UL, TypeValue(implementationType));
                                        TraceGvmValue(0x19C2UL, implementationMethodToken);
                                        return true;
                                    }
                                }
                            }
                        }
                    }

                    previous = current;
                    current = current->BaseType;
                }
            }

            TraceGvmStage(0x19CFUL);
            return false;
        }

        // .NET 10 TypeBuilder-compatible dynamic GenericMethodDictionary fallback.  Method
        // dictionaries start one pointer after their header; each NativeLayout DictionaryLayout
        // cell is then materialised in order.  0.0.117 covers the full emitted fixup set for Inu's
        // supported C# surface while preserving the exact .NET 10 NativeLayout encodings.
        // Static, ldtoken, constructor and thread-static cells reuse the same NativeAOT mapping
        // tables emitted by ILC; unknown/future fixup kinds remain explicit fail-closed.
        private static Boolean TryBuildGenericMethodDictionary(TypeManagerHandle module, UInt32 nativeLayoutToken, RuntimeTypeHandle declaringType, MethodHandleInfo* slot, out IntPtr dictionaryPointer)
            => TryBuildGenericMethodDictionary(module, nativeLayoutToken, declaringType, slot, out dictionaryPointer, 0);

        private static Boolean TryBuildGenericMethodDictionary(TypeManagerHandle module, UInt32 nativeLayoutToken, RuntimeTypeHandle declaringType, MethodHandleInfo* slot, out IntPtr dictionaryPointer, Int32 recursionDepth)
        {
            dictionaryPointer = IntPtr.Zero;
            if (recursionDepth > 24)
            {
                TraceGvmStage(0x19BEUL);
                return false;
            }
            TraceGvmStage(0x19B0UL);
            TraceGvmValue(0x19B1UL, nativeLayoutToken);

            Byte* layoutBlob;
            UInt32 layoutSize;
            if (!TryFindBlob(module, ReflectionMapBlob.NativeLayoutInfo, out layoutBlob, out layoutSize) || layoutBlob == null || nativeLayoutToken >= layoutSize)
            {
                TraceGvmStage(0x19BEUL);
                return false;
            }

            NativeReader reader = new NativeReader(layoutBlob, layoutSize);
            NativeParser bag = new NativeParser(reader, nativeLayoutToken);
            NativeParser dictionaryLayout = default;
            BagElementKind kind;
            while ((kind = bag.GetBagElementKind()) != BagElementKind.End)
            {
                if (kind == BagElementKind.DictionaryLayout)
                    dictionaryLayout = bag.GetParserFromRelativeOffset();
                else
                    bag.SkipInteger();
            }

            if (dictionaryLayout.IsNull)
            {
                dictionaryPointer = FunctionPointerOps.AllocateGenericMethodDictionary(0);
                TraceGvmStage(0x19BFUL);
                return dictionaryPointer != IntPtr.Zero;
            }

            UInt32 count = dictionaryLayout.GetSequenceCount();
            TraceGvmValue(0x19B2UL, count);
            if (count > 4096U)
            {
                TraceGvmStage(0x19BEUL);
                return false;
            }

            dictionaryPointer = FunctionPointerOps.AllocateGenericMethodDictionary((Int32)count);
            IntPtr* cells = (IntPtr*)(void*)dictionaryPointer;
            for (UInt32 index = 0U; index < count; index++)
            {
                FixupSignatureKind cellKind = dictionaryLayout.GetFixupSignatureKind();
                TraceGvmValue(0x19B3UL, (UInt64)(UInt32)cellKind);
                RuntimeTypeHandle resolvedType;
                switch (cellKind)
                {
                    case FixupSignatureKind.TypeHandle:
                        if (!TryResolveNativeLayoutType(ref dictionaryLayout, module, declaringType, slot, out resolvedType))
                        {
                            TraceGvmStage(0x19BEUL);
                            return false;
                        }
                        cells[index] = RuntimeTypeHandle.ToIntPtr(resolvedType);
                        break;

                    case FixupSignatureKind.UnwrapNullableType:
                        if (!TryResolveNativeLayoutType(ref dictionaryLayout, module, declaringType, slot, out resolvedType))
                        {
                            TraceGvmStage(0x19BEUL);
                            return false;
                        }
                        MethodTable* resolvedTable = resolvedType.ToMethodTable();
                        if (resolvedTable != null && resolvedTable->IsNullable && resolvedTable->GenericArity == 1U)
                        {
                            MethodTable* underlying = resolvedTable->GetGenericArgument(0U);
                            if (underlying == null) return false;
                            resolvedType = new RuntimeTypeHandle(underlying);
                        }
                        cells[index] = RuntimeTypeHandle.ToIntPtr(resolvedType);
                        break;

                    case FixupSignatureKind.InterfaceCall:
                        {
                            RuntimeTypeHandle interfaceType;
                            if (!TryResolveNativeLayoutType(ref dictionaryLayout, module, declaringType, slot, out interfaceType))
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            UInt32 interfaceSlot = dictionaryLayout.GetUnsigned();
                            cells[index] = FunctionPointerOps.CreateInterfaceDispatchCell(interfaceType, checked((Int32)interfaceSlot));
                            TraceGvmValue(0x19B7UL, TypeValue(interfaceType));
                            TraceGvmValue(0x19B8UL, interfaceSlot);
                        }
                        break;

                    case FixupSignatureKind.StaticData:
                        {
                            RuntimeTypeHandle staticType;
                            if (!TryResolveNativeLayoutType(ref dictionaryLayout, module, declaringType, slot, out staticType))
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            StaticDataKind staticKind = (StaticDataKind)dictionaryLayout.GetUnsigned();
                            if (!TryGetStaticDataPointer(staticType, staticKind, out cells[index]))
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            TraceGvmValue(0x19B7UL, TypeValue(staticType));
                            TraceGvmValue(0x19BCUL, (UInt64)(UInt32)staticKind);
                        }
                        break;

                    case FixupSignatureKind.FieldLdToken:
                        {
                            RuntimeTypeHandle fieldDeclaringType;
                            if (!TryResolveNativeLayoutType(ref dictionaryLayout, module, declaringType, slot, out fieldDeclaringType))
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            UInt32 fieldToken = dictionaryLayout.GetUnsigned();
                            RuntimeFieldHandle fieldHandle = FunctionPointerOps.AllocateRuntimeFieldHandle(fieldDeclaringType, fieldToken);
                            cells[index] = RuntimeFieldHandle.ToIntPtr(fieldHandle);
                            if (cells[index] == IntPtr.Zero)
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            TraceGvmValue(0x19B7UL, TypeValue(fieldDeclaringType));
                            TraceGvmValue(0x19BCUL, fieldToken);
                        }
                        break;

                    case FixupSignatureKind.DefaultConstructor:
                        {
                            RuntimeTypeHandle constructorType;
                            if (!TryResolveNativeLayoutType(ref dictionaryLayout, module, declaringType, slot, out constructorType))
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            cells[index] = TryGetDefaultConstructorPointer(constructorType);
                            if (cells[index] == IntPtr.Zero)
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            TraceGvmValue(0x19B7UL, TypeValue(constructorType));
                            TraceGvmValue(0x19BDUL, PointerValue(cells[index]));
                        }
                        break;

                    case FixupSignatureKind.ThreadStaticIndex:
                        {
                            RuntimeTypeHandle threadStaticType;
                            if (!TryResolveNativeLayoutType(ref dictionaryLayout, module, declaringType, slot, out threadStaticType) ||
                                !TryGetThreadStaticIndexPointer(threadStaticType, out cells[index]))
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            TraceGvmValue(0x19B7UL, TypeValue(threadStaticType));
                            TraceGvmValue(0x19BDUL, PointerValue(cells[index]));
                        }
                        break;

                    case FixupSignatureKind.MethodDictionary:
                        {
                            RuntimeTypeHandle methodDeclaringType;
                            UInt32 methodToken;
                            RuntimeTypeHandle[] methodArguments;
                            IntPtr embeddedFunctionPointer;
                            Boolean isUnboxingStub;
                            if (!TryResolveNativeLayoutMethod(ref dictionaryLayout, module, declaringType, slot,
                                out methodDeclaringType, out methodToken, out methodArguments, out embeddedFunctionPointer, out isUnboxingStub) ||
                                methodArguments.Length == 0 ||
                                !TryGetOrBuildMethodDictionary(methodDeclaringType, methodToken, methodArguments, out cells[index], recursionDepth))
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            TraceGvmValue(0x19B9UL, TypeValue(methodDeclaringType));
                            TraceGvmValue(0x19BAUL, methodToken);
                        }
                        break;

                    case FixupSignatureKind.Method:
                        {
                            RuntimeTypeHandle methodDeclaringType;
                            UInt32 methodToken;
                            RuntimeTypeHandle[] methodArguments;
                            IntPtr embeddedFunctionPointer;
                            Boolean isUnboxingStub;
                            if (!TryResolveNativeLayoutMethod(ref dictionaryLayout, module, declaringType, slot,
                                out methodDeclaringType, out methodToken, out methodArguments, out embeddedFunctionPointer, out isUnboxingStub) ||
                                !TryResolveMethodCell(methodDeclaringType, methodToken, methodArguments, embeddedFunctionPointer, out cells[index], recursionDepth))
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            TraceGvmValue(0x19B9UL, TypeValue(methodDeclaringType));
                            TraceGvmValue(0x19BAUL, methodToken);
                        }
                        break;

                    case FixupSignatureKind.AllocateObject:
                        {
                            if (!TryResolveNativeLayoutType(ref dictionaryLayout, module, declaringType, slot, out resolvedType))
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            MethodTable* allocationTable = resolvedType.ToMethodTable();
                            if (allocationTable != null && allocationTable->IsNullable && allocationTable->GenericArity == 1U)
                            {
                                MethodTable* underlying = allocationTable->GetGenericArgument(0U);
                                if (underlying == null)
                                {
                                    TraceGvmStage(0x19BEUL);
                                    return false;
                                }
                                resolvedType = new RuntimeTypeHandle(underlying);
                            }
                            cells[index] = FunctionPointerOps.GetAllocateObjectHelper(resolvedType);
                            if (cells[index] == IntPtr.Zero)
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            TraceGvmValue(0x19B7UL, TypeValue(resolvedType));
                        }
                        break;

                    case FixupSignatureKind.NonGenericInstanceConstrainedMethod:
                    case FixupSignatureKind.NonGenericStaticConstrainedMethod:
                        {
                            RuntimeTypeHandle constraintType;
                            RuntimeTypeHandle constrainedMethodType;
                            if (!TryResolveNativeLayoutType(ref dictionaryLayout, module, declaringType, slot, out constraintType) ||
                                !TryResolveNativeLayoutType(ref dictionaryLayout, module, declaringType, slot, out constrainedMethodType))
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            UInt32 constrainedSlot = dictionaryLayout.GetUnsigned();
                            Boolean staticDispatch = cellKind == FixupSignatureKind.NonGenericStaticConstrainedMethod;
                            if (!TryResolveConstrainedDispatch(constraintType, constrainedMethodType, constrainedSlot, staticDispatch, out cells[index]))
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            TraceGvmValue(0x19B7UL, TypeValue(constraintType));
                            TraceGvmValue(0x19B8UL, constrainedSlot);
                        }
                        break;

                    case FixupSignatureKind.GenericConstrainedMethod:
                        {
                            RuntimeTypeHandle constraintType;
                            RuntimeTypeHandle constrainedDeclaringType;
                            UInt32 constrainedToken;
                            RuntimeTypeHandle[] constrainedArguments;
                            IntPtr embeddedFunctionPointer;
                            Boolean isUnboxingStub;
                            if (!TryResolveNativeLayoutType(ref dictionaryLayout, module, declaringType, slot, out constraintType) ||
                                !TryResolveNativeLayoutMethod(ref dictionaryLayout, module, declaringType, slot,
                                    out constrainedDeclaringType, out constrainedToken, out constrainedArguments, out embeddedFunctionPointer, out isUnboxingStub) ||
                                constrainedArguments.Length == 0)
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }

                            cells[index] = ResolveGenericConstrainedMethodCell(
                                constraintType,
                                constrainedDeclaringType,
                                constrainedToken,
                                constrainedArguments);
                            if (cells[index] == IntPtr.Zero)
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            TraceGvmValue(0x19B7UL, TypeValue(constraintType));
                            TraceGvmValue(0x19BAUL, constrainedToken);
                        }
                        break;

                    case FixupSignatureKind.MethodLdToken:
                        {
                            RuntimeTypeHandle methodDeclaringType;
                            UInt32 methodToken;
                            RuntimeTypeHandle[] methodArguments;
                            IntPtr embeddedFunctionPointer;
                            Boolean isUnboxingStub;
                            if (!TryResolveNativeLayoutMethod(ref dictionaryLayout, module, declaringType, slot,
                                out methodDeclaringType, out methodToken, out methodArguments, out embeddedFunctionPointer, out isUnboxingStub))
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }

                            RuntimeMethodHandle methodHandle = FunctionPointerOps.AllocateRuntimeMethodHandle(methodDeclaringType, methodToken, methodArguments);
                            cells[index] = RuntimeMethodHandle.ToIntPtr(methodHandle);
                            if (cells[index] == IntPtr.Zero)
                            {
                                TraceGvmStage(0x19BEUL);
                                return false;
                            }
                            TraceGvmValue(0x19B9UL, TypeValue(methodDeclaringType));
                            TraceGvmValue(0x19BAUL, methodToken);
                            TraceGvmValue(0x19BBUL, (UInt64)(UInt32)methodArguments.Length);
                        }
                        break;

                    default:
                        // All .NET 10 fixup kinds emitted by Inu's supported C# surface are
                        // materialised above. Unknown/future kinds remain explicit fail-closed.
                        TraceGvmValue(0x19B4UL, (UInt64)(UInt32)cellKind);
                        TraceGvmStage(0x19BEUL);
                        return false;
                }
                TraceGvmValue(0x19B5UL, PointerValue(cells[index]));
            }

            TraceGvmValue(0x19B6UL, PointerValue(dictionaryPointer));
            TraceGvmStage(0x19BFUL);
            return true;
        }

        internal static IntPtr ResolveGenericVirtualMethodTarget(RuntimeTypeHandle targetType, RuntimeMethodHandle slot)
        {
            TraceGvmStage(0x1910UL);
            MethodHandleInfo* slotInfo = slot.ToMethodHandleInfo();
            TraceGvmValue(0x1911UL, TypeValue(targetType));
            TraceGvmValue(0x1912UL, PointerValue(RuntimeMethodHandle.ToIntPtr(slot)));
            if (slotInfo == null || targetType.IsNull)
            {
                TraceGvmStage(0x191FUL);
                throw new BadImageFormatException();
            }
            TraceGvmValue(0x1913UL, TypeValue(slotInfo->DeclaringType));
            TraceGvmValue(0x1914UL, (UInt64)(UInt32)slotInfo->Handle.Value);
            TraceGvmValue(0x1915UL, (UInt64)(UInt32)slotInfo->NumGenericArgs);
            for (Int32 argIndex = 0; argIndex < slotInfo->NumGenericArgs && argIndex < 8; argIndex++)
                TraceGvmValue(0x1960UL + (UInt64)(UInt32)argIndex, TypeValue(GetMethodArgument(slotInfo, argIndex)));

            RuntimeTypeHandle implementationType;
            UInt32 implementationToken;
            MethodTable* slotDeclaringTable = slotInfo->DeclaringType.ToMethodTable();
            Boolean implementationFound = slotDeclaringTable != null && slotDeclaringTable->IsInterface
                ? TryResolveInterfaceGvmImplementation(targetType, slotInfo, out implementationType, out implementationToken)
                : TryResolveClassGvmImplementation(targetType, slotInfo, out implementationType, out implementationToken);
            if (!implementationFound)
            {
                TraceGvmStage(0x191EUL);
                throw new InvalidOperationException("NativeAOT generic virtual method implementation was not found.");
            }
            TraceGvmValue(0x191AUL, TypeValue(implementationType));
            TraceGvmValue(0x191BUL, implementationToken);

            IntPtr exactPointer;
            if (TryGetExactMethodPointer(implementationType, implementationToken, slotInfo, out exactPointer))
            {
                TraceGvmStage(0x191CUL);
                TraceGvmValue(0x191DUL, PointerValue(exactPointer));
                return exactPointer;
            }

            IntPtr templatePointer;
            TypeManagerHandle nativeLayoutModule;
            UInt32 nativeLayoutToken;
            if (!TryGetTemplateMethodPointer(implementationType, implementationToken, slotInfo->NumGenericArgs, out templatePointer, out nativeLayoutModule, out nativeLayoutToken))
            {
                TraceGvmStage(0x1918UL);
                throw new InvalidOperationException("NativeAOT generic virtual method template was not found.");
            }

            IntPtr dictionaryPointer;
            if (!TryGetGenericDictionary(implementationType, implementationToken, slotInfo, out dictionaryPointer) &&
                !TryBuildGenericMethodDictionary(nativeLayoutModule, nativeLayoutToken, implementationType, slotInfo, out dictionaryPointer))
            {
                TraceGvmStage(0x1919UL);
                throw new InvalidOperationException("NativeAOT generic method dictionary was not found or contains an unsupported dynamic fixup cell.");
            }

            IntPtr result = FunctionPointerOps.GetGenericMethodFunctionPointer(templatePointer, dictionaryPointer);
            TraceGvmStage(0x1917UL);
            TraceGvmValue(0x190FUL, PointerValue(result));
            return result;
        }
    }
}

namespace System.Runtime
{
    using Internal.Runtime.CompilerServices;
    using Internal.Runtime.TypeLoader;

    // .NET 10 ILC binds ReadyToRunHelper.GVMLookupForSlot to this exact type/method.
    internal static unsafe class TypeLoaderExports
    {
        private struct CacheEntry
        {
            public IntPtr Context;
            public IntPtr Signature;
            public IntPtr Result;
        }

        // The upstream runtime uses GenericCache<Key,Value>. Inu's collector/threading
        // substrate does not yet expose the CoreCLR GenericCache atomics, so this retains
        // the same cache key/value semantics with a bounded direct-mapped cache. It is a
        // performance adaptation only; all resolution/data formats remain the .NET 10 ones.
        private static readonly CacheEntry[] s_gvmCache = new CacheEntry[128];

#pragma warning disable CS0626
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [System.Runtime.RuntimeImport("*", "InuEhTrace")]
        private static extern void TraceGvmExportStage(UInt64 code);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        [System.Runtime.RuntimeImport("*", "InuEhTraceValue")]
        private static extern void TraceGvmExportValue(UInt64 tag, UInt64 value);
#pragma warning restore CS0626

        public static IntPtr GVMLookupForSlot(Object obj, RuntimeMethodHandle slot)
        {
            TraceGvmExportStage(0x1900UL);
            if (obj == null) throw new NullReferenceException();
            IntPtr context = (IntPtr)System.Runtime.CompilerServices.RuntimeHelpers.GetMethodTable(obj);
            IntPtr signature = RuntimeMethodHandle.ToIntPtr(slot);
            TraceGvmExportValue(0x1901UL, (UInt64)(void*)context);
            TraceGvmExportValue(0x1902UL, (UInt64)(void*)signature);
            UInt64 hash = ((UInt64)(void*)context >> 3) ^ ((UInt64)(void*)signature >> 3);
            Int32 index = (Int32)(hash & 127UL);
            TraceGvmExportValue(0x1903UL, (UInt64)(UInt32)index);
            CacheEntry cached = s_gvmCache[index];
            if (cached.Context == context && cached.Signature == signature && cached.Result != IntPtr.Zero)
            {
                TraceGvmExportStage(0x1904UL);
                TraceGvmExportValue(0x1905UL, (UInt64)(void*)cached.Result);
                return cached.Result;
            }

            TraceGvmExportStage(0x1906UL);
            IntPtr result = TypeLoaderEnvironment.ResolveGenericVirtualMethodTarget(new RuntimeTypeHandle((Internal.Runtime.MethodTable*)(void*)context), slot);
            TraceGvmExportValue(0x1907UL, (UInt64)(void*)result);
            s_gvmCache[index] = new CacheEntry { Context = context, Signature = signature, Result = result };
            TraceGvmExportStage(0x1908UL);
            return result;
        }
    }
}
