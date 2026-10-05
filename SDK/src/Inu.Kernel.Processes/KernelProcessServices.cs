using System;
using Inu.ApplicationFormat;
using Inu.Kernel.Memory;
using Inu.Kernel.Storage;
using Inu.Kernel.SystemCalls;

namespace Inu.Kernel.Processes;

/// <summary>Neutral registries for optional process-management responsibilities.</summary>
public static unsafe class KernelProcessLifecycleServices
{
    private static delegate*<UInt64,UInt64,KernelProcessOwnership,KernelProcessInfo*,InuApplicationLoadError*,Boolean> _createImage;
    private static delegate*<KernelMountNamespaceHandle,String,KernelProcessOwnership,KernelProcessInfo*,Boolean> _createFile;
    private static delegate*<UInt64,Int64,Boolean> _terminate;
    public static Boolean IsRegistered=>_createImage!=null&&_createFile!=null&&_terminate!=null;
    public static Boolean Register(delegate*<UInt64,UInt64,KernelProcessOwnership,KernelProcessInfo*,InuApplicationLoadError*,Boolean> createImage,delegate*<KernelMountNamespaceHandle,String,KernelProcessOwnership,KernelProcessInfo*,Boolean> createFile,delegate*<UInt64,Int64,Boolean> terminate)
    {if(createImage==null||createFile==null||terminate==null||IsRegistered)return false;_createImage=createImage;_createFile=createFile;_terminate=terminate;return true;}
    internal static Boolean TryCreateImage(UInt64 address,UInt64 length,KernelProcessOwnership ownership,out KernelProcessInfo process,out InuApplicationLoadError error){process=default;error=InuApplicationLoadError.InvalidArgument;if(_createImage==null)return false;KernelProcessInfo value=default;InuApplicationLoadError loadError=InuApplicationLoadError.InvalidArgument;Boolean ok=_createImage(address,length,ownership,&value,&loadError);process=value;error=loadError;return ok;}
    internal static Boolean TryCreateFile(KernelMountNamespaceHandle ns,String path,KernelProcessOwnership ownership,out KernelProcessInfo process){process=default;if(_createFile==null)return false;KernelProcessInfo value=default;Boolean ok=_createFile(ns,path,ownership,&value);process=value;return ok;}
    internal static Boolean TryTerminate(UInt64 id,Int64 exitCode)=>_terminate!=null&&_terminate(id,exitCode);
}

public static unsafe class KernelProcessAddressSpaceServices
{
    private static delegate*<KernelProcessRecordHandle,Boolean> _releaseOwned;
    private static delegate*<KernelProcessRecordHandle,KernelPhysicalAllocation*,UInt32,Boolean> _releaseTemporary;
    private static delegate*<KernelProcessRecordHandle,KernelPhysicalAllocation,Boolean> _storeAllocation;
    public static Boolean IsRegistered=>_releaseOwned!=null&&_releaseTemporary!=null&&_storeAllocation!=null;
    public static Boolean Register(delegate*<KernelProcessRecordHandle,Boolean> releaseOwned,delegate*<KernelProcessRecordHandle,KernelPhysicalAllocation*,UInt32,Boolean> releaseTemporary,delegate*<KernelProcessRecordHandle,KernelPhysicalAllocation,Boolean> storeAllocation)
    {if(releaseOwned==null||releaseTemporary==null||storeAllocation==null||IsRegistered)return false;_releaseOwned=releaseOwned;_releaseTemporary=releaseTemporary;_storeAllocation=storeAllocation;return true;}
    internal static Boolean ReleaseOwned(KernelProcessRecordHandle record)=>_releaseOwned!=null&&_releaseOwned(record);
    internal static Boolean ReleaseTemporary(KernelProcessRecordHandle record,KernelPhysicalAllocation* tables,UInt32 count)=>_releaseTemporary!=null&&_releaseTemporary(record,tables,count);
    internal static Boolean StoreAllocation(KernelProcessRecordHandle record,KernelPhysicalAllocation allocation)=>_storeAllocation!=null&&_storeAllocation(record,allocation);
}

public static unsafe class KernelProcessForegroundServices
{
    private static delegate*<UInt64> _get;
    private static delegate*<Boolean> _begin;
    private static delegate*<Boolean> _requestCancel;
    private static delegate*<Boolean> _clearCancel;
    private static delegate*<Boolean> _isCancel;
    private static delegate*<KernelProcessRecordHandle,Boolean> _claim;
    private static delegate*<UInt64,void> _release;
    private static delegate*<UInt64,Boolean> _isCancellationFor;
    public static Boolean IsRegistered=>_get!=null&&_begin!=null&&_requestCancel!=null&&_clearCancel!=null&&_isCancel!=null&&_claim!=null&&_release!=null&&_isCancellationFor!=null;
    public static Boolean Register(delegate*<UInt64> get,delegate*<Boolean> begin,delegate*<Boolean> requestCancel,delegate*<Boolean> clearCancel,delegate*<Boolean> isCancel,delegate*<KernelProcessRecordHandle,Boolean> claim,delegate*<UInt64,void> release,delegate*<UInt64,Boolean> isCancellationFor)
    {if(get==null||begin==null||requestCancel==null||clearCancel==null||isCancel==null||claim==null||release==null||isCancellationFor==null||IsRegistered)return false;_get=get;_begin=begin;_requestCancel=requestCancel;_clearCancel=clearCancel;_isCancel=isCancel;_claim=claim;_release=release;_isCancellationFor=isCancellationFor;return true;}
    internal static UInt64 GetForegroundProcessId()=>_get==null?0UL:_get();
    internal static Boolean Begin()=>_begin!=null&&_begin();
    internal static Boolean RequestCancellation()=>_requestCancel!=null&&_requestCancel();
    internal static Boolean ClearCancellation()=>_clearCancel!=null&&_clearCancel();
    internal static Boolean IsCancellationRequested()=>_isCancel!=null&&_isCancel();
    internal static Boolean TryClaim(KernelProcessRecordHandle record)=>_claim!=null?_claim(record):KernelProcessRecordStore.GetOwnership(record)!=KernelProcessOwnership.Foreground;
    internal static void Release(UInt64 id){if(_release!=null)_release(id);}
    internal static Boolean IsCancellationRequestedFor(UInt64 id)=>_isCancellationFor!=null&&_isCancellationFor(id);
}

public static unsafe class KernelProcessSignalServices
{
    private static delegate*<UInt64,KernelProcessControl,Boolean> _setControl;
    private static delegate*<KernelSystemCallFrame*,Boolean> _syscallCancellation;
    private static delegate*<Boolean> _interruptCancellation;
    public static Boolean IsRegistered=>_setControl!=null&&_syscallCancellation!=null&&_interruptCancellation!=null;
    public static Boolean Register(delegate*<UInt64,KernelProcessControl,Boolean> setControl,delegate*<KernelSystemCallFrame*,Boolean> syscallCancellation,delegate*<Boolean> interruptCancellation)
    {if(setControl==null||syscallCancellation==null||interruptCancellation==null||IsRegistered)return false;_setControl=setControl;_syscallCancellation=syscallCancellation;_interruptCancellation=interruptCancellation;return true;}
    internal static Boolean TrySetControl(UInt64 id,KernelProcessControl control)=>_setControl!=null&&_setControl(id,control);
    internal static Boolean HandleSyscallCancellation(KernelSystemCallFrame* frame)=>_syscallCancellation!=null&&_syscallCancellation(frame);
    internal static Boolean HandleInterruptCancellation()=>_interruptCancellation!=null&&_interruptCancellation();
}
