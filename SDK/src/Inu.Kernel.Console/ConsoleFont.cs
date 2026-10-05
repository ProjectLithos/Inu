using System;

namespace Inu.Kernel.Console;

/// <summary>Identifies the source format of the active framebuffer console font.</summary>
public enum ConsoleFontFormat
{
    /// <summary>The guaranteed embedded Inu boot font.</summary>
    Embedded = 0,
    /// <summary>A PC Screen Font version 2 face installed from kernel-accessible memory.</summary>
    Psf2 = 2
}

/// <summary>Describes the active framebuffer font face independently of its rendered size.</summary>
public readonly struct ConsoleFontInformation
{
    internal ConsoleFontInformation(ConsoleFontFormat format, UInt32 glyphWidth, UInt32 glyphHeight, UInt32 glyphCount, Boolean hasUnicodeTable)
    {
        Format = format; GlyphWidth = glyphWidth; GlyphHeight = glyphHeight; GlyphCount = glyphCount; HasUnicodeTable = hasUnicodeTable;
    }
    /// <summary>Gets the font storage format.</summary>
    public ConsoleFontFormat Format { get; }
    /// <summary>Gets the source glyph width in pixels.</summary>
    public UInt32 GlyphWidth { get; }
    /// <summary>Gets the source glyph height in pixels.</summary>
    public UInt32 GlyphHeight { get; }
    /// <summary>Gets the number of glyphs supplied by the face.</summary>
    public UInt32 GlyphCount { get; }
    /// <summary>Gets whether the PSF2 face supplies a Unicode table.</summary>
    public Boolean HasUnicodeTable { get; }
}

internal static unsafe class ConsoleFont
{
    private const UInt32 Psf2Magic = 0x864AB572U;
    private const UInt32 Psf2HasUnicodeTable = 1U;
    private struct FontState
    {
        internal UInt64 Address, Length;
        internal UInt32 HeaderSize, Flags, GlyphCount, BytesPerGlyph, Height, Width;
    }
    private static FontState _external;
    private static Boolean _useExternal;

    internal static ConsoleFontInformation GetInformation() => !_useExternal
        ? new ConsoleFontInformation(ConsoleFontFormat.Embedded, BitmapFont.GlyphWidth, BitmapFont.GlyphHeight, 95U, false)
        : new ConsoleFontInformation(ConsoleFontFormat.Psf2, _external.Width, _external.Height, _external.GlyphCount, (_external.Flags & Psf2HasUnicodeTable) != 0U);

    internal static Boolean UseEmbedded() { _useExternal = false; return true; }

    internal static Boolean InstallPsf2(UInt64 address, UInt64 length)
    {
        if (address == 0UL || length < 32UL) return false;
        Byte* data = (Byte*)address;
        UInt32 magic = ReadUInt32(data, 0U), version = ReadUInt32(data, 4U), headerSize = ReadUInt32(data, 8U), flags = ReadUInt32(data, 12U);
        UInt32 glyphCount = ReadUInt32(data, 16U), bytesPerGlyph = ReadUInt32(data, 20U), height = ReadUInt32(data, 24U), width = ReadUInt32(data, 28U);
        if (magic != Psf2Magic || version != 0U || headerSize < 32U || (UInt64)headerSize > length) return false;
        if (glyphCount == 0U || bytesPerGlyph == 0U || height == 0U || width == 0U || width > 32U) return false;
        UInt32 rowBytes = (width + 7U) / 8U;
        if ((UInt64)rowBytes * height > bytesPerGlyph) return false;
        UInt64 glyphBytes = (UInt64)glyphCount * bytesPerGlyph;
        if (glyphBytes > length - headerSize) return false;
        _external.Address = address; _external.Length = length; _external.HeaderSize = headerSize; _external.Flags = flags;
        _external.GlyphCount = glyphCount; _external.BytesPerGlyph = bytesPerGlyph; _external.Height = height; _external.Width = width;
        if ((flags & Psf2HasUnicodeTable) != 0U && !ValidateUnicodeTable()) return false;
        _useExternal = true;
        return true;
    }

    internal static UInt32 GetSourceWidth() => _useExternal ? _external.Width : BitmapFont.GlyphWidth;
    internal static UInt32 GetSourceHeight() => _useExternal ? _external.Height : BitmapFont.GlyphHeight;
    internal static UInt32 GetRenderedGlyphWidth(UInt32 h) => (UInt32)((((UInt64)GetSourceWidth() * h) + GetSourceHeight() - 1U) / GetSourceHeight());
    internal static UInt32 GetRenderedCharacterAdvance(UInt32 h)
    {
        if (!_useExternal) return BitmapFont.GetRenderedCharacterAdvance(h);
        UInt32 s = h / 8U; if (s == 0U) s = 1U; return GetRenderedGlyphWidth(h) + s;
    }
    internal static UInt32 GetRenderedLineHeight(UInt32 h)
    {
        if (!_useExternal) return BitmapFont.GetRenderedLineHeight(h);
        UInt32 s = h / 4U; if (s == 0U) s = 1U; return h + s;
    }
    internal static UInt32 GetSourceRow(UInt32 renderedRow, UInt32 renderedHeight) => (UInt32)(((UInt64)renderedRow * GetSourceHeight()) / renderedHeight);
    internal static UInt32 GetSourceColumn(UInt32 renderedColumn, UInt32 renderedWidth) => (UInt32)(((UInt64)renderedColumn * GetSourceWidth()) / renderedWidth);

