using System;

namespace Inu.Kernel.Scheduler;

/// <summary>Registry for the scheduling policy selected by the OS author.</summary>
public static unsafe class KernelSchedulingPolicyServices
{
    private static delegate*<UInt32,UInt64*,Boolean> _selectLocal;
    private static delegate*<UInt32,UInt64*,Boolean> _selectBalanced;
    private static delegate*<UInt32,Boolean> _hasDispatchableWork;
    public static Boolean IsRegistered=>_selectLocal!=null&&_selectBalanced!=null&&_hasDispatchableWork!=null;
    public static Boolean Register(delegate*<UInt32,UInt64*,Boolean> selectLocal,delegate*<UInt32,UInt64*,Boolean> selectBalanced,delegate*<UInt32,Boolean> hasDispatchableWork)
    { if(selectLocal==null||selectBalanced==null||hasDispatchableWork==null||IsRegistered)return false;_selectLocal=selectLocal;_selectBalanced=selectBalanced;_hasDispatchableWork=hasDispatchableWork;return true; }
    internal static Boolean TrySelectLocal(UInt32 processorIndex,out UInt64 threadId)
    { threadId=0UL;if(_selectLocal==null)return false;UInt64 value=0UL;Boolean ok=_selectLocal(processorIndex,&value);threadId=value;return ok; }
    internal static Boolean TrySelectBalanced(UInt32 processorIndex,out UInt64 threadId)
    { threadId=0UL;if(_selectBalanced==null)return false;UInt64 value=0UL;Boolean ok=_selectBalanced(processorIndex,&value);threadId=value;return ok; }
    internal static Boolean HasDispatchableWork(UInt32 processorIndex)=>_hasDispatchableWork!=null&&_hasDispatchableWork(processorIndex);
}
