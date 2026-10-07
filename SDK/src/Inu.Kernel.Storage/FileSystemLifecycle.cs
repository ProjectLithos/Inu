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

    /// <summary><inu.api>Maps a stable logical filesystem purpose to the OS author's concrete absolute path.</inu.api></summary>
    public static Boolean SetLogicalPath(FileSystemLogicalPath kind,String path)=>FileSystemLogicalPaths.TrySet(kind,path);

    /// <summary><inu.api>Gets a logical filesystem path using the currently selected external separator, or an empty string when it is unset.</inu.api></summary>
    public static String GetLogicalPath(FileSystemLogicalPath kind)=>FileSystemLogicalPaths.External(kind);

    /// <summary><inu.api>Removes a logical filesystem-path mapping.</inu.api></summary>
    public static Boolean ClearLogicalPath(FileSystemLogicalPath kind)=>FileSystemLogicalPaths.Clear(kind);

    /// <summary><inu.api>Gets the active external path separator.</inu.api></summary>
    public static Char GetPathSeparator()=>FileSystemPathPolicyRuntime.Current.Separator;

    /// <summary><inu.api>Changes only the external path separator while preserving the rest of the active path policy.</inu.api></summary>
    public static Boolean SetPathSeparator(Char separator)
    {
        FileSystemPathPolicy current=FileSystemPathPolicyRuntime.Current;
        return FileSystemPathPolicyRuntime.TrySet(new FileSystemPathPolicy(separator,current.CaseSensitive,current.MaximumComponentLength,current.AllowSpaces,current.AllowNumbers,current.InvalidCharacters));
    }

    /// <summary><inu.api>Gets the first configured command search path, or an empty string if none is configured.</inu.api></summary>
    public static String GetCommandsPath()=>FileSystemLogicalPaths.ExternalCommand(0U);

    /// <summary><inu.api>Gets all configured command search paths in search order.</inu.api></summary>
    public static String[] GetCommandsPaths()
    {
        UInt32 count=FileSystemLogicalPaths.CommandCount;String[] result=new String[(Int32)count];
        for(UInt32 i=0U;i<count;i++)result[(Int32)i]=FileSystemLogicalPaths.ExternalCommand(i);
        return result;
    }

    /// <summary><inu.api>Replaces the command search path list with one location.</inu.api></summary>
    public static Boolean SetCommandsPath(String path)=>FileSystemLogicalPaths.TrySetCommands(new[]{path});

    /// <summary><inu.api>Replaces the command search path list. Paths are searched in the supplied order.</inu.api></summary>
    public static Boolean SetCommandsPaths(String[] paths)=>FileSystemLogicalPaths.TrySetCommands(paths);

}
/// <summary><inu.api>Spelling-compatible alias for the coder-facing FileSystem lifecycle facade.</inu.api></summary>
public static class Filesystem
{
    public static Boolean Initialize()=>FileSystem.Initialize(); public static Byte GetLifecycleState()=>FileSystem.GetLifecycleState(); public static Boolean Start()=>FileSystem.Start(); public static Boolean Run()=>FileSystem.Run(); public static Boolean Pause()=>FileSystem.Pause(); public static Boolean Resume()=>FileSystem.Resume(); public static Boolean Stop()=>FileSystem.Stop(); public static Boolean Unload()=>FileSystem.Unload(); public static FileSystemPathPolicy GetPathPolicy()=>FileSystem.GetPathPolicy(); public static Boolean SetPathPolicy(FileSystemPathPolicy policy)=>FileSystem.SetPathPolicy(policy); public static Boolean SetLogicalPath(FileSystemLogicalPath kind,String path)=>FileSystem.SetLogicalPath(kind,path); public static String GetLogicalPath(FileSystemLogicalPath kind)=>FileSystem.GetLogicalPath(kind); public static Boolean ClearLogicalPath(FileSystemLogicalPath kind)=>FileSystem.ClearLogicalPath(kind); public static Char GetPathSeparator()=>FileSystem.GetPathSeparator(); public static Boolean SetPathSeparator(Char separator)=>FileSystem.SetPathSeparator(separator); public static String GetCommandsPath()=>FileSystem.GetCommandsPath(); public static String[] GetCommandsPaths()=>FileSystem.GetCommandsPaths(); public static Boolean SetCommandsPath(String path)=>FileSystem.SetCommandsPath(path); public static Boolean SetCommandsPaths(String[] paths)=>FileSystem.SetCommandsPaths(paths);
}
