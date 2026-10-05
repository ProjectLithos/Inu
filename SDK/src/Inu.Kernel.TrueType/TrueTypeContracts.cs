using System;

namespace Inu.Kernel.TrueType;

/// <summary>Reports the TrueType tables and global metrics validated by Inu.</summary>
public readonly struct TrueTypeFontInformation
{
    public TrueTypeFontInformation(UInt16 unitsPerEm, Int16 ascender, Int16 descender, UInt16 glyphCount, Boolean hasUnicodeBmp, Boolean hasUnicodeFull, Boolean hasKerning)
    { UnitsPerEm=unitsPerEm; Ascender=ascender; Descender=descender; GlyphCount=glyphCount; HasUnicodeBmp=hasUnicodeBmp; HasUnicodeFull=hasUnicodeFull; HasKerning=hasKerning; }
    public UInt16 UnitsPerEm { get; }
    public Int16 Ascender { get; }
    public Int16 Descender { get; }
    public UInt16 GlyphCount { get; }
    public Boolean HasUnicodeBmp { get; }
    public Boolean HasUnicodeFull { get; }
    public Boolean HasKerning { get; }
}

/// <summary>Pixel-space metrics for one scaled TrueType glyph.</summary>
public readonly struct TrueTypeGlyphMetrics
{
    public TrueTypeGlyphMetrics(UInt16 glyphIndex, Int32 width, Int32 height, Int32 bearingX, Int32 bearingY, Int32 advanceX)
    { GlyphIndex=glyphIndex; Width=width; Height=height; BearingX=bearingX; BearingY=bearingY; AdvanceX=advanceX; }
    public UInt16 GlyphIndex { get; }
    public Int32 Width { get; }
    public Int32 Height { get; }
    public Int32 BearingX { get; }
    public Int32 BearingY { get; }
    public Int32 AdvanceX { get; }
}

/// <summary>Caller-owned memory requirements for rasterising a glyph without managed allocation.</summary>
public readonly struct TrueTypeRasterWorkspace
{
    public TrueTypeRasterWorkspace(UInt32 pointBytes, UInt32 edgeBytes, UInt32 totalBytes) { PointBytes=pointBytes; EdgeBytes=edgeBytes; TotalBytes=totalBytes; }
    public UInt32 PointBytes { get; }
    public UInt32 EdgeBytes { get; }
    public UInt32 TotalBytes { get; }
}

/// <summary>32-bit linear surface used by the optional glyph compositor.</summary>
public readonly struct TrueTypeSurface32
{
    public TrueTypeSurface32(UInt64 address, UInt32 width, UInt32 height, UInt32 pixelsPerScanLine)
    { Address=address; Width=width; Height=height; PixelsPerScanLine=pixelsPerScanLine; }
    public UInt64 Address { get; }
    public UInt32 Width { get; }
    public UInt32 Height { get; }
    public UInt32 PixelsPerScanLine { get; }
    public Boolean IsValid => Address!=0UL && Width!=0U && Height!=0U && PixelsPerScanLine>=Width;
}
