using System;

namespace Inu.Kernel.Synchronization;

/// <summary>ABA-resistant tagged lock-free LIFO stack over caller-owned unmanaged nodes.</summary>
public unsafe struct KernelLockFreeStack64
{
    private KernelLockFreeStackNode64* _nodes;
    private UInt32 _capacity;
    private UInt64 _head; // high 32 bits tag, low 32 bits index+1; zero low half means empty.

    public Boolean Initialize(UInt64 nodesAddress, UInt32 capacity)
    { if(nodesAddress==0UL||capacity==0U)return false; _nodes=(KernelLockFreeStackNode64*)nodesAddress;_capacity=capacity;_head=0UL;for(UInt32 i=0;i<capacity;i++){_nodes[i].Value=0UL;_nodes[i].NextIndexPlusOne=0U;_nodes[i].Reserved=0U;}return true; }

    public Boolean TryPush(UInt32 nodeIndex, UInt64 value)
    {
        if(_nodes==null||nodeIndex>=_capacity)return false; KernelLockFreeStackNode64* node=_nodes+nodeIndex; node->Value=value;
        fixed(UInt64* head=&_head)
        {
            for(UInt32 attempt=0;attempt<1024U;attempt++)
            {
                if(!KernelAtomic.TryLoad(head,out UInt64 oldHead))return false;
                node->NextIndexPlusOne=(UInt32)oldHead;
                UInt64 tag=((oldHead>>32)+1UL)&0xFFFFFFFFUL;
                UInt64 next=(tag<<32)|((UInt64)nodeIndex+1UL);
                if(KernelAtomic.TryCompareExchange(head,oldHead,next,out UInt64 observed)&&observed==oldHead)return true;
                KernelAtomic.SpinWaitHint();
            }
        }
        return false;
    }

    public Boolean TryPop(out UInt32 nodeIndex, out UInt64 value)
    {
        nodeIndex=0U;value=0UL;if(_nodes==null)return false;
        fixed(UInt64* head=&_head)
        {
            for(UInt32 attempt=0;attempt<1024U;attempt++)
            {
                if(!KernelAtomic.TryLoad(head,out UInt64 oldHead))return false;
                UInt32 indexPlusOne=(UInt32)oldHead;if(indexPlusOne==0U)return false;UInt32 index=indexPlusOne-1U;if(index>=_capacity)return false;
                KernelLockFreeStackNode64* node=_nodes+index; UInt64 tag=((oldHead>>32)+1UL)&0xFFFFFFFFUL; UInt64 next=(tag<<32)|node->NextIndexPlusOne;
                if(KernelAtomic.TryCompareExchange(head,oldHead,next,out UInt64 observed)&&observed==oldHead){nodeIndex=index;value=node->Value;return true;}
                KernelAtomic.SpinWaitHint();
            }
        }
        return false;
    }
}
