using System;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.TrueType;

namespace Inu.Kernel.Console;

/// <summary>Owns the allocation-free TrueType face and pre-rasterised ASCII glyph cache used by the graphics console.</summary>
internal static unsafe class TrueTypeConsoleFont
{
    private const UInt32 CacheGlyphCount = 128U;
    private const UInt32 CacheHeaderBytes = 32U;
    private const UInt32 CacheCoverageBytes = 16384U; // 128x128, matching the largest supported console font height.
    private const UInt32 CacheSlotBytes = CacheHeaderBytes + CacheCoverageBytes;
    private static UInt64 _fontAddress,_fontLength,_coverageAddress,_workspaceAddress,_cacheAddress,_cacheLength;
    private static UInt32 _coverageLength,_workspaceLength,_pixelHeight,_advance,_lineHeight,_ascent,_warmNext;
    private static UInt32 _cachedGlyphs,_fallbackGlyphs,_lastFallbackGlyph;
    private static Boolean _active;
    private static TrueTypeConsoleState _state=TrueTypeConsoleState.Inactive;

    internal static Boolean IsActive() => _active;
    internal static TrueTypeConsoleState State => _state;
    internal static UInt64 FontLength => _fontLength;
    internal static UInt32 Advance => _advance;
    internal static UInt32 LineHeight => _lineHeight;
    internal static UInt32 GlyphWidth => _advance > 1U ? _advance - 1U : _advance;
    internal static UInt32 CachedGlyphCount => _cachedGlyphs;
    internal static UInt32 FallbackGlyphCount => _fallbackGlyphs;
    internal static UInt32 LastFallbackGlyph => _lastFallbackGlyph;
    internal static UInt32 CacheTargetGlyphCount => 95U; // printable ASCII 32..126
    internal static Boolean IsCacheWarm => _warmNext>126U;

    internal static Boolean TryEnable(UInt32 pixelHeight)
    {
        _active=false; _state=TrueTypeConsoleState.Inactive; _cachedGlyphs=0U; _fallbackGlyphs=0U; _lastFallbackGlyph=0U; _warmNext=32U;
        UInt64 address=0UL,length=0UL;
        if(!SystemAssetCatalog.TryFind("SYSTEM/FONTS/CONSOLE.TTF",out address,out length) &&
           !SystemAssetCatalog.TryFind("SYSTEM/FONTS/DEJAVU.TTF",out address,out length))
        {
            address=Native.GetConsoleTrueTypeFontAddress();length=Native.GetConsoleTrueTypeFontLength();
        }
        _fontAddress=address; _fontLength=length;
        if(address==0UL||length<12UL){_state=TrueTypeConsoleState.FontMissing;return false;}
        if(pixelHeight<8U||pixelHeight>128U){_state=TrueTypeConsoleState.MetricsUnavailable;return false;}
        TrueTypeFontInformation info; if(!KernelTrueType.TryInspect(address,length,out info)){_state=TrueTypeConsoleState.FontInvalid;return false;}

        Int32 measured,ascent,descent;
        if(!KernelTrueType.TryMeasureText(address,length,"M",pixelHeight,out measured,out ascent,out descent) || measured<=0)
        { _state=TrueTypeConsoleState.MetricsUnavailable; return false; }
        TrueTypeGlyphMetrics metrics;
        if(!KernelTrueType.TryGetGlyphMetrics(address,length,(UInt32)'M',pixelHeight,out metrics))
        { _state=TrueTypeConsoleState.MetricsUnavailable; return false; }

        UInt64 coverageAddress=Native.GetConsoleTrueTypeCoverageAddress(), workspaceAddress=Native.GetConsoleTrueTypeWorkspaceAddress();
        UInt64 coverageCapacity=Native.GetConsoleTrueTypeCoverageLength(), workspaceCapacity=Native.GetConsoleTrueTypeWorkspaceLength();
        UInt64 cacheAddress=Native.GetConsoleTrueTypeCacheAddress(), cacheCapacity=Native.GetConsoleTrueTypeCacheLength();
        if(coverageAddress==0UL||workspaceAddress==0UL||coverageCapacity==0UL||workspaceCapacity==0UL||coverageCapacity>UInt32.MaxValue||workspaceCapacity>UInt32.MaxValue)
        { _state=TrueTypeConsoleState.ScratchUnavailable; return false; }
        if(cacheAddress==0UL||cacheCapacity<(UInt64)CacheSlotBytes*CacheGlyphCount)
        { _state=TrueTypeConsoleState.ScratchUnavailable; return false; }

        UInt32 advance=(UInt32)measured;
        UInt32 line=(UInt32)((ascent>0?ascent:0)+(descent>0?descent:0));
        if(line<pixelHeight)line=pixelHeight; line+=2U;
        _coverageAddress=coverageAddress; _workspaceAddress=workspaceAddress;
        _coverageLength=(UInt32)coverageCapacity; _workspaceLength=(UInt32)workspaceCapacity;
        _cacheAddress=cacheAddress; _cacheLength=cacheCapacity;
        _pixelHeight=pixelHeight; _advance=advance; _lineHeight=line;
        _ascent=(UInt32)(ascent>0?ascent:(Int32)pixelHeight);
        ClearCacheHeaders();
        _active=true; _state=TrueTypeConsoleState.Active;
        return true;
    }

