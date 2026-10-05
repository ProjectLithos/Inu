using System;

namespace Inu.Kernel.Console;

/// <summary>Identifies the framebuffer console output-buffering strategy.</summary>
public enum FramebufferBufferMode
{
    /// <summary>Renders directly to the firmware/QEMU scan-out framebuffer.</summary>
    Single = 1,
    /// <summary>Renders to one software backbuffer and presents it to the scan-out framebuffer.</summary>
    Double = 2,
    /// <summary>Alternates between two software backbuffers and presents completed frames to the scan-out framebuffer.</summary>
    Triple = 3
}

/// <summary>Describes framebuffer buffering capacity and the currently selected mode.</summary>
public readonly struct FramebufferBufferCapabilities
{
    internal FramebufferBufferCapabilities(FramebufferBufferMode mode, UInt32 availableBufferCount, UInt64 frameByteCount)
    {
        Mode = mode;
        AvailableBufferCount = availableBufferCount;
        FrameByteCount = frameByteCount;
    }

    /// <summary>Gets the active framebuffer buffering mode.</summary>
    public FramebufferBufferMode Mode { get; }

    /// <summary>Gets the maximum available buffer count, including the hardware scan-out framebuffer.</summary>
    public UInt32 AvailableBufferCount { get; }

    /// <summary>Gets the bytes required by one complete framebuffer image.</summary>
    public UInt64 FrameByteCount { get; }
}
