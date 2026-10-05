using System;
namespace Inu.Kernel.Console;
public interface IAcpiRootPointerContext : IBootContext
{ UInt64 GetAcpiRootPointerAddress(); }
