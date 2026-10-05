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
    public static Boolean HandleKeyboardEvent(Ps2KeyboardEvent input)
        =>HandleKeyboard((UInt64)input.Key,input.Character,input.Pressed,(UInt32)((input.Shift?1U:0U)|(input.Control?2U:0U)|(input.Alt?4U:0U)|(input.CapsLock?8U:0U)));

    /// <summary>Routes a device-neutral keyboard transition to the focused GUI process.</summary>
    private static Boolean HandleKeyboardUnlocked(UInt64 key,Char character,Boolean pressed,UInt32 modifiers)
    {
        if(!_initialized||!_desktopActive||_focused==0UL)return false;Surface* s=Find(_focused);if(s==null||s->State!=(Byte)GuiSurfaceState.Visible)return false;
        GuiEvent e=default;e.Kind=(UInt32)(pressed?GuiEventKind.KeyDown:GuiEventKind.KeyUp);e.SurfaceId=s->Id;e.Value0=key;e.Value1=(UInt64)character;e.Modifiers=modifiers;return Queue(s->OwnerPid,e);
    }

    /// <summary>Polls the PS/2 pointer, performs hit-testing/focus routing and redraws the cursor when it changes.</summary>
    private static Boolean ServiceInputUnlocked()
    {
        if(!_initialized||!_desktopActive)return true;Boolean changed=false;
        Ps2Capabilities pc=KernelPs2.GetCapabilities();if(pc.Mouse){Ps2MouseState m=KernelPs2.GetMouseState();if(!_mouseKnown){_lastMouseX=m.X;_lastMouseY=m.Y;_lastLeft=m.LeftButton;_lastRight=m.RightButton;_lastMiddle=m.MiddleButton;_mouseKnown=true;}else{Int64 dx=m.X-_lastMouseX,dy=m.Y-_lastMouseY;_lastMouseX=m.X;_lastMouseY=m.Y;changed=RoutePointerDelta(dx,dy,m.LeftButton,m.RightButton,m.MiddleButton,ref _lastLeft,ref _lastRight,ref _lastMiddle)||changed;}}
        UsbHidCapabilities uc=UsbHid.GetCapabilities();if(uc.Initialized&&uc.Mice!=0U){UsbHidMouseState m=UsbHid.GetMouseState();Boolean l=(m.Buttons&1U)!=0U,r=(m.Buttons&2U)!=0U,mid=(m.Buttons&4U)!=0U;if(!_usbMouseKnown){_lastUsbMouseX=m.X;_lastUsbMouseY=m.Y;_lastUsbLeft=l;_lastUsbRight=r;_lastUsbMiddle=mid;_usbMouseKnown=true;}else{Int64 dx=m.X-_lastUsbMouseX,dy=m.Y-_lastUsbMouseY;_lastUsbMouseX=m.X;_lastUsbMouseY=m.Y;changed=RoutePointerDelta(dx,dy,l,r,mid,ref _lastUsbLeft,ref _lastUsbRight,ref _lastUsbMiddle)||changed;}}
        return !changed||CompositeUnlocked();
    }

    private static Boolean RoutePointerDelta(Int64 dx,Int64 dy,Boolean left,Boolean right,Boolean middle,ref Boolean oldLeft,ref Boolean oldRight,ref Boolean oldMiddle)
    {
        Boolean changed=dx!=0L||dy!=0L||left!=oldLeft||right!=oldRight||middle!=oldMiddle;if(!changed)return false;
        if(KernelGraphics.TryGetPrimaryDisplay(out KernelGraphicsDisplayInfo d)){Int64 maxX=(Int64)(d.Framebuffer.Mode.Width==0U?0U:d.Framebuffer.Mode.Width-1U),maxY=(Int64)(d.Framebuffer.Mode.Height==0U?0U:d.Framebuffer.Mode.Height-1U);_cursorX=Clamp(_cursorX+dx,0L,maxX);_cursorY=Clamp(_cursorY+dy,0L,maxY);}
        Surface* hit=HitTest(_cursorX,_cursorY);if(hit!=null&&(dx!=0L||dy!=0L)){GuiEvent move=default;move.Kind=(UInt32)GuiEventKind.PointerMove;move.SurfaceId=hit->Id;move.X=_cursorX-hit->X;move.Y=_cursorY-hit->Y;Queue(hit->OwnerPid,move);}
        if(left!=oldLeft){if(left&&hit!=null)Focus(hit);QueuePointerButton(hit,0U,left);oldLeft=left;}if(right!=oldRight){QueuePointerButton(hit,1U,right);oldRight=right;}if(middle!=oldMiddle){QueuePointerButton(hit,2U,middle);oldMiddle=middle;}return true;
    }
    /// <summary>Releases every GUI object owned by a process during process teardown.</summary>
    private static void ReleaseProcessUnlocked(UInt64 processId)
    {
        if(!_initialized||processId==0UL)return;for(UInt32 i=0U;i<MaximumSurfaces;i++){Surface* s=At(i);if(s->State==0||s->OwnerPid!=processId)continue;Destroy(s,false);}if(_focused!=0UL){Surface* f=Find(_focused);if(f==null||f->OwnerPid==processId)_focused=0UL;}CompositeUnlocked();
    }

    public static Boolean HandleKeyboard(UInt64 key,Char character,Boolean pressed,UInt32 modifiers)
    {
        if(!_guiMutex.Lock())return false;Boolean result=HandleKeyboardUnlocked(key,character,pressed,modifiers);_guiMutex.Unlock();return result;
    }
    /// <summary>Consumes one decoded PS/2 mouse state transition without polling the device from the compositor.</summary>
    public static Boolean HandlePs2Mouse(Ps2MouseState m)
    {
        if(!_guiMutex.Lock())return false;
        Boolean result=true;
        if(!_mouseKnown){_lastMouseX=m.X;_lastMouseY=m.Y;_lastLeft=m.LeftButton;_lastRight=m.RightButton;_lastMiddle=m.MiddleButton;_mouseKnown=true;}
        else{Int64 dx=m.X-_lastMouseX,dy=m.Y-_lastMouseY;_lastMouseX=m.X;_lastMouseY=m.Y;Boolean changed=RoutePointerDelta(dx,dy,m.LeftButton,m.RightButton,m.MiddleButton,ref _lastLeft,ref _lastRight,ref _lastMiddle);if(changed)result=CompositeUnlocked();}
        _guiMutex.Unlock();return result;
    }
    /// <summary>Consumes one decoded USB HID mouse state transition without polling the device from the compositor.</summary>
    public static Boolean HandleUsbMouse(UsbHidMouseState m)
    {
        if(!_guiMutex.Lock())return false;
        Boolean l=(m.Buttons&1U)!=0U,r=(m.Buttons&2U)!=0U,mid=(m.Buttons&4U)!=0U;Boolean result=true;
        if(!_usbMouseKnown){_lastUsbMouseX=m.X;_lastUsbMouseY=m.Y;_lastUsbLeft=l;_lastUsbRight=r;_lastUsbMiddle=mid;_usbMouseKnown=true;}
        else{Int64 dx=m.X-_lastUsbMouseX,dy=m.Y-_lastUsbMouseY;_lastUsbMouseX=m.X;_lastUsbMouseY=m.Y;Boolean changed=RoutePointerDelta(dx,dy,l,r,mid,ref _lastUsbLeft,ref _lastUsbRight,ref _lastUsbMiddle);if(changed)result=CompositeUnlocked();}
        _guiMutex.Unlock();return result;
    }
    public static Boolean ServiceInput()
    {
        if(!_guiMutex.Lock())return false;Boolean result=ServiceInputUnlocked();_guiMutex.Unlock();return result;
    }
    public static void ReleaseProcess(UInt64 processId)
    {
        if(!_guiMutex.Lock())return;ReleaseProcessUnlocked(processId);_guiMutex.Unlock();
    }

    /// <summary>Composites desktop, windows and cursor into the active CPU-visible framebuffer.</summary>
}
