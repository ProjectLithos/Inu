using System;
using Inu.Kernel.Graphics;
using Inu.Kernel.Heap;
using Inu.Kernel.Ps2;
using Inu.Kernel.Security;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Synchronization;
using Inu.Usb.Hid;

namespace Inu.Kernel.Gui;

public static unsafe partial class KernelGui
{
    private static Boolean CompositeUnlocked()
    {
        if(!_initialized)return false;if(!_desktopActive)return true;if(!KernelGraphics.TryGetPrimaryDisplay(out KernelGraphicsDisplayInfo d))return false;KernelGraphicsFramebuffer fb=d.Framebuffer;if(!fb.IsValid||fb.VirtualAddress==0UL)return false;
        UInt32* dst=(UInt32*)(nuint)fb.VirtualAddress;UInt32 pitch=fb.Mode.PixelsPerScanLine,w=fb.Mode.Width,h=fb.Mode.Height;
        Fill(dst,pitch,w,h,0xFF18202AUL);if(h>36U)FillRect(dst,pitch,w,h,0,(Int32)h-36,(Int32)w,36,0xFF252F3BUL);
        for(UInt32 i=0U;i<MaximumSurfaces;i++){Surface* s=At(i);if(s->State==(Byte)GuiSurfaceState.Visible&&(s->Flags&(UInt32)GuiSurfaceFlags.Desktop)!=0U)DrawSurface(dst,pitch,w,h,s);}
        for(UInt32 z=1U;z<_nextZ;z++)for(UInt32 i=0U;i<MaximumSurfaces;i++){Surface* s=At(i);if(s->State!=(Byte)GuiSurfaceState.Visible||s->Z!=z||(s->Flags&(UInt32)GuiSurfaceFlags.Desktop)!=0U)continue;DrawSurface(dst,pitch,w,h,s);}
        DrawCursor(dst,pitch,w,h,(Int32)_cursorX,(Int32)_cursorY);return KernelGraphics.Present(d.Handle,0U,0U,w,h);
    }

}
