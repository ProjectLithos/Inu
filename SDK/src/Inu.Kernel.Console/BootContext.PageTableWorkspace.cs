using System;
namespace Inu.Kernel.Console;
public readonly unsafe partial struct NativeBootContext : IBootstrapPageTableWorkspaceContext
{
    public UInt64 GetBootstrapPageTableWorkspaceAddress(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0UL:c->BootstrapPageTableWorkspaceAddress;}
    public UInt64 GetBootstrapPageTableWorkspacePages(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0UL:c->BootstrapPageTableWorkspacePages;}
}
