using System;
namespace Inu.Kernel.Console;
public interface IApplicationProcessorTrampolineContext : IBootContext
{ UInt64 GetApplicationProcessorTrampolineAddress(); UInt64 GetApplicationProcessorTrampolinePages(); }
