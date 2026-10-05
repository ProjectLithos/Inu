using System;
namespace Inu.Kernel.Console;
public interface IBootFramebufferContext : IBootContext
{
    UInt64 GetFramebufferAddress(); UInt64 GetFramebufferSize(); UInt32 GetFramebufferWidth(); UInt32 GetFramebufferHeight(); UInt32 GetFramebufferPitchInPixels(); UInt32 GetFramebufferPixelFormat(); UInt32 GetFramebufferRedMask(); UInt32 GetFramebufferGreenMask(); UInt32 GetFramebufferBlueMask(); UInt32 GetFramebufferReservedMask();
}
