namespace Inu.Console.Framebuffer;

// Inu Mono 8x16 contains the complete printable ASCII range (U+0020-U+007E).
// The embedded monochrome glyphs are rasterised from DejaVu Sans Mono Bold 2.37.
internal static class BitmapFont
{
    internal const uint GlyphWidth = 8U;
    internal const uint GlyphHeight = 16U;
    internal const uint CharacterAdvance = 10U;
    internal const uint LineHeight = 20U;
    internal const uint DefaultFontSize = 16U;
    internal const uint MinimumFontSize = 8U;
    internal const uint MaximumFontSize = 128U;

    // FontSize is the rendered glyph height in framebuffer pixels. All other
    // renderer measurements are derived from that one value.
    internal static uint GetRenderedGlyphWidth(uint fontSize)
    {
        return ScaleMetric(GlyphWidth, fontSize);
    }

    internal static uint GetRenderedCharacterAdvance(uint fontSize)
    {
        return ScaleMetric(CharacterAdvance, fontSize);
    }

    internal static uint GetRenderedLineHeight(uint fontSize)
    {
        return ScaleMetric(LineHeight, fontSize);
    }

    internal static uint GetSourceRow(uint renderedRow, uint fontSize)
    {
        return (uint)(((ulong)renderedRow * GlyphHeight) / fontSize);
    }

    internal static uint GetSourceColumn(uint renderedColumn, uint renderedWidth)
    {
        return (uint)(((ulong)renderedColumn * GlyphWidth) / renderedWidth);
    }

    private static uint ScaleMetric(uint metric, uint fontSize)
    {
        return (uint)((((ulong)metric * fontSize) + GlyphHeight - 1U) / GlyphHeight);
    }

    internal static uint GetFontContractVersion()
    {
        return 2U;
    }

    internal static bool TryGetGlyphRow(char value, uint row, out byte bits)
    {
        if (row >= GlyphHeight)
        {
            bits = 0;
            return false;
        }

        bits = GetGlyphRow(value, row);
        return true;
    }

    internal static byte GetGlyphRow(char value, uint row)
    {
        if (row >= GlyphHeight) return 0;
        if (value < (char)0x20 || value > (char)0x7E) value = (char)0x3F;

        ulong packed = row < 8U ? GetTopHalf((byte)value) : GetBottomHalf((byte)value);
        uint rowInHalf = row < 8U ? row : row - 8U;
        uint shift = (7U - rowInHalf) * 8U;
        return (byte)((packed >> (int)shift) & 0xFFUL);
    }

