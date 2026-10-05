using Inu.Boot.Contracts;
using Inu.Console.Contracts;
using BootFramebuffer = Inu.Boot.Contracts.Framebuffer;

namespace Inu.Console.Framebuffer;

/// <summary>Defines the colours, rendered font size, and margin used by a framebuffer console.</summary>
public readonly struct FramebufferConfiguration
{
    /// <summary>Creates a framebuffer-console configuration.</summary>
    /// <nova.when>Use when selecting exact framebuffer colours, font height, and margins.</nova.when>
    /// <nova.depends>Inu.Boot.Contracts and Inu.Console.Contracts.</nova.depends>
    /// <returns>A framebuffer configuration value.</returns>
    /// <example><code>FramebufferConfiguration configuration = FramebufferConfiguration.Default(16U);</code></example>
    /// <param name="foregroundRed">Foreground red component.</param>
    /// <param name="foregroundGreen">Foreground green component.</param>
    /// <param name="foregroundBlue">Foreground blue component.</param>
    /// <param name="backgroundRed">Background red component.</param>
    /// <param name="backgroundGreen">Background green component.</param>
    /// <param name="backgroundBlue">Background blue component.</param>
    /// <param name="fontSize">Rendered glyph height in framebuffer pixels.</param>
    /// <param name="margin">Top and left margin in framebuffer pixels.</param>
    public FramebufferConfiguration(
        byte foregroundRed,
        byte foregroundGreen,
        byte foregroundBlue,
        byte backgroundRed,
        byte backgroundGreen,
        byte backgroundBlue,
        uint fontSize,
        uint margin)
    {
        ForegroundRed = foregroundRed;
        ForegroundGreen = foregroundGreen;
        ForegroundBlue = foregroundBlue;
        BackgroundRed = backgroundRed;
        BackgroundGreen = backgroundGreen;
        BackgroundBlue = backgroundBlue;
        FontSize = fontSize;
        Margin = margin;
    }

    /// <summary>Gets the foreground red component.</summary>
    public byte ForegroundRed { get; }

    /// <summary>Gets the foreground green component.</summary>
    public byte ForegroundGreen { get; }

    /// <summary>Gets the foreground blue component.</summary>
    public byte ForegroundBlue { get; }

    /// <summary>Gets the background red component.</summary>
    public byte BackgroundRed { get; }

    /// <summary>Gets the background green component.</summary>
    public byte BackgroundGreen { get; }

    /// <summary>Gets the background blue component.</summary>
    public byte BackgroundBlue { get; }

    /// <summary>Gets the rendered glyph height in framebuffer pixels.</summary>
    /// <nova.when>Use when inspecting the exact pixel height requested for glyph rendering.</nova.when>
    /// <nova.depends>The value supplied to the framebuffer configuration constructor.</nova.depends>
    public uint FontSize { get; }

    /// <summary>Gets the top and left margin in framebuffer pixels.</summary>
    public uint Margin { get; }

    /// <summary>Creates the default 16-pixel framebuffer-font configuration.</summary>
    /// <returns>The default framebuffer configuration.</returns>
    public static FramebufferConfiguration Default()
    {
        return Default(BitmapFont.DefaultFontSize);
    }

    /// <summary>Creates the default framebuffer colours and margin with an exact font size.</summary>
    /// <nova.when>Use when the kernel wants to choose the actual rendered glyph height without configuring colours individually.</nova.when>
    /// <nova.depends>Inu Mono embedded raster metrics.</nova.depends>
    /// <param name="fontSize">Rendered glyph height in framebuffer pixels.</param>
    /// <returns>A framebuffer configuration using the requested font size.</returns>
    /// <example><code>FramebufferConfiguration configuration = FramebufferConfiguration.Default(24U);</code></example>
    public static FramebufferConfiguration Default(uint fontSize)
    {
        return new FramebufferConfiguration(
            232, 240, 248,
            9, 16, 24,
            fontSize,
            fontSize / 2U);
    }
}

/// <summary>Writes text directly to a UEFI-provided linear framebuffer.</summary>
public sealed unsafe class FramebufferConsole : IConsole
{
    private FramebufferConfiguration _configuration;
    private BootFramebuffer _framebuffer;
    private uint _cursorX;
    private uint _cursorY;
    private uint _glyphWidth;
    private uint _characterAdvance;
    private uint _lineHeight;
    private uint _foreground;
    private uint _background;
    private bool _initialized;

