using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Graphics;

/// <summary>Owns generic framebuffer targets independently of the firmware or graphics driver that created them.</summary>
public static unsafe partial class KernelGraphics
{
    public static Boolean SetMode(KernelGraphicsDisplayHandle handle,KernelGraphicsMode mode)
    {if(!mode.IsValid||!TryRecord(handle,out DisplayRecord* r)||r->CanSetMode==0||r->SetMode==0)return false;delegate*<KernelGraphicsDisplayHandle,KernelGraphicsMode,Boolean> callback=(delegate*<KernelGraphicsDisplayHandle,KernelGraphicsMode,Boolean>)(void*)r->SetMode;return callback(handle,mode);}
    /// <summary>Gets the number of modes explicitly advertised by a display driver. A fixed framebuffer advertises its current mode only.</summary>
    public static UInt32 GetModeCount(KernelGraphicsDisplayHandle handle)
    {if(!TryRecord(handle,out DisplayRecord* r))return 0U;if(r->GetModeCount==0UL)return 1U;delegate*<KernelGraphicsDisplayHandle,UInt32> callback=(delegate*<KernelGraphicsDisplayHandle,UInt32>)(void*)r->GetModeCount;return callback(handle);}
    /// <summary>Gets one driver-advertised mode by zero-based index.</summary>
    public static Boolean TryGetMode(KernelGraphicsDisplayHandle handle,UInt32 index,out KernelGraphicsMode mode)
    {mode=default;if(!TryRecord(handle,out DisplayRecord* r))return false;if(r->TryGetMode==0UL){if(index!=0U)return false;mode=new KernelGraphicsMode(r->Width,r->Height,r->Pitch,(KernelGraphicsPixelFormat)r->PixelFormat);return true;}KernelGraphicsMode value=default;delegate*<KernelGraphicsDisplayHandle,UInt32,KernelGraphicsMode*,Boolean> callback=(delegate*<KernelGraphicsDisplayHandle,UInt32,KernelGraphicsMode*,Boolean>)(void*)r->TryGetMode;if(!callback(handle,index,&value))return false;mode=value;return true;}
    /// <summary>Presents a modified rectangle from the current framebuffer to the display scan-out.</summary>
    public static Boolean Present(KernelGraphicsDisplayHandle handle,UInt32 x,UInt32 y,UInt32 width,UInt32 height)
    {if(!TryRecord(handle,out DisplayRecord* r)||r->Present==0||width==0U||height==0U||x>=r->Width||y>=r->Height||width>r->Width-x||height>r->Height-y)return false;delegate*<KernelGraphicsDisplayHandle,UInt32,UInt32,UInt32,UInt32,Boolean> callback=(delegate*<KernelGraphicsDisplayHandle,UInt32,UInt32,UInt32,UInt32,Boolean>)(void*)r->Present;return callback(handle,x,y,width,height);}
    /// <summary>Activates a prepared driver-owned display after its framebuffer has been populated. Fixed/direct framebuffers require no activation.</summary>
    public static Boolean ActivateDisplay(KernelGraphicsDisplayHandle handle)
    {if(!TryRecord(handle,out DisplayRecord* r))return false;if(r->Activate==0UL)return true;delegate*<KernelGraphicsDisplayHandle,Boolean> callback=(delegate*<KernelGraphicsDisplayHandle,Boolean>)(void*)r->Activate;return callback(handle);}
    /// <summary>Selects any registered display as the generic primary graphics target.</summary>
    public static Boolean SetPrimaryDisplay(KernelGraphicsDisplayHandle handle)
    {
        if(!TryRecord(handle,out DisplayRecord* r))return false;
        if(_primary!=0U&&TryRecord(new KernelGraphicsDisplayHandle(_primary),out DisplayRecord* old))old->Primary=0;
        r->Primary=1;
        _primary=handle.Value;
        return true;
    }
}
