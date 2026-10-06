using System;
using System.Runtime;
using Inu.Kernel.Heap;
using Inu.Kernel.Smp;
using Inu.Kernel.Time;
using Inu.Kernel.Internal.X64;
using Inu.Runtime.NativeAot;

namespace Inu.Kernel.Scheduler;

/// <summary><inu.api>Coder-facing scheduler lifecycle facade. Use Scheduler.Run/Pause/Resume/Stop instead of depending on scheduler internals.</inu.api></summary>
public static class Scheduler
{
    public static Boolean Initialize()
    {
#if INU_COMPONENT_SCHEDULER_PRIORITY_POLICY
        if(!KernelSchedulingPolicyServices.IsRegistered && !KernelPrioritySchedulingPolicy.Register())return false;
#endif
#if INU_COMPONENT_SCHEDULER_LOAD_AWARE_PLACEMENT
        if(!KernelThreadPlacementServices.IsRegistered && !KernelLoadAwarePlacementPolicy.Register())return false;
#endif
        return KernelScheduler.Initialize();
    }
    public static Boolean IsInitialized()=>KernelScheduler.IsInitialized();
    public static Byte GetLifecycleState()=>KernelScheduler.GetLifecycleState();
    public static Boolean Start()=>KernelScheduler.Start();
    public static Boolean Run()=>KernelScheduler.Run();
    public static Boolean Pause()=>KernelScheduler.Pause();
    public static Boolean Resume()=>KernelScheduler.Resume();
    public static Boolean Stop()=>KernelScheduler.Stop();
}