    // Both packed eight-row halves for each printable ASCII glyph.
    private const ulong Top20 = 0x0000000000000000UL, Bottom20 = 0x0000000000000000UL; // space
    private const ulong Top21 = 0x0000000018181818UL, Bottom21 = 0x1818001818000000UL; // !
    private const ulong Top22 = 0x0000000064646464UL, Bottom22 = 0x0000000000000000UL; // "
    private const ulong Top23 = 0x000000000016167FUL, Bottom23 = 0x342CFE6858000000UL; // #
    private const ulong Top24 = 0x00000018183C7C78UL, Bottom24 = 0x3C1E5E7E3C181800UL; // $
    private const ulong Top25 = 0x0000000070D0D076UL, Bottom25 = 0x184E0B0B0E000000UL; // %
    private const ulong Top26 = 0x0000000038647030UL, Bottom26 = 0x7BDFCFEE7F000000UL; // &
    private const ulong Top27 = 0x0000000018181818UL, Bottom27 = 0x0000000000000000UL; // apostrophe
    private const ulong Top28 = 0x00000C1818183030UL, Bottom28 = 0x30301818180C0000UL; // (
    private const ulong Top29 = 0x0000301018181818UL, Bottom29 = 0x1818181810300000UL; // )
    private const ulong Top2A = 0x00000000185E3C3CUL, Bottom2A = 0x5E18000000000000UL; // *
    private const ulong Top2B = 0x0000000000181818UL, Bottom2B = 0xFFFF181818000000UL; // +
    private const ulong Top2C = 0x0000000000000000UL, Bottom2C = 0x0000001818183000UL; // ,
    private const ulong Top2D = 0x0000000000000000UL, Bottom2D = 0x3C3C000000000000UL; // -
    private const ulong Top2E = 0x0000000000000000UL, Bottom2E = 0x0000001818000000UL; // .
    private const ulong Top2F = 0x0000000006060C0CUL, Bottom2F = 0x0818103020604000UL; // /
    private const ulong Top30 = 0x000000003C6E667EUL, Bottom30 = 0x7E66666E3C000000UL; // 0
    private const ulong Top31 = 0x0000000078181818UL, Bottom31 = 0x181818187E000000UL; // 1
    private const ulong Top32 = 0x000000003C4E060EUL, Bottom32 = 0x0C1830607E000000UL; // 2
    private const ulong Top33 = 0x000000003C46063CUL, Bottom33 = 0x0E06064E7C000000UL; // 3
    private const ulong Top34 = 0x000000000C1C3C6CUL, Bottom34 = 0x6C7E0C0C0C000000UL; // 4
    private const ulong Top35 = 0x000000007E60607CUL, Bottom35 = 0x4E06064E3C000000UL; // 5
    private const ulong Top36 = 0x000000003C72607CUL, Bottom36 = 0x666666663C000000UL; // 6
    private const ulong Top37 = 0x000000007E0E0C0CUL, Bottom37 = 0x1C18183030000000UL; // 7
    private const ulong Top38 = 0x000000003C66663CUL, Bottom38 = 0x666666663C000000UL; // 8
    private const ulong Top39 = 0x000000003C6E6666UL, Bottom39 = 0x6E3E064C38000000UL; // 9
    private const ulong Top3A = 0x0000000000001818UL, Bottom3A = 0x0000001818000000UL; // :
    private const ulong Top3B = 0x0000000000001818UL, Bottom3B = 0x0000001818183000UL; // ;
    private const ulong Top3C = 0x000000000000021EUL, Bottom3C = 0x78E0781E02000000UL; // <
    private const ulong Top3D = 0x00000000000000FEUL, Bottom3D = 0xFE00FEFE00000000UL; // =
    private const ulong Top3E = 0x000000000000C078UL, Bottom3E = 0x1E061E78C0000000UL; // >
    private const ulong Top3F = 0x000000003C66060CUL, Bottom3F = 0x1818001818000000UL; // ?
    private const ulong Top40 = 0x000000003C625ED2UL, Bottom40 = 0xF2B2F2D25E623E00UL; // @
    private const ulong Top41 = 0x00000000383C3C3CUL, Bottom41 = 0x647E6646C3000000UL; // A
    private const ulong Top42 = 0x000000007C666666UL, Bottom42 = 0x7C6666667E000000UL; // B
    private const ulong Top43 = 0x000000001C326060UL, Bottom43 = 0x606060321C000000UL; // C
    private const ulong Top44 = 0x000000007C6E6666UL, Bottom44 = 0x6666666E7C000000UL; // D
    private const ulong Top45 = 0x000000007E606060UL, Bottom45 = 0x7E6060607E000000UL; // E
    private const ulong Top46 = 0x000000007E606060UL, Bottom46 = 0x7E60606060000000UL; // F
    private const ulong Top47 = 0x000000003C726060UL, Bottom47 = 0x6E6666763E000000UL; // G
    private const ulong Top48 = 0x0000000066666666UL, Bottom48 = 0x7E66666666000000UL; // H
    private const ulong Top49 = 0x000000007E181818UL, Bottom49 = 0x181818187E000000UL; // I
    private const ulong Top4A = 0x000000003E0E0E0EUL, Bottom4A = 0x0E0E0E4C7C000000UL; // J
    private const ulong Top4B = 0x00000000666E7C78UL, Bottom4B = 0x786C6C6667000000UL; // K
    private const ulong Top4C = 0x0000000060606060UL, Bottom4C = 0x606060607E000000UL; // L
    private const ulong Top4D = 0x00000000E6EEFEFEUL, Bottom4D = 0xDEDAC2C2C2000000UL; // M
    private const ulong Top4E = 0x0000000066667676UL, Bottom4E = 0x7E6E6E6E66000000UL; // N
    private const ulong Top4F = 0x000000003C666666UL, Bottom4F = 0xE66666663C000000UL; // O
    private const ulong Top50 = 0x000000007C666666UL, Bottom50 = 0x667C606060000000UL; // P
    private const ulong Top51 = 0x000000003C666666UL, Bottom51 = 0xE66666663C0E0400UL; // Q
    private const ulong Top52 = 0x000000007C6E6666UL, Bottom52 = 0x6E7C6C6667000000UL; // R
    private const ulong Top53 = 0x000000003C666070UL, Bottom53 = 0x3C0E06463C000000UL; // S
    private const ulong Top54 = 0x00000000FE181818UL, Bottom54 = 0x1818181818000000UL; // T
    private const ulong Top55 = 0x0000000066666666UL, Bottom55 = 0x666666663C000000UL; // U
    private const ulong Top56 = 0x00000000C6666666UL, Bottom56 = 0x6C3C3C3C38000000UL; // V
    private const ulong Top57 = 0x00000000C3C3DBDBUL, Bottom57 = 0xFE7E7E6666000000UL; // W
    private const ulong Top58 = 0x00000000E6663C3CUL, Bottom58 = 0x183C3C66E6000000UL; // X
    private const ulong Top59 = 0x00000000C7666E3CUL, Bottom59 = 0x3818181818000000UL; // Y
    private const ulong Top5A = 0x000000007E060E1CUL, Bottom5A = 0x183870607F000000UL; // Z
    private const ulong Top5B = 0x00001C1818181818UL, Bottom5B = 0x18181818181C0000UL; // [
    private const ulong Top5C = 0x0000000040602030UL, Bottom5C = 0x1018080C0C060600UL; // backslash
    private const ulong Top5D = 0x0000381818181818UL, Bottom5D = 0x1818181818380000UL; // ]
    private const ulong Top5E = 0x00000000183C6E46UL, Bottom5E = 0x0000000000000000UL; // ^
    private const ulong Top5F = 0x0000000000000000UL, Bottom5F = 0x00000000000000FFUL; // _
    private const ulong Top60 = 0x0000003018000000UL, Bottom60 = 0x0000000000000000UL; // `
    private const ulong Top61 = 0x0000000000003C46UL, Bottom61 = 0x067E66667E000000UL; // a
    private const ulong Top62 = 0x0000606060607C66UL, Bottom62 = 0x666666667C000000UL; // b
    private const ulong Top63 = 0x0000000000003C32UL, Bottom63 = 0x606060723C000000UL; // c
    private const ulong Top64 = 0x0000060606063E6EUL, Bottom64 = 0x66E6666E3E000000UL; // d
    private const ulong Top65 = 0x0000000000003C66UL, Bottom65 = 0x66FE60623C000000UL; // e
    private const ulong Top66 = 0x00001E1818187E18UL, Bottom66 = 0x1818181818000000UL; // f
    private const ulong Top67 = 0x0000000000003E6EUL, Bottom67 = 0x6666666E3E064E3CUL; // g
    private const ulong Top68 = 0x0000606060607C66UL, Bottom68 = 0x6666666666000000UL; // h
    private const ulong Top69 = 0x0000181800007818UL, Bottom69 = 0x181818187F000000UL; // i
    private const ulong Top6A = 0x00001C1C00003C1CUL, Bottom6A = 0x1C1C1C1C1C181878UL; // j
    private const ulong Top6B = 0x000060606060666CUL, Bottom6B = 0x78786C6666000000UL; // k
    private const ulong Top6C = 0x0000F03030303030UL, Bottom6C = 0x303030181E000000UL; // l
    private const ulong Top6D = 0x000000000000FEDAUL, Bottom6D = 0xDADADADADA000000UL; // m
    private const ulong Top6E = 0x0000000000007C66UL, Bottom6E = 0x6666666666000000UL; // n
    private const ulong Top6F = 0x0000000000003C66UL, Bottom6F = 0x666666663C000000UL; // o
    private const ulong Top70 = 0x0000000000007C66UL, Bottom70 = 0x666666667C606060UL; // p
    private const ulong Top71 = 0x0000000000003E6EUL, Bottom71 = 0x66E6666E3E060606UL; // q
    private const ulong Top72 = 0x0000000000003E38UL, Bottom72 = 0x3030303030000000UL; // r
    private const ulong Top73 = 0x0000000000003C64UL, Bottom73 = 0x703C0E463C000000UL; // s
    private const ulong Top74 = 0x0000000038387E38UL, Bottom74 = 0x383838381E000000UL; // t
    private const ulong Top75 = 0x0000000000006666UL, Bottom75 = 0x6666666E3E000000UL; // u
    private const ulong Top76 = 0x0000000000006666UL, Bottom76 = 0x662C3C3C38000000UL; // v
    private const ulong Top77 = 0x000000000000C3C3UL, Bottom77 = 0xDA5A7E7E66000000UL; // w
    private const ulong Top78 = 0x000000000000663CUL, Bottom78 = 0x3C183C7E66000000UL; // x
    private const ulong Top79 = 0x000000000000E666UL, Bottom79 = 0x663C3C3C18183870UL; // y
    private const ulong Top7A = 0x0000000000007E0EUL, Bottom7A = 0x1C1838707E000000UL; // z
    private const ulong Top7B = 0x00000E1818181818UL, Bottom7B = 0x70181818181E0000UL; // {
    private const ulong Top7C = 0x0000181818181818UL, Bottom7C = 0x1818181818181800UL; // |
    private const ulong Top7D = 0x0000701818181818UL, Bottom7D = 0x0E18181818700000UL; // }
    private const ulong Top7E = 0x0000000000000000UL, Bottom7E = 0x720E000000000000UL; // ~

