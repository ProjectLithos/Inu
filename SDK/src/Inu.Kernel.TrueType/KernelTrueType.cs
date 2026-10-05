using System;

namespace Inu.Kernel.TrueType;

/// <summary>Allocation-free TrueType parser, Unicode mapper, metrics engine and glyph rasteriser for Inu.</summary>
public static unsafe partial class KernelTrueType
{
    private const UInt32 TagHead=0x68656164U, TagMaxp=0x6D617870U, TagHhea=0x68686561U, TagHmtx=0x686D7478U, TagLoca=0x6C6F6361U, TagGlyf=0x676C7966U, TagCmap=0x636D6170U, TagKern=0x6B65726EU;
    private const UInt16 OnCurve=1, XShort=2, YShort=4, Repeat=8, XSame=16, YSame=32;
    private const UInt16 ArgWords=1, ArgsAreXY=2, MoreComponents=32, HaveScale=8, HaveXYScale=64, Have2x2=128;
    private const Int32 CurveSteps=8;

    private struct Table { internal UInt32 Offset, Length; }
    private struct Face
    {
        internal UInt64 Address, Length; internal Table Head,Maxp,Hhea,Hmtx,Loca,Glyf,Cmap,Kern; internal UInt16 UnitsPerEm,GlyphCount,NumberHMetrics; internal Int16 Ascender,Descender,LocaFormat; internal UInt32 Cmap4,Cmap12;
    }
    private struct Point { internal Int32 X,Y; internal Byte On; internal Byte Pad0; }
    private struct Edge { internal Int32 X0,Y0,X1,Y1; }

    /// <summary>Validates a memory-resident TrueType font and returns its global capabilities.</summary>
}
