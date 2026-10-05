using System;

namespace Inu.Kernel.Scheduler;

/// <summary>Registry for the CPU-placement policy selected by the OS author.</summary>
public static unsafe class KernelThreadPlacementServices
{
    private static delegate*<UInt64,UInt32*,Boolean> _selectInitial;
    private static delegate*<KernelRunnableThreadHandle,UInt32,UInt32*,Boolean> _rebalance;
    private static delegate*<UInt32,UInt64*,Boolean> _stealTo;
    private static delegate*<UInt32,Boolean> _hasStealableWork;
    public static Boolean IsRegistered=>_selectInitial!=null&&_rebalance!=null&&_stealTo!=null&&_hasStealableWork!=null;
    public static Boolean Register(delegate*<UInt64,UInt32*,Boolean> selectInitial,delegate*<KernelRunnableThreadHandle,UInt32,UInt32*,Boolean> rebalance,delegate*<UInt32,UInt64*,Boolean> stealTo,delegate*<UInt32,Boolean> hasStealableWork)
    { if(selectInitial==null||rebalance==null||stealTo==null||hasStealableWork==null||IsRegistered)return false;_selectInitial=selectInitial;_rebalance=rebalance;_stealTo=stealTo;_hasStealableWork=hasStealableWork;return true; }
    internal static Boolean TrySelectInitial(UInt64 affinityMask,out UInt32 processorIndex)
    { processorIndex=UInt32.MaxValue;if(_selectInitial==null)return false;UInt32 value=UInt32.MaxValue;Boolean ok=_selectInitial(affinityMask,&value);processorIndex=value;return ok; }
    internal static Boolean TryRebalance(KernelRunnableThreadHandle handle,UInt32 currentProcessor,out UInt32 processorIndex)
    { processorIndex=currentProcessor;if(_rebalance==null)return false;UInt32 value=currentProcessor;Boolean ok=_rebalance(handle,currentProcessor,&value);processorIndex=value;return ok; }
    internal static Boolean TryStealTo(UInt32 destinationProcessor,out UInt64 threadId)
    { threadId=0UL;if(_stealTo==null)return false;UInt64 value=0UL;Boolean ok=_stealTo(destinationProcessor,&value);threadId=value;return ok; }
    internal static Boolean HasStealableWork(UInt32 destinationProcessor)=>_hasStealableWork!=null&&_hasStealableWork(destinationProcessor);
}