    private static ulong GetTopHalf(byte value)
    {
        if (value < 0x30U) return GetTop20To2F(value);
        if (value < 0x40U) return GetTop30To3F(value);
        if (value < 0x50U) return GetTop40To4F(value);
        if (value < 0x60U) return GetTop50To5F(value);
        if (value < 0x70U) return GetTop60To6F(value);
        return GetTop70To7E(value);
    }

    private static ulong GetTop20To2F(byte value)
    {
        if (((uint)value & 0x08U) == 0U)
        {
            if (((uint)value & 0x04U) == 0U)
            {
                if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top20 : Top21;
                return ((uint)value & 0x01U) == 0U ? Top22 : Top23;
            }
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top24 : Top25;
            return ((uint)value & 0x01U) == 0U ? Top26 : Top27;
        }
        if (((uint)value & 0x04U) == 0U)
        {
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top28 : Top29;
            return ((uint)value & 0x01U) == 0U ? Top2A : Top2B;
        }
        if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top2C : Top2D;
        return ((uint)value & 0x01U) == 0U ? Top2E : Top2F;
    }

    private static ulong GetTop30To3F(byte value)
    {
        if (((uint)value & 0x08U) == 0U)
        {
            if (((uint)value & 0x04U) == 0U)
            {
                if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top30 : Top31;
                return ((uint)value & 0x01U) == 0U ? Top32 : Top33;
            }
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top34 : Top35;
            return ((uint)value & 0x01U) == 0U ? Top36 : Top37;
        }
        if (((uint)value & 0x04U) == 0U)
        {
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top38 : Top39;
            return ((uint)value & 0x01U) == 0U ? Top3A : Top3B;
        }
        if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top3C : Top3D;
        return ((uint)value & 0x01U) == 0U ? Top3E : Top3F;
    }

