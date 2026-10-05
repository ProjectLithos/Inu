using System;
namespace Inu.Kernel.Console;
public readonly unsafe partial struct NativeBootContext : IMemoryMapCaptureDiagnosticsContext
{
    public UInt32 GetMemoryMapCaptureAttempts(){ NativeBootHandoffLayout* c=GetCSharpontext(); return c==null?0U:c->FinalMemoryMapCaptureAttempts; }
    public UInt64 GetExitBootServicesStatus(){ NativeBootHandoffLayout* c=GetCSharpontext(); return c==null?UInt64.MaxValue:c->ExitBootServicesStatus; }
}