    /// <summary>Pre-rasterises a bounded number of printable ASCII glyphs while the interactive console is otherwise idle.</summary>
    internal static Boolean WarmCacheStep(UInt32 budget)
    {
        if(!_active||budget==0U)return true;
        while(budget!=0U && _warmNext<=126U)
        {
            Byte value=(Byte)_warmNext++;
            if(!IsCached(value)) CacheGlyph(value);
            budget--;
        }
        return true;
    }

    internal static Boolean Draw(Byte value,UInt64 surfaceAddress,UInt32 width,UInt32 height,UInt32 pitch,UInt32 originX,UInt32 originY,UInt32 rgb)
    {
        if(!_active||surfaceAddress==0UL)return false;
        if(value>=CacheGlyphCount)return RegisterFallback(value);
        if(!IsCached(value) && !CacheGlyph(value))
        {
            // Cache storage is an optimisation, not part of the TrueType rendering contract.
            // Retry the glyph through the dedicated scratch coverage/workspace buffers before
            // allowing the framebuffer console to use its emergency bitmap face. This keeps a
            // single cache-slot/raster-cache problem from producing a visibly mixed font.
            if(DrawUncached(value,surfaceAddress,width,height,pitch,originX,originY,rgb))
            {
                if(_state==TrueTypeConsoleState.RenderFallback)_state=TrueTypeConsoleState.Active;
                return true;
            }
            return RegisterFallback(value);
        }
        Byte* slot=GetSlot(value);
        UInt32 gw=ReadU32(slot+4),gh=ReadU32(slot+8);
        if(gw==0U||gh==0U)return true;
        Int32 bearingX=ReadI32(slot+12),bearingY=ReadI32(slot+16);
        Int32 dx=(Int32)originX+bearingX; Int32 baseline=(Int32)originY+(Int32)_ascent; Int32 dy=baseline-bearingY;
        UInt64 coverage=(UInt64)(nuint)(slot+CacheHeaderBytes);
        if(!KernelTrueType.BlendCoverage32(new TrueTypeSurface32(surfaceAddress,width,height,pitch),dx,dy,coverage,CacheCoverageBytes,gw,gh,gw,rgb&0x00FFFFFFU))return RegisterFallback(value);
        if(_state==TrueTypeConsoleState.RenderFallback)_state=TrueTypeConsoleState.Active;
        return true;
    }

