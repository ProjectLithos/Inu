using System;
using Inu.Kernel.Internal.X64;
using Inu.Text;

namespace Inu.Kernel.Console;

public static partial class KernelConsole
{
    public static Boolean ReconfigureFramebuffer(UInt64 address,UInt64 size,UInt32 width,UInt32 height,UInt32 pixelsPerScanLine,UInt32 pixelFormat)
    {if(!_initialized||_outputPaused||_liveViewActive)return false;return _framebuffer.ReconfigureFramebuffer(address,size,width,height,pixelsPerScanLine,pixelFormat);}

    /// <summary>Rebinds the text console to a 32-bit RGB/BGR framebuffer and routes completed dirty rectangles through an optional driver-owned presenter.</summary>
    public static unsafe Boolean ReconfigureFramebuffer(UInt64 address,UInt64 size,UInt32 width,UInt32 height,UInt32 pixelsPerScanLine,UInt32 pixelFormat,delegate*<UInt32,UInt32,UInt32,UInt32,Boolean> presenter)
    {if(!_initialized||_outputPaused||_liveViewActive)return false;return _framebuffer.ReconfigureFramebuffer(address,size,width,height,pixelsPerScanLine,pixelFormat,presenter);}

    /// <summary>Gets the byte count required by one complete framebuffer image.</summary>
    public static UInt64 GetFramebufferBufferByteCount()
    {
        return _initialized ? _framebuffer.FrameByteCount : 0UL;
    }

    /// <summary>Gets the active framebuffer buffering mode and available buffer count.</summary>
    public static FramebufferBufferCapabilities GetFramebufferBufferCapabilities()
    {
        FramebufferBufferMode mode = _framebuffer.BufferCount == 3U ? FramebufferBufferMode.Triple : (_framebuffer.BufferCount == 2U ? FramebufferBufferMode.Double : FramebufferBufferMode.Single);
        return new FramebufferBufferCapabilities(mode, _framebuffer.AvailableBufferCount, _framebuffer.FrameByteCount);
    }

    /// <summary>Gets the requested framebuffer buffering setting: 0 = automatic, 1 = single, 2 = double, 3 = triple.</summary>
    public static UInt32 GetFramebufferBufferSetting()
    {
        if (!_initialized) return 0U;
        return _framebuffer.AutomaticBuffering ? 0U : _framebuffer.BufferCount;
    }

    /// <summary>Gets the effective framebuffer image count currently active after resolving automatic mode.</summary>
    public static UInt32 GetFramebufferEffectiveBufferCount()
    {
        return _initialized ? _framebuffer.BufferCount : 1U;
    }

    /// <summary>Gets the number of framebuffer images that can currently be selected.</summary>
    public static UInt32 GetFramebufferAvailableBufferCount()
    {
        return _initialized ? _framebuffer.AvailableBufferCount : 1U;
    }

    /// <summary>Attaches two heap-backed framebuffer images and automatically selects the best text-console buffering mode (double buffering).</summary>
    public static Boolean ConfigureFramebufferBuffers(UInt64 backBufferA, UInt64 backBufferB, UInt64 bufferByteCount)
    {
        if (!_initialized) return false;
        return _framebuffer.ConfigureBuffers(backBufferA, backBufferB, bufferByteCount);
    }

    /// <summary>Selects automatic (0), single (1), double (2), or triple (3) framebuffer buffering. Automatic currently selects double buffering for the text console.</summary>
    public static Boolean SetFramebufferBufferCount(UInt32 bufferCount)
    {
        if (!_initialized) return false;
        return _framebuffer.SetBufferCount(bufferCount);
    }

    /// <summary>Uses a dimmer framebuffer text shade for read-only/system entries; serial output is unaffected.</summary>
    public static Boolean SetReadOnlyTextShade(Boolean enabled)
    {
        if(!_initialized)return false;
        return !_framebufferEnabled||_framebuffer.SetReadOnlyShade(enabled);
    }

    /// <summary>Begins a bounded kernel-side response capture for a userland syscall broker. While active, normal console writes are copied to the supplied kernel buffer instead of being presented directly.</summary>
    public static unsafe Boolean BeginUserlandResponseCapture(Byte* buffer, UInt32 capacity)
    {
        if(!_initialized||buffer==null||capacity==0U||_captureActive)return false;
        _captureBuffer=buffer;_captureCapacity=capacity;_captureLength=0U;_captureActive=true;return true;
    }

    /// <summary>Ends the current userland response capture and returns the number of captured bytes.</summary>
    public static unsafe Boolean EndUserlandResponseCapture(out UInt32 length)
    {
        length=0U;if(!_captureActive)return false;length=_captureLength;_captureActive=false;_captureBuffer=null;_captureCapacity=0U;_captureLength=0U;return true;
    }

    /// <summary>Cancels a response capture after an exceptional command path.</summary>
    public static unsafe void CancelUserlandResponseCapture()
    { _captureActive=false;_captureBuffer=null;_captureCapacity=0U;_captureLength=0U; }

    /// <summary>Clears the framebuffer console, retained scrollback, caret area, and redraws an empty live viewport.</summary>
}
