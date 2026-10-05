using System;

namespace Inu.Kernel.TrueType;

public static unsafe partial class KernelTrueType
{
    private static void Raster(Edge* edges,UInt32 edgeCount,Byte* dst,UInt32 width,UInt32 height,UInt32 stride,UInt32 px,UInt16 upm,Int16 xMin,Int16 yMax)
    {
        const Int32 samples=4;for(UInt32 y=0;y<height;y++)for(UInt32 x=0;x<width;x++){UInt32 inside=0;for(Int32 sy=0;sy<samples;sy++)for(Int32 sx=0;sx<samples;sx++){Int64 px64=((Int64)x*64)+((2*sx+1)*64/(samples*2));Int64 py64=((Int64)y*64)+((2*sy+1)*64/(samples*2));Int64 fx=((px64*(Int64)upm)/px)+(Int64)xMin*64;Int64 fy=(Int64)yMax*64-((py64*(Int64)upm)/px);if(Inside(edges,edgeCount,fx,fy))inside++;}dst[(UInt64)y*stride+x]=(Byte)(inside*255U/(samples*samples));}
    }
    private static Boolean Inside(Edge* edges,UInt32 n,Int64 x64,Int64 y64){Int32 winding=0;for(UInt32 i=0;i<n;i++){Int64 x0=(Int64)edges[i].X0*64,y0=(Int64)edges[i].Y0*64,x1=(Int64)edges[i].X1*64,y1=(Int64)edges[i].Y1*64;if(y0<=y64){if(y1>y64&&Cross(x0,y0,x1,y1,x64,y64)>0)winding++;}else if(y1<=y64&&Cross(x0,y0,x1,y1,x64,y64)<0)winding--;}return winding!=0;}
    private static Int64 Cross(Int64 x0,Int64 y0,Int64 x1,Int64 y1,Int64 x,Int64 y)=>(x1-x0)*(y-y0)-(y1-y0)*(x-x0);

    private static UInt32 BlendChannel(UInt32 bg,UInt32 fg,UInt32 a)=>(bg*(255U-a)+fg*a+127U)/255U;
    private static Int32 F2Dot14(Int16 v)=>(Int32)v<<2;
    private static Int32 ScaleRound(Int32 v,UInt32 px,UInt16 upm)=>(Int32)(((Int64)v*px+(v>=0?upm/2:-(Int32)upm/2))/upm);
    private static Int32 ScaleFloor(Int32 v,UInt32 px,UInt16 upm){Int64 n=(Int64)v*px;if(n>=0)return(Int32)(n/upm);return(Int32)(-(((-n)+upm-1)/upm));}
    private static Int32 ScaleCeil(Int32 v,UInt32 px,UInt16 upm){Int64 n=(Int64)v*px;if(n>=0)return(Int32)((n+upm-1)/upm);return(Int32)(-((-n)/upm));}
    private static UInt64 Align8(UInt64 v)=>(v+7UL)&~7UL;
    private static UInt16 U16(Byte* p)=>(UInt16)(((UInt16)p[0]<<8)|p[1]);
    private static Int16 S16(Byte* p)=>(Int16)U16(p);
    private static UInt32 U32(Byte* p)=>((UInt32)p[0]<<24)|((UInt32)p[1]<<16)|((UInt32)p[2]<<8)|p[3];
}
