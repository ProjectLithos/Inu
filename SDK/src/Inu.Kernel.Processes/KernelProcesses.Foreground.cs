using System;
using Inu.Kernel.Internal.X64;
using System.Runtime;

namespace Inu.Kernel.Processes;

public static unsafe partial class KernelProcesses
{
    private static UInt64 _foregroundCancellationRequested,_foregroundExecutionActive,_foregroundProcessId;
    internal static UInt64 GetForegroundCommandProcessIdImplementation(){if(!_initialized)return 0UL;UInt64 processId=0UL;fixed(UInt64* p=&_foregroundProcessId)Native.AtomicLoad64(p,&processId);return processId;}

    internal static Boolean BeginForegroundCommandExecutionImplementation(){Boolean ok=true;fixed(UInt64* p=&_foregroundCancellationRequested)ok&=Native.AtomicStore64(p,0UL);fixed(UInt64* p=&_foregroundProcessId)ok&=Native.AtomicStore64(p,0UL);fixed(UInt64* p=&_foregroundExecutionActive)ok&=Native.AtomicStore64(p,1UL);return ok;}

    internal static Boolean RequestForegroundCommandCancellationImplementation()
    {
        if(!_initialized)return false;UInt64 pid=GetForegroundCommandProcessIdImplementation();if(pid!=0UL)return KernelProcessSignalServices.TrySetControl(pid,KernelProcessControl.Kill);Boolean stored;fixed(UInt64* p=&_foregroundCancellationRequested)stored=Native.AtomicStore64(p,1UL);return stored;
    }

    internal static Boolean ClearForegroundCommandCancellationImplementation(){Boolean stored=true;fixed(UInt64* p=&_foregroundCancellationRequested)stored&=Native.AtomicStore64(p,0UL);fixed(UInt64* p=&_foregroundProcessId)stored&=Native.AtomicStore64(p,0UL);fixed(UInt64* p=&_foregroundExecutionActive)stored&=Native.AtomicStore64(p,0UL);return stored;}

    internal static Boolean IsForegroundCommandCancellationRequestedImplementation()
    {
        UInt64 value=0UL;fixed(UInt64* p=&_foregroundCancellationRequested)Native.AtomicLoad64(p,&value);if(value!=0UL)return true;UInt64 pid=GetForegroundCommandProcessIdImplementation();return pid!=0UL&&IsProcessKillRequested(pid);
    }

    internal static Boolean TryClaimForegroundProcessImplementation(KernelProcessRecordHandle record)
    {
        if(!record.IsValid||KernelProcessRecordStore.GetOwnership(record)!=KernelProcessOwnership.Foreground)return true;UInt64 active=0UL;fixed(UInt64* p=&_foregroundExecutionActive)if(!Native.AtomicLoad64(p,&active))return false;if(active==0UL)return true;UInt64 processId=KernelProcessRecordStore.GetId(record),previous=0UL;fixed(UInt64* p=&_foregroundProcessId)return Native.AtomicCompareExchange64(p,0UL,processId,&previous)&&previous==0UL;
    }

    internal static void ReleaseForegroundProcessClaimImplementation(UInt64 processId){if(processId==0UL)return;UInt64 previous=0UL;fixed(UInt64* p=&_foregroundProcessId)Native.AtomicCompareExchange64(p,processId,0UL,&previous);}

    internal static Boolean IsForegroundCancellationRequestedForImplementation(UInt64 processId)
    {
        UInt64 foreground=0UL,pending=0UL;fixed(UInt64* p=&_foregroundProcessId)Native.AtomicLoad64(p,&foreground);fixed(UInt64* p=&_foregroundCancellationRequested)Native.AtomicLoad64(p,&pending);return pending!=0UL&&foreground==processId;
    }
}
