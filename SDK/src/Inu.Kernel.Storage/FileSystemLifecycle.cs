using System;

namespace Inu.Kernel.Storage;

/// <summary><inu.api>Coder-facing lifecycle facade for VFS/filesystem services.</inu.api></summary>
public static class FileSystem
{
    private const Byte Stopped=0,Ready=1,Running=2,Paused=3,Unloaded=4; private static Byte _state; private static Boolean _observed;
    private static void Sync(){if(!_observed&&KernelVfs.IsInitialized()){_state=Running;_observed=true;}}
    public static Boolean Initialize(){if(!KernelStorage.IsInitialized()&&!KernelStorage.Initialize())return false;if(!KernelVfs.IsInitialized())return false;_state=Ready;_observed=true;return true;}
    public static Byte GetLifecycleState(){Sync();return _state;}
    public static Boolean Start(){Sync();if(_state!=Ready&&_state!=Stopped)return false;_state=Running;return true;}
    public static Boolean Run(){Sync();if(_state==Paused)return Resume();if(_state==Running)return true;return Start();}
    public static Boolean Pause(){Sync();if(_state!=Running)return false;_state=Paused;return true;}
    public static Boolean Resume(){Sync();if(_state!=Paused)return false;_state=Running;return true;}
    public static Boolean Stop(){Sync();if(_state==Stopped||_state==Unloaded)return false;_state=Stopped;return true;}
    public static Boolean Unload(){Sync();if(KernelVfs.OpenFileCount!=0U||KernelVfs.MountCount!=0U)return false;_state=Unloaded;_observed=true;return true;}

    /// <summary><inu.api>Gets the active OS path syntax policy.</inu.api></summary>
    public static FileSystemPathPolicy GetPathPolicy()=>FileSystemPathPolicyRuntime.Current;

    /// <summary><inu.api>Applies path syntax policy. Case-sensitivity changes are refused when an already-mounted provider cannot honour the requested semantics.</inu.api></summary>
    public static Boolean SetPathPolicy(FileSystemPathPolicy policy)=>FileSystemPathPolicyRuntime.TrySet(policy);

}
/// <summary><inu.api>Spelling-compatible alias for the coder-facing FileSystem lifecycle facade.</inu.api></summary>
public static class Filesystem
{
    public static Boolean Initialize()=>FileSystem.Initialize(); public static Byte GetLifecycleState()=>FileSystem.GetLifecycleState(); public static Boolean Start()=>FileSystem.Start(); public static Boolean Run()=>FileSystem.Run(); public static Boolean Pause()=>FileSystem.Pause(); public static Boolean Resume()=>FileSystem.Resume(); public static Boolean Stop()=>FileSystem.Stop(); public static Boolean Unload()=>FileSystem.Unload(); public static FileSystemPathPolicy GetPathPolicy()=>FileSystem.GetPathPolicy(); public static Boolean SetPathPolicy(FileSystemPathPolicy policy)=>FileSystem.SetPathPolicy(policy);
}
