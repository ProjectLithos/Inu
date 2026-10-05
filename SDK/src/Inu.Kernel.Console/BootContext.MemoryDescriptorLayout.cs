using System;
namespace Inu.Kernel.Console;
public readonly unsafe partial struct NativeBootContext : IMemoryDescriptorLayoutContext
{
    public Boolean HasMemoryDescriptorLayout()
    {
        NativeBootHandoffLayout* c = GetCSharpontext();
        return c != null && c->FinalMemoryDescriptorSize >= 40UL && c->FinalMemoryMapLength >= c->FinalMemoryDescriptorSize;
    }
    public UInt64 GetMemoryDescriptorSize(){ NativeBootHandoffLayout* c=GetCSharpontext(); return c==null?0UL:c->FinalMemoryDescriptorSize; }
    public UInt32 GetMemoryDescriptorVersion(){ NativeBootHandoffLayout* c=GetCSharpontext(); return c==null?0U:c->FinalMemoryDescriptorVersion; }
}
