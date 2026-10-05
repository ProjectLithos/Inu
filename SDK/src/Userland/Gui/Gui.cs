using System;

namespace Inu.Userland.Gui;

/// <summary>Portable ring-3 GUI contract. The application supplies its existing Inu Get/Set/Event syscall bridge.</summary>
public static class GuiMessages
{
    public const String Capabilities="gui.capabilities",SurfaceCreate="gui.surface.create",SurfaceDestroy="gui.surface.destroy",SurfaceGeometry="gui.surface.geometry",SurfaceVisibility="gui.surface.visibility",SurfacePresent="gui.surface.present",Focus="gui.focus",EventNext="gui.event.next",WindowClose="gui.window.close";
}
public enum GuiEventKind : UInt32 { None=0,FocusGained=1,FocusLost=2,KeyDown=3,KeyUp=4,PointerMove=5,PointerDown=6,PointerUp=7,CloseRequested=8 }
public struct GuiEvent { public UInt32 Kind,Modifiers; public UInt64 SurfaceId; public Int64 X,Y; public UInt64 Value0,Value1,Sequence; }
public delegate Int64 GuiGet(String message,Byte[] output);
public delegate Int64 GuiSet(String message,UInt64 value0,UInt64 value1,UInt64 value2,UInt64 value3,Byte[] data);
public delegate Int64 GuiSignal(String message,UInt64 value0,UInt64 value1,UInt64 value2,UInt64 value3,Byte[] data);

public sealed class GuiApplication
{
    private readonly GuiGet _get; private readonly GuiSet _set; private readonly GuiSignal _event;
    public GuiApplication(GuiGet get,GuiSet set,GuiSignal signal){_get=get??throw new ArgumentNullException(nameof(get));_set=set??throw new ArgumentNullException(nameof(set));_event=signal??throw new ArgumentNullException(nameof(signal));}
    public Int64 CreateWindow(UInt32 width,UInt32 height,String title){Byte[] t=Ascii(title);return _set(GuiMessages.SurfaceCreate,width,height,3U,0U,t);}
    public Int64 CreateDesktop(UInt32 width,UInt32 height,String title){Byte[] t=Ascii(title);return _set(GuiMessages.SurfaceCreate,width,height,4U,0U,t);}
    public Boolean Show(UInt64 surface)=>_set(GuiMessages.SurfaceVisibility,surface,1UL,0UL,0UL,null)==0L;
    public Boolean Hide(UInt64 surface)=>_set(GuiMessages.SurfaceVisibility,surface,0UL,0UL,0UL,null)==0L;
    public Boolean Move(UInt64 surface,Int32 x,Int32 y)=>_set(GuiMessages.SurfaceGeometry,surface,unchecked((UInt32)x),unchecked((UInt32)y),0UL,null)==0L;
    public Boolean Focus(UInt64 surface)=>_set(GuiMessages.Focus,surface,0UL,0UL,0UL,null)==0L;
    public Boolean Destroy(UInt64 surface)=>_set(GuiMessages.SurfaceDestroy,surface,0UL,0UL,0UL,null)==0L;
    public Boolean Present(UInt64 surface,Byte[] bgra32)=>bgra32!=null&&_event(GuiMessages.SurfacePresent,surface,0UL,0UL,0UL,bgra32)>=0L;
    public Boolean RequestClose(UInt64 surface)=>_event(GuiMessages.WindowClose,surface,0UL,0UL,0UL,null)==0L;
    public Int64 Poll(Byte[] serializedEvent)=>serializedEvent!=null&&serializedEvent.Length>=64?_get(GuiMessages.EventNext,serializedEvent):-22L;
    private static Byte[] Ascii(String value){if(String.IsNullOrEmpty(value))return new Byte[0];Int32 n=value.Length>64?64:value.Length;Byte[] b=new Byte[n];for(Int32 i=0;i<n;i++){Char c=value[i];b[i]=(Byte)(c>=32&&c<=126?c:'?');}return b;}
}

