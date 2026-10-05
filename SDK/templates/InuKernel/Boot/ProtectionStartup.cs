using System;
using Inu.Kernel.Console;
using Inu.Kernel.Contracts;
using Inu.Kernel.Platform.X64;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Memory;
using Inu.Kernel.VirtualMemory;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Heap;
using Inu.Runtime.NativeAot;
using Inu.Runtime.Conformance;
using Inu.Kernel.Acpi;
using Inu.Kernel.Power;
using Inu.Kernel.Time;
using Inu.Kernel.Smp;
using Inu.Kernel.Scheduler;
using Inu.Kernel.InterruptDispatch;
using Inu.Kernel.Protection;
using Inu.Kernel.Security;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Graphics;
using Inu.Kernel.Bootstrap;

namespace Inu.Kernel.Bootstrap.Boot;

/// <summary>Initializes user/kernel protection, process security and the selected syscall registrations.</summary>
public static unsafe class ProtectionStartup
{
    public static Boolean Initialize()
    {
        if (!KernelProtection.Initialize()) return false;
        KernelProtectionCapabilities protection = KernelProtection.GetCapabilities();
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("User range: ")) return false;
        if (!KernelConsole.WriteHex(protection.MinimumUserAddress)) return false;
        if (!KernelConsole.Write(" - ")) return false;
        if (!KernelConsole.WriteHex(protection.MaximumUserAddress)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Ring 3 selectors code/data: ")) return false;
        if (!KernelConsole.WriteHex(protection.UserCodeSelector)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteHex(protection.UserDataSelector)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Supervisor protections WP/SMEP/SMAP: ")) return false;
        if (!KernelConsole.Write(protection.WriteProtectEnabled ? "on" : "off")) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.Write(protection.SmepEnabled ? "on" : (protection.SmepSupported ? "available" : "unsupported"))) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteLine(protection.SmapSupported ? "available for syscall copy guards" : "unsupported")) return false;
        if (!KernelStructuredLogging.InfoLine("protection","BootStartup.Initialize","User/kernel separation online.")) return false;
        if (!KernelSecurity.Initialize()) return false;
        if (!KernelStructuredLogging.InfoLine("security","BootStartup.Initialize","Process isolation/security policy online.")) return false;
        if (!KernelSystemCalls.Initialize()) return false;
        if (!KernelSystemCalls.RegisterGet(KernelSystemCallMessages.ConsoleFont, &GetFontPresetSyscall)) return false;
        if (!KernelSystemCalls.RegisterSet(KernelSystemCallMessages.ConsoleFont, &SetFontPresetSyscall)) return false;
        if (!KernelSystemCalls.RegisterGet(KernelSystemCallMessages.ConsoleBuffering, &GetBufferingPresetSyscall)) return false;
        if (!KernelSystemCalls.RegisterSet(KernelSystemCallMessages.ConsoleBuffering, &SetBufferingPresetSyscall)) return false;
        KernelSystemCallCapabilities systemCalls = KernelSystemCalls.GetCapabilities();
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("System calls: Get/Set/Event + Linux-style + NT-style; syscall stack: ")) return false;
        if (!KernelConsole.WriteByteSize(systemCalls.SyscallStackBytes)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("SMAP guarded user copies: ")) return false;
        if (!KernelConsole.WriteLine(systemCalls.SmapEnabled ? "enabled" : "not supported")) return false;
        if (!KernelStructuredLogging.InfoLine("syscalls","BootStartup.Initialize","System calls online.")) return false;
        return true;
    }

    private static unsafe Int64 GetFontPresetSyscall(KernelSystemCallFrame* frame) => (Int64)KernelConsole.GetFontPreset();
    private static unsafe Int64 SetFontPresetSyscall(KernelSystemCallFrame* frame) => KernelConsole.SetFontPreset((UInt32)frame->Argument0) ? 0L : (Int64)KernelSystemCallError.InvalidArgument;
    private static unsafe Int64 GetBufferingPresetSyscall(KernelSystemCallFrame* frame) => (Int64)KernelConsole.GetFramebufferBufferSetting();
    private static unsafe Int64 SetBufferingPresetSyscall(KernelSystemCallFrame* frame) => KernelConsole.SetFramebufferBufferCount((UInt32)frame->Argument0) ? 0L : (Int64)KernelSystemCallError.InvalidArgument;
}
