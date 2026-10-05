using System;
namespace Inu.Kernel.Console;
public interface IKernelImageContext : IBootContext
{ UInt64 GetKernelImageBase(); }
