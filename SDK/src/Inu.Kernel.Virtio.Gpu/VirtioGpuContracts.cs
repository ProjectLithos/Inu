using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Graphics;

namespace Inu.Kernel.Virtio.Gpu;

/// <summary>Describes one started VirtIO GPU controller and its scan-out.</summary>
public readonly struct VirtioGpuInfo
{
    public VirtioGpuInfo(KernelDeviceHandle device,KernelGraphicsDisplayHandle display,KernelGraphicsMode mode,UInt32 scanout,Boolean started){Device=device;Display=display;Mode=mode;Scanout=scanout;Started=started;}
    public KernelDeviceHandle Device { get; }
    public KernelGraphicsDisplayHandle Display { get; }
    public KernelGraphicsMode Mode { get; }
    public UInt32 Scanout { get; }
    public Boolean Started { get; }
}
/// <summary>Summarizes discovered and started VirtIO GPU controllers.</summary>
public readonly struct VirtioGpuCapabilities
{
    public VirtioGpuCapabilities(Boolean initialized,UInt32 controllers,UInt32 displays,Boolean modeChanges,Boolean twoDimensionalResources){Initialized=initialized;Controllers=controllers;Displays=displays;ModeChanges=modeChanges;TwoDimensionalResources=twoDimensionalResources;}
    public Boolean Initialized { get; }
    public UInt32 Controllers { get; }
    public UInt32 Displays { get; }
    public Boolean ModeChanges { get; }
    public Boolean TwoDimensionalResources { get; }
}
