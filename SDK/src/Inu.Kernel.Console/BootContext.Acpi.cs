using System;
namespace Inu.Kernel.Console;
public readonly unsafe partial struct NativeBootContext : IAcpiRootPointerContext
{
    public UInt64 GetAcpiRootPointerAddress(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0UL:c->AcpiRootPointerAddress;}
}
