using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Security;
using Inu.Kernel.SystemCalls;

namespace Inu.Kernel.Processes;

public static unsafe partial class KernelProcesses
{
    private static void TraceUserPreflight(KernelProcessRecordHandle record,UInt64 kernelRoot)
    {
        UInt64 processId=KernelProcessRecordStore.GetId(record),root=KernelProcessRecordStore.GetRoot(record),entry=KernelProcessRecordStore.GetEntry(record),stackTop=KernelProcessRecordStore.GetStackTop(record),guard=KernelProcessRecordStore.GetStackGuardBase(record);
        if(Native.BeginSerialRecord()){TraceUserText("[USR] preflight apic=");TraceUserHex(Native.GetCurrentApicId());TraceUserText(" kernelCR3=");TraceUserHex(kernelRoot);TraceUserText(" userCR3=");TraceUserHex(root);TraceUserText("\r\n");Native.EndSerialRecord();}
        if(Native.BeginSerialRecord()){TraceUserText("[USR] syscall state=");TraceUserHex(KernelSystemCalls.GetSyscallStateAddress());TraceUserText(" stackBase=");TraceUserHex(KernelSystemCalls.GetSyscallStackBase());TraceUserText(" stackTop=");TraceUserHex(KernelSystemCalls.GetSyscallStackTop());TraceUserText(" smap=");TraceUserHex(KernelSystemCalls.GetCapabilities().SmapEnabled?1UL:0UL);TraceUserText("\r\n");Native.EndSerialRecord();}
        if(Native.BeginSerialRecord()){TraceUserText("[USR] msr EFER=");TraceUserHex(Native.ReadModelSpecificRegister(0xC0000080U));TraceUserText(" STAR=");TraceUserHex(Native.ReadModelSpecificRegister(0xC0000081U));TraceUserText(" LSTAR=");TraceUserHex(Native.ReadModelSpecificRegister(0xC0000082U));TraceUserText(" FMASK=");TraceUserHex(Native.ReadModelSpecificRegister(0xC0000084U));TraceUserText(" KGSBASE=");TraceUserHex(Native.ReadModelSpecificRegister(0xC0000102U));TraceUserText("\r\n");Native.EndSerialRecord();}
        TraceUserMapping(processId,"entry",entry);TraceUserMapping(processId,"stack",stackTop-8UL);TraceUserMapping(processId,"guard",guard);
        if(Native.BeginSerialRecord()){TraceUserText("[USR] validation exec=");TraceUserHex(KernelSecurity.TryValidateExecutableRange(processId,entry,1UL)?1UL:0UL);TraceUserText(" stackRW=");TraceUserHex(KernelSecurity.TryValidateUserPointer(processId,stackTop-8UL,8UL,KernelUserMemoryAccess.Read|KernelUserMemoryAccess.Write)?1UL:0UL);TraceUserText("\r\n");Native.EndSerialRecord();}
    }

    private static Boolean ValidateProcessRootBeforeSwitch(KernelProcessRecordHandle record)
    {
        UInt64 root=KernelProcessRecordStore.GetRoot(record);
        if(!Native.CapturePanicContext(out UInt64 rip,out UInt64 rsp,out UInt64 rbp,out UInt64 flags,out UInt64 activeRoot)){TraceUserRecord("[USR] capture kernel context FAILED\r\n");return false;}
#if DEBUG
        if(Native.BeginSerialRecord()){TraceUserText("[USR] kernel context rip=");TraceUserHex(rip);TraceUserText(" rsp=");TraceUserHex(rsp);TraceUserText(" rbp=");TraceUserHex(rbp);TraceUserText(" rflags=");TraceUserHex(flags);TraceUserText(" activeCR3=");TraceUserHex(activeRoot);TraceUserText("\r\n");Native.EndSerialRecord();}
#endif
        Boolean ripMapped=ValidateCandidateRootMapping(root,"kernel-rip",rip);
        Boolean rspMapped=ValidateCandidateRootMapping(root,"kernel-rsp",rsp);
        UInt64 syscallState=KernelSystemCalls.GetSyscallStateAddress();UInt64 syscallStack=KernelSystemCalls.GetSyscallStackTop();
        Boolean stateMapped=syscallState==0UL||ValidateCandidateRootMapping(root,"syscall-state",syscallState);
        Boolean syscallStackMapped=syscallStack==0UL||ValidateCandidateRootMapping(root,"syscall-stack",syscallStack-8UL);
        return ripMapped&&rspMapped&&stateMapped&&syscallStackMapped;
    }

    private static Boolean ValidateCandidateRootMapping(UInt64 root,String label,UInt64 virtualAddress)
    {
        Boolean mapped=ProcessAddressSpace.TryInspectMapping(root,virtualAddress,out UInt64 physical,out UInt64 raw,out UInt64 pageSize);
#if DEBUG
        if(Native.BeginSerialRecord())
        {
            TraceUserText("[USR] processCR3 map ");TraceUserText(label);TraceUserText(" va=");TraceUserHex(virtualAddress);
            if(!mapped)TraceUserText(" MISSING");
            else{TraceUserText(" pa=");TraceUserHex(physical);TraceUserText(" flags=");TraceUserHex(raw);TraceUserText(" page=");TraceUserHex(pageSize);}
            TraceUserText("\r\n");Native.EndSerialRecord();
        }
#endif
        return mapped;
    }

    private static void TraceUserMapping(UInt64 processId,String label,UInt64 virtualAddress)
    {
        if(!Native.BeginSerialRecord())return;
        TraceUserText("[USR] map ");TraceUserText(label);TraceUserText(" va=");TraceUserHex(virtualAddress);
        if(!KernelSecurity.TryInspectUserMapping(processId,virtualAddress,out KernelUserMappingInspection mapping)){TraceUserText(" inspect=FAILED\r\n");Native.EndSerialRecord();return;}
        TraceUserText(" mapped=");TraceUserHex(mapping.Mapped?1UL:0UL);if(mapping.Mapped){TraceUserText(" pa=");TraceUserHex(mapping.PhysicalAddress);TraceUserText(" flags=");TraceUserHex(mapping.RawFlags);TraceUserText(" page=");TraceUserHex(mapping.PageSize);}TraceUserText("\r\n");
        Native.EndSerialRecord();
    }

    private static void TraceUserProcessStart(KernelProcessRecordHandle record)
    {
        if(!Native.BeginSerialRecord())return;
        TraceUserText("[USR] start pid=");TraceUserHex(KernelProcessRecordStore.GetId(record));TraceUserText(" root=");TraceUserHex(KernelProcessRecordStore.GetRoot(record));TraceUserText(" entry=");TraceUserHex(KernelProcessRecordStore.GetEntry(record));TraceUserText(" stack=");TraceUserHex(KernelProcessRecordStore.GetStackTop(record));TraceUserText("\r\n");
        Native.EndSerialRecord();
    }

    private static void TraceUserRecord(String value){if(value==null||!Native.BeginSerialRecord())return;TraceUserText(value);Native.EndSerialRecord();}
    private static void TraceUserText(String value){if(value==null)return;for(Int32 i=0;i<value.Length;i++)Native.WriteSerial((Byte)value[i]);}
    private static void TraceUserHex(UInt64 value){const String digits="0123456789ABCDEF";Native.WriteSerial((Byte)'0');Native.WriteSerial((Byte)'x');Boolean started=false;for(Int32 shift=60;shift>=0;shift-=4){UInt32 nibble=(UInt32)((value>>shift)&0xFUL);if(!started&&nibble==0U&&shift!=0)continue;started=true;Native.WriteSerial((Byte)digits[(Int32)nibble]);}}
}
