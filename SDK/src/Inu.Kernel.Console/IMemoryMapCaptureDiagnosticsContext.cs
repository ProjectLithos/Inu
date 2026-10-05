using System;
namespace Inu.Kernel.Console;
public interface IMemoryMapCaptureDiagnosticsContext : IBootContext
{
    UInt32 GetMemoryMapCaptureAttempts();
    UInt64 GetExitBootServicesStatus();
}
