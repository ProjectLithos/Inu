using System;

namespace Inu.Kernel.TrueType;

public static unsafe partial class KernelTrueType
{
    public static Boolean TryInspect(UInt64 address, UInt64 length, out TrueTypeFontInformation information)
    {
        information=default; Face f; if(!TryOpen(address,length,out f)) return false;
        information=new TrueTypeFontInformation(f.UnitsPerEm,f.Ascender,f.Descender,f.GlyphCount,f.Cmap4!=0U,f.Cmap12!=0U,f.Kern.Length!=0U); return true;
    }

    /// <summary>Returns the glyph index selected by the font's Unicode cmap.</summary>
    public static Boolean TryGetGlyphIndex(UInt64 address, UInt64 length, UInt32 codePoint, out UInt16 glyphIndex)
    { glyphIndex=0; Face f; return TryOpen(address,length,out f)&&TryMap(&f,codePoint,out glyphIndex); }

    /// <summary>Returns scaled glyph metrics. pixelHeight is the em height in pixels.</summary>
    public static Boolean TryGetGlyphMetrics(UInt64 address, UInt64 length, UInt32 codePoint, UInt32 pixelHeight, out TrueTypeGlyphMetrics metrics)
    {
        metrics=default; if(pixelHeight==0U||pixelHeight>4096U)return false; Face f; UInt16 glyph; if(!TryOpen(address,length,out f)||!TryMap(&f,codePoint,out glyph))return false;
        return TryMetrics(&f,glyph,pixelHeight,out metrics);
    }

    /// <summary>Returns conservative scratch memory required to decode the glyph outline.</summary>
    public static Boolean TryGetRasterWorkspace(UInt64 address, UInt64 length, UInt32 codePoint, out TrueTypeRasterWorkspace workspace)
    {
        workspace=default; Face f; UInt16 glyph; if(!TryOpen(address,length,out f)||!TryMap(&f,codePoint,out glyph))return false;
        UInt32 points; if(!TryCountPoints(&f,glyph,0U,out points))return false;
        if(points>65535U)return false; UInt64 pointBytes=(UInt64)points*(UInt64)sizeof(Point); UInt64 edgeBytes=(UInt64)(points*CurveSteps+points+64U)*(UInt64)sizeof(Edge); UInt64 total=Align8(pointBytes)+edgeBytes;
        if(total>UInt32.MaxValue)return false; workspace=new TrueTypeRasterWorkspace((UInt32)pointBytes,(UInt32)edgeBytes,(UInt32)total); return true;
    }

    /// <summary>Rasterises a glyph into an 8-bit coverage bitmap. The destination is cleared before rendering.</summary>
    public static Boolean TryRasterizeGlyph(UInt64 address, UInt64 length, UInt32 codePoint, UInt32 pixelHeight, UInt64 coverageAddress, UInt32 coverageLength, UInt32 stride, UInt64 workspaceAddress, UInt32 workspaceLength, out TrueTypeGlyphMetrics metrics)
    {
        metrics=default; if(coverageAddress==0UL||workspaceAddress==0UL||pixelHeight==0U||stride==0U)return false; Face f; UInt16 glyph; if(!TryOpen(address,length,out f)||!TryMap(&f,codePoint,out glyph)||!TryMetrics(&f,glyph,pixelHeight,out metrics))return false;
        UInt32 width=(UInt32)(metrics.Width<0?0:metrics.Width),height=(UInt32)(metrics.Height<0?0:metrics.Height); if(width==0U||height==0U)return true; if(stride<width||(UInt64)stride*height>coverageLength)return false;
        TrueTypeRasterWorkspace need; if(!TryGetRasterWorkspace(address,length,codePoint,out need)||workspaceLength<need.TotalBytes)return false;
        Byte* coverage=(Byte*)coverageAddress; for(UInt64 i=0;i<(UInt64)stride*height;i++)coverage[i]=0;
        Byte* work=(Byte*)workspaceAddress; Point* points=(Point*)work; Edge* edges=(Edge*)(work+Align8(need.PointBytes)); UInt32 pointCapacity=need.PointBytes/(UInt32)sizeof(Point),edgeCapacity=(workspaceLength-(UInt32)Align8(need.PointBytes))/(UInt32)sizeof(Edge),edgeCount=0;
        Int16 xMin,yMin,xMax,yMax; if(!TryGlyphBox(&f,glyph,out xMin,out yMin,out xMax,out yMax))return false;
        if(!BuildEdges(&f,glyph,0U,0,0,65536,0,0,65536,points,pointCapacity,edges,edgeCapacity,ref edgeCount))return false;
        Raster(edges,edgeCount,coverage,width,height,stride,pixelHeight,f.UnitsPerEm,xMin,yMax); return true;
    }

