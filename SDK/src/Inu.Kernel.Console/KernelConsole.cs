using System;
using Inu.Kernel.Internal.X64;
using Inu.Text;

namespace Inu.Kernel.Console;

/// <summary>Describes the current TrueType graphics-console activation state.</summary>
public enum TrueTypeConsoleState : UInt32
{
    Inactive = 0,
    FontMissing = 1,
    FontInvalid = 2,
    MetricsUnavailable = 3,
    ScratchUnavailable = 4,
    Active = 5,
    RenderFallback = 6
}

/// <summary>Provides normal managed C# console output for a freestanding Inu kernel.</summary>
public static partial class KernelConsole
{
    private const UInt32 DefaultFontSize = BitmapFont.DefaultFontSize;
    private static FramebufferConsole _framebuffer;
    private static Boolean _initialized;
    private static unsafe delegate*<Byte, Boolean> _secondarySerialWriter;
    private static Boolean _serialDiagnosticLine;
    private static Boolean _serialEnabled = true;
    private static Boolean _framebufferEnabled = true;
    private static Boolean _outputPaused;
    private static Boolean _ansiEnabled;
    private static Byte _ansiState;
    private static UInt32 _ansiParameter;
    private static Boolean _ansiPrivate;
    private static Boolean _liveViewActive;
    private static unsafe Byte* _captureBuffer;
    private static UInt32 _captureCapacity;
    private static UInt32 _captureLength;
    private static Boolean _captureActive;
    private static UInt64 _presentationGate;
    private static UInt64 _presentationOwner;
    private static UInt32 _presentationDepth;

    /// <summary>Gets the exact glyph height used by the framebuffer renderer, in pixels.</summary>
    public static UInt32 FontSize
    {
        get { return _framebuffer.FontSize; }
    }

    /// <summary>Gets the number of text columns currently available in the framebuffer console.</summary>
    public static UInt32 GetTextColumnCount() => _initialized && HasFramebuffer() ? _framebuffer.TextColumnCount : 80U;

    /// <summary>Initializes serial output and a 16-pixel framebuffer font.</summary>
    public static Boolean Initialize<TBoot>(TBoot boot) where TBoot : IBootFramebufferContext
    {
        return Initialize(boot, DefaultFontSize);
    }
    /// <summary>Initializes only the bootstrap COM1 console without touching GOP, assets, or framebuffer memory.</summary>
    public static Boolean InitializeSerialOnly()
    {
        if (_initialized) return true;
        if (!Native.InitializeSerial()) return false;
        _initialized = true;
        _serialEnabled = true;
        _framebufferEnabled = false;
        _outputPaused = false;
        _ansiEnabled = false;
        _ansiState = 0;
        _ansiParameter = 0U;
        _ansiPrivate = false;
        _liveViewActive = false;
        _presentationGate=0UL;_presentationOwner=0UL;_presentationDepth=0U;
        return true;
    }

    /// <summary>Probes and attaches the firmware framebuffer after serial bootstrap has succeeded.</summary>
    public static Boolean TryInitializeFramebuffer<TBoot>(TBoot boot) where TBoot : IBootFramebufferContext
    {
        if (!_initialized || _outputPaused) return false;
        if (!_framebuffer.Initialize(boot, DefaultFontSize)) return false;
        return true;
    }


    /// <summary>Initializes serial and framebuffer output with an exact rendered font size.</summary>
    public static Boolean Initialize<TBoot>(TBoot boot, UInt32 fontSize) where TBoot : IBootFramebufferContext
    {
        // COM1 is the non-destructive bootstrap console and must be established first.
        // System assets and GOP are optional capabilities; neither may prevent serial boot.
        if (!Native.InitializeSerial()) return false;
        Boolean framebufferReady = _framebuffer.Initialize(boot, fontSize);
        // Do not clear the firmware scan-out during probing. The framebuffer becomes an
        // active output target only after ConfigureOutput commits a usable console path.
        _initialized = true;
        _serialEnabled = true;
        _framebufferEnabled = framebufferReady;
        _outputPaused = false;
        _ansiEnabled = false;
        _ansiState = 0;
        _ansiParameter = 0U;
        _ansiPrivate = false;
        _liveViewActive = false;
        _presentationGate=0UL;_presentationOwner=0UL;_presentationDepth=0U;
        return true;
    }

    /// <summary>Begins one cross-CPU console presentation transaction. Nested calls on the owning CPU are safe.</summary>
}