    private static Boolean DrawUncached(Byte value,UInt64 surfaceAddress,UInt32 width,UInt32 height,UInt32 pitch,UInt32 originX,UInt32 originY,UInt32 rgb)
    {
        TrueTypeGlyphMetrics metrics;
        if(!KernelTrueType.TryGetGlyphMetrics(_fontAddress,_fontLength,value,_pixelHeight,out metrics))return false;
        UInt32 gw=metrics.Width>0?(UInt32)metrics.Width:0U,gh=metrics.Height>0?(UInt32)metrics.Height:0U;
        if(gw==0U||gh==0U)return true;
        if(gw>128U||gh>128U||(UInt64)gw*gh>_coverageLength)return false;
        TrueTypeRasterWorkspace need;
        if(!KernelTrueType.TryGetRasterWorkspace(_fontAddress,_fontLength,value,out need)||need.TotalBytes>_workspaceLength)return false;
        if(!KernelTrueType.TryRasterizeGlyph(_fontAddress,_fontLength,value,_pixelHeight,_coverageAddress,_coverageLength,gw,_workspaceAddress,_workspaceLength,out metrics))return false;
        Int32 dx=(Int32)originX+metrics.BearingX;Int32 baseline=(Int32)originY+(Int32)_ascent;Int32 dy=baseline-metrics.BearingY;
        return KernelTrueType.BlendCoverage32(new TrueTypeSurface32(surfaceAddress,width,height,pitch),dx,dy,_coverageAddress,_coverageLength,gw,gh,gw,rgb&0x00FFFFFFU);
    }

    private static Boolean RegisterFallback(Byte value)
    {
        _state=TrueTypeConsoleState.RenderFallback;_fallbackGlyphs++;_lastFallbackGlyph=value;return false;
    }

    private static Boolean CacheGlyph(Byte value)
    {
        if(value>=CacheGlyphCount)return false;
        Byte* slot=GetSlot(value);
        TrueTypeGlyphMetrics metrics;
        if(!KernelTrueType.TryGetGlyphMetrics(_fontAddress,_fontLength,value,_pixelHeight,out metrics))return false;
        UInt32 gw=metrics.Width>0?(UInt32)metrics.Width:0U, gh=metrics.Height>0?(UInt32)metrics.Height:0U;
        if(gw>128U||gh>128U||(UInt64)gw*gh>CacheCoverageBytes)return false;
        if(gw!=0U&&gh!=0U)
        {
            TrueTypeRasterWorkspace needWorkspace;
            if(!KernelTrueType.TryGetRasterWorkspace(_fontAddress,_fontLength,value,out needWorkspace)||needWorkspace.TotalBytes>_workspaceLength)return false;
            UInt64 destination=(UInt64)(nuint)(slot+CacheHeaderBytes);
            if(!KernelTrueType.TryRasterizeGlyph(_fontAddress,_fontLength,value,_pixelHeight,destination,CacheCoverageBytes,gw,_workspaceAddress,_workspaceLength,out metrics))return false;
        }
        WriteU32(slot+4,gw); WriteU32(slot+8,gh); WriteI32(slot+12,metrics.BearingX); WriteI32(slot+16,metrics.BearingY);
        WriteU32(slot+20,metrics.AdvanceX>0?(UInt32)metrics.AdvanceX:_advance);
        *slot=1; _cachedGlyphs++;
        return true;
    }

    private static Byte* GetSlot(Byte value)=> (Byte*)(nuint)(_cacheAddress+(UInt64)value*CacheSlotBytes);
    private static Boolean IsCached(Byte value)=>value<CacheGlyphCount && *GetSlot(value)!=0;
    private static void ClearCacheHeaders(){for(UInt32 i=0U;i<CacheGlyphCount;i++)*GetSlot((Byte)i)=0;}
    private static UInt32 ReadU32(Byte* p)=>*(UInt32*)p;
    private static Int32 ReadI32(Byte* p)=>*(Int32*)p;
    private static void WriteU32(Byte* p,UInt32 v)=>*(UInt32*)p=v;
    private static void WriteI32(Byte* p,Int32 v)=>*(Int32*)p=v;
}