    private static ulong GetTop40To4F(byte value)
    {
        if (((uint)value & 0x08U) == 0U)
        {
            if (((uint)value & 0x04U) == 0U)
            {
                if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top40 : Top41;
                return ((uint)value & 0x01U) == 0U ? Top42 : Top43;
            }
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top44 : Top45;
            return ((uint)value & 0x01U) == 0U ? Top46 : Top47;
        }
        if (((uint)value & 0x04U) == 0U)
        {
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top48 : Top49;
            return ((uint)value & 0x01U) == 0U ? Top4A : Top4B;
        }
        if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top4C : Top4D;
        return ((uint)value & 0x01U) == 0U ? Top4E : Top4F;
    }

    private static ulong GetTop50To5F(byte value)
    {
        if (((uint)value & 0x08U) == 0U)
        {
            if (((uint)value & 0x04U) == 0U)
            {
                if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top50 : Top51;
                return ((uint)value & 0x01U) == 0U ? Top52 : Top53;
            }
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top54 : Top55;
            return ((uint)value & 0x01U) == 0U ? Top56 : Top57;
        }
        if (((uint)value & 0x04U) == 0U)
        {
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top58 : Top59;
            return ((uint)value & 0x01U) == 0U ? Top5A : Top5B;
        }
        if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top5C : Top5D;
        return ((uint)value & 0x01U) == 0U ? Top5E : Top5F;
    }

