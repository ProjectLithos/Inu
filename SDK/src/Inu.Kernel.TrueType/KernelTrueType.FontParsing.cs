using System;

namespace Inu.Kernel.TrueType;

public static unsafe partial class KernelTrueType
{
    private static Boolean TryOpen(UInt64 address,UInt64 length,out Face f)
    {
        f=default;if(address==0UL||length<12UL)return false;Byte* b=(Byte*)address;UInt32 sfnt=U32(b);if(sfnt!=0x00010000U&&sfnt!=0x74727565U)return false;UInt16 count=U16(b+4);if(count==0U||(UInt64)12U+(UInt64)count*16UL>length)return false;f.Address=address;f.Length=length;
        for(UInt16 i=0;i<count;i++){Byte* r=b+12U+(UInt32)i*16U;UInt32 tag=U32(r),off=U32(r+8),len=U32(r+12);if((UInt64)off+len>length)return false;Table t=new Table{Offset=off,Length=len};if(tag==TagHead)f.Head=t;else if(tag==TagMaxp)f.Maxp=t;else if(tag==TagHhea)f.Hhea=t;else if(tag==TagHmtx)f.Hmtx=t;else if(tag==TagLoca)f.Loca=t;else if(tag==TagGlyf)f.Glyf=t;else if(tag==TagCmap)f.Cmap=t;else if(tag==TagKern)f.Kern=t;}
        if(f.Head.Length<54U||f.Maxp.Length<6U||f.Hhea.Length<36U||f.Hmtx.Length<4U||f.Loca.Length<4U||f.Glyf.Length<10U||f.Cmap.Length<4U)return false;Byte* head=b+f.Head.Offset;if(U32(head+12)!=0x5F0F3CF5U)return false;f.UnitsPerEm=U16(head+18);f.LocaFormat=S16(head+50);f.GlyphCount=U16(b+f.Maxp.Offset+4);f.Ascender=S16(b+f.Hhea.Offset+4);f.Descender=S16(b+f.Hhea.Offset+6);f.NumberHMetrics=U16(b+f.Hhea.Offset+34);if(f.UnitsPerEm<16U||f.GlyphCount==0U||f.NumberHMetrics==0U||f.NumberHMetrics>f.GlyphCount||(f.LocaFormat!=0&&f.LocaFormat!=1))return false;
        UInt64 locaNeed=(UInt64)(f.GlyphCount+1U)*(f.LocaFormat==0?2UL:4UL);if(locaNeed>f.Loca.Length||(UInt64)f.NumberHMetrics*4UL+(UInt64)(f.GlyphCount-f.NumberHMetrics)*2UL>f.Hmtx.Length)return false;Face selected=f;if(!SelectCmap(&selected))return false;f=selected;return true;
    }

    private static Boolean SelectCmap(Face* f)
    {
        Byte* b=(Byte*)f->Address+f->Cmap.Offset;UInt16 n=U16(b+2);if(4UL+(UInt64)n*8UL>f->Cmap.Length)return false;for(UInt16 i=0;i<n;i++){Byte* r=b+4U+(UInt32)i*8U;UInt32 off=U32(r+4);if(off+2U>f->Cmap.Length)continue;UInt16 platform=U16(r),encoding=U16(r+2),format=U16(b+off);if((platform==0U||(platform==3U&&(encoding==1U||encoding==10U)))&&format==12U)f->Cmap12=off;else if((platform==0U||(platform==3U&&encoding==1U))&&format==4U&&f->Cmap4==0U)f->Cmap4=off;}return f->Cmap12!=0U||f->Cmap4!=0U;
    }

