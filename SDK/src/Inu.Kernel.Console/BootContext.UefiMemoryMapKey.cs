using System;
namespace Inu.Kernel.Console;
public readonly unsafe partial struct NativeBootContext : IUefiMemoryMapKeyContext
{
    public UInt64 GetUefiMemoryMapKey(){ NativeBootHandoffLayout* c=GetCSharpontext(); return c==null?0UL:c->FinalMemoryMapKey; }
}
