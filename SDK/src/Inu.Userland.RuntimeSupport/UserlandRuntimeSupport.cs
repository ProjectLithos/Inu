using System;

namespace Inu.Kernel.Heap
{
    public readonly struct KernelHeapAllocation
    {
        internal KernelHeapAllocation(UInt64 token, UInt64 address, UInt64 byteCount){Token=token;Address=address;ByteCount=byteCount;}
        public UInt64 Token { get; }
        public UInt64 Address { get; }
        public UInt64 ByteCount { get; }
    }

    public static unsafe class KernelHeap
    {
        private const Int32 ArenaBytes = 16 * 1024 * 1024;
        private unsafe struct Arena { internal fixed Byte Bytes[ArenaBytes]; }
#pragma warning disable CS0169 // Fixed-buffer address access below is not counted as use of the containing field.
        private static Arena _arena;
#pragma warning restore CS0169
        private static UInt64 _used;
        private static UInt64 _token;

        public static Boolean IsInitialized()=>true;
        public static Boolean TryAllocate(UInt64 byteCount,UInt64 alignment,Boolean zero,out KernelHeapAllocation allocation)
        {
            allocation=default;if(byteCount==0UL||alignment==0UL||(alignment&(alignment-1UL))!=0UL)return false;
            UInt64 used=_used;UInt64 aligned=(used+alignment-1UL)&~(alignment-1UL);if(aligned>=(UInt64)ArenaBytes||byteCount>(UInt64)ArenaBytes-aligned)return false;
            fixed(Byte* arena=_arena.Bytes)
            {
                Byte* address=arena+aligned;if(zero)for(UInt64 i=0;i<byteCount;i++)address[i]=0;
                _used=aligned+byteCount;UInt64 token=++_token;allocation=new KernelHeapAllocation(token,(UInt64)(nuint)address,byteCount);return true;
            }
        }
    }
}

namespace Inu.Arch.X64
{
    public static unsafe class X64ArchitectureBoundary
    {
        public static Boolean SpinWaitHint()=>true;
        public static Boolean AtomicCompareExchange64(UInt64* location,UInt64 expected,UInt64 replacement,out UInt64 previous)
        {previous=location==null?0UL:*location;if(location==null)return false;if(previous==expected)*location=replacement;return true;}
        public static Boolean AtomicLoad64(UInt64* location,out UInt64 value){value=location==null?0UL:*location;return location!=null;}
        public static Boolean AtomicStore64(UInt64* location,UInt64 value){if(location==null)return false;*location=value;return true;}
    }
}
