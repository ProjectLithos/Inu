using System;

namespace Inu.Kernel.Gui;

public enum GuiSurfaceState : Byte { Free=0, Hidden=1, Visible=2 }
public enum GuiEventKind : UInt32 { None=0, FocusGained=1, FocusLost=2, KeyDown=3, KeyUp=4, PointerMove=5, PointerDown=6, PointerUp=7, CloseRequested=8 }
[Flags] public enum GuiSurfaceFlags : UInt32 { None=0, Window=1, Decorated=2, Desktop=4 }

/// <summary>Fixed-size user-visible event returned by Get(gui.event.next).</summary>
public struct GuiEvent
{
    public const UInt64 SerializedBytes=64UL;
    public UInt32 Kind;
    public UInt32 Modifiers;
    public UInt64 SurfaceId;
    public Int64 X;
    public Int64 Y;
    public UInt64 Value0;
    public UInt64 Value1;
    public UInt64 Sequence;
}

public readonly struct GuiCapabilities
{
    public GuiCapabilities(Boolean initialized,UInt32 surfaces,UInt32 visible,UInt64 focused,UInt32 width,UInt32 height)
    {Initialized=initialized;Surfaces=surfaces;VisibleSurfaces=visible;FocusedSurface=focused;DisplayWidth=width;DisplayHeight=height;}
    public Boolean Initialized{get;} public UInt32 Surfaces{get;} public UInt32 VisibleSurfaces{get;} public UInt64 FocusedSurface{get;} public UInt32 DisplayWidth{get;} public UInt32 DisplayHeight{get;}
}
