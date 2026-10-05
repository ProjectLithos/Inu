using System;

namespace Inu.Kernel.Synchronization;

/// <summary>Writer-preferring reader/writer lock with bounded reader count and timeout support.</summary>
public unsafe struct KernelReaderWriterLock
{
    private const UInt64 WriterBit=0x8000000000000000UL;
    private const UInt64 ReaderMask=0x00000000FFFFFFFFUL;
    private UInt64 _state;
    private UInt64 _waitingWriters;

    public Boolean TryEnterRead()
    {
        fixed(UInt64* waiting=&_waitingWriters) if(!KernelAtomic.TryLoad(waiting,out UInt64 w)||w!=0UL)return false;
        fixed(UInt64* state=&_state)
        {
            for(UInt32 i=0;i<64U;i++)
            {
                if(!KernelAtomic.TryLoad(state,out UInt64 current)||(current&WriterBit)!=0UL||(current&ReaderMask)==ReaderMask)return false;
                if(KernelAtomic.TryCompareExchange(state,current,current+1UL,out UInt64 observed)&&observed==current)return true;
            }
        }
        return false;
    }

    public Boolean EnterRead(UInt64 timeoutNanoseconds=KernelSynchronizationTimeout.Infinite)
    { UInt64 start=KernelSynchronizationWait.StartTimestamp(timeoutNanoseconds);UInt32 i=0;do{if(TryEnterRead())return true;KernelSynchronizationWait.Pause(++i);}while(!KernelSynchronizationWait.HasTimedOut(start,timeoutNanoseconds));return false; }

    public Boolean ExitRead()
    {
        fixed(UInt64* state=&_state)
        {
            for(UInt32 i=0;i<64U;i++)
            {
                if(!KernelAtomic.TryLoad(state,out UInt64 current)||(current&WriterBit)!=0UL||(current&ReaderMask)==0UL)return false;
                if(KernelAtomic.TryCompareExchange(state,current,current-1UL,out UInt64 observed)&&observed==current)return true;
            }
        }
        return false;
    }

    public Boolean TryEnterWrite()
    { fixed(UInt64* state=&_state) return KernelAtomic.TryCompareExchange(state,0UL,WriterBit,out UInt64 previous)&&previous==0UL; }

    public Boolean EnterWrite(UInt64 timeoutNanoseconds=KernelSynchronizationTimeout.Infinite)
    {
        fixed(UInt64* waiting=&_waitingWriters) if(!KernelAtomic.TryIncrement(waiting,out _))return false;
        UInt64 start=KernelSynchronizationWait.StartTimestamp(timeoutNanoseconds);UInt32 i=0;Boolean acquired=false;
        do{if(TryEnterWrite()){acquired=true;break;}KernelSynchronizationWait.Pause(++i);}while(!KernelSynchronizationWait.HasTimedOut(start,timeoutNanoseconds));
        fixed(UInt64* waiting=&_waitingWriters) KernelAtomic.TryDecrement(waiting,out _);
        return acquired;
    }

    public Boolean ExitWrite()
    { fixed(UInt64* state=&_state) return KernelAtomic.TryCompareExchange(state,WriterBit,0UL,out UInt64 previous)&&previous==WriterBit; }

    public KernelReaderWriterLockInfo GetInfo()
    {
        UInt64 state=0,waiting=0; fixed(UInt64* p=&_state)KernelAtomic.TryLoad(p,out state); fixed(UInt64* p=&_waitingWriters)KernelAtomic.TryLoad(p,out waiting);
        return new KernelReaderWriterLockInfo((UInt32)(state&ReaderMask),(state&WriterBit)!=0UL,(UInt32)waiting);
    }
}
