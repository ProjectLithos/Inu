using System;
namespace Inu.Kernel.Console;
public interface IMemoryDescriptorLayoutContext : IBootContext
{
    Boolean HasMemoryDescriptorLayout();
    UInt64 GetMemoryDescriptorSize();
    UInt32 GetMemoryDescriptorVersion();
}
