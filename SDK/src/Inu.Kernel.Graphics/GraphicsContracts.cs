using System;
using Inu.Kernel.Drivers;

namespace Inu.Kernel.Graphics;

/// <summary>Identifies the implementation that owns a graphics display target.</summary>
public enum KernelGraphicsTargetKind : Byte { Unknown=0, FirmwareFramebuffer=1, VirtioGpu=2, SimpleFramebuffer=3 }
/// <summary>Identifies the packed pixel layout used by a graphics framebuffer.</summary>
public enum KernelGraphicsPixelFormat : Byte { Unknown=0, BlueGreenRedReserved8=1, RedGreenBlueReserved8=2, DirectBitMask32=3 }
/// <summary>Opaque handle for a registered kernel graphics display.</summary>
public readonly struct KernelGraphicsDisplayHandle { public KernelGraphicsDisplayHandle(UInt32 value){Value=value;} public UInt32 Value { get; } }
/// <summary>Describes one display mode in pixels.</summary>
public readonly struct KernelGraphicsMode
{
    public KernelGraphicsMode(UInt32 width,UInt32 height,UInt32 pixelsPerScanLine,KernelGraphicsPixelFormat pixelFormat){Width=width;Height=height;PixelsPerScanLine=pixelsPerScanLine;PixelFormat=pixelFormat;}
    public UInt32 Width { get; }
    public UInt32 Height { get; }
    public UInt32 PixelsPerScanLine { get; }
    public KernelGraphicsPixelFormat PixelFormat { get; }
    public UInt64 RequiredBytes => (UInt64)PixelsPerScanLine*Height*4UL;
    public Boolean IsValid => Width!=0U&&Height!=0U&&PixelsPerScanLine>=Width&&PixelFormat!=KernelGraphicsPixelFormat.Unknown&&RequiredBytes/4UL/PixelsPerScanLine==Height;
}
/// <summary>Describes the CPU-visible storage behind a display framebuffer.</summary>
public readonly struct KernelGraphicsFramebuffer
{
    public KernelGraphicsFramebuffer(UInt64 physicalAddress,UInt64 virtualAddress,UInt64 byteLength,KernelGraphicsMode mode){PhysicalAddress=physicalAddress;VirtualAddress=virtualAddress;ByteLength=byteLength;Mode=mode;}
    public UInt64 PhysicalAddress { get; }
    public UInt64 VirtualAddress { get; }
    public UInt64 ByteLength { get; }
    public KernelGraphicsMode Mode { get; }
    public Boolean IsValid => VirtualAddress!=0UL&&ByteLength>=Mode.RequiredBytes&&Mode.IsValid;
}
/// <summary>Reports one registered display and its current scan-out framebuffer.</summary>
public readonly struct KernelGraphicsDisplayInfo
{
    public KernelGraphicsDisplayInfo(KernelGraphicsDisplayHandle handle,KernelDeviceHandle device,KernelGraphicsTargetKind kind,KernelGraphicsFramebuffer framebuffer,Boolean canSetMode,Boolean primary){Handle=handle;Device=device;Kind=kind;Framebuffer=framebuffer;CanSetMode=canSetMode;Primary=primary;}
    public KernelGraphicsDisplayHandle Handle { get; }
    public KernelDeviceHandle Device { get; }
    public KernelGraphicsTargetKind Kind { get; }
    public KernelGraphicsFramebuffer Framebuffer { get; }
    public Boolean CanSetMode { get; }
    public Boolean Primary { get; }
}
/// <summary>Summarizes the generic graphics subsystem.</summary>
public readonly struct KernelGraphicsCapabilities
{
    /// <summary>Creates a graphics capability snapshot. This compatibility constructor reports no separately registered simple framebuffers.</summary>
    public KernelGraphicsCapabilities(Boolean initialized,UInt32 displays,UInt32 firmwareFramebuffers,UInt32 virtioGpus,KernelGraphicsDisplayHandle primary):this(initialized,displays,firmwareFramebuffers,0U,virtioGpus,primary){}
    /// <summary>Creates a graphics capability snapshot including explicit simple-linear-framebuffer targets.</summary>
    public KernelGraphicsCapabilities(Boolean initialized,UInt32 displays,UInt32 firmwareFramebuffers,UInt32 simpleFramebuffers,UInt32 virtioGpus,KernelGraphicsDisplayHandle primary):this(initialized,displays,firmwareFramebuffers,simpleFramebuffers,virtioGpus,primary,0U,0U,0U){}
    /// <summary>Creates a graphics capability snapshot including driver-discovery lifecycle diagnostics.</summary>
    public KernelGraphicsCapabilities(Boolean initialized,UInt32 displays,UInt32 firmwareFramebuffers,UInt32 simpleFramebuffers,UInt32 virtioGpus,KernelGraphicsDisplayHandle primary,UInt32 virtioGpuDetectedPciDevices,UInt32 virtioGpuStartedControllers,UInt32 virtioGpuStartFailures){Initialized=initialized;Displays=displays;FirmwareFramebuffers=firmwareFramebuffers;SimpleFramebuffers=simpleFramebuffers;VirtioGpus=virtioGpus;Primary=primary;VirtioGpuDetectedPciDevices=virtioGpuDetectedPciDevices;VirtioGpuStartedControllers=virtioGpuStartedControllers;VirtioGpuStartFailures=virtioGpuStartFailures;}
    public Boolean Initialized { get; }
    public UInt32 Displays { get; }
    public UInt32 FirmwareFramebuffers { get; }
    public UInt32 SimpleFramebuffers { get; }
    public UInt32 VirtioGpus { get; }
    public UInt32 VirtioGpuDetectedPciDevices { get; }
    public UInt32 VirtioGpuStartedControllers { get; }
    public UInt32 VirtioGpuStartFailures { get; }
    public KernelGraphicsDisplayHandle Primary { get; }
}
/// <summary>Driver callbacks used by the graphics core for presentation and mode changes.</summary>
public readonly unsafe struct KernelGraphicsCallbacks
{
    public readonly delegate*<KernelGraphicsDisplayHandle,UInt32,UInt32,UInt32,UInt32,Boolean> Present;
    public readonly delegate*<KernelGraphicsDisplayHandle,KernelGraphicsMode,Boolean> SetMode;
    public readonly delegate*<KernelGraphicsDisplayHandle,UInt32> GetModeCount;
    public readonly delegate*<KernelGraphicsDisplayHandle,UInt32,KernelGraphicsMode*,Boolean> TryGetMode;
    public readonly delegate*<KernelGraphicsDisplayHandle,Boolean> Activate;
    public KernelGraphicsCallbacks(delegate*<KernelGraphicsDisplayHandle,UInt32,UInt32,UInt32,UInt32,Boolean> present,delegate*<KernelGraphicsDisplayHandle,KernelGraphicsMode,Boolean> setMode):this(present,setMode,null,null,null){}
    public KernelGraphicsCallbacks(delegate*<KernelGraphicsDisplayHandle,UInt32,UInt32,UInt32,UInt32,Boolean> present,delegate*<KernelGraphicsDisplayHandle,KernelGraphicsMode,Boolean> setMode,delegate*<KernelGraphicsDisplayHandle,UInt32> getModeCount,delegate*<KernelGraphicsDisplayHandle,UInt32,KernelGraphicsMode*,Boolean> tryGetMode):this(present,setMode,getModeCount,tryGetMode,null){}
    public KernelGraphicsCallbacks(delegate*<KernelGraphicsDisplayHandle,UInt32,UInt32,UInt32,UInt32,Boolean> present,delegate*<KernelGraphicsDisplayHandle,KernelGraphicsMode,Boolean> setMode,delegate*<KernelGraphicsDisplayHandle,UInt32> getModeCount,delegate*<KernelGraphicsDisplayHandle,UInt32,KernelGraphicsMode*,Boolean> tryGetMode,delegate*<KernelGraphicsDisplayHandle,Boolean> activate){Present=present;SetMode=setMode;GetModeCount=getModeCount;TryGetMode=tryGetMode;Activate=activate;}
}
