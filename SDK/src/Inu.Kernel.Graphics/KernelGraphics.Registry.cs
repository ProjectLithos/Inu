using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Graphics;

/// <summary>Owns generic framebuffer targets independently of the firmware or graphics driver that created them.</summary>
public static unsafe partial class KernelGraphics
{
    public static KernelGraphicsCapabilities GetCapabilities()=>new(_initialized,_count,_firmwareCount,_simpleCount,_virtioCount,new KernelGraphicsDisplayHandle(_primary),_virtioDetectedPci,_virtioStartedControllers,_virtioStartFailures);
    /// <summary>Publishes driver-neutral VirtIO-GPU discovery/start diagnostics for status tools.</summary>
    public static void ReportVirtioGpuDriverState(UInt32 detectedPciDevices,UInt32 startedControllers,UInt32 startFailures){_virtioDetectedPci=detectedPciDevices;_virtioStartedControllers=startedControllers;_virtioStartFailures=startFailures;}

    /// <summary>Registers a CPU-visible framebuffer supplied by firmware, a simple framebuffer source, or a graphics driver.</summary>
    public static Boolean RegisterDisplay(KernelDeviceHandle device,KernelGraphicsTargetKind kind,KernelGraphicsFramebuffer framebuffer,KernelGraphicsCallbacks callbacks,Boolean canSetMode,Boolean makePrimary,out KernelGraphicsDisplayHandle handle)
    {
        handle=default;if(!_initialized||kind==KernelGraphicsTargetKind.Unknown||!framebuffer.IsValid||callbacks.Present==null||(canSetMode&&callbacks.SetMode==null))return false;
        Int32 slot=Free();if(slot<0){if(!Grow())return false;slot=Free();if(slot<0)return false;}DisplayRecord* r=_records+slot;r->Used=1;r->Kind=(Byte)kind;r->PixelFormat=(Byte)framebuffer.Mode.PixelFormat;r->CanSetMode=canSetMode?(Byte)1:(Byte)0;r->Device=device.Value;r->Width=framebuffer.Mode.Width;r->Height=framebuffer.Mode.Height;r->Pitch=framebuffer.Mode.PixelsPerScanLine;r->Physical=framebuffer.PhysicalAddress;r->Virtual=framebuffer.VirtualAddress;r->Bytes=framebuffer.ByteLength;r->Present=(UInt64)(void*)callbacks.Present;r->SetMode=(UInt64)(void*)callbacks.SetMode;r->GetModeCount=(UInt64)(void*)callbacks.GetModeCount;r->TryGetMode=(UInt64)(void*)callbacks.TryGetMode;r->Activate=(UInt64)(void*)callbacks.Activate;handle=new KernelGraphicsDisplayHandle((UInt32)slot+1U);_count++;if(kind==KernelGraphicsTargetKind.FirmwareFramebuffer)_firmwareCount++;else if(kind==KernelGraphicsTargetKind.SimpleFramebuffer)_simpleCount++;else if(kind==KernelGraphicsTargetKind.VirtioGpu)_virtioCount++;if(_primary==0U||makePrimary){if(_primary!=0U&&TryRecord(new KernelGraphicsDisplayHandle(_primary),out DisplayRecord* old))old->Primary=0;r->Primary=1;_primary=handle.Value;}return true;
    }

    /// <summary>Unregisters a driver-owned display and selects another live display as primary when required.</summary>
    public static Boolean UnregisterDisplay(KernelGraphicsDisplayHandle handle)
    {if(!TryRecord(handle,out DisplayRecord* r))return false;KernelGraphicsTargetKind kind=(KernelGraphicsTargetKind)r->Kind;Boolean wasPrimary=r->Primary!=0;Clear((Byte*)r,sizeof(DisplayRecord));if(_count>0U)_count--;if(kind==KernelGraphicsTargetKind.FirmwareFramebuffer&&_firmwareCount>0U)_firmwareCount--;else if(kind==KernelGraphicsTargetKind.SimpleFramebuffer&&_simpleCount>0U)_simpleCount--;else if(kind==KernelGraphicsTargetKind.VirtioGpu&&_virtioCount>0U)_virtioCount--;if(wasPrimary){_primary=0U;for(UInt32 i=0;i<_capacity;i++){DisplayRecord* candidate=_records+i;if(candidate->Used==0)continue;candidate->Primary=1;_primary=i+1U;break;}}return true;}

    /// <summary>Updates framebuffer metadata after a driver-owned mode change.</summary>
    public static Boolean UpdateFramebuffer(KernelGraphicsDisplayHandle handle,KernelGraphicsFramebuffer framebuffer)
    {if(!framebuffer.IsValid||!TryRecord(handle,out DisplayRecord* r))return false;r->PixelFormat=(Byte)framebuffer.Mode.PixelFormat;r->Width=framebuffer.Mode.Width;r->Height=framebuffer.Mode.Height;r->Pitch=framebuffer.Mode.PixelsPerScanLine;r->Physical=framebuffer.PhysicalAddress;r->Virtual=framebuffer.VirtualAddress;r->Bytes=framebuffer.ByteLength;return true;}
    /// <summary>Gets one registered display by zero-based enumeration index.</summary>
    public static Boolean TryGetDisplay(UInt32 index,out KernelGraphicsDisplayInfo info)
    {info=default;if(!_initialized||index>=_count)return false;UInt32 found=0;for(UInt32 i=0;i<_capacity;i++){DisplayRecord* r=_records+i;if(r->Used==0)continue;if(found++==index){info=Info(i,r);return true;}}return false;}
    /// <summary>Gets display information for an opaque handle.</summary>
    public static Boolean TryGetDisplay(KernelGraphicsDisplayHandle handle,out KernelGraphicsDisplayInfo info)
    {info=default;if(!TryRecord(handle,out DisplayRecord* r))return false;info=Info(handle.Value-1U,r);return true;}
    /// <summary>Gets the primary graphics target.</summary>
    public static Boolean TryGetPrimaryDisplay(out KernelGraphicsDisplayInfo info)=>TryGetDisplay(new KernelGraphicsDisplayHandle(_primary),out info);
}
