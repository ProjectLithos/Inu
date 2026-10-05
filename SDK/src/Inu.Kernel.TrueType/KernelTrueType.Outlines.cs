using System;

namespace Inu.Kernel.TrueType;

public static unsafe partial class KernelTrueType
{
    private static Boolean TryCountPoints(Face* f,UInt16 glyph,UInt32 depth,out UInt32 points)
    {
        points=0;if(depth>8U)return false;UInt32 s,e;if(!TryGlyphRange(f,glyph,out s,out e))return false;if(s==e)return true;Byte* g=(Byte*)f->Address+f->Glyf.Offset+s;Int16 contours=S16(g);if(contours>=0){if(contours==0)return true;UInt32 need=10U+(UInt32)contours*2U;if(s+need>f->Glyf.Length)return false;points=(UInt32)U16(g+10U+(UInt32)(contours-1)*2U)+1U;return true;}UInt32 at=10U;UInt16 flags;do{if(at+4U>e-s)return false;flags=U16(g+at);UInt16 child=U16(g+at+2);at+=4U;UInt32 args=(flags&ArgWords)!=0U?4U:2U;if(at+args>e-s)return false;at+=args;if((flags&HaveScale)!=0U)at+=2U;else if((flags&HaveXYScale)!=0U)at+=4U;else if((flags&Have2x2)!=0U)at+=8U;if(at>e-s)return false;UInt32 childPoints;if(!TryCountPoints(f,child,depth+1U,out childPoints)||UInt32.MaxValue-points<childPoints)return false;points+=childPoints;}while((flags&MoreComponents)!=0U);return true;
    }

    private static Boolean BuildEdges(Face* f,UInt16 glyph,UInt32 depth,Int32 tx,Int32 ty,Int32 m00,Int32 m01,Int32 m10,Int32 m11,Point* points,UInt32 pointCapacity,Edge* edges,UInt32 edgeCapacity,ref UInt32 edgeCount)
    {
        if(depth>8U)return false;UInt32 s,e;if(!TryGlyphRange(f,glyph,out s,out e))return false;if(s==e)return true;Byte* g=(Byte*)f->Address+f->Glyf.Offset+s;Int16 contours=S16(g);if(contours>=0)return BuildSimple(g,e-s,(UInt16)contours,tx,ty,m00,m01,m10,m11,points,pointCapacity,edges,edgeCapacity,ref edgeCount);
        UInt32 at=10U;UInt16 flags;do{if(at+4U>e-s)return false;flags=U16(g+at);UInt16 child=U16(g+at+2);at+=4U;Int32 a1=0,a2=0;if((flags&ArgWords)!=0U){if(at+4U>e-s)return false;a1=S16(g+at);a2=S16(g+at+2);at+=4U;}else{if(at+2U>e-s)return false;a1=(SByte)g[at];a2=(SByte)g[at+1];at+=2U;}if((flags&ArgsAreXY)==0U)return false;Int32 ctx=tx+((m00*a1+m01*a2)>>16),cty=ty+((m10*a1+m11*a2)>>16);Int32 c00=65536,c01=0,c10=0,c11=65536;if((flags&HaveScale)!=0U){if(at+2U>e-s)return false;c00=c11=F2Dot14(S16(g+at));at+=2U;}else if((flags&HaveXYScale)!=0U){if(at+4U>e-s)return false;c00=F2Dot14(S16(g+at));c11=F2Dot14(S16(g+at+2));at+=4U;}else if((flags&Have2x2)!=0U){if(at+8U>e-s)return false;c00=F2Dot14(S16(g+at));c01=F2Dot14(S16(g+at+2));c10=F2Dot14(S16(g+at+4));c11=F2Dot14(S16(g+at+6));at+=8U;}Int32 n00=(Int32)(((Int64)m00*c00+(Int64)m01*c10)>>16),n01=(Int32)(((Int64)m00*c01+(Int64)m01*c11)>>16),n10=(Int32)(((Int64)m10*c00+(Int64)m11*c10)>>16),n11=(Int32)(((Int64)m10*c01+(Int64)m11*c11)>>16);if(!BuildEdges(f,child,depth+1U,ctx,cty,n00,n01,n10,n11,points,pointCapacity,edges,edgeCapacity,ref edgeCount))return false;}while((flags&MoreComponents)!=0U);return true;
    }

