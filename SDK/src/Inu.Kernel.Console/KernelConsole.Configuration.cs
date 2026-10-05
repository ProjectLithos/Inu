using System;
using Inu.Kernel.Internal.X64;
using Inu.Text;

namespace Inu.Kernel.Console;

public static partial class KernelConsole
{
    public static Boolean TryEnableTrueTypeFont()
    {
        if (!_initialized || !HasFramebuffer()) return false;
        return _framebuffer.EnableTrueTypeFont();
    }

    /// <summary>Gets whether the graphics console is currently using the TrueType renderer.</summary>
    public static Boolean IsTrueTypeFontActive() => TrueTypeConsoleFont.IsActive();

    /// <summary>Gets the most recent TrueType graphics-console activation state.</summary>
    public static TrueTypeConsoleState GetTrueTypeConsoleState() => TrueTypeConsoleFont.State;

    /// <summary>Gets the number of embedded TrueType font bytes visible to the running kernel.</summary>
    public static UInt64 GetTrueTypeFontLength() => TrueTypeConsoleFont.FontLength;

    /// <summary>Warms a bounded number of TrueType glyph-cache entries. Intended for low-priority/idle servicing.</summary>
    public static Boolean WarmTrueTypeGlyphCache(UInt32 glyphBudget) => TrueTypeConsoleFont.WarmCacheStep(glyphBudget);

    /// <summary>Gets the number of pre-rasterised TrueType glyphs currently cached.</summary>
    public static UInt32 GetTrueTypeCachedGlyphCount() => TrueTypeConsoleFont.CachedGlyphCount;

    /// <summary>Gets the number of glyphs that required the emergency bitmap fallback after both cached and direct TrueType rendering failed.</summary>
    public static UInt32 GetTrueTypeFallbackGlyphCount() => TrueTypeConsoleFont.FallbackGlyphCount;

    /// <summary>Gets the most recent byte value that required the emergency bitmap fallback, or zero when none has.</summary>
    public static UInt32 GetTrueTypeLastFallbackGlyph() => TrueTypeConsoleFont.LastFallbackGlyph;

    /// <summary>Gets the printable-ASCII glyph-cache target count.</summary>
    public static UInt32 GetTrueTypeCacheTargetGlyphCount() => TrueTypeConsoleFont.CacheTargetGlyphCount;

    /// <summary>Gets whether the printable-ASCII TrueType glyph cache has completed background warmup.</summary>
    public static Boolean IsTrueTypeGlyphCacheWarm() => TrueTypeConsoleFont.IsCacheWarm;

    /// <summary>Gets whether framebuffer text output is available for the current boot target.</summary>
    public static Boolean HasFramebuffer() => _initialized && _framebuffer.FrameByteCount != 0UL;

    /// <summary>Configures the active serial/framebuffer targets and ANSI-control parser for the unified Console subsystem facade.</summary>
    public static Boolean ConfigureOutput(Boolean serialEnabled, Boolean framebufferEnabled, Boolean ansiEnabled)
    {
        if (!_initialized || _outputPaused) return false;
        if (framebufferEnabled && !HasFramebuffer()) return false;
        _serialEnabled = serialEnabled;
        _framebufferEnabled = framebufferEnabled;
        _ansiEnabled = ansiEnabled;
        _ansiState = 0;
        _ansiParameter = 0U;
        _ansiPrivate = false;
        if (!framebufferEnabled) _framebuffer.SetCaretEnabled(false);
        return serialEnabled || framebufferEnabled;
    }

    /// <summary>Enables or disables framebuffer presentation without changing the selected single/double/triple buffering policy or serial diagnostics.</summary>
    public static Boolean SetFramebufferPresentationEnabled(Boolean enabled)
    {
        if (!_initialized || _outputPaused) return false;
        if (enabled && !HasFramebuffer()) return false;
        _framebufferEnabled = enabled;
        if (!enabled) _framebuffer.SetCaretEnabled(false);
        return true;
    }

    /// <summary>Stops normal console output while preserving the configured console and retained state for Resume.</summary>
    public static Boolean StopOutput()
    {
        if (!_initialized || _outputPaused) return false;
        _outputPaused = true;
        return true;
    }

    /// <summary>Resumes normal console output after StopOutput.</summary>
    public static Boolean ResumeOutput()
    {
        if (!_initialized || !_outputPaused) return false;
        _outputPaused = false;
        return true;
    }

    /// <summary>Unloads the logical console subsystem. The boot context is retained by the high-level Console facade so it can be run again.</summary>
    public static unsafe Boolean Unload()
    {
        if (!_initialized) return false;
        _serialDiagnosticLine = false;
        _secondarySerialWriter = null;
        _inputService = null;
        _interactiveDeferredService = null;
        _serialEnabled = false;
        _framebufferEnabled = false;
        _outputPaused = false;
        _ansiEnabled = false;
        _ansiState = 0;
        _ansiParameter = 0U;
        _ansiPrivate = false;
        _liveViewActive = false;
        _initialized = false;
        return true;
    }

    /// <summary>Begins one structured diagnostic line that is routed to serial/debug transports only.</summary>
}