    private static ulong GetTop60To6F(byte value)
    {
        if (((uint)value & 0x08U) == 0U)
        {
            if (((uint)value & 0x04U) == 0U)
            {
                if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top60 : Top61;
                return ((uint)value & 0x01U) == 0U ? Top62 : Top63;
            }
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top64 : Top65;
            return ((uint)value & 0x01U) == 0U ? Top66 : Top67;
        }
        if (((uint)value & 0x04U) == 0U)
        {
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top68 : Top69;
            return ((uint)value & 0x01U) == 0U ? Top6A : Top6B;
        }
        if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top6C : Top6D;
        return ((uint)value & 0x01U) == 0U ? Top6E : Top6F;
    }

    private static ulong GetTop70To7E(byte value)
    {
        if (((uint)value & 0x08U) == 0U)
        {
            if (((uint)value & 0x04U) == 0U)
            {
                if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top70 : Top71;
                return ((uint)value & 0x01U) == 0U ? Top72 : Top73;
            }
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top74 : Top75;
            return ((uint)value & 0x01U) == 0U ? Top76 : Top77;
        }
        if (((uint)value & 0x04U) == 0U)
        {
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top78 : Top79;
            return ((uint)value & 0x01U) == 0U ? Top7A : Top7B;
        }
        if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Top7C : Top7D;
        return ((uint)value & 0x01U) == 0U ? Top7E : Top3F;
    }

    private static ulong GetBottomHalf(byte value)
    {
        if (value < 0x30U) return GetBottom20To2F(value);
        if (value < 0x40U) return GetBottom30To3F(value);
        if (value < 0x50U) return GetBottom40To4F(value);
        if (value < 0x60U) return GetBottom50To5F(value);
        if (value < 0x70U) return GetBottom60To6F(value);
        return GetBottom70To7E(value);
    }

    private static ulong GetBottom20To2F(byte value)
    {
        if (((uint)value & 0x08U) == 0U)
        {
            if (((uint)value & 0x04U) == 0U)
            {
                if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom20 : Bottom21;
                return ((uint)value & 0x01U) == 0U ? Bottom22 : Bottom23;
            }
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom24 : Bottom25;
            return ((uint)value & 0x01U) == 0U ? Bottom26 : Bottom27;
        }
        if (((uint)value & 0x04U) == 0U)
        {
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom28 : Bottom29;
            return ((uint)value & 0x01U) == 0U ? Bottom2A : Bottom2B;
        }
        if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom2C : Bottom2D;
        return ((uint)value & 0x01U) == 0U ? Bottom2E : Bottom2F;
    }

