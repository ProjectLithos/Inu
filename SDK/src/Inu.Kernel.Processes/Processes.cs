using System;

namespace Inu.Kernel.Processes;

/// <summary><inu.api>Coder-facing process-policy facade for inspecting, starting, terminating and controlling foreground processes.</inu.api></summary>
public static class Processes
{
    public static UInt32 GetActiveCount()=>KernelProcesses.GetActiveProcessCount();
    public static Boolean TryStart(UInt64 processId,UInt64 argument=0UL)=>KernelProcesses.TryStart(processId,argument);
    public static Boolean Terminate(UInt64 processId,Int64 exitCode=0L)=>KernelProcesses.TryTerminate(processId,exitCode);
    public static UInt64 GetForegroundProcessId()=>KernelProcesses.GetForegroundCommandProcessId();
    public static Boolean CancelForeground()=>KernelProcesses.RequestForegroundCommandCancellation();
    public static Boolean IsForegroundCancellationRequested()=>KernelProcesses.IsForegroundCommandCancellationRequested();

    /// <summary><inu.api>Sets the OS-owned desktop and login executable paths used by graphical-session startup.</inu.api></summary>
    public static Boolean ConfigureGraphicalSession(String desktopPath,String loginPath)=>KernelProcesses.ConfigureGraphicalSessionPaths(desktopPath,loginPath);

    /// <summary><inu.api>Starts the configured graphical session asynchronously. Desktop and login remain isolated ring-3 processes.</inu.api></summary>
    public static Boolean StartGraphicalSession()=>KernelProcesses.BeginGraphicalSessionAsync();

    /// <summary><inu.api>Reports whether graphical-session startup is currently in progress.</inu.api></summary>
    public static Boolean IsGraphicalSessionStarting()=>KernelProcesses.IsGraphicalSessionStarting();

    /// <summary><inu.api>Reports whether the graphical session is active.</inu.api></summary>
    public static Boolean IsGraphicalSessionActive()=>KernelProcesses.IsGraphicalSessionActive();

    /// <summary><inu.api>Reports whether the most recent graphical-session startup failed.</inu.api></summary>
    public static Boolean HasGraphicalSessionFailed()=>KernelProcesses.HasGraphicalSessionFailed();
}
