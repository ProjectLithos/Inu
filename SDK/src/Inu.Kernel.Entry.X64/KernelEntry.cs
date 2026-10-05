using System;
using System.Runtime;
using Inu.Kernel.Bootstrap;
using Inu.Kernel.Console;

namespace Inu.Kernel.Internal.X64;

internal static class KernelEntry
{
    [RuntimeExport("InuManagedEntry")]
    private static Boolean NativeEntry(UInt64 bootContextAddress)
    {
        EmitEntryStage();
        if (bootContextAddress == 0UL) { EmitBootContextFailureStage(); return false; }
        NativeBootContext boot = new NativeBootContext(bootContextAddress);
        if (!boot.IsAvailable()) { EmitSignatureFailureStage(); return false; }

        // SDK-owned managed acceptance must complete before control is handed to a
        // generated OS Kernel.cs. Existing projects refresh kernel orchestration
        // source across IDE upgrades, so runtime safety cannot depend on template code.
        EmitPreflightStage();
        if (!global::Inu.Kernel.Console.Console.Run(ConsoleType.Serial))
        {
            EmitPreflightConsoleFailureStage();
            return false;
        }
        EmitPreflightConsoleStage();

        String probe = "NO";
        if (probe.Length != 2)
        {
            EmitPreflightStringLengthFailureStage();
            return false;
        }
        EmitPreflightStringLengthStage();
        if (probe[0] != 'N')
        {
            EmitPreflightStringChar0FailureStage();
            return false;
        }
        EmitPreflightStringChar0Stage();
        if (probe[1] != 'O')
        {
            EmitPreflightStringChar1FailureStage();
            return false;
        }
        EmitPreflightStringChar1Stage();
        EmitPreflightStringStage();

        EmitKMainStage();
        Boolean result = global::Inu.Kernel.Bootstrap.Kernel.KMain(boot);
        if (result) EmitReturnOkStage(); else EmitReturnFailureStage();
        return result;
    }

    // Earliest managed-entry diagnostics must not depend on System.String, arrays,
    // allocation, or any other CoreLib surface whose ABI has not yet been proven.
    private static void EmitEntryStage()
    {
        W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('E'); W('N'); W('T'); W('R'); W('Y'); W('\n');
    }

    private static void EmitBootContextFailureStage()
    {
        W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('F'); W('A'); W('I'); W('L'); W(':'); W('B'); W('O'); W('O'); W('T'); W('C'); W('T'); W('X'); W('\n');
    }

    private static void EmitSignatureFailureStage()
    {
        W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('F'); W('A'); W('I'); W('L'); W(':'); W('S'); W('I'); W('G'); W('N'); W('A'); W('T'); W('U'); W('R'); W('E'); W('\n');
    }


    private static void EmitPreflightStage()
    {
        W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('P'); W('R'); W('E'); W('F'); W('L'); W('I'); W('G'); W('H'); W('T'); W('\n');
    }

    private static void EmitPreflightConsoleStage()
    {
        W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('P'); W('R'); W('E'); W(':'); W('C'); W('O'); W('N'); W('S'); W('O'); W('L'); W('E'); W('\n');
    }

    private static void EmitPreflightStringStage()
    {
        W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('P'); W('R'); W('E'); W(':'); W('S'); W('T'); W('R'); W('I'); W('N'); W('G'); W('\n');
    }

    private static void EmitPreflightConsoleFailureStage()
    {
        W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('F'); W('A'); W('I'); W('L'); W(':'); W('P'); W('R'); W('E'); W(':'); W('C'); W('O'); W('N'); W('S'); W('O'); W('L'); W('E'); W('\n');
    }


    private static void EmitPreflightStringLengthStage()
    { W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('P'); W('R'); W('E'); W(':'); W('S'); W('T'); W('R'); W(':'); W('L'); W('E'); W('N'); W('G'); W('T'); W('H'); W('\n'); }
    private static void EmitPreflightStringChar0Stage()
    { W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('P'); W('R'); W('E'); W(':'); W('S'); W('T'); W('R'); W(':'); W('C'); W('H'); W('A'); W('R'); W('0'); W('\n'); }
    private static void EmitPreflightStringChar1Stage()
    { W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('P'); W('R'); W('E'); W(':'); W('S'); W('T'); W('R'); W(':'); W('C'); W('H'); W('A'); W('R'); W('1'); W('\n'); }
    private static void EmitPreflightStringLengthFailureStage()
    { W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('F'); W('A'); W('I'); W('L'); W(':'); W('P'); W('R'); W('E'); W(':'); W('S'); W('T'); W('R'); W(':'); W('L'); W('E'); W('N'); W('G'); W('T'); W('H'); W('\n'); }
    private static void EmitPreflightStringChar0FailureStage()
    { W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('F'); W('A'); W('I'); W('L'); W(':'); W('P'); W('R'); W('E'); W(':'); W('S'); W('T'); W('R'); W(':'); W('C'); W('H'); W('A'); W('R'); W('0'); W('\n'); }
    private static void EmitPreflightStringChar1FailureStage()
    { W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('F'); W('A'); W('I'); W('L'); W(':'); W('P'); W('R'); W('E'); W(':'); W('S'); W('T'); W('R'); W(':'); W('C'); W('H'); W('A'); W('R'); W('1'); W('\n'); }
    private static void EmitPreflightStringFailureStage()
    {
        W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('F'); W('A'); W('I'); W('L'); W(':'); W('P'); W('R'); W('E'); W(':'); W('S'); W('T'); W('R'); W('I'); W('N'); W('G'); W('\n');
    }

    private static void EmitKMainStage()
    {
        W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('K'); W('M'); W('A'); W('I'); W('N'); W('\n');
    }

    private static void EmitReturnFailureStage()
    {
        W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('R'); W('E'); W('T'); W('U'); W('R'); W('N'); W(':'); W('F'); W('A'); W('I'); W('L'); W('\n');
    }

    private static void EmitReturnOkStage()
    {
        W('N'); W('O'); W('M'); W('N'); W('G'); W(':'); W('R'); W('E'); W('T'); W('U'); W('R'); W('N'); W(':'); W('O'); W('K'); W('\n');
    }

    private static void W(Char value) => Native.WriteSerial((Byte)value);
}