    private static ulong GetBottom30To3F(byte value)
    {
        if (((uint)value & 0x08U) == 0U)
        {
            if (((uint)value & 0x04U) == 0U)
            {
                if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom30 : Bottom31;
                return ((uint)value & 0x01U) == 0U ? Bottom32 : Bottom33;
            }
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom34 : Bottom35;
            return ((uint)value & 0x01U) == 0U ? Bottom36 : Bottom37;
        }
        if (((uint)value & 0x04U) == 0U)
        {
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom38 : Bottom39;
            return ((uint)value & 0x01U) == 0U ? Bottom3A : Bottom3B;
        }
        if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom3C : Bottom3D;
        return ((uint)value & 0x01U) == 0U ? Bottom3E : Bottom3F;
    }

    private static ulong GetBottom40To4F(byte value)
    {
        if (((uint)value & 0x08U) == 0U)
        {
            if (((uint)value & 0x04U) == 0U)
            {
                if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom40 : Bottom41;
                return ((uint)value & 0x01U) == 0U ? Bottom42 : Bottom43;
            }
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom44 : Bottom45;
            return ((uint)value & 0x01U) == 0U ? Bottom46 : Bottom47;
        }
        if (((uint)value & 0x04U) == 0U)
        {
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom48 : Bottom49;
            return ((uint)value & 0x01U) == 0U ? Bottom4A : Bottom4B;
        }
        if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom4C : Bottom4D;
        return ((uint)value & 0x01U) == 0U ? Bottom4E : Bottom4F;
    }

    private static ulong GetBottom50To5F(byte value)
    {
        if (((uint)value & 0x08U) == 0U)
        {
            if (((uint)value & 0x04U) == 0U)
            {
                if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom50 : Bottom51;
                return ((uint)value & 0x01U) == 0U ? Bottom52 : Bottom53;
            }
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom54 : Bottom55;
            return ((uint)value & 0x01U) == 0U ? Bottom56 : Bottom57;
        }
        if (((uint)value & 0x04U) == 0U)
        {
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom58 : Bottom59;
            return ((uint)value & 0x01U) == 0U ? Bottom5A : Bottom5B;
        }
        if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom5C : Bottom5D;
        return ((uint)value & 0x01U) == 0U ? Bottom5E : Bottom5F;
    }

    private static ulong GetBottom60To6F(byte value)
    {
        if (((uint)value & 0x08U) == 0U)
        {
            if (((uint)value & 0x04U) == 0U)
            {
                if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom60 : Bottom61;
                return ((uint)value & 0x01U) == 0U ? Bottom62 : Bottom63;
            }
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom64 : Bottom65;
            return ((uint)value & 0x01U) == 0U ? Bottom66 : Bottom67;
        }
        if (((uint)value & 0x04U) == 0U)
        {
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom68 : Bottom69;
            return ((uint)value & 0x01U) == 0U ? Bottom6A : Bottom6B;
        }
        if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom6C : Bottom6D;
        return ((uint)value & 0x01U) == 0U ? Bottom6E : Bottom6F;
    }

    private static ulong GetBottom70To7E(byte value)
    {
        if (((uint)value & 0x08U) == 0U)
        {
            if (((uint)value & 0x04U) == 0U)
            {
                if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom70 : Bottom71;
                return ((uint)value & 0x01U) == 0U ? Bottom72 : Bottom73;
            }
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom74 : Bottom75;
            return ((uint)value & 0x01U) == 0U ? Bottom76 : Bottom77;
        }
        if (((uint)value & 0x04U) == 0U)
        {
            if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom78 : Bottom79;
            return ((uint)value & 0x01U) == 0U ? Bottom7A : Bottom7B;
        }
        if (((uint)value & 0x02U) == 0U) return ((uint)value & 0x01U) == 0U ? Bottom7C : Bottom7D;
        return ((uint)value & 0x01U) == 0U ? Bottom7E : Bottom3F;
    }

}