    private static Boolean BuildSimple(Byte* g,UInt32 length,UInt16 contours,Int32 tx,Int32 ty,Int32 m00,Int32 m01,Int32 m10,Int32 m11,Point* p,UInt32 capacity,Edge* edges,UInt32 edgeCapacity,ref UInt32 edgeCount)
    {
        if(contours==0U)return true;UInt32 endsAt=10U,headerEnd=endsAt+(UInt32)contours*2U;if(headerEnd+2U>length)return false;UInt32 count=(UInt32)U16(g+endsAt+(UInt32)(contours-1U)*2U)+1U;if(count==0U||count>capacity)return false;UInt16 instructionLength=U16(g+headerEnd);UInt32 at=headerEnd+2U+instructionLength;if(at>length)return false;
        UInt32 i=0;while(i<count){if(at>=length)return false;Byte flag=g[at++],repeat=1;if((flag&Repeat)!=0U){if(at>=length)return false;repeat=(Byte)(g[at++]+1U);}if(i+repeat>count)return false;for(Byte r=0;r<repeat;r++){p[i].On=(Byte)((flag&OnCurve)!=0U?1:0);p[i].Pad0=flag;i++;}}
        Int32 x=0;for(i=0;i<count;i++){Byte flag=p[i].Pad0;Int32 d=0;if((flag&XShort)!=0U){if(at>=length)return false;d=g[at++];if((flag&XSame)==0U)d=-d;}else if((flag&XSame)==0U){if(at+2U>length)return false;d=S16(g+at);at+=2U;}x+=d;p[i].X=x;}
        Int32 y=0;for(i=0;i<count;i++){Byte flag=p[i].Pad0;Int32 d=0;if((flag&YShort)!=0U){if(at>=length)return false;d=g[at++];if((flag&YSame)==0U)d=-d;}else if((flag&YSame)==0U){if(at+2U>length)return false;d=S16(g+at);at+=2U;}y+=d;Int32 ox=p[i].X,oy=y;p[i].X=tx+(Int32)(((Int64)m00*ox+(Int64)m01*oy)>>16);p[i].Y=ty+(Int32)(((Int64)m10*ox+(Int64)m11*oy)>>16);p[i].Pad0=0;}
        UInt32 first=0;for(UInt16 c=0;c<contours;c++){UInt32 last=U16(g+endsAt+(UInt32)c*2U);if(last>=count||last<first)return false;if(!Contour(p,first,last,edges,edgeCapacity,ref edgeCount))return false;first=last+1U;}return first==count;
    }

    private static Boolean Contour(Point* p,UInt32 first,UInt32 last,Edge* edges,UInt32 cap,ref UInt32 count)
    {
        Point start;if(p[first].On!=0)start=p[first];else if(p[last].On!=0)start=p[last];else start=Mid(p[last],p[first]);Point current=start;UInt32 i=first;while(i<=last){Point q=p[i];if(q.On!=0){if(!Line(current,q,edges,cap,ref count))return false;current=q;i++;continue;}Point next=i==last?p[first]:p[i+1U];if(next.On!=0){if(!Quad(current,q,next,edges,cap,ref count))return false;current=next;i+=2U;}else{Point implied=Mid(q,next);if(!Quad(current,q,implied,edges,cap,ref count))return false;current=implied;i++;}}return Line(current,start,edges,cap,ref count);
    }
    private static Point Mid(Point a,Point b)=>new Point{X=(a.X+b.X)/2,Y=(a.Y+b.Y)/2,On=1};
    private static Boolean Line(Point a,Point b,Edge* edges,UInt32 cap,ref UInt32 count){if(a.X==b.X&&a.Y==b.Y)return true;if(count>=cap)return false;edges[count++]=new Edge{X0=a.X,Y0=a.Y,X1=b.X,Y1=b.Y};return true;}
    private static Boolean Quad(Point a,Point c,Point b,Edge* edges,UInt32 cap,ref UInt32 count){Point prev=a;for(Int32 s=1;s<=CurveSteps;s++){Int64 t=(Int64)s*65536/CurveSteps,u=65536-t;Point q=new Point{X=(Int32)((u*u*a.X+2*u*t*c.X+t*t*b.X)>>32),Y=(Int32)((u*u*a.Y+2*u*t*c.Y+t*t*b.Y)>>32),On=1};if(!Line(prev,q,edges,cap,ref count))return false;prev=q;}return true;}
}
