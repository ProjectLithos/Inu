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
    private static Int64 GetCapabilitiesSyscallUnlocked(KernelSystemCallFrame* frame)
    {UInt32 cw=0U,ch=0U;if(KernelGraphics.TryGetPrimaryDisplay(out KernelGraphicsDisplayInfo cd)){cw=cd.Framebuffer.Mode.Width;ch=cd.Framebuffer.Mode.Height;}GuiCapabilities c=new(_initialized,_surfaceCount,_visibleCount,_focused,cw,ch);if(frame->NativeMessage.OutputCapacity>=32UL){UInt64* o=stackalloc UInt64[4];o[0]=((UInt64)c.DisplayHeight<<32)|c.DisplayWidth;o[1]=((UInt64)c.VisibleSurfaces<<32)|c.Surfaces;o[2]=c.FocusedSurface;o[3]=1UL;if(!KernelSystemCalls.TryCopyToUser(frame->NativeMessage.OutputAddress,(UInt64)(nuint)o,32UL))return (Int64)KernelSystemCallError.Fault;}return unchecked((Int64)(((UInt64)c.DisplayHeight<<32)|c.DisplayWidth));}
    private static Int64 CreateSurfaceSyscallUnlocked(KernelSystemCallFrame* frame)
    {
        if(!CurrentPid(out UInt64 pid))return (Int64)KernelSystemCallError.NotPermitted;UInt32 w=(UInt32)frame->NativeMessage.Value0,h=(UInt32)frame->NativeMessage.Value1,flags=(UInt32)frame->NativeMessage.Value2;if(w<32U||h<24U||w>4096U||h>4096U)return (Int64)KernelSystemCallError.InvalidArgument;
        UInt64 bytes=(UInt64)w*(UInt64)h*4UL;if(bytes/4UL!=(UInt64)w*(UInt64)h)return (Int64)KernelSystemCallError.InvalidArgument;Surface* s=Free();if(s==null)return (Int64)KernelSystemCallError.Busy;if(!KernelHeap.TryAllocate(bytes,64UL,true,out KernelHeapAllocation a))return (Int64)KernelSystemCallError.Fault;
        s->Id=_nextSurface++;s->OwnerPid=pid;s->Pixels=a.Address;s->PixelBytes=bytes;s->Allocation=a;s->Width=w;s->Height=h;s->Flags=flags;s->X=40+(Int32)(_surfaceCount*24U);s->Y=40+(Int32)(_surfaceCount*20U);s->Z=_nextZ++;s->State=(Byte)GuiSurfaceState.Hidden;CopyTitle(frame,s);_surfaceCount++;return unchecked((Int64)s->Id);
    }
    private static Int64 DestroySurfaceSyscallUnlocked(KernelSystemCallFrame* frame){if(!Owned(frame->NativeMessage.Value0,out Surface* s))return (Int64)KernelSystemCallError.NotPermitted;return Destroy(s,true)?0L:(Int64)KernelSystemCallError.Fault;}
    private static Int64 SetGeometrySyscallUnlocked(KernelSystemCallFrame* frame){if(!Owned(frame->NativeMessage.Value0,out Surface* s))return (Int64)KernelSystemCallError.NotPermitted;s->X=unchecked((Int32)(UInt32)frame->NativeMessage.Value1);s->Y=unchecked((Int32)(UInt32)frame->NativeMessage.Value2);return CompositeUnlocked()?0L:(Int64)KernelSystemCallError.Fault;}
    private static Int64 SetVisibilitySyscallUnlocked(KernelSystemCallFrame* frame){if(!Owned(frame->NativeMessage.Value0,out Surface* s))return (Int64)KernelSystemCallError.NotPermitted;Boolean show=frame->NativeMessage.Value1!=0UL;if(show&&s->State!=(Byte)GuiSurfaceState.Visible){_desktopActive=true;s->State=(Byte)GuiSurfaceState.Visible;Boolean desktop=(s->Flags&(UInt32)GuiSurfaceFlags.Desktop)!=0U;s->Z=desktop?0U:_nextZ++;_visibleCount++;if(!desktop)Focus(s);}else if(!show&&s->State==(Byte)GuiSurfaceState.Visible){s->State=(Byte)GuiSurfaceState.Hidden;if(_visibleCount>0U)_visibleCount--;if(_focused==s->Id)_focused=0UL;}return CompositeUnlocked()?0L:(Int64)KernelSystemCallError.Fault;}
    private static Int64 SetFocusSyscallUnlocked(KernelSystemCallFrame* frame){if(!Owned(frame->NativeMessage.Value0,out Surface* s)||s->State!=(Byte)GuiSurfaceState.Visible)return (Int64)KernelSystemCallError.NotPermitted;Focus(s);return CompositeUnlocked()?0L:(Int64)KernelSystemCallError.Fault;}
    private static Int64 PresentSurfaceSyscallUnlocked(KernelSystemCallFrame* frame)
    {
        if(!Owned(frame->NativeMessage.Value0,out Surface* s))return (Int64)KernelSystemCallError.NotPermitted;
        UInt32 sourceWidth=(UInt32)frame->NativeMessage.Value1,sourceHeight=(UInt32)frame->NativeMessage.Value2;
        if(sourceWidth==0U||sourceHeight==0U)
        {
            if(frame->NativeMessage.DataLength!=s->PixelBytes)return (Int64)KernelSystemCallError.InvalidArgument;
            if(!KernelSystemCalls.TryCopyFromUser(frame->NativeMessage.DataAddress,s->Pixels,s->PixelBytes))return (Int64)KernelSystemCallError.Fault;
        }
        else
        {
            UInt64 sourceBytes=(UInt64)sourceWidth*(UInt64)sourceHeight*4UL;
            if(sourceWidth>4096U||sourceHeight>4096U||sourceBytes/4UL!=(UInt64)sourceWidth*(UInt64)sourceHeight||frame->NativeMessage.DataLength!=sourceBytes)return (Int64)KernelSystemCallError.InvalidArgument;
            if(!KernelHeap.TryAllocate(sourceBytes,64UL,false,out KernelHeapAllocation source))return (Int64)KernelSystemCallError.Fault;
            Boolean copied=KernelSystemCalls.TryCopyFromUser(frame->NativeMessage.DataAddress,source.Address,sourceBytes);
            if(copied)ScaleCover((UInt32*)(nuint)source.Address,sourceWidth,sourceHeight,(UInt32*)(nuint)s->Pixels,s->Width,s->Height);
            Boolean released=KernelHeap.TryRelease(source);if(!copied||!released)return (Int64)KernelSystemCallError.Fault;
        }
        return CompositeUnlocked()?unchecked((Int64)s->PixelBytes):(Int64)KernelSystemCallError.Fault;
    }
    private static Int64 WithGuiLock(KernelSystemCallFrame* frame, delegate*<KernelSystemCallFrame*,Int64> action)
    {
        if(!_guiMutex.Lock())return (Int64)KernelSystemCallError.Busy;
        Int64 result=action(frame);
        _guiMutex.Unlock();
        return result;
    }
    private static Int64 GetCapabilitiesSyscall(KernelSystemCallFrame* frame)=>WithGuiLock(frame,&GetCapabilitiesSyscallUnlocked);
    private static Int64 CreateSurfaceSyscall(KernelSystemCallFrame* frame)=>WithGuiLock(frame,&CreateSurfaceSyscallUnlocked);
    private static Int64 DestroySurfaceSyscall(KernelSystemCallFrame* frame)=>WithGuiLock(frame,&DestroySurfaceSyscallUnlocked);
    private static Int64 SetGeometrySyscall(KernelSystemCallFrame* frame)=>WithGuiLock(frame,&SetGeometrySyscallUnlocked);
    private static Int64 SetVisibilitySyscall(KernelSystemCallFrame* frame)=>WithGuiLock(frame,&SetVisibilitySyscallUnlocked);
    private static Int64 SetFocusSyscall(KernelSystemCallFrame* frame)=>WithGuiLock(frame,&SetFocusSyscallUnlocked);
    private static Int64 PresentSurfaceSyscall(KernelSystemCallFrame* frame)=>WithGuiLock(frame,&PresentSurfaceSyscallUnlocked);
    private static Int64 CloseWindowSyscall(KernelSystemCallFrame* frame)=>WithGuiLock(frame,&CloseWindowSyscallUnlocked);
    private static Int64 GetNextEventSyscall(KernelSystemCallFrame* frame)=>WithGuiLock(frame,&GetNextEventSyscallUnlocked);

}
