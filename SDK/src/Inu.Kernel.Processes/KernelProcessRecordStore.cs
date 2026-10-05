using System;
using Inu.Kernel.Internal.X64;
using System.Runtime;
using Inu.Kernel.Memory;
using Inu.ApplicationFormat;

namespace Inu.Kernel.Processes;

/// <summary>Opaque handle used by process-management components without exposing the record layout.</summary>
public readonly struct KernelProcessRecordHandle
{
    internal KernelProcessRecordHandle(UInt32 token){Token=token;}
    internal UInt32 Token { get; }
    internal Boolean IsValid=>Token!=0U;
}

/// <summary>Owns the private process table, PID allocation, lifecycle state and record-backed resource metadata.</summary>
internal static unsafe class KernelProcessRecordStore
{
    private struct ProcessRecord
    {
        internal UInt64 Id,Root,Entry,StackBase,StackTop,StackGuardBase,ApplicationIdHash,ApplicationNameHash,ApplicationVersionHash,KillRequested;
        internal Int64 ExitCode;
        internal UInt32 State,Format,Ownership,SyscallAbi,TableCount,AllocationCount;
        internal fixed UInt64 TableTokens[(Int32)KernelProcesses.MaximumTablesPerProcess],TableStarts[(Int32)KernelProcesses.MaximumTablesPerProcess],TablePages[(Int32)KernelProcesses.MaximumTablesPerProcess];
        internal fixed UInt64 AllocationTokens[(Int32)KernelProcesses.MaximumImageAllocations],AllocationStarts[(Int32)KernelProcesses.MaximumImageAllocations],AllocationPages[(Int32)KernelProcesses.MaximumImageAllocations];
    }

    private struct ProcessTable { internal fixed Byte Bytes[(Int32)KernelProcesses.MaximumProcesses*2304]; }
#pragma warning disable CS0169
    private static ProcessTable _table;
#pragma warning restore CS0169
    private static UInt64 _nextId=1UL,_lock;
    private static UInt32 _active;

    internal static UInt32 ActiveCount=>_active;

    private static ProcessRecord* Record(Int32 slot){fixed(Byte* b=_table.Bytes)return (ProcessRecord*)(b+(UInt64)slot*(UInt64)sizeof(ProcessRecord));}
    private static ProcessRecord* Record(KernelProcessRecordHandle handle)=>handle.IsValid&&handle.Token<=KernelProcesses.MaximumProcesses?Record((Int32)(handle.Token-1U)):null;
    private static void Clear(ProcessRecord* r){Byte* p=(Byte*)r;for(Int32 i=0;i<sizeof(ProcessRecord);i++)p[i]=0;}
    private static KernelProcessInfo Snapshot(ProcessRecord* r)=>new(r->Id,(KernelProcessState)r->State,(KernelProcessOwnership)r->Ownership,(ProcessExecutableFormat)r->Format,(InuApplicationAbi)r->SyscallAbi,r->Root,r->Entry,r->StackBase,r->StackTop,r->StackGuardBase,r->ApplicationIdHash,r->ApplicationNameHash,r->ApplicationVersionHash,r->ExitCode);

    private static Boolean Acquire()
    {
        for(;;){UInt64 previous=1UL;fixed(UInt64* p=&_lock){if(!Native.AtomicCompareExchange64(p,0UL,1UL,&previous))return false;}if(previous==0UL)return true;Native.Pause();}
    }
    private static void Release(){UInt64 previous=0UL;fixed(UInt64* p=&_lock)Native.AtomicExchange64(p,0UL,&previous);}

    private static Boolean TryFind(UInt64 id,Boolean includeTerminated,out KernelProcessRecordHandle handle)
    {
        handle=default;
        for(Int32 i=0;i<(Int32)KernelProcesses.MaximumProcesses;i++)
        {
            ProcessRecord* r=Record(i);if(r->Id!=id||r->State==(UInt32)KernelProcessState.Unused)continue;
            if(!includeTerminated&&r->State==(UInt32)KernelProcessState.Terminated)continue;
            handle=new KernelProcessRecordHandle((UInt32)i+1U);return true;
        }
        return false;
    }
    private static Int32 FindFreeSlot(){for(Int32 i=0;i<(Int32)KernelProcesses.MaximumProcesses;i++){UInt32 s=Record(i)->State;if(s==(UInt32)KernelProcessState.Unused||s==(UInt32)KernelProcessState.Terminated)return i;}return -1;}

    internal static Boolean TryGet(UInt64 id,out KernelProcessRecordHandle handle){if(!Acquire()){handle=default;return false;}Boolean ok=TryFind(id,false,out handle);Release();return ok;}
    internal static Boolean TryGetAny(UInt64 id,out KernelProcessRecordHandle handle){if(!Acquire()){handle=default;return false;}Boolean ok=TryFind(id,true,out handle);Release();return ok;}
    internal static Boolean TryGetInfo(UInt64 id,out KernelProcessInfo process)
    {
        process=default;if(!Acquire())return false;if(!TryFind(id,true,out KernelProcessRecordHandle handle)){Release();return false;}ProcessRecord* r=Record(handle);process=Snapshot(r);Release();return true;
    }
    internal static Boolean TryGetInfo(KernelProcessRecordHandle handle,out KernelProcessInfo process)
    {
        process=default;if(!Acquire())return false;ProcessRecord* r=Record(handle);if(r==null||r->State==(UInt32)KernelProcessState.Unused){Release();return false;}process=Snapshot(r);Release();return true;
    }

