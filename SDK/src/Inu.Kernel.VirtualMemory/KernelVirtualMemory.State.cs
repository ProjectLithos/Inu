using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Memory;

namespace Inu.Kernel.VirtualMemory;

public static unsafe partial class KernelVirtualMemory
{
    private static Boolean ResetState()
    {
        _protectedTableCount = 0;
        _rootPhysicalAddress = 0UL;
        _createdPageTables = 0UL;
        _mapped4KiB = 0UL;
        _mapped2MiB = 0UL;
        _mapped1GiB = 0UL;
        _executeDisableEnabled = false;
        _page1GiBSupported = false;
        ResetDirectMapState();
        fixed (UInt64* protectedTables = _state.ProtectedTables)
        {
            for (Int32 index = 0; index < MaximumProtectedTables; index++) protectedTables[index] = 0UL;
        }
        return true;
    }

    private static Boolean SetFailure(KernelVirtualMemoryStatus status)
    {
        _lastStatus = status;
        return false;
    }
}
