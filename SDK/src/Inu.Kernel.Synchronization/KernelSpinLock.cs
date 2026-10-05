using System;

namespace Inu.Kernel.Synchronization;

/// <summary>Non-recursive queued-free spin lock for very short kernel critical sections.</summary>
public unsafe struct KernelSpinLock
{
    private UInt64 _state;

    public Boolean IsHeld()
    { fixed(UInt64* p=&_state) return KernelAtomic.TryLoad(p,out UInt64 value)&&value!=0UL; }

    public Boolean TryEnter()
    {
        fixed(UInt64* p=&_state)
        {
            if(!KernelAtomic.TryCompareExchange(p,0UL,1UL,out UInt64 previous))return false;
            return previous==0UL;
        }
    }

    public Boolean Enter(UInt32 spinLimit=0U)
    {
        UInt32 iteration=0U;
        while(!TryEnter())
        {
            if(spinLimit!=0U&&++iteration>=spinLimit)return false;
            KernelAtomic.SpinWaitHint();
        }
        return true;
    }

    public Boolean Exit()
    {
        fixed(UInt64* p=&_state)
        {
            if(!KernelAtomic.TryExchange(p,0UL,out UInt64 previous))return false;
            return previous==1UL;
        }
    }
}
