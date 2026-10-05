using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Console;
using Inu.Arch.X64;
using Inu.Kernel.Smp;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Processes;
using Inu.Kernel.Acpi;
using Inu.Kernel.Time;

namespace Inu.Kernel.Bootstrap;

/// <summary>Freestanding, allocation-free bridge between KernelPanic and x64/platform services.</summary>
public static unsafe class KernelPanicTransport
{
    public static Boolean Initialize()
        => KernelPanic.ConfigureFreestanding(&GetContext,&CaptureRegisters,&CaptureCallStack,&RequestCrashDump,&BreakDebugger,&Halt,&Reboot);

    public static Boolean GetContext(UInt32* cpu,UInt64* threadId,UInt64* processId,UInt64* instructionPointer,UInt64* stackPointer,UInt64* timestampNanoseconds)
    {
        UInt32 c=0;UInt64 t=0,p=0,rip=0,rsp=0,rbp=0,flags=0,cr3=0;
        if(KernelSmp.TryGetCurrentProcessor(out KernelProcessorState processor))c=processor.Index;
        KernelScheduler.TryGetCurrentThreadId(out t);
        KernelProcesses.TryGetCurrentProcessId(out p);
        X64ArchitectureBoundary.CapturePanicContext(out rip,out rsp,out rbp,out flags,out cr3);
        if(cpu!=null)*cpu=c;if(threadId!=null)*threadId=t;if(processId!=null)*processId=p;
        if(instructionPointer!=null)*instructionPointer=rip;if(stackPointer!=null)*stackPointer=rsp;
        if(timestampNanoseconds!=null)*timestampNanoseconds=KernelTime.IsInitialized?KernelTime.GetMonotonicNanoseconds():0UL;
        return true;
    }

    public static Boolean CaptureRegisters(KernelPanicRegisters* snapshot)
    {
        if(snapshot==null)return false;
        UInt64 rip=0,rsp=0,rbp=0,flags=0,cr3=0;
        if(!X64ArchitectureBoundary.CapturePanicContext(out rip,out rsp,out rbp,out flags,out cr3))return false;
        // Volatile GPRs have already been used by the managed call boundary; leave them zero
        // rather than publishing misleading values. RIP/RSP/RBP/RFLAGS/CR3 are authoritative.
        *snapshot=new KernelPanicRegisters(0,0,0,0,0,0,rbp,rsp,0,0,0,0,0,0,0,0,rip,flags,cr3);
        return true;
    }

    public static Boolean CaptureCallStack(KernelPanicCallStack* stack)
    {
        if(stack==null)return false;
        UInt64 rip=0,rsp=0,rbp=0,flags=0,cr3=0;
        if(!X64ArchitectureBoundary.CapturePanicContext(out rip,out rsp,out rbp,out flags,out cr3))return false;
        UInt64 f0=rip,f1=0,f2=0,f3=0,f4=0,f5=0,f6=0,f7=0;UInt32 count=rip!=0?1U:0U;
        // Conservative frame-pointer walk. Stop immediately on non-monotonic or implausibly distant frames.
        UInt64 current=rbp;
        for(UInt32 index=1;index<8&&current!=0;index++)
        {
            UInt64* frame=(UInt64*)current;
            UInt64 next=frame[0],ret=frame[1];
            if(ret==0||next<=current||next-current>1048576UL)break;
            if(index==1)f1=ret;else if(index==2)f2=ret;else if(index==3)f3=ret;else if(index==4)f4=ret;else if(index==5)f5=ret;else if(index==6)f6=ret;else f7=ret;
            count=index+1;current=next;
        }
        *stack=new KernelPanicCallStack(count,f0,f1,f2,f3,f4,f5,f6,f7);
        return true;
    }

    public static Boolean RequestCrashDump(KernelPanicNativeInfo* info,KernelPanicRegisters* registers,KernelPanicCallStack* stack)
    {
        if(info==null)return false;
        KernelPanicRegisters r=registers==null?default:*registers;KernelPanicCallStack s=stack==null?default:*stack;
        // This single allocation-free record is durable enough for the launcher to materialise
        // a valid NOCD v1 JSON dump even when no debugger is attached. Kath can enrich the same
        // format with page tables, heap, module and driver state when a paused debug session exists.
        if(!KernelConsole.Write("[INU:CRASHDUMP] format=1.1 code="))return false;if(!KernelConsole.WriteUInt64(info->Code))return false;
        if(!KernelConsole.Write(" cpu="))return false;if(!KernelConsole.WriteUInt64(info->Cpu))return false;
        if(!KernelConsole.Write(" thread="))return false;if(!KernelConsole.WriteUInt64(info->ThreadId))return false;
        if(!KernelConsole.Write(" process="))return false;if(!KernelConsole.WriteUInt64(info->ProcessId))return false;
        if(!KernelConsole.Write(" rip="))return false;if(!KernelConsole.WriteHex(r.Rip))return false;
        if(!KernelConsole.Write(" rsp="))return false;if(!KernelConsole.WriteHex(r.Rsp))return false;
        if(!KernelConsole.Write(" rbp="))return false;if(!KernelConsole.WriteHex(r.Rbp))return false;
        if(!KernelConsole.Write(" rflags="))return false;if(!KernelConsole.WriteHex(r.Flags))return false;
        if(!KernelConsole.Write(" cr3="))return false;if(!KernelConsole.WriteHex(r.Cr3))return false;
        if(!KernelConsole.Write(" frames="))return false;if(!KernelConsole.WriteUInt64(s.Count))return false;
        if(!KernelConsole.Write(" f0="))return false;if(!KernelConsole.WriteHex(s.Frame0))return false;
        if(!KernelConsole.Write(" f1="))return false;if(!KernelConsole.WriteHex(s.Frame1))return false;
        if(!KernelConsole.Write(" f2="))return false;if(!KernelConsole.WriteHex(s.Frame2))return false;
        if(!KernelConsole.Write(" f3="))return false;if(!KernelConsole.WriteHex(s.Frame3))return false;
        if(!KernelConsole.Write(" f4="))return false;if(!KernelConsole.WriteHex(s.Frame4))return false;
        if(!KernelConsole.Write(" f5="))return false;if(!KernelConsole.WriteHex(s.Frame5))return false;
        if(!KernelConsole.Write(" f6="))return false;if(!KernelConsole.WriteHex(s.Frame6))return false;
        if(!KernelConsole.Write(" f7="))return false;if(!KernelConsole.WriteHex(s.Frame7))return false;
        if(!KernelConsole.WriteLine(""))return false;
        if(!KernelConsole.Write("[INU:PANIC] code="))return false;if(!KernelConsole.WriteUInt64(info->Code))return false;
        if(!KernelConsole.Write(" cpu="))return false;if(!KernelConsole.WriteUInt64(info->Cpu))return false;
        if(!KernelConsole.Write(" thread="))return false;if(!KernelConsole.WriteUInt64(info->ThreadId))return false;
        if(!KernelConsole.Write(" process="))return false;if(!KernelConsole.WriteUInt64(info->ProcessId))return false;
        return KernelConsole.WriteLine(" dump=1");
    }

    public static Boolean BreakDebugger(KernelPanicNativeInfo* info)=>X64ArchitectureBoundary.PanicDebuggerBreak();
    public static Boolean Reboot()=>KernelAcpiPowerServices.Reboot();
    public static Boolean Halt()=>X64ArchitectureBoundary.Halt();
}