    /// <summary>Alpha-composites a previously rasterised coverage bitmap onto a packed 32-bit surface.</summary>
    public static Boolean BlendCoverage32(TrueTypeSurface32 surface, Int32 destinationX, Int32 destinationY, UInt64 coverageAddress, UInt32 coverageLength, UInt32 width, UInt32 height, UInt32 stride, UInt32 rgb)
    {
        if(!surface.IsValid||coverageAddress==0UL||stride<width||(UInt64)stride*height>coverageLength)return false; Byte* cov=(Byte*)coverageAddress; UInt32* pixels=(UInt32*)surface.Address;
        for(UInt32 y=0;y<height;y++){Int32 dy=destinationY+(Int32)y;if(dy<0||(UInt32)dy>=surface.Height)continue;for(UInt32 x=0;x<width;x++){Int32 dx=destinationX+(Int32)x;if(dx<0||(UInt32)dx>=surface.Width)continue;UInt32 a=cov[(UInt64)y*stride+x];if(a==0U)continue;UInt32* p=pixels+(UInt64)(UInt32)dy*surface.PixelsPerScanLine+(UInt32)dx;UInt32 bg=*p;UInt32 rb=BlendChannel(bg&255U,rgb&255U,a), rg=BlendChannel((bg>>8)&255U,(rgb>>8)&255U,a), rr=BlendChannel((bg>>16)&255U,(rgb>>16)&255U,a);*p=(bg&0xFF000000U)|(rr<<16)|(rg<<8)|rb;}}
        return true;
    }

    /// <summary>Returns legacy TrueType 'kern' horizontal kerning in scaled pixels. GPOS kerning is intentionally separate.</summary>
    public static Boolean TryGetKerning(UInt64 address, UInt64 length, UInt32 leftCodePoint, UInt32 rightCodePoint, UInt32 pixelHeight, out Int32 pixels)
    {
        pixels=0; Face f; UInt16 l,r;if(pixelHeight==0U||!TryOpen(address,length,out f)||!TryMap(&f,leftCodePoint,out l)||!TryMap(&f,rightCodePoint,out r))return false;if(f.Kern.Length<4U)return true;
        Byte* b=(Byte*)f.Address+f.Kern.Offset;UInt16 version=U16(b);UInt16 tables=U16(b+2);if(version!=0U)return true;UInt32 at=4U;for(UInt16 t=0;t<tables;t++){if(at+6U>f.Kern.Length)return false;UInt16 len=U16(b+at+2),coverage=U16(b+at+4);if(len<6U||at+len>f.Kern.Length)return false;if((coverage&0xFFU)==0U&&len>=14U){Byte* s=b+at+6;UInt16 n=U16(s);UInt32 lo=0,hi=n;UInt32 key=((UInt32)l<<16)|r;while(lo<hi){UInt32 mid=(lo+hi)>>1;Byte* p=s+8U+mid*6U;UInt32 k=((UInt32)U16(p)<<16)|U16(p+2);if(k<key)lo=mid+1;else hi=mid;}if(lo<n){Byte* p=s+8U+lo*6U;if(U16(p)==l&&U16(p+2)==r){Int16 v=S16(p+4);pixels=ScaleRound(v,pixelHeight,f.UnitsPerEm);return true;}}}at+=len;}return true;
    }

    /// <summary>Measures a UTF-16 string using advances and legacy kern pairs without allocating.</summary>
    public static Boolean TryMeasureText(UInt64 address, UInt64 length, String text, UInt32 pixelHeight, out Int32 width, out Int32 ascent, out Int32 descent)
    {
        width=0;ascent=0;descent=0;if(text==null||pixelHeight==0U)return false;Face f;if(!TryOpen(address,length,out f))return false;ascent=ScaleRound(f.Ascender,pixelHeight,f.UnitsPerEm);descent=ScaleRound(-f.Descender,pixelHeight,f.UnitsPerEm);UInt32 previous=0;Boolean havePrevious=false;for(Int32 i=0;i<text.Length;i++){UInt32 cp=text[i];if(cp>=0xD800U&&cp<=0xDBFFU&&i+1<text.Length){UInt32 low=text[i+1];if(low>=0xDC00U&&low<=0xDFFFU){cp=0x10000U+((cp-0xD800U)<<10)+(low-0xDC00U);i++;}}UInt16 g;if(!TryMap(&f,cp,out g))g=0;if(havePrevious){Int32 k;if(TryGetKerning(address,length,previous,cp,pixelHeight,out k))width+=k;}UInt16 adv;Int16 lsb;if(!TryHMetric(&f,g,out adv,out lsb))return false;width+=ScaleRound(adv,pixelHeight,f.UnitsPerEm);previous=cp;havePrevious=true;}return true;
    }
}
