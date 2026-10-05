using System;
using System.Runtime;
using System.Runtime.InteropServices;
using Inu.Kernel.Acpi;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Heap;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Smp;

namespace Inu.Kernel.InterruptDispatch;

/// <summary>Owns freestanding managed interrupt dispatch, formal vector allocation and per-CPU interrupt runtime state.</summary>
public static unsafe partial class KernelInterruptDispatch
{
    public static Boolean RegisterUserCancellationHandler(delegate*<Boolean> handler)
    {
        if(!_initialized||handler==null||_userCancellationHandler!=0UL)return false;_userCancellationHandler=(UInt64)(void*)handler;return true;
    }

    /// <summary>Clears the retained CPL3-fault diagnostic before a new user process run.</summary>
    public static void ClearLastUserFault()
    {
        _lastUserFaultVector=0;_lastUserFaultError=_lastUserFaultRip=_lastUserFaultCs=_lastUserFaultRflags=_lastUserFaultRsp=_lastUserFaultSs=_lastUserFaultCr2=_lastUserFaultCr3=0UL;
        fixed(UInt64* sequence=&_lastUserFaultSequence)Native.AtomicStore64(sequence,0UL);
    }

    /// <summary>Gets the most recent unhandled CPL3 CPU exception contained by the kernel.</summary>
    public static Boolean TryGetLastUserFault(out UserFaultInfo fault)
    {
        fault=default;UInt64 sequence=0UL;fixed(UInt64* p=&_lastUserFaultSequence)Native.AtomicLoad64(p,&sequence);if(sequence==0UL)return false;
        fault=new UserFaultInfo(_lastUserFaultVector,_lastUserFaultError,_lastUserFaultRip,_lastUserFaultCs,_lastUserFaultRflags,_lastUserFaultRsp,_lastUserFaultSs,_lastUserFaultCr2,_lastUserFaultCr3);return true;
    }
    private static void RecordUserFault(NativeInterruptContext* c,Byte vector)
    {
        _lastUserFaultVector=vector;_lastUserFaultError=c->ErrorCode;_lastUserFaultRip=c->Rip;_lastUserFaultCs=c->Cs;_lastUserFaultRflags=c->Rflags;_lastUserFaultRsp=c->Rsp;_lastUserFaultSs=c->Ss;_lastUserFaultCr2=c->Cr2;_lastUserFaultCr3=c->Cr3;
        UInt64 previous=0UL;fixed(UInt64* sequence=&_lastUserFaultSequence)Native.AtomicFetchAdd64(sequence,1UL,&previous);
        if(!Native.TryBeginSerialRecord())return;
        TraceFaultText("[UFAULT] vector=");TraceFaultHex(vector);TraceFaultText(" error=");TraceFaultHex(c->ErrorCode);TraceFaultText(" rip=");TraceFaultHex(c->Rip);TraceFaultText(" cr2=");TraceFaultHex(c->Cr2);TraceFaultText(" rsp=");TraceFaultHex(c->Rsp);TraceFaultText("\r\n");
        Native.EndSerialRecord();
    }
    private static void TraceFaultText(String text){if(text==null)return;for(Int32 i=0;i<text.Length;i++)Native.WriteSerial((Byte)text[i]);}
    private static void TraceFaultHex(UInt64 value){const String digits="0123456789ABCDEF";Native.WriteSerial((Byte)'0');Native.WriteSerial((Byte)'x');Boolean started=false;for(Int32 shift=60;shift>=0;shift-=4){UInt32 nibble=(UInt32)((value>>shift)&0xFUL);if(!started&&nibble==0U&&shift!=0)continue;started=true;Native.WriteSerial((Byte)digits[(Int32)nibble]);}}
}
