using System;

namespace Inu.Kernel.Interrupts;

/// <summary><inu.api>Coder-facing interrupt lifecycle facade. Vector tables and dispatch implementation remain internal to the selected interrupt component.</inu.api></summary>
public static class Interrupts
{
    public static Boolean Initialize()=>global::Inu.Kernel.InterruptDispatch.Interrupts.Initialize();
    public static Boolean IsInitialized()=>global::Inu.Kernel.InterruptDispatch.Interrupts.IsInitialized();
    public static Byte GetLifecycleState()=>global::Inu.Kernel.InterruptDispatch.Interrupts.GetLifecycleState();
    public static Boolean Start()=>global::Inu.Kernel.InterruptDispatch.Interrupts.Start();
    public static Boolean Run()=>global::Inu.Kernel.InterruptDispatch.Interrupts.Run();
    public static Boolean Pause()=>global::Inu.Kernel.InterruptDispatch.Interrupts.Pause();
    public static Boolean Resume()=>global::Inu.Kernel.InterruptDispatch.Interrupts.Resume();
    public static Boolean Stop()=>global::Inu.Kernel.InterruptDispatch.Interrupts.Stop();
    public static Boolean Wait()=>global::Inu.Kernel.InterruptDispatch.Interrupts.Wait();
}
