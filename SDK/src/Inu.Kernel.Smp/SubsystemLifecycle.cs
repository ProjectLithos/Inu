using System;
using Inu.Kernel.Console;

namespace Inu.Kernel.Smp;

/// <summary><inu.api>Coder-facing lifecycle facade for SMP services. The bootstrap processor is never unloaded.</inu.api></summary>
public static class Smp
{
    private const Byte Stopped=0,Ready=1,Running=2,Paused=3,Unloaded=4; private static Byte _state; private static Boolean _observed;
    private static void Sync(){if(!_observed&&KernelSmp.IsInitialized()){_state=Running;_observed=true;}}
    public static Boolean Initialize<TBoot>(TBoot boot) where TBoot : IApplicationProcessorTrampolineContext{if(!KernelSmp.Initialize(boot))return false;_state=Ready;_observed=true;return true;} public static Byte GetLifecycleState(){Sync();return _state;}
    public static Boolean Start(){Sync();if(_state!=Ready&&_state!=Stopped)return false;_state=Running;return true;} public static Boolean Run(){Sync();if(_state==Paused)return Resume();if(_state==Running)return true;return Start();}
    public static Boolean Pause(){Sync();if(_state!=Running)return false;_state=Paused;return true;} public static Boolean Resume(){Sync();if(_state!=Paused)return false;_state=Running;return true;} public static Boolean Stop(){Sync();if(_state==Stopped||_state==Unloaded)return false;_state=Stopped;return true;}
    public static Boolean Unload(){Sync();if(!KernelSmp.IsInitialized())return false;UInt32 bootstrap=KernelSmp.GetBootstrapProcessorIndex();UInt32 count=KernelSmp.GetProcessorCount();Boolean ok=true;for(UInt32 i=0U;i<count;i++){if(i==bootstrap)continue;if(!KernelSmp.TryShutdownProcessor(i))ok=false;}if(ok){_state=Unloaded;_observed=true;}return ok;}

    /// <summary><inu.api>Assigns one execution role to a set of logical CPUs.</inu.api></summary>
    public static Boolean SetRole(KernelCpuRole role,KernelCpuSet processors)=>KernelSmp.SetRoleCpuSet(role,processors);

    /// <summary><inu.api>Assigns one execution role to a single logical CPU.</inu.api></summary>
    public static Boolean SetRole(KernelCpuRole role,UInt32 processor)=>KernelSmp.SetRoleCpuSet(role,KernelCpuSet.Single(processor));

    /// <summary><inu.api>Gets the current CPU set assigned to an execution role.</inu.api></summary>
    public static KernelCpuSet GetRole(KernelCpuRole role)=>KernelSmp.GetRoleCpuSet(role);

    /// <summary><inu.api>Gets the number of processors discovered by Inu SMP.</inu.api></summary>
    public static UInt32 GetProcessorCount()=>KernelSmp.GetProcessorCount();
}
