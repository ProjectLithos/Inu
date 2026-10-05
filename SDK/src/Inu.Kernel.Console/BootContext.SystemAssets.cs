using System;
namespace Inu.Kernel.Console;
public readonly unsafe partial struct NativeBootContext : ISystemAssetBundleContext
{
    public UInt64 GetSystemAssetBundleAddress(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0UL:c->SystemAssetBundleAddress;}
    public UInt64 GetSystemAssetBundleLength(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0UL:c->SystemAssetBundleLength;}
    public Boolean HasSystemAssetBundle(){UInt64 address=GetSystemAssetBundleAddress(),length=GetSystemAssetBundleLength();return address!=0UL&&length>=16UL&&address<=UInt64.MaxValue-length;}
}
