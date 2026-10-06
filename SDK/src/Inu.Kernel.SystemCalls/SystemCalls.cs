using System;

namespace Inu.Kernel.SystemCalls;

/// <summary><inu.api>Coder-facing Get/Set/Event syscall policy facade. Native Inu syscall registration remains exactly three semantic operations.</inu.api></summary>
public static unsafe class SystemCalls
{
    public static Boolean Initialize()=>KernelSystemCalls.Initialize();
    public static Boolean IsInitialized()=>KernelSystemCalls.IsInitialized();
    public static KernelSystemCallCapabilities GetCapabilities()=>KernelSystemCalls.GetCapabilities();
    public static Boolean RegisterGet(String message,delegate*<KernelSystemCallFrame*,Int64> handler)=>KernelSystemCalls.RegisterGet(message,handler);
    public static Boolean RegisterSet(String message,delegate*<KernelSystemCallFrame*,Int64> handler)=>KernelSystemCalls.RegisterSet(message,handler);
    public static Boolean RegisterEvent(String message,delegate*<KernelSystemCallFrame*,Int64> handler)=>KernelSystemCalls.RegisterEvent(message,handler);
}
