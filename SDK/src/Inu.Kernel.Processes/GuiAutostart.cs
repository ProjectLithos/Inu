using System;
using Inu.Kernel.Gui;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Smp;
using Inu.Kernel.Storage;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Processes;

public static unsafe partial class KernelProcesses
{
    private const UInt64 GraphicalSessionIdle=0UL,GraphicalSessionStarting=1UL,GraphicalSessionActive=2UL,GraphicalSessionFailed=3UL;
    private static UInt64 _desktopAutostartProcessId,_loginAutostartProcessId,_graphicalSessionState,_graphicalSessionWorkerThreadId;
    private static String _guiDesktopPath="/BIN/INU-DESKTOP.EXE",_guiLoginPath="/BIN/INU-LOGIN.EXE";

    /// <summary>Configures the OS-owned filesystem paths used for graphical-session autostart.</summary>
    public static Boolean ConfigureGraphicalSessionPaths(String desktopPath,String loginPath)
    {
        if(String.IsNullOrWhiteSpace(desktopPath)||String.IsNullOrWhiteSpace(loginPath))return false;
        if(desktopPath[0]!='/'||loginPath[0]!='/')return false;
        _guiDesktopPath=desktopPath;_guiLoginPath=loginPath;return true;
    }

    /// <summary>Queues graphical-session construction on the scheduler's GUI CPU set and returns without performing filesystem, compositor or process-load work on the caller.</summary>
    public static Boolean BeginGraphicalSessionAsync()
    {
        if(!_initialized||!KernelScheduler.IsInitialized())return false;
        UInt64 state=ReadGraphicalSessionState();
        if(state==GraphicalSessionStarting||state==GraphicalSessionActive)return true;
        if(!StoreGraphicalSessionState(GraphicalSessionStarting))return false;
        KernelCpuSet gui=KernelSmp.GetRoleCpuSet(KernelCpuRole.Gui);UInt64 guiMask=gui.Cpu0To63;
        if(guiMask==0UL)guiMask=1UL;
        if(!KernelScheduler.TryCreateOneShotThread(&GraphicalSessionAutostartWorker,KernelThreadPriority.Normal,guiMask,131072UL,out UInt64 worker))
        {
            StoreGraphicalSessionState(GraphicalSessionFailed);return false;
        }
        _graphicalSessionWorkerThreadId=worker;
        return true;
    }

    /// <summary>Compatibility entrypoint. Graphical autostart is asynchronous from 0.1.6 onward.</summary>
    public static Boolean TryAutostartGraphicalSession()=>BeginGraphicalSessionAsync();

    public static Boolean IsGraphicalSessionStarting()=>ReadGraphicalSessionState()==GraphicalSessionStarting;
    public static Boolean IsGraphicalSessionActive()=>ReadGraphicalSessionState()==GraphicalSessionActive;
    public static Boolean HasGraphicalSessionFailed()=>ReadGraphicalSessionState()==GraphicalSessionFailed;
    public static UInt64 GetGraphicalSessionWorkerThreadId()=>_graphicalSessionWorkerThreadId;

    private static void GraphicalSessionAutostartWorker()
    {
        Boolean ok=StartGraphicalSessionNow();
        StoreGraphicalSessionState(ok?GraphicalSessionActive:GraphicalSessionFailed);
    }

    private static Boolean StartGraphicalSessionNow()
    {
        if(!GraphicalSessionFileSystemReady())return false;
        if(!KernelGui.IsInitialized()&&!KernelGui.Initialize())return false;
        KernelMountNamespaceHandle ns=KernelVfs.DefaultNamespace;
        if(String.IsNullOrEmpty(_guiDesktopPath)||String.IsNullOrEmpty(_guiLoginPath))return false;
        if(!TryCreateFromFile(ns,_guiDesktopPath,KernelProcessOwnership.Background,out KernelProcessInfo desktop))return false;
        if(!TryCreateFromFile(ns,_guiLoginPath,KernelProcessOwnership.Background,out KernelProcessInfo login)){TryTerminate(desktop.Id,-1L);return false;}
        _desktopAutostartProcessId=desktop.Id;_loginAutostartProcessId=login.Id;
        KernelCpuSet gui=KernelSmp.GetRoleCpuSet(KernelCpuRole.Gui);UInt64 guiMask=gui.Cpu0To63;
        KernelCpuSet user=KernelSmp.GetRoleCpuSet(KernelCpuRole.Userland);UInt64 userMask=user.Cpu0To63;
        if(guiMask==0UL)guiMask=1UL;if(userMask==0UL)userMask=guiMask;
        // Desktop and Login are independent isolated ring-3 processes. Once both images are ready,
        // schedule their entry independently so distinct eligible CPUs can run them concurrently.
        if(!KernelScheduler.TryCreateOneShotThread(&StartDesktopAutostart,KernelThreadPriority.Normal,guiMask,131072UL,out _)){TryTerminate(login.Id,-1L);TryTerminate(desktop.Id,-1L);return false;}
        if(!KernelScheduler.TryCreateOneShotThread(&StartLoginAutostart,KernelThreadPriority.Normal,userMask,131072UL,out _)){TryTerminate(login.Id,-1L);TryTerminate(desktop.Id,-1L);return false;}
        return true;
    }

    private static UInt64 ReadGraphicalSessionState()
    {
        UInt64 state=GraphicalSessionIdle;fixed(UInt64* p=&_graphicalSessionState)Native.AtomicLoad64(p,&state);return state;
    }

    private static Boolean StoreGraphicalSessionState(UInt64 state)
    {
        fixed(UInt64* p=&_graphicalSessionState)return Native.AtomicStore64(p,state);
    }

    private static Boolean GraphicalSessionFileSystemReady()
    {
        if(!KernelVfs.IsInitialized())return false;
        Byte* root=stackalloc Byte[1];root[0]=(Byte)'/';
        if(!KernelVfs.OpenDirectoryAscii(KernelVfs.DefaultNamespace,root,1U,out KernelDirectoryHandle directory))return false;
        return KernelVfs.CloseDirectory(directory);
    }

    private static void StartDesktopAutostart(){UInt64 id=_desktopAutostartProcessId;if(id!=0UL)TryStart(id,0UL);}
    private static void StartLoginAutostart(){UInt64 id=_loginAutostartProcessId;if(id!=0UL)TryStart(id,0UL);}
}
