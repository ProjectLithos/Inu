using System;
using System.Runtime;
using Inu.Kernel.Heap;
using Inu.Kernel.Smp;
using Inu.Kernel.Time;
using Inu.Kernel.Internal.X64;
using Inu.Runtime.NativeAot;

namespace Inu.Kernel.Scheduler;

public static unsafe partial class KernelScheduler
{
    private static Boolean WaitForApplicationProcessorSchedulersReady()
    {
        if(_processorCount<=1U)return true;
#if DEBUG
        DebugApSchedulerTrace("BSP waiting for AP managed schedulers; online CPUs = ", KernelSmp.GetOnlineProcessorCount());
#endif
        if(!KernelTime.TryCreateDeadline(ApplicationProcessorSchedulerReadyTimeoutNanoseconds,out UInt64 deadline))return false;
        UInt32 bootstrap=KernelSmp.GetBootstrapProcessorIndex();
#if DEBUG
        UInt32 diagnosticPass=0U;
#endif
        for(;;)
        {
            Boolean allReady=true;
            UInt32 waitingCpu=NoProcessor;
            for(UInt32 cpu=0U;cpu<_processorCount;cpu++)
            {
                if(cpu==bootstrap)continue;
                if(!KernelSmp.TryGetProcessor(cpu,out KernelProcessorState processor))return false;
                if(processor.StartupState!=KernelProcessorStartupState.OnlineParked)continue;
                if(!KernelSmp.IsProcessorSchedulerReady(cpu)){allReady=false;waitingCpu=cpu;break;}
            }
            if(allReady)
            {
#if DEBUG
                DebugApSchedulerTrace("all native-online AP managed schedulers READY; count = ", KernelSmp.GetSchedulerReadyProcessorCount());
#endif
                return true;
            }
            if(KernelTime.HasReached(deadline))
            {
#if DEBUG
                DebugApSchedulerTrace("AP scheduler-ready wait TIMEOUT; waiting CPU = ", waitingCpu);
                DebugApSchedulerTrace("scheduler-ready processor count = ", KernelSmp.GetSchedulerReadyProcessorCount());
                DebugApSchedulerTrace("native-online processor count = ", KernelSmp.GetOnlineProcessorCount());
#endif
                return false;
            }
#if DEBUG
            if((diagnosticPass++&0x3FFU)==0U)DebugApSchedulerTrace("waking AP schedulers; first not-ready CPU = ", waitingCpu);
#endif
            WakeApplicationProcessors();
            for(UInt32 spin=0U;spin<1024U;spin++)Native.Pause();
        }
    }

    private static void WakeProcessor(UInt32 cpu)
    { if(!_initialized||cpu>=_processorCount||cpu==KernelSmp.GetBootstrapProcessorIndex())return;KernelSmp.TrySendIpi(cpu,KernelIpiPurpose.Reschedule); }
    private static void WakeApplicationProcessors(){UInt32 bootstrap=KernelSmp.GetBootstrapProcessorIndex();for(UInt32 cpu=0U;cpu<_processorCount;cpu++)if(cpu!=bootstrap)KernelSmp.TrySendIpi(cpu,KernelIpiPurpose.Reschedule);}
}
