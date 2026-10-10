using System;

namespace Inu.Kernel.Console;

internal unsafe partial struct FramebufferConsole
{
    internal const UInt32 SmallFontSize = 8U;
    internal const UInt32 MediumFontSize = 16U;
    internal const UInt32 LargeFontSize = 24U;
    internal const UInt32 ScrollbackCapacity = 262144U;
    internal const UInt32 ScrollbarWidth = 10U;
    internal const UInt32 CaretBlinkTicks = 500U;
    internal const UInt32 MaxDirtyRegions = 64U;
    // Retained-history control byte used only by the framebuffer to remember a word-boundary soft wrap.
    // It is never emitted to serial output or rendered as a glyph.
    internal const Byte SoftWrapMarker = 0xFF;

    private UInt64 _address;
    private delegate*<UInt32,UInt32,UInt32,UInt32,Boolean> _presenter;
    private UInt64 _size;
    private UInt32 _width;
    private UInt32 _height;
    private UInt32 _pitch;
    private UInt32 _pixelFormat;
    private UInt32 _redMask;
    private UInt32 _greenMask;
    private UInt32 _blueMask;
    private UInt32 _cursorX;
    private UInt32 _cursorY;
    private UInt32 _fontSize;
    private UInt32 _glyphWidth;
    private UInt32 _characterAdvance;
    private UInt32 _lineHeight;
    private UInt32 _margin;
    private UInt32 _foreground;
    private UInt32 _background;
    private UInt32 _foregroundRgb;
    private UInt32 _backgroundRgb;
    private UInt32 _historyStart;
    private UInt32 _historyLength;
    private UInt32 _scrollLinesFromBottom;
    private UInt64 _backBufferA;
    private UInt64 _backBufferB;
    private UInt64 _drawBuffer;
    private UInt64 _frameByteCount;
    private UInt64 _bufferStorageBytes;
    private UInt32 _availableBufferCount;
    private UInt32 _bufferCount;
    private Boolean _automaticBuffering;
    private Boolean _batchUpdate;
    private Boolean _liveView;
    private Boolean _liveFrameBatch;
    private Boolean _liveFrameDirty;
    private UInt32 _liveDirtyLeft;
    private UInt32 _liveDirtyTop;
    private UInt32 _liveDirtyRight;
    private UInt32 _liveDirtyBottom;
    private Boolean _dirty;
    private Boolean _caretEnabled;
    private Boolean _caretVisible;
    private Boolean _caretActive;
    private UInt32 _caretMode;
    private UInt32 _caretHeightPercent;
    private UInt32 _caretTicks;
    private UInt32 _dirtyLeft;
    private UInt32 _dirtyTop;
    private UInt32 _dirtyRight;
    private UInt32 _dirtyBottom;
    private UInt32 _dirtyRegionCount;
    private fixed UInt32 _dirtyRegions[256];
    private fixed Byte _history[(Int32)ScrollbackCapacity];

    internal UInt32 FontSize
    {
        get { return _fontSize; }
    }

    internal UInt32 TextColumnCount
    {
        get { return GetColumnCount(); }
    }

    internal UInt32 ScrollLinesFromBottom
    {
        get { return _scrollLinesFromBottom; }
    }

    internal UInt64 FrameByteCount
    {
        get { return _frameByteCount; }
    }

    internal UInt32 BufferCount
    {
        get { return _bufferCount; }
    }

    internal UInt32 AvailableBufferCount
    {
        get { return _availableBufferCount; }
    }

    internal Boolean AutomaticBuffering
    {
        get { return _automaticBuffering; }
    }

    

    

    

    

    

    

    

    // Live views are framebuffer-only transient dashboards. They do not append to shell
    // scrollback, so a fast monitor cannot flood history or the serial/debug transport.
    

    // Starts one buffered live-dashboard frame. Rows are rendered into the active software
    // back buffer and collected as independent dirty rectangles. Nothing is copied to GOP
    // scan-out until EndLiveFrame, so double/triple buffering performs one coherent present.
    

    

    

    // Updates exactly one row of a live dashboard. During a live frame the row is drawn only
    // into the back buffer and its rectangle is queued; outside a frame it remains usable as
    // a one-row immediate update for other console dashboards.
    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    // Replaces the editable byte tail owned by the shell without appending fake backspace
    // characters to retained scrollback. This is the primitive used by command history and
    // in-line cursor editing. oldLength is the shell's previous input length, while cursorOffset
    // selects the caret position inside the replacement text.
    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    

    
}