    /// <summary>Gets the exact glyph height currently configured for rendering, in framebuffer pixels.</summary>
    /// <nova.when>Use after configuration to report or verify the renderer's active font size.</nova.when>
    /// <nova.depends>FramebufferConfiguration.FontSize.</nova.depends>
    public uint FontSize => _configuration.FontSize;

    /// <summary>Applies console colours, font size, and margin.</summary>
    /// <param name="configuration">The configuration to apply.</param>
    /// <returns><see langword="true"/> when the configuration is valid.</returns>
    public bool Configure(FramebufferConfiguration configuration)
    {
        if (configuration.FontSize < BitmapFont.MinimumFontSize ||
            configuration.FontSize > BitmapFont.MaximumFontSize)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration));
        }

        _configuration = configuration;
        return true;
    }

    /// <summary>Initializes the console from a validated boot context.</summary>
    /// <param name="boot">The boot context containing framebuffer information.</param>
    /// <returns><see langword="true"/> when initialization succeeds.</returns>
    public bool Initialize(IBootFramebuffer boot)
    {
        if (!boot.TryGetFramebuffer(out BootFramebuffer framebuffer)) return false;
        if (_configuration.FontSize == 0 && !Configure(FramebufferConfiguration.Default())) return false;

        if (BitmapFont.GetFontContractVersion() != 2U) return false;

        uint fontSize = _configuration.FontSize;
        uint glyphWidth = BitmapFont.GetRenderedGlyphWidth(fontSize);
        uint characterAdvance = BitmapFont.GetRenderedCharacterAdvance(fontSize);
        uint lineHeight = BitmapFont.GetRenderedLineHeight(fontSize);
        if (glyphWidth == 0 || characterAdvance < glyphWidth || lineHeight < fontSize) return false;
        if (_configuration.Margin >= framebuffer.Width || _configuration.Margin >= framebuffer.Height) return false;
        if (glyphWidth > framebuffer.Width - _configuration.Margin) return false;
        if (fontSize > framebuffer.Height - _configuration.Margin) return false;

        _framebuffer = framebuffer;
        _cursorX = _configuration.Margin;
        _cursorY = _configuration.Margin;
        _glyphWidth = glyphWidth;
        _characterAdvance = characterAdvance;
        _lineHeight = lineHeight;
        _foreground = PackColor(_configuration.ForegroundRed, _configuration.ForegroundGreen, _configuration.ForegroundBlue);
        _background = PackColor(_configuration.BackgroundRed, _configuration.BackgroundGreen, _configuration.BackgroundBlue);
        _initialized = true;
        return Clear();
    }

    /// <summary>Clears the visible framebuffer and resets the cursor.</summary>
    /// <returns><see langword="true"/> when the framebuffer is cleared.</returns>
    public bool Clear()
    {
        if (!_initialized) return false;
        ulong pixelCount = checked((ulong)_framebuffer.PixelsPerScanLine * _framebuffer.Height);
        if (pixelCount > _framebuffer.SizeInBytes / 4UL) return false;
        uint* pixel = (uint*)_framebuffer.Address.Value;
        while (pixelCount != 0)
        {
            *pixel++ = _background;
            pixelCount--;
        }
        _cursorX = _configuration.Margin;
        _cursorY = _configuration.Margin;
        return true;
    }

    /// <summary>Writes text at the current framebuffer cursor.</summary>
    /// <param name="text">The text to write.</param>
    /// <returns><see langword="true"/> when every character is written.</returns>
    public bool Write(ReadOnlySpan<char> text)
    {
        if (!_initialized) return false;
        foreach (char character in text)
        {
            if (!WriteCharacter(character)) return false;
        }
        return true;
    }

    /// <summary>Writes text and advances to the next line.</summary>
    /// <param name="text">The text to write.</param>
    /// <returns><see langword="true"/> when the text and line break are written.</returns>
    public bool WriteLine(ReadOnlySpan<char> text) => Write(text) && WriteLine();

    /// <summary>Advances to the next line.</summary>
    /// <returns><see langword="true"/> when the next line fits.</returns>
    public bool WriteLine() => Write("\r\n");

    private bool WriteCharacter(char value)
    {
        if (value == '\r') return true;
        if (value == '\n') return MoveToNextLine();
        if (_cursorX > _framebuffer.Width - _glyphWidth && !MoveToNextLine()) return false;
        if (_cursorY > _framebuffer.Height - _configuration.FontSize) return false;
        if (!DrawGlyph(value, _cursorX, _cursorY)) return false;
        _cursorX += _characterAdvance;
        return true;
    }

    private bool MoveToNextLine()
    {
        _cursorX = _configuration.Margin;
        uint lastLineY = _framebuffer.Height - _configuration.FontSize;
        if (_cursorY <= lastLineY && _lineHeight <= lastLineY - _cursorY)
        {
            _cursorY += _lineHeight;
            return true;
        }
        return ScrollUpOneLine();
    }

    private bool ScrollUpOneLine()
    {
        if (_lineHeight == 0 || _configuration.Margin >= _framebuffer.Height ||
            _lineHeight >= _framebuffer.Height - _configuration.Margin) return false;

        uint sourceY = _configuration.Margin + _lineHeight;
        uint destinationY = _configuration.Margin;
        uint rowsToMove = _framebuffer.Height - sourceY;
        ulong framebufferPixels = _framebuffer.SizeInBytes / 4UL;
        uint* pixels = (uint*)_framebuffer.Address.Value;

        for (uint row = 0; row < rowsToMove; row++)
        {
            ulong sourceIndex = checked((ulong)(sourceY + row) * _framebuffer.PixelsPerScanLine);
            ulong destinationIndex = checked((ulong)(destinationY + row) * _framebuffer.PixelsPerScanLine);
            if (sourceIndex + _framebuffer.PixelsPerScanLine > framebufferPixels ||
                destinationIndex + _framebuffer.PixelsPerScanLine > framebufferPixels) return false;

            for (uint column = 0; column < _framebuffer.PixelsPerScanLine; column++)
                pixels[destinationIndex + column] = pixels[sourceIndex + column];
        }

        uint clearStartY = _framebuffer.Height - _lineHeight;
        for (uint row = clearStartY; row < _framebuffer.Height; row++)
        {
            ulong destinationIndex = checked((ulong)row * _framebuffer.PixelsPerScanLine);
            if (destinationIndex + _framebuffer.PixelsPerScanLine > framebufferPixels) return false;
            for (uint column = 0; column < _framebuffer.PixelsPerScanLine; column++)
                pixels[destinationIndex + column] = _background;
        }

        return _cursorY <= _framebuffer.Height - _configuration.FontSize;
    }

    private bool DrawGlyph(char value, uint originX, uint originY)
    {
        uint fontSize = _configuration.FontSize;
        for (uint renderedRow = 0; renderedRow < fontSize; renderedRow++)
        {
            uint sourceRow = BitmapFont.GetSourceRow(renderedRow, fontSize);
            if (!BitmapFont.TryGetGlyphRow(value, sourceRow, out byte sourceBits)) return false;
            uint bits = sourceBits;
            for (uint renderedColumn = 0; renderedColumn < _glyphWidth; renderedColumn++)
            {
                uint sourceColumn = BitmapFont.GetSourceColumn(renderedColumn, _glyphWidth);
                uint mask = 1U << (int)((BitmapFont.GlyphWidth - 1U) - sourceColumn);
                if ((bits & mask) == 0) continue;
                if (!DrawPixel(originX + renderedColumn, originY + renderedRow)) return false;
            }
        }
        return true;
    }

    private bool DrawPixel(uint pixelX, uint pixelY)
    {
        if (pixelX >= _framebuffer.Width || pixelY >= _framebuffer.Height) return false;
        ulong index = checked(((ulong)pixelY * _framebuffer.PixelsPerScanLine) + pixelX);
        if (index >= _framebuffer.SizeInBytes / 4UL) return false;
        *((uint*)_framebuffer.Address.Value + index) = _foreground;
        return true;
    }

    private uint PackColor(byte red, byte green, byte blue)
    {
        return _framebuffer.PixelFormat switch
        {
            FramebufferPixelFormat.RedGreenBlueReserved8BitPerColor => red | ((uint)green << 8) | ((uint)blue << 16),
            FramebufferPixelFormat.BlueGreenRedReserved8BitPerColor => blue | ((uint)green << 8) | ((uint)red << 16),
            FramebufferPixelFormat.BitMask => EncodeMask(red, _framebuffer.PixelMask.Red) |
                                              EncodeMask(green, _framebuffer.PixelMask.Green) |
                                              EncodeMask(blue, _framebuffer.PixelMask.Blue),
            _ => 0U
        };
    }

    private static uint EncodeMask(byte component, uint mask)
    {
        int shift = System.Numerics.BitOperations.TrailingZeroCount(mask);
        int bits = System.Numerics.BitOperations.PopCount(mask);
        ulong maximum = bits == 32 ? uint.MaxValue : ((1UL << bits) - 1UL);
        return ((uint)(((ulong)component * maximum) / byte.MaxValue) << shift) & mask;
    }
}
