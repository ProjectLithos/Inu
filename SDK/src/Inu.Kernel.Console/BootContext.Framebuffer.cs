using System;

namespace Inu.Kernel.Console;

public readonly unsafe partial struct NativeBootContext : IBootFramebufferContext
{
    public UInt64 GetFramebufferAddress(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0UL:c->FramebufferAddress;}
    public UInt64 GetFramebufferSize(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0UL:c->FramebufferSize;}
    public UInt32 GetFramebufferWidth(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0U:c->Width;}
    public UInt32 GetFramebufferHeight(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0U:c->Height;}
    public UInt32 GetFramebufferPitchInPixels(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0U:c->PixelsPerScanLine;}
    public UInt32 GetFramebufferPixelFormat(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?3U:c->PixelFormat;}
    public UInt32 GetFramebufferRedMask(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0U:c->RedMask;}
    public UInt32 GetFramebufferGreenMask(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0U:c->GreenMask;}
    public UInt32 GetFramebufferBlueMask(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0U:c->BlueMask;}
    public UInt32 GetFramebufferReservedMask(){NativeBootHandoffLayout* c=GetCSharpontext();return c==null?0U:c->ReservedMask;}
}
