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

/// <summary>Starts scheduling/interrupt dispatch and seals the managed GC root map after all CPUs can participate.</summary>
public static unsafe class SchedulerRuntimeStartup
{
    public static Boolean Initialize()
    {
        // The public facade registers selected policy providers before the mechanism
        // initializer checks them. Existing author-registered providers are retained.
        if (!Inu.Kernel.Scheduler.Scheduler.Initialize())
        {
            KernelConsole.WriteLine("NOBT:FAIL:SCHEDULER-INITIALIZE");
            return false;
        }
        // The reschedule IPI must have a live managed dispatcher before Scheduler.Run wakes APs.
        if (!global::Inu.Kernel.InterruptDispatch.Interrupts.Initialize())
        {
            KernelConsole.WriteLine("NOBT:FAIL:SCHEDULER-INTERRUPTS");
            return false;
        }
        if (!Inu.Kernel.Scheduler.Scheduler.Run())
        {
            KernelConsole.WriteLine("NOBT:FAIL:SCHEDULER-RUN");
            return false;
        }

        // 0.0.77: all NativeAOT startup/static/frozen-root registration is complete by
        // this post-scheduler boundary. Seal the root map before the first real collection.
        // 0.0.77: report the actual static/root registries immediately before sealing.
        // This is diagnostic-only and does not add, remove, or retain any root.
        NativeAotGarbageCollector.TraceRootRegistrationState(0x18BUL);
        NativeAotGarbageCollector.SealRootMap();
        if (!NativeAotGarbageCollector.IsRootMapReady())
        {
            if (!KernelConsole.WriteLine("NOBT:FAIL:GC:ROOTMAP")) return false;
            return false;
        }
        if (!KernelConsole.WriteLine("NOBT:GC:ROOTMAP:OK")) return false;

        // 0.0.77 mature tracing-GC gate. It runs only after all scheduler CPUs can
        // participate in the stop-the-world rendezvous and the root map is sealed.
        if (!KernelConsole.WriteLine("NOBT:GC:RUN")) return false;
        if (!ManagedRuntimeConformance.RunGarbageCollectorChecks(out UInt32 gcPassed, out UInt32 gcFailed))
        {
            if (!KernelConsole.Write("GC conformance passed/failed: ")) return false;
            if (!KernelConsole.WriteUInt64(gcPassed)) return false;
            if (!KernelConsole.Write("/")) return false;
            if (!KernelConsole.WriteUInt64(gcFailed)) return false;
            if (!KernelConsole.WriteLine("")) return false;
            KernelStructuredLogging.CriticalLine("runtime","BootStartup.Initialize","Mature tracing-GC conformance rejected startup.");
            return false;
        }
        if (!KernelConsole.Write("GC conformance passed/failed: ")) return false;
        if (!KernelConsole.WriteUInt64(gcPassed)) return false;
        if (!KernelConsole.Write("/")) return false;
        if (!KernelConsole.WriteUInt64(gcFailed)) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelConsole.WriteLine("NOBT:GC:OK")) return false;

        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Scheduler threads active: ")) return false;
        if (!KernelConsole.WriteUInt64(KernelScheduler.GetActiveThreadCount())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Scheduler quantum: ")) return false;
        if (!KernelConsole.WriteDurationNanoseconds(KernelScheduler.GetQuantumNanoseconds())) return false;
        if (!KernelConsole.WriteLine("")) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"boot-detail","BootStartup.Initialize")) return false;
        if (!KernelConsole.Write("Timer preemption: ")) return false;
        if (!KernelConsole.WriteLine(KernelScheduler.GetCapabilities().HasTimerPreemption ? "available" : "cooperative only")) return false;
        if (!KernelStructuredLogging.InfoLine("scheduler","BootStartup.Initialize","Scheduler and threads online.")) return false;
        return true;
    }
}
