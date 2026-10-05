using System;
namespace Inu.Kernel.Console;
public readonly unsafe partial struct NativeBootContext : IFinalMemoryMapBufferContext
{
    public Boolean HasFinalMemoryMapBuffer()
    {
        NativeBootHandoffLayout* c = GetCSharpontext();
        if (c == null || c->FinalMemoryMapFlag != 1UL || c->ExitBootServicesStatus != 0UL) return false;
        if (c->FinalMemoryMapAddress == 0UL || c->FinalMemoryMapLength == 0UL) return false;
        return c->FinalMemoryMapAddress <= UInt64.MaxValue - c->FinalMemoryMapLength;
    }
    public UInt64 GetFinalMemoryMapAddress(){ NativeBootHandoffLayout* c=GetCSharpontext(); return c==null?0UL:c->FinalMemoryMapAddress; }
    public UInt64 GetFinalMemoryMapLength(){ NativeBootHandoffLayout* c=GetCSharpontext(); return c==null?0UL:c->FinalMemoryMapLength; }
}
