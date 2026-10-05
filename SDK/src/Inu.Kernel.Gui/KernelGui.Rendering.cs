using System;
using Inu.Kernel.Graphics;
using Inu.Kernel.Heap;
using Inu.Kernel.Ps2;
using Inu.Kernel.Security;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Synchronization;
using Inu.Usb.Hid;

namespace Inu.Kernel.Gui;

public static unsafe partial class KernelGui
{
    public static Boolean Composite()
    {
        if(!_guiMutex.Lock())return false;
        Boolean result=CompositeUnlocked();
        _guiMutex.Unlock();
        return result;
    }

    private static void ScaleCover(UInt32* source,UInt32 sourceWidth,UInt32 sourceHeight,UInt32* destination,UInt32 destinationWidth,UInt32 destinationHeight)
    {
        if(source==null||destination==null||sourceWidth==0U||sourceHeight==0U||destinationWidth==0U||destinationHeight==0U)return;
        UInt64 sx=(UInt64)sourceWidth*destinationHeight,sy=(UInt64)sourceHeight*destinationWidth;Boolean cropWidth=sx>sy;
        UInt32 cropW=cropWidth?(UInt32)((UInt64)sourceHeight*destinationWidth/destinationHeight):sourceWidth;
        UInt32 cropH=cropWidth?sourceHeight:(UInt32)((UInt64)sourceWidth*destinationHeight/destinationWidth);
        UInt32 ox=(sourceWidth-cropW)/2U,oy=(sourceHeight-cropH)/2U;
        for(UInt32 y=0U;y<destinationHeight;y++){UInt32 srcY=oy+(UInt32)((UInt64)y*cropH/destinationHeight);for(UInt32 x=0U;x<destinationWidth;x++){UInt32 srcX=ox+(UInt32)((UInt64)x*cropW/destinationWidth);destination[y*destinationWidth+x]=source[srcY*sourceWidth+srcX];}}
    }
}
