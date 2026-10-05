using System;
namespace Inu.Kernel.Console;
public readonly unsafe partial struct NativeBootContext : IApplicationProcessorTrampolineContext
{
    public UInt64 GetApplicationProcessorTrampolineAddress(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0UL:c->ApplicationProcessorTrampolineAddress;}
    public UInt64 GetApplicationProcessorTrampolinePages(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0UL:c->ApplicationProcessorTrampolinePages;}
}
