using System;
using Inu.Kernel.InterruptDispatch;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Bootstrap.Startup;

/// <summary>Enables runtime interrupt delivery after the OS-selected handlers are ready.</summary>
public static class InterruptRuntimeStartup
{
    /// <summary>Writes a tiny dependency-free transition record to QEMU isa-debugcon (0xE9).</summary>
    private static void Trace(Byte phase)
    {
        Native.WritePort8(0xE9, (Byte)'I');
        Native.WritePort8(0xE9, phase);
        Native.WritePort8(0xE9, (Byte)'\n');
    }

    public static Boolean Enable()
    {
        // 0x10 = entered immediately before Interrupts.Run/STI.
        Trace(0x10);
        Boolean enabled = global::Inu.Kernel.InterruptDispatch.Interrupts.Run();
        // 0x11 = Interrupts.Run returned true; 0x1F = it returned false.
        Trace(enabled ? (Byte)0x11 : (Byte)0x1F);
        return enabled;
    }
}