/// <summary>Small retained-mode widget base used entirely in ring 3.</summary>
public abstract class Widget { public Int32 X,Y,Width,Height; public Boolean Visible=true,Enabled=true; public virtual Boolean Hit(Int32 x,Int32 y)=>Visible&&x>=X&&y>=Y&&x<X+Width&&y<Y+Height; public abstract void Paint(PixelCanvas canvas); }
public sealed class Panel : Widget { public UInt32 Background=0xFF202830U; public override void Paint(PixelCanvas c){c.Fill(X,Y,Width,Height,Background);} }
public sealed class Label : Widget { public String Text=String.Empty; public UInt32 Background=0x00000000U; public override void Paint(PixelCanvas c){if((Background>>24)!=0)c.Fill(X,Y,Width,Height,Background);c.DrawAscii(Text,X+4,Y+4,0xFFFFFFFFU);} }
public sealed class Button : Widget { public String Text="Button"; public Boolean Pressed; public override void Paint(PixelCanvas c){c.Fill(X,Y,Width,Height,Pressed?0xFF31577AU:0xFF3B6A94U);c.Frame(X,Y,Width,Height,0xFFFFFFFFU);c.DrawAscii(Text,X+6,Y+6,0xFFFFFFFFU);} }
public sealed class TextBox : Widget { public String Text=String.Empty; public Boolean Focused; public override void Paint(PixelCanvas c){c.Fill(X,Y,Width,Height,0xFFFFFFFFU);c.Frame(X,Y,Width,Height,Focused?0xFF3B82F6U:0xFF606060U);c.DrawAscii(Text,X+4,Y+5,0xFF101010U);} }

/// <summary>Minimal BGRA32 software canvas for ordinary userland GUI processes.</summary>
public sealed class PixelCanvas
{
    public PixelCanvas(Int32 width,Int32 height){if(width<=0||height<=0)throw new ArgumentOutOfRangeException();Width=width;Height=height;Pixels=new Byte[width*height*4];}
    public Int32 Width{get;} public Int32 Height{get;} public Byte[] Pixels{get;}
    public void Clear(UInt32 color)=>Fill(0,0,Width,Height,color);
    public void Fill(Int32 x,Int32 y,Int32 w,Int32 h,UInt32 color){for(Int32 yy=0;yy<h;yy++)for(Int32 xx=0;xx<w;xx++)Put(x+xx,y+yy,color);}
    public void Frame(Int32 x,Int32 y,Int32 w,Int32 h,UInt32 color){for(Int32 i=0;i<w;i++){Put(x+i,y,color);Put(x+i,y+h-1,color);}for(Int32 i=0;i<h;i++){Put(x,y+i,color);Put(x+w-1,y+i,color);}}
    public void DrawAscii(String text,Int32 x,Int32 y,UInt32 color){if(String.IsNullOrEmpty(text))return;for(Int32 i=0;i<text.Length;i++){DrawGlyph(text[i],x+i*6,y,color);}}
    public void BlitCover(Byte[] bgra,Int32 sourceWidth,Int32 sourceHeight){if(bgra==null||sourceWidth<=0||sourceHeight<=0||bgra.Length<sourceWidth*sourceHeight*4)return;Int64 sx=(Int64)sourceWidth*Height,sy=(Int64)sourceHeight*Width;Boolean byWidth=sx>sy;Int32 cropW=byWidth?(Int32)((Int64)sourceHeight*Width/Height):sourceWidth;Int32 cropH=byWidth?sourceHeight:(Int32)((Int64)sourceWidth*Height/Width);Int32 ox=(sourceWidth-cropW)/2,oy=(sourceHeight-cropH)/2;for(Int32 y=0;y<Height;y++){Int32 srcY=oy+(Int32)((Int64)y*cropH/Height);for(Int32 x=0;x<Width;x++){Int32 srcX=ox+(Int32)((Int64)x*cropW/Width);Int32 si=(srcY*sourceWidth+srcX)*4,di=(y*Width+x)*4;Pixels[di]=bgra[si];Pixels[di+1]=bgra[si+1];Pixels[di+2]=bgra[si+2];Pixels[di+3]=bgra[si+3];}}}
    private void DrawGlyph(Char ch,Int32 x,Int32 y,UInt32 color){UInt32 seed=(UInt32)ch*2654435761U;for(Int32 row=0;row<7;row++){UInt32 bits=(seed>>(row*3))^(seed>>(row+9));for(Int32 col=0;col<5;col++)if(((bits>>col)&1U)!=0U)Put(x+col,y+row,color);}}
    private void Put(Int32 x,Int32 y,UInt32 c){if(x<0||y<0||x>=Width||y>=Height)return;Int32 i=(y*Width+x)*4;Pixels[i]=(Byte)c;Pixels[i+1]=(Byte)(c>>8);Pixels[i+2]=(Byte)(c>>16);Pixels[i+3]=(Byte)(c>>24);}
}

/// <summary>Reference userland desktop-shell surface; policy stays outside the kernel compositor.</summary>
public sealed class DesktopShell
{
    public DesktopShell(GuiApplication gui,Int32 width,Int32 height){Gui=gui;Canvas=new PixelCanvas(width,height);}
    public GuiApplication Gui{get;} public PixelCanvas Canvas{get;}
    public void Paint(){Canvas.Clear(0xFF18202AU);Canvas.Fill(0,Canvas.Height-36,Canvas.Width,36,0xFF252F3BU);Canvas.DrawAscii("INU",12,Canvas.Height-25,0xFFFFFFFFU);}
}
