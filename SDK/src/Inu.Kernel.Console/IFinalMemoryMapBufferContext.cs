using System;
namespace Inu.Kernel.Console;
public interface IFinalMemoryMapBufferContext : IBootContext
{
    Boolean HasFinalMemoryMapBuffer();
    UInt64 GetFinalMemoryMapAddress();
    UInt64 GetFinalMemoryMapLength();
}
