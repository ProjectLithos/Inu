using System;

namespace Inu.Kernel.Processes;

public static unsafe partial class KernelProcesses
{
    public static Boolean TryGetProcess(UInt64 processId,out KernelProcessInfo process)
    {
        process=default;return _initialized&&KernelProcessRecordStore.TryGetInfo(processId,out process);
    }

    public static UInt32 GetActiveProcessCount()=>KernelProcessRecordStore.ActiveCount;
}
