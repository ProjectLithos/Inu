using Inu.Interrupts;

namespace Inu.Architecture.X64.Interrupts;

/// <summary>Registers essential x64 exception handling, including resumable asynchronous NMI and fatal synchronous diagnostics.</summary>
public sealed class EssentialExceptionHandlers
{
    private readonly IInterruptDescriptorTable table;
    private readonly IExceptionDiagnosticSink sink;

    /// <summary>Creates the essential exception handler set.</summary>
    public EssentialExceptionHandlers(IInterruptDescriptorTable table, IExceptionDiagnosticSink sink)
    {
        this.table = table;
        this.sink = sink;
    }

    /// <summary>Registers essential x64 exception handlers. NMI is asynchronous and resumable; fatal synchronous exceptions retain stop-on-fault diagnostics.</summary>
    public bool RegisterAll()
    {
        InterruptRegistrationResult nmi = table.Register((byte)CpuExceptionVector.NonMaskableInterrupt, HandleNonMaskableInterrupt,
            new InterruptRegistrationOptions(Inu.Architecture.DescriptorPrivilegeLevel.Kernel,
                InterruptGateType.Interrupt, 2, true));
        if (!nmi.Succeeded) return false;

        foreach (byte vector in new byte[] { 0, 6, 8, 12, 13, 14, 18 })
        {
            byte ist = vector switch { 8 => 1, 18 => 3, _ => 0 };
            InterruptRegistrationResult result = table.Register(vector, HandleFatal,
                new InterruptRegistrationOptions(Inu.Architecture.DescriptorPrivilegeLevel.Kernel,
                    InterruptGateType.Interrupt, ist, true));
            if (!result.Succeeded) return false;
        }
        return true;
    }

    private static InterruptResult HandleNonMaskableInterrupt(ref InterruptContext context)
    {
        // NMI is asynchronous: the interrupted RIP is not evidence of a fault in the
        // instruction that happened to be running. Firmware, APIC/watchdog/debug paths,
        // and physical hardware may all deliver NMIs. Until Inu has source-specific NMI
        // decoding, acknowledge the event and resume; known-fatal hardware conditions
        // such as #MC remain on their dedicated fatal vector.
        return InterruptResult.Handled;
    }

    private InterruptResult HandleFatal(ref InterruptContext context)
    {
        sink.WriteLine("FATAL CPU EXCEPTION");
        sink.WriteLine($"Exception: {GetName((byte)context.Vector)} ({context.Vector})");
        sink.WriteLine($"Error code: 0x{context.ErrorCode:X16} {DecodeErrorCode(ref context)}");
        sink.WriteLine($"RIP=0x{context.InstructionPointer:X16} CS=0x{context.CodeSegment:X4} RFLAGS=0x{context.Flags:X16}");
        sink.WriteLine($"RSP=0x{context.StackPointer:X16} SS=0x{context.StackSegment:X4} CPL transition={context.HasPrivilegeTransition()}");
        sink.WriteLine($"CR0=0x{context.ControlRegister0:X16} CR2=0x{context.ControlRegister2:X16}");
        sink.WriteLine($"CR3=0x{context.ControlRegister3:X16} CR4=0x{context.ControlRegister4:X16}");
        sink.WriteLine($"RAX={context.Rax:X16} RBX={context.Rbx:X16} RCX={context.Rcx:X16} RDX={context.Rdx:X16}");
        sink.WriteLine($"RSI={context.Rsi:X16} RDI={context.Rdi:X16} RBP={context.Rbp:X16}");
        sink.WriteLine($"R8={context.R8:X16} R9={context.R9:X16} R10={context.R10:X16} R11={context.R11:X16}");
        sink.WriteLine($"R12={context.R12:X16} R13={context.R13:X16} R14={context.R14:X16} R15={context.R15:X16}");
        sink.WriteLine($"CPU: {context.ProcessorId}");
        sink.WriteLine("Current thread/process: unavailable until scheduler integration.");
        if (context.IsPageFault()) sink.WriteLine($"Page fault address=0x{context.ControlRegister2:X16}; {DecodePageFault(context.ErrorCode)}");
        sink.WriteLine("Stack trace: unavailable until unwind metadata integration.");
        sink.StopProcessor();
        return InterruptResult.Fatal;
    }

    private static string GetName(byte vector) => vector switch
    {
        0 => "Divide error", 2 => "Non-maskable interrupt", 6 => "Invalid opcode",
        8 => "Double fault", 12 => "Stack-segment fault", 13 => "General protection fault",
        14 => "Page fault", 18 => "Machine check", _ => "CPU exception"
    };

    private static string DecodeErrorCode(ref InterruptContext context)
    {
        if (context.Vector == 14) return DecodePageFault(context.ErrorCode);
        if (context.Vector is 10 or 11 or 12 or 13)
        {
            ulong code = context.ErrorCode;
            return $"external={(code & 1) != 0}, table={((code >> 1) & 3)}, selector-index={code >> 3}";
        }
        return "architectural error code not applicable";
    }

    private static string DecodePageFault(ulong code) =>
        $"present={(code & 1) != 0}, access={((code & 2) != 0 ? "write" : "read")}, " +
        $"mode={((code & 4) != 0 ? "user" : "supervisor")}, reserved={(code & 8) != 0}, " +
        $"instruction-fetch={(code & 16) != 0}, protection-key={(code & 32) != 0}, shadow-stack={(code & 64) != 0}";
}
