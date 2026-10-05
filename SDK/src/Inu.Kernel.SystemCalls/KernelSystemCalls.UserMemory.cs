using System;
using System.Runtime;
using Inu.Kernel.Heap;
using Inu.Kernel.Contracts;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Protection;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Smp;
using Inu.Kernel.Security;
using Inu.Kernel.Time;
using Inu.Kernel.VirtualMemory;

namespace Inu.Kernel.SystemCalls;

public static unsafe partial class KernelSystemCalls
{
    public static Boolean TryCopyFromUser(UInt64 userSource, UInt64 kernelDestination, UInt64 byteCount)
    {
        if (!_initialized || kernelDestination == 0UL || byteCount==0UL || !ValidateUserRange(userSource, byteCount, false)) return false;
        if (!Native.BeginUserMemoryAccess()) return false;
        Byte* src=(Byte*)userSource; Byte* dst=(Byte*)kernelDestination;
        for (UInt64 i=0UL;i<byteCount;i++) dst[i]=src[i];
        return Native.EndUserMemoryAccess();
    }

    /// <summary>Copies bytes into a validated writable user range while honoring SMAP when enabled.</summary>
    public static Boolean TryCopyToUser(UInt64 userDestination, UInt64 kernelSource, UInt64 byteCount)
    {
        if (!_initialized || kernelSource == 0UL || byteCount==0UL || !ValidateUserRange(userDestination, byteCount, true)) return false;
        if (!Native.BeginUserMemoryAccess()) return false;
        Byte* src=(Byte*)kernelSource; Byte* dst=(Byte*)userDestination;
        for (UInt64 i=0UL;i<byteCount;i++) dst[i]=src[i];
        return Native.EndUserMemoryAccess();
    }

    /// <summary>Dispatches one protected syscall. Native Inu uses RAX only for Get/Set/Event and RDI for the envelope pointer.</summary>
}