    internal static UInt32 GetGlyphRow(Byte value, UInt32 row)
    {
        if (!_useExternal) return BitmapFont.GetGlyphRow(value, row);
        if (row >= _external.Height) return 0U;
        UInt32 glyph = GetGlyphIndex(value); if (glyph >= _external.GlyphCount) glyph = GetGlyphIndex((Byte)'?');
        UInt32 rowBytes = (_external.Width + 7U) / 8U;
        UInt64 offset = (UInt64)_external.HeaderSize + ((UInt64)glyph * _external.BytesPerGlyph) + ((UInt64)row * rowBytes);
        if (offset + rowBytes > _external.Length) return 0U;
        Byte* data = (Byte*)_external.Address + offset;
        UInt32 bits = 0U, column = 0U;
        while (column < _external.Width)
        {
            UInt32 byteIndex = column >> 3, bitInByte = column & 7U;
            if ((data[byteIndex] & (Byte)(0x80U >> (Int32)bitInByte)) != 0U) bits |= 1U << (Int32)((_external.Width - 1U) - column);
            column++;
        }
        return bits;
    }

    private static UInt32 GetGlyphIndex(Byte value)
    {
        if ((_external.Flags & Psf2HasUnicodeTable) == 0U) return value < _external.GlyphCount ? value : 0U;
        UInt32 mapped;
        if (TryFindUnicodeGlyph(value, out mapped)) return mapped;
        if (value != (Byte)'?' && TryFindUnicodeGlyph((Byte)'?', out mapped)) return mapped;
        return 0U;
    }

    private static Boolean ValidateUnicodeTable()
    {
        UInt64 position = (UInt64)_external.HeaderSize + ((UInt64)_external.GlyphCount * _external.BytesPerGlyph);
        UInt32 glyph = 0U; Byte* data = (Byte*)_external.Address;
        while (glyph < _external.GlyphCount)
        {
            if (position >= _external.Length) return false;
            while (position < _external.Length)
            {
                Byte first = data[position++];
                if (first == 0xFFU) break;
                if (first == 0xFEU) continue;
                UInt32 ignored; if (!TryDecodeUtf8(first, data, ref position, _external.Length, out ignored)) return false;
            }
            glyph++;
        }
        return true;
    }

    private static Boolean TryFindUnicodeGlyph(UInt32 wanted, out UInt32 mappedGlyph)
    {
        UInt64 position = (UInt64)_external.HeaderSize + ((UInt64)_external.GlyphCount * _external.BytesPerGlyph);
        UInt32 glyph = 0U; Byte* data = (Byte*)_external.Address;
        while (glyph < _external.GlyphCount && position < _external.Length)
        {
            Boolean sequence = false;
            while (position < _external.Length)
            {
                Byte first = data[position++];
                if (first == 0xFFU) break;
                if (first == 0xFEU) { sequence = true; continue; }
                UInt32 codePoint; if (!TryDecodeUtf8(first, data, ref position, _external.Length, out codePoint)) break;
                if (!sequence && codePoint == wanted) { mappedGlyph = glyph; return true; }
            }
            glyph++;
        }
        mappedGlyph = 0U; return false;
    }

    private static Boolean TryDecodeUtf8(Byte first, Byte* data, ref UInt64 position, UInt64 length, out UInt32 codePoint)
    {
        if ((first & 0x80U) == 0U) { codePoint = first; return true; }
        UInt32 needed, value;
        if ((first & 0xE0U) == 0xC0U) { needed = 1U; value = (UInt32)(first & 0x1FU); }
        else if ((first & 0xF0U) == 0xE0U) { needed = 2U; value = (UInt32)(first & 0x0FU); }
        else if ((first & 0xF8U) == 0xF0U) { needed = 3U; value = (UInt32)(first & 0x07U); }
        else { codePoint = 0U; return false; }
        UInt32 i = 0U; while (i < needed) { if (position >= length) { codePoint = 0U; return false; } Byte c = data[position++]; if ((c & 0xC0U) != 0x80U) { codePoint = 0U; return false; } value = (value << 6) | (UInt32)(c & 0x3FU); i++; }
        codePoint = value; return true;
    }
    private static UInt32 ReadUInt32(Byte* data, UInt32 offset) => (UInt32)data[offset] | ((UInt32)data[offset + 1U] << 8) | ((UInt32)data[offset + 2U] << 16) | ((UInt32)data[offset + 3U] << 24);
}