    private static Boolean TryMap(Face* f,UInt32 cp,out UInt16 glyph)
    {
        glyph=0;Byte* cmap=(Byte*)f->Address+f->Cmap.Offset;if(f->Cmap12!=0U){Byte* s=cmap+f->Cmap12;if(f->Cmap12+16U>f->Cmap.Length)return false;UInt32 len=U32(s+4),groups=U32(s+12);if((UInt64)f->Cmap12+len>f->Cmap.Length||len<16U+(UInt64)groups*12UL)return false;UInt32 lo=0,hi=groups;while(lo<hi){UInt32 mid=(lo+hi)>>1;Byte* g=s+16U+mid*12U;UInt32 end=U32(g+4);if(end<cp)lo=mid+1;else hi=mid;}if(lo<groups){Byte* g=s+16U+lo*12U;UInt32 start=U32(g),end=U32(g+4),baseGlyph=U32(g+8);if(cp>=start&&cp<=end){UInt32 v=baseGlyph+(cp-start);if(v<f->GlyphCount){glyph=(UInt16)v;return true;}}}}
        if(cp<=0xFFFFU&&f->Cmap4!=0U){Byte* s=cmap+f->Cmap4;if(f->Cmap4+14U>f->Cmap.Length)return false;UInt16 len=U16(s+2),segCount=(UInt16)(U16(s+6)/2U);if((UInt32)f->Cmap4+len>f->Cmap.Length||segCount==0U)return false;Byte* endCodes=s+14;Byte* startCodes=endCodes+(UInt32)segCount*2U+2U;Byte* deltas=startCodes+(UInt32)segCount*2U;Byte* ranges=deltas+(UInt32)segCount*2U;for(UInt16 i=0;i<segCount;i++){UInt16 end=U16(endCodes+(UInt32)i*2U);if(cp>end)continue;UInt16 start=U16(startCodes+(UInt32)i*2U);if(cp<start)return true;UInt16 range=U16(ranges+(UInt32)i*2U);Int16 delta=S16(deltas+(UInt32)i*2U);UInt16 v;if(range==0U)v=(UInt16)((cp+(UInt32)(Int32)delta)&0xFFFFU);else{Byte* ro=ranges+(UInt32)i*2U;Byte* gp=ro+range+(cp-start)*2U;if(gp+2>s+len)return false;v=U16(gp);if(v!=0U)v=(UInt16)((v+(UInt32)(Int32)delta)&0xFFFFU);}if(v<f->GlyphCount)glyph=v;return true;}}
        return true;
    }

    private static Boolean TryMetrics(Face* f,UInt16 glyph,UInt32 px,out TrueTypeGlyphMetrics m)
    {m=default;Int16 x0,y0,x1,y1;if(!TryGlyphBox(f,glyph,out x0,out y0,out x1,out y1))return false;UInt16 adv;Int16 lsb;if(!TryHMetric(f,glyph,out adv,out lsb))return false;Int32 w=ScaleCeil((Int32)x1-x0,px,f->UnitsPerEm),h=ScaleCeil((Int32)y1-y0,px,f->UnitsPerEm);m=new TrueTypeGlyphMetrics(glyph,w,h,ScaleFloor(x0,px,f->UnitsPerEm),ScaleCeil(y1,px,f->UnitsPerEm),ScaleRound(adv,px,f->UnitsPerEm));return true;}
    private static Boolean TryHMetric(Face* f,UInt16 glyph,out UInt16 adv,out Int16 lsb){adv=0;lsb=0;Byte* b=(Byte*)f->Address+f->Hmtx.Offset;if(glyph<f->NumberHMetrics){adv=U16(b+(UInt32)glyph*4U);lsb=S16(b+(UInt32)glyph*4U+2U);return true;}adv=U16(b+(UInt32)(f->NumberHMetrics-1U)*4U);UInt32 off=(UInt32)f->NumberHMetrics*4U+(UInt32)(glyph-f->NumberHMetrics)*2U;if(off+2U>f->Hmtx.Length)return false;lsb=S16(b+off);return true;}
    private static Boolean TryGlyphRange(Face* f,UInt16 glyph,out UInt32 start,out UInt32 end){start=end=0;if(glyph>=f->GlyphCount)return false;Byte* l=(Byte*)f->Address+f->Loca.Offset;if(f->LocaFormat==0){start=(UInt32)U16(l+(UInt32)glyph*2U)*2U;end=(UInt32)U16(l+(UInt32)(glyph+1U)*2U)*2U;}else{start=U32(l+(UInt32)glyph*4U);end=U32(l+(UInt32)(glyph+1U)*4U);}return start<=end&&end<=f->Glyf.Length;}
    private static Boolean TryGlyphBox(Face* f,UInt16 glyph,out Int16 x0,out Int16 y0,out Int16 x1,out Int16 y1){x0=y0=x1=y1=0;UInt32 s,e;if(!TryGlyphRange(f,glyph,out s,out e))return false;if(s==e)return true;if(e-s<10U)return false;Byte* g=(Byte*)f->Address+f->Glyf.Offset+s;x0=S16(g+2);y0=S16(g+4);x1=S16(g+6);y1=S16(g+8);return x1>=x0&&y1>=y0;}
}
