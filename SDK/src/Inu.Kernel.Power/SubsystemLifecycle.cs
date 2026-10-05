using System;

namespace Inu.Kernel.Power;

/// <summary>Stable public lifecycle facade for coordinated power-management services.</summary>
public static class Power
{
    private const Byte Stopped=0,Ready=1,Running=2,Paused=3,Unloaded=4; private static Byte _state; private static Boolean _observed;
    private static void Sync(){if(!_observed&&KernelPowerManagement.IsInitialized()){_state=Running;_observed=true;}}
    public static Boolean Initialize(){if(!KernelPowerManagement.Initialize())return false;_state=Ready;_observed=true;return true;} public static Byte GetLifecycleState(){Sync();return _state;}
    public static Boolean Start(){Sync();if(_state!=Ready&&_state!=Stopped)return false;_state=Running;return true;} public static Boolean Run(){Sync();if(_state==Paused)return Resume();if(_state==Running)return true;return Start();}
    public static Boolean Pause(){Sync();if(_state!=Running)return false;_state=Paused;return true;} public static Boolean Resume(){Sync();if(_state!=Paused)return false;_state=Running;return true;} public static Boolean Stop(){Sync();if(_state==Stopped||_state==Unloaded)return false;_state=Stopped;return true;} public static Boolean Unload(){Sync();if(KernelPowerManagement.GetSystemState()!=KernelSystemPowerState.Working)return false;_state=Unloaded;_observed=true;return true;}
}
