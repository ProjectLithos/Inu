using System;
namespace Inu.Kernel.Console;
public interface IUefiMemoryMapKeyContext : IBootContext
{
    UInt64 GetUefiMemoryMapKey();
}