    internal static Boolean TryReserve(KernelProcessOwnership ownership,InuApplicationAbi abi,out KernelProcessRecordHandle handle)
    {
        handle=default;if(!Acquire())return false;Int32 slot=FindFreeSlot();if(slot<0){Release();return false;}ProcessRecord* r=Record(slot);Clear(r);r->Id=_nextId++;r->State=(UInt32)KernelProcessState.Loading;r->Ownership=(UInt32)ownership;r->SyscallAbi=(UInt32)abi;handle=new KernelProcessRecordHandle((UInt32)slot+1U);Release();return true;
    }
    internal static void Abandon(KernelProcessRecordHandle handle){if(!handle.IsValid||!Acquire())return;ProcessRecord* r=Record(handle);if(r!=null&&r->State==(UInt32)KernelProcessState.Loading)Clear(r);Release();}
    internal static Boolean Activate(KernelProcessRecordHandle handle,out KernelProcessInfo process)
    {
        process=default;if(!Acquire())return false;ProcessRecord* r=Record(handle);if(r==null||r->State!=(UInt32)KernelProcessState.Loading){Release();return false;}r->State=(UInt32)KernelProcessState.Ready;_active++;process=Snapshot(r);Release();return true;
    }
    internal static void Deactivate(KernelProcessRecordHandle handle,KernelProcessState finalState,Int64 exitCode)
    {
        if(!Acquire())return;ProcessRecord* r=Record(handle);if(r!=null){r->ExitCode=exitCode;r->State=(UInt32)finalState;if(_active!=0U)_active--;}Release();
    }
    internal static Boolean TryMarkRunning(UInt64 processId,out KernelProcessRecordHandle handle)
    {
        handle=default;if(!Acquire())return false;if(!TryFind(processId,false,out handle)){Release();return false;}ProcessRecord* r=Record(handle);if(r==null||r->State!=(UInt32)KernelProcessState.Ready){handle=default;Release();return false;}r->State=(UInt32)KernelProcessState.Running;Release();return true;
    }
    internal static Boolean ReturnToReady(KernelProcessRecordHandle handle)
    {
        if(!Acquire())return false;ProcessRecord* r=Record(handle);if(r==null){Release();return false;}if(r->State==(UInt32)KernelProcessState.Running)r->State=(UInt32)KernelProcessState.Ready;Release();return true;
    }
    internal static Boolean TryBeginTermination(UInt64 processId,out KernelProcessRecordHandle handle,out KernelProcessState state,out Int64 exitCode)
    {
        handle=default;state=KernelProcessState.Unused;exitCode=0L;if(!Acquire())return false;if(!TryFind(processId,true,out handle)){Release();return false;}ProcessRecord* r=Record(handle);state=(KernelProcessState)r->State;exitCode=r->ExitCode;
        if(state==KernelProcessState.Terminated){Release();return true;}
        if(state==KernelProcessState.Running||state==KernelProcessState.Loading||state==KernelProcessState.Terminating){handle=default;Release();return false;}
        if(state==KernelProcessState.Faulted){r->State=(UInt32)KernelProcessState.Terminated;Release();return true;}
        r->State=(UInt32)KernelProcessState.Terminating;Release();return true;
    }

