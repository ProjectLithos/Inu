using System;
using Inu.Kernel.TrueType;

namespace Inu.Samples.TrueTypeRenderer;

public static unsafe class TrueTypeRendererSample
{
    public static Boolean InspectAndMeasure(UInt64 fontAddress, UInt64 fontLength, out Int32 width)
    {
        width=0;
        if(!KernelTrueType.TryInspect(fontAddress,fontLength,out TrueTypeFontInformation information))return false;
        return KernelTrueType.TryMeasureText(fontAddress,fontLength,"Inu",32U,out width,out _,out _);
    }

    public static Boolean RasterizeLetterA(UInt64 fontAddress,UInt64 fontLength,UInt64 coverageAddress,UInt32 coverageBytes,UInt32 stride,UInt64 workspaceAddress,UInt32 workspaceBytes,out TrueTypeGlyphMetrics metrics)
        => KernelTrueType.TryRasterizeGlyph(fontAddress,fontLength,(UInt32)'A',32U,coverageAddress,coverageBytes,stride,workspaceAddress,workspaceBytes,out metrics);
}
