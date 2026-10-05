using System;
using Inu.Kernel.Internal.X64;
using Inu.Text;

namespace Inu.Kernel.Console;

public static partial class KernelConsole
{
    public static unsafe Boolean BeginAtomicPresentation()
    {
        if(!_initialized||_outputPaused)return false;
        UInt64 owner=((UInt64)Native.GetCurrentApicId())+1UL,current=0UL;
        fixed(UInt64* ownerSlot=&_presentationOwner)
        {
            Native.AtomicLoad64(ownerSlot,&current);
            if(current==owner){_presentationDepth++;return true;}
        }
        fixed(UInt64* gate=&_presentationGate)
        {
            for(;;)
            {
                UInt64 observed=0UL;
                if(Native.AtomicCompareExchange64(gate,0UL,1UL,&observed)&&observed==0UL)break;
                Native.Pause();
            }
        }
        fixed(UInt64* ownerSlot=&_presentationOwner)Native.AtomicStore64(ownerSlot,owner);
        _presentationDepth=1U;
        if(!Native.BeginSerialRecord())
        {
            _presentationDepth=0U;
            fixed(UInt64* ownerSlot=&_presentationOwner)Native.AtomicStore64(ownerSlot,0UL);
            fixed(UInt64* gate=&_presentationGate)Native.AtomicStore64(gate,0UL);
            return false;
        }
        return true;
    }

    /// <summary>Ends the current cross-CPU console presentation transaction.</summary>
    public static unsafe Boolean EndAtomicPresentation()
    {
        if(!_initialized)return false;
        UInt64 owner=((UInt64)Native.GetCurrentApicId())+1UL,current=0UL;
        fixed(UInt64* ownerSlot=&_presentationOwner)Native.AtomicLoad64(ownerSlot,&current);
        if(current!=owner||_presentationDepth==0U)return false;
        if(--_presentationDepth!=0U)return true;
        Boolean serialReleased=Native.EndSerialRecord();
        fixed(UInt64* ownerSlot=&_presentationOwner)Native.AtomicStore64(ownerSlot,0UL);
        fixed(UInt64* gate=&_presentationGate)Native.AtomicStore64(gate,0UL);
        return serialReleased;
    }

    private static unsafe Boolean TryBeginInterruptPresentation()
    {
        if(!_initialized||_outputPaused)return false;
        UInt64 observed=0UL;
        fixed(UInt64* gate=&_presentationGate)
        {
            if(!Native.AtomicCompareExchange64(gate,0UL,1UL,&observed)||observed!=0UL)return false;
        }
        UInt64 owner=((UInt64)Native.GetCurrentApicId())+1UL;
        fixed(UInt64* ownerSlot=&_presentationOwner)Native.AtomicStore64(ownerSlot,owner);
        _presentationDepth=1U;
        if(!Native.TryBeginSerialRecord())
        {
            _presentationDepth=0U;
            fixed(UInt64* ownerSlot=&_presentationOwner)Native.AtomicStore64(ownerSlot,0UL);
            fixed(UInt64* gate=&_presentationGate)Native.AtomicStore64(gate,0UL);
            return false;
        }
        return true;
    }

    /// <summary>Gets whether the low-level console transport has been initialized.</summary>
    public static Boolean IsInitialized() => _initialized;

    /// <summary>Enables the build-selected free TrueType face and redraws the retained framebuffer console through the TrueType renderer.</summary>
}