    internal static UInt64 GetId(KernelProcessRecordHandle handle){ProcessRecord* r=Record(handle);return r==null?0UL:r->Id;}
    internal static KernelProcessOwnership GetOwnership(KernelProcessRecordHandle handle){ProcessRecord* r=Record(handle);return r==null?(KernelProcessOwnership)0:(KernelProcessOwnership)r->Ownership;}
    internal static KernelProcessState GetState(KernelProcessRecordHandle handle){ProcessRecord* r=Record(handle);return r==null?KernelProcessState.Unused:(KernelProcessState)r->State;}
    internal static UInt64 GetRoot(KernelProcessRecordHandle handle){ProcessRecord* r=Record(handle);return r==null?0UL:r->Root;}
    internal static UInt64 GetEntry(KernelProcessRecordHandle handle){ProcessRecord* r=Record(handle);return r==null?0UL:r->Entry;}
    internal static UInt64 GetStackTop(KernelProcessRecordHandle handle){ProcessRecord* r=Record(handle);return r==null?0UL:r->StackTop;}
    internal static UInt64 GetStackGuardBase(KernelProcessRecordHandle handle){ProcessRecord* r=Record(handle);return r==null?0UL:r->StackGuardBase;}
    internal static InuApplicationAbi GetSyscallAbi(KernelProcessRecordHandle handle){ProcessRecord* r=Record(handle);return r==null?InuApplicationAbi.Inu:(InuApplicationAbi)r->SyscallAbi;}
    internal static Int64 GetExitCode(KernelProcessRecordHandle handle){ProcessRecord* r=Record(handle);return r==null?0L:r->ExitCode;}
    internal static void SetExitCode(KernelProcessRecordHandle handle,Int64 value){ProcessRecord* r=Record(handle);if(r!=null)r->ExitCode=value;}
    internal static void Complete(KernelProcessRecordHandle handle,Int64 exitCode){if(!Acquire())return;ProcessRecord* r=Record(handle);if(r!=null){r->ExitCode=exitCode;r->State=(UInt32)KernelProcessState.Completed;}Release();}
    internal static void SetStateAndExit(KernelProcessRecordHandle handle,KernelProcessState state,Int64 exitCode){if(!Acquire())return;ProcessRecord* r=Record(handle);if(r!=null){r->ExitCode=exitCode;r->State=(UInt32)state;}Release();}
    internal static void SetApplicationHashes(KernelProcessRecordHandle handle,UInt64 idHash,UInt64 nameHash,UInt64 versionHash){ProcessRecord* r=Record(handle);if(r!=null){r->ApplicationIdHash=idHash;r->ApplicationNameHash=nameHash;r->ApplicationVersionHash=versionHash;}}
    internal static void SetExecutable(KernelProcessRecordHandle handle,UInt64 root,UInt64 entry,ProcessExecutableFormat format){ProcessRecord* r=Record(handle);if(r!=null){r->Root=root;r->Entry=entry;r->Format=(UInt32)format;}}
    internal static void SetStack(KernelProcessRecordHandle handle,UInt64 stackBase,UInt64 stackTop,UInt64 stackGuardBase){ProcessRecord* r=Record(handle);if(r!=null){r->StackBase=stackBase;r->StackTop=stackTop;r->StackGuardBase=stackGuardBase;}}
    internal static void SetTables(KernelProcessRecordHandle handle,KernelPhysicalAllocation* tables,UInt32 count)
    {
        ProcessRecord* r=Record(handle);if(r==null)return;r->TableCount=count;for(UInt32 i=0;i<count;i++){r->TableTokens[(Int32)i]=tables[i].Token;r->TableStarts[(Int32)i]=tables[i].StartAddress;r->TablePages[(Int32)i]=tables[i].PageCount;}
    }
    internal static UInt32 GetTableCount(KernelProcessRecordHandle handle){ProcessRecord* r=Record(handle);return r==null?0U:r->TableCount;}
    internal static Boolean TryGetTable(KernelProcessRecordHandle handle,UInt32 index,out KernelPhysicalAllocation allocation)
    {
        allocation=default;ProcessRecord* r=Record(handle);if(r==null||index>=r->TableCount)return false;allocation=new KernelPhysicalAllocation(r->TableTokens[(Int32)index],r->TableStarts[(Int32)index],r->TablePages[(Int32)index]);return true;
    }
    internal static UInt32 GetAllocationCount(KernelProcessRecordHandle handle){ProcessRecord* r=Record(handle);return r==null?0U:r->AllocationCount;}
    internal static Boolean TryAppendAllocation(KernelProcessRecordHandle handle,KernelPhysicalAllocation allocation)
    {
        ProcessRecord* r=Record(handle);if(r==null||r->AllocationCount>=KernelProcesses.MaximumImageAllocations)return false;UInt32 i=r->AllocationCount++;r->AllocationTokens[(Int32)i]=allocation.Token;r->AllocationStarts[(Int32)i]=allocation.StartAddress;r->AllocationPages[(Int32)i]=allocation.PageCount;return true;
    }
    internal static Boolean TryGetAllocation(KernelProcessRecordHandle handle,UInt32 index,out KernelPhysicalAllocation allocation)
    {
        allocation=default;ProcessRecord* r=Record(handle);if(r==null||index>=r->AllocationCount)return false;allocation=new KernelPhysicalAllocation(r->AllocationTokens[(Int32)index],r->AllocationStarts[(Int32)index],r->AllocationPages[(Int32)index]);return true;
    }
    internal static void ClearOwnedResourceCounts(KernelProcessRecordHandle handle){ProcessRecord* r=Record(handle);if(r!=null){r->AllocationCount=0U;r->TableCount=0U;}}
    internal static Boolean RequestKill(KernelProcessRecordHandle handle){ProcessRecord* r=Record(handle);if(r==null)return false;UInt64* requested=&r->KillRequested;return Native.AtomicStore64(requested,1UL);}
    internal static Boolean IsKillRequested(KernelProcessRecordHandle handle){ProcessRecord* r=Record(handle);if(r==null)return false;UInt64 requested=0UL;UInt64* p=&r->KillRequested;Native.AtomicLoad64(p,&requested);return requested!=0UL;}
    internal static UInt32 CountObserved()
    {
        UInt32 observed=0U;if(!Acquire())return 0U;for(Int32 i=0;i<(Int32)KernelProcesses.MaximumProcesses;i++){UInt32 state=Record(i)->State;if(state!=(UInt32)KernelProcessState.Unused&&state!=(UInt32)KernelProcessState.Terminated)observed++;}Release();return observed;
    }
}
