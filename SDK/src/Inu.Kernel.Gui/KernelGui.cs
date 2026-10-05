using System;
using Inu.Kernel.Graphics;
using Inu.Kernel.Heap;
using Inu.Kernel.Ps2;
using Inu.Kernel.Security;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Synchronization;
using Inu.Usb.Hid;

namespace Inu.Kernel.Gui;

/// <summary>
/// Kernel-owned compositor and window broker. Ring-3 applications own opaque surface handles only;
/// framebuffer access, z-order, focus and input routing remain in the kernel.
/// </summary>
public static unsafe partial class KernelGui
{
    private const UInt32 MaximumSurfaces=32U, EventCapacity=128U, TitleBytes=64U;
    private const UInt32 Border=2U, TitleBar=24U, CursorWidth=10U, CursorHeight=16U;
    private struct Surface
    {
        internal UInt64 Id,OwnerPid,Pixels,PixelBytes; internal KernelHeapAllocation Allocation;
        internal Int32 X,Y; internal UInt32 Width,Height,Flags,Z; internal Byte State; internal fixed Byte Title[(Int32)TitleBytes];
    }
    private struct SurfaceStore { internal fixed Byte Bytes[(Int32)MaximumSurfaces*192]; }
    private struct EventSlot { internal UInt64 OwnerPid; internal GuiEvent Event; }
    private struct EventStore { internal fixed Byte Bytes[(Int32)EventCapacity*80]; }
#pragma warning disable CS0169
    private static SurfaceStore _surfaceStore; private static EventStore _eventStore;
#pragma warning restore CS0169
    private static Boolean _initialized,_desktopActive; private static KernelMutex _guiMutex; private static UInt64 _nextSurface=1UL,_focused,_eventHead,_eventTail,_eventSequence;
    private static UInt32 _surfaceCount,_visibleCount,_nextZ=1U;
    private static Int64 _cursorX,_cursorY,_lastMouseX,_lastMouseY,_lastUsbMouseX,_lastUsbMouseY; private static Boolean _mouseKnown,_usbMouseKnown,_lastLeft,_lastRight,_lastMiddle,_lastUsbLeft,_lastUsbRight,_lastUsbMiddle;

    public static Boolean Initialize()
    {
        if(_initialized)return true;
        if(!KernelGraphics.IsInitialized()||!KernelHeap.IsInitialized()||!KernelSystemCalls.IsInitialized()||!KernelSecurity.IsInitialized())return false;
        _initialized=true;
        if(!KernelSystemCalls.RegisterGet(KernelSystemCallMessages.GuiCapabilities,&GetCapabilitiesSyscall) ||
           !KernelSystemCalls.RegisterGet(KernelSystemCallMessages.GuiEventNext,&GetNextEventSyscall) ||
           !KernelSystemCalls.RegisterSet(KernelSystemCallMessages.GuiSurfaceCreate,&CreateSurfaceSyscall) ||
           !KernelSystemCalls.RegisterSet(KernelSystemCallMessages.GuiSurfaceDestroy,&DestroySurfaceSyscall) ||
           !KernelSystemCalls.RegisterSet(KernelSystemCallMessages.GuiSurfaceGeometry,&SetGeometrySyscall) ||
           !KernelSystemCalls.RegisterSet(KernelSystemCallMessages.GuiSurfaceVisibility,&SetVisibilitySyscall) ||
           !KernelSystemCalls.RegisterSet(KernelSystemCallMessages.GuiFocus,&SetFocusSyscall) ||
           !KernelSystemCalls.RegisterEvent(KernelSystemCallMessages.GuiSurfacePresent,&PresentSurfaceSyscall) ||
           !KernelSystemCalls.RegisterEvent(KernelSystemCallMessages.GuiWindowClose,&CloseWindowSyscall))
        {_initialized=false;return false;}
        if(KernelGraphics.TryGetPrimaryDisplay(out KernelGraphicsDisplayInfo display)){_cursorX=(Int64)(display.Framebuffer.Mode.Width/2U);_cursorY=(Int64)(display.Framebuffer.Mode.Height/2U);}
        return true;
    }
    public static Boolean IsInitialized()=>_initialized;
    public static GuiCapabilities GetCapabilities()
    {
        if(!_guiMutex.Lock())return new GuiCapabilities(false,0U,0U,0UL,0U,0U);
        UInt32 w=0U,h=0U;if(KernelGraphics.TryGetPrimaryDisplay(out KernelGraphicsDisplayInfo d)){w=d.Framebuffer.Mode.Width;h=d.Framebuffer.Mode.Height;}
        GuiCapabilities result=new(_initialized,_surfaceCount,_visibleCount,_focused,w,h);_guiMutex.Unlock();return result;
    }

    /// <summary>Routes one decoded keyboard transition to the focused GUI process. Returns true only when GUI focus consumed it.</summary>
}
