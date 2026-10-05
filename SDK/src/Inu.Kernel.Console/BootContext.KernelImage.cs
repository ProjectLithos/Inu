using System;
namespace Inu.Kernel.Console;
public readonly unsafe partial struct NativeBootContext : IKernelImageContext
{
    public UInt64 GetKernelImageBase(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0UL:c->KernelImageBase;}
}
