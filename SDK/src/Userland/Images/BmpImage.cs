using System;
namespace Inu.Userland.Images;
public sealed class BmpImage
{
    public Int32 Width{get;} public Int32 Height{get;} public Byte[] Bgra32{get;}
    private BmpImage(Int32 w,Int32 h,Byte[] p){Width=w;Height=h;Bgra32=p;}
    public static Boolean TryDecode(Byte[] data,out BmpImage image)
    {
        image=null;if(data==null||data.Length<54||data[0]!=(Byte)'B'||data[1]!=(Byte)'M')return false;
        Int32 offset=Read32(data,10),dib=Read32(data,14),w=Read32(data,18),hRaw=Read32(data,22);Int32 bpp=Read16(data,28);Int32 compression=Read32(data,30);
        if(dib<40||w<=0||hRaw==0||(bpp!=24&&bpp!=32)||compression!=0)return false;Boolean topDown=hRaw<0;Int32 h=hRaw<0?-hRaw:hRaw;Int64 stride64=((Int64)w*bpp+31L)/32L*4L;if(stride64>Int32.MaxValue)return false;Int32 stride=(Int32)stride64;
        if(offset<0||(Int64)offset+(Int64)stride*h>data.Length)return false;Byte[] pixels=new Byte[w*h*4];
        for(Int32 y=0;y<h;y++){Int32 sy=topDown?y:h-1-y;Int32 src=offset+sy*stride;Int32 dst=y*w*4;for(Int32 x=0;x<w;x++){pixels[dst++]=data[src++];pixels[dst++]=data[src++];pixels[dst++]=data[src++];pixels[dst++]=bpp==32?data[src++]: (Byte)255;}}
        image=new BmpImage(w,h,pixels);return true;
    }
    private static Int32 Read16(Byte[] b,Int32 o)=>b[o]|b[o+1]<<8;
    private static Int32 Read32(Byte[] b,Int32 o)=>b[o]|b[o+1]<<8|b[o+2]<<16|b[o+3]<<24;
}
