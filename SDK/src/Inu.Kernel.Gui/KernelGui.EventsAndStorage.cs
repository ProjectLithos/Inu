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
    private static Int64 CloseWindowSyscallUnlocked(KernelSystemCallFrame* frame){if(!Owned(frame->NativeMessage.Value0,out Surface* s))return (Int64)KernelSystemCallError.NotPermitted;GuiEvent e=default;e.Kind=(UInt32)GuiEventKind.CloseRequested;e.SurfaceId=s->Id;return Queue(s->OwnerPid,e)?0L:(Int64)KernelSystemCallError.Busy;}
    private static Int64 GetNextEventSyscallUnlocked(KernelSystemCallFrame* frame)
    {
        if(!CurrentPid(out UInt64 pid))return (Int64)KernelSystemCallError.NotPermitted;if(frame->NativeMessage.OutputCapacity<GuiEvent.SerializedBytes)return (Int64)KernelSystemCallError.InvalidArgument;
        for(UInt64 seq=_eventTail;seq<_eventHead;seq++){EventSlot* slot=EventAt((UInt32)(seq%EventCapacity));if(slot->OwnerPid!=pid)continue;GuiEvent e=slot->Event;slot->OwnerPid=0UL;while(_eventTail<_eventHead&&EventAt((UInt32)(_eventTail%EventCapacity))->OwnerPid==0UL)_eventTail++;return KernelSystemCalls.TryCopyToUser(frame->NativeMessage.OutputAddress,(UInt64)(nuint)(&e),GuiEvent.SerializedBytes)?1L:(Int64)KernelSystemCallError.Fault;}return 0L;
    }
    private static Boolean Queue(UInt64 owner,GuiEvent e){if(owner==0UL)return false;while(_eventHead-_eventTail>=EventCapacity){EventAt((UInt32)(_eventTail%EventCapacity))->OwnerPid=0UL;_eventTail++;}EventSlot* slot=EventAt((UInt32)(_eventHead%EventCapacity));e.Sequence=++_eventSequence;slot->OwnerPid=owner;slot->Event=e;_eventHead++;return true;}
    private static void QueuePointerButton(Surface* s,UInt64 button,Boolean down){if(s==null)return;GuiEvent e=default;e.Kind=(UInt32)(down?GuiEventKind.PointerDown:GuiEventKind.PointerUp);e.SurfaceId=s->Id;e.X=_cursorX-s->X;e.Y=_cursorY-s->Y;e.Value0=button;Queue(s->OwnerPid,e);}
    private static void Focus(Surface* s){if(s==null||s->State!=(Byte)GuiSurfaceState.Visible)return;if(_focused==s->Id){s->Z=_nextZ++;return;}Surface* old=Find(_focused);if(old!=null){GuiEvent lost=default;lost.Kind=(UInt32)GuiEventKind.FocusLost;lost.SurfaceId=old->Id;Queue(old->OwnerPid,lost);} _focused=s->Id;s->Z=_nextZ++;GuiEvent gained=default;gained.Kind=(UInt32)GuiEventKind.FocusGained;gained.SurfaceId=s->Id;Queue(s->OwnerPid,gained);}
    private static Boolean Destroy(Surface* s,Boolean redraw){if(s==null||s->State==0)return false;UInt64 id=s->Id;if(s->State==(Byte)GuiSurfaceState.Visible&&_visibleCount>0U)_visibleCount--;if(_focused==id)_focused=0UL;KernelHeapAllocation a=s->Allocation;Boolean released=KernelHeap.TryRelease(a);Clear((Byte*)s,sizeof(Surface));if(_surfaceCount>0U)_surfaceCount--;return released&&(!redraw||CompositeUnlocked());}
    private static Boolean Owned(UInt64 id,out Surface* s){s=Find(id);return s!=null&&CurrentPid(out UInt64 pid)&&s->OwnerPid==pid;}
    private static Boolean CurrentPid(out UInt64 pid){pid=0UL;return KernelSecurity.TryGetCurrentProcess(out pid)&&pid!=0UL;}
    private static void CopyTitle(KernelSystemCallFrame* frame,Surface* s){if(frame->NativeMessage.DataLength==0UL)return;UInt64 n=frame->NativeMessage.DataLength>TitleBytes?TitleBytes:frame->NativeMessage.DataLength;Byte* t=s->Title;KernelSystemCalls.TryCopyFromUser(frame->NativeMessage.DataAddress,(UInt64)(nuint)t,n);}
    private static Surface* HitTest(Int64 x,Int64 y){Surface* best=null;UInt32 z=0U;for(UInt32 i=0U;i<MaximumSurfaces;i++){Surface* s=At(i);if(s->State!=(Byte)GuiSurfaceState.Visible)continue;Int64 left=s->X,top=s->Y,right=left+s->Width,bottom=top+s->Height+(((s->Flags&(UInt32)GuiSurfaceFlags.Decorated)!=0U)?TitleBar:0U);if(x>=left&&x<right&&y>=top&&y<bottom&&s->Z>=z){best=s;z=s->Z;}}return best;}
    private static void DrawSurface(UInt32* dst,UInt32 pitch,UInt32 sw,UInt32 sh,Surface* s){Int32 y=s->Y;Boolean decorated=(s->Flags&(UInt32)GuiSurfaceFlags.Decorated)!=0U;if(decorated){FillRect(dst,pitch,sw,sh,s->X-(Int32)Border,y-(Int32)Border,(Int32)s->Width+(Int32)Border*2,(Int32)s->Height+(Int32)TitleBar+(Int32)Border*2,0xFF0C1118UL);FillRect(dst,pitch,sw,sh,s->X,y,(Int32)s->Width,(Int32)TitleBar,_focused==s->Id?0xFF355C8AUL:0xFF35404DUL);y+=(Int32)TitleBar;}UInt32* src=(UInt32*)(nuint)s->Pixels;for(UInt32 sy=0;sy<s->Height;sy++){Int32 dy=y+(Int32)sy;if(dy<0||dy>=(Int32)sh)continue;for(UInt32 sx=0;sx<s->Width;sx++){Int32 dx=s->X+(Int32)sx;if(dx<0||dx>=(Int32)sw)continue;dst[(UInt32)dy*pitch+(UInt32)dx]=src[sy*s->Width+sx];}}}
    private static void DrawCursor(UInt32* d,UInt32 p,UInt32 w,UInt32 h,Int32 x,Int32 y){for(UInt32 cy=0;cy<CursorHeight;cy++)for(UInt32 cx=0;cx<CursorWidth;cx++){if(cx>cy/2U+1U)continue;Int32 px=x+(Int32)cx,py=y+(Int32)cy;if(px<0||py<0||px>=(Int32)w||py>=(Int32)h)continue;d[(UInt32)py*p+(UInt32)px]=cx==0U||cx==cy/2U+1U?0xFF000000U:0xFFFFFFFFU;}}
    private static void Fill(UInt32* d,UInt32 p,UInt32 w,UInt32 h,UInt64 c){for(UInt32 y=0;y<h;y++)for(UInt32 x=0;x<w;x++)d[y*p+x]=(UInt32)c;}
    private static void FillRect(UInt32* d,UInt32 p,UInt32 sw,UInt32 sh,Int32 x,Int32 y,Int32 w,Int32 h,UInt64 c){for(Int32 yy=0;yy<h;yy++){Int32 py=y+yy;if(py<0||py>=(Int32)sh)continue;for(Int32 xx=0;xx<w;xx++){Int32 px=x+xx;if(px<0||px>=(Int32)sw)continue;d[(UInt32)py*p+(UInt32)px]=(UInt32)c;}}}
    private static Int64 Clamp(Int64 v,Int64 lo,Int64 hi)=>v<lo?lo:(v>hi?hi:v);
    private static Surface* Find(UInt64 id){if(id==0UL)return null;for(UInt32 i=0;i<MaximumSurfaces;i++){Surface* s=At(i);if(s->State!=0&&s->Id==id)return s;}return null;}
    private static Surface* Free(){for(UInt32 i=0;i<MaximumSurfaces;i++){Surface* s=At(i);if(s->State==0)return s;}return null;}
    private static Surface* At(UInt32 i){fixed(Byte* p=_surfaceStore.Bytes)return (Surface*)(p+i*192U);}
    private static EventSlot* EventAt(UInt32 i){fixed(Byte* p=_eventStore.Bytes)return (EventSlot*)(p+i*80U);}
    private static void Clear(Byte* p,Int32 n){for(Int32 i=0;i<n;i++)p[i]=0;}
}
