using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Graphics;

/// <summary>Owns generic framebuffer targets independently of the firmware or graphics driver that created them.</summary>
public static unsafe partial class KernelGraphics
{
    private struct DisplayRecord { internal Byte Used,Kind,PixelFormat,CanSetMode,Primary; internal UInt32 Device,Width,Height,Pitch; internal UInt64 Physical,Virtual,Bytes,Present,SetMode,GetModeCount,TryGetMode,Activate; }
    private static DisplayRecord* _records; private static KernelHeapAllocation _allocation; private static UInt32 _capacity,_count,_firmwareCount,_simpleCount,_virtioCount,_primary,_virtioDetectedPci,_virtioStartedControllers,_virtioStartFailures; private static Boolean _initialized;

    /// <summary>Initializes the heap-backed graphics display registry.</summary>
    public static Boolean Initialize()
    {if(_initialized)return true;if(!KernelHeap.IsInitialized()||!Allocate(8U,out _allocation,out _records))return false;_capacity=8U;_initialized=true;return true;}
    /// <summary>Gets whether the generic graphics subsystem is initialized.</summary>
    public static Boolean IsInitialized()=>_initialized;
}
