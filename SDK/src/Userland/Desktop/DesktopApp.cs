using System; using Inu.Userland.Gui; using Inu.Userland.Images;
namespace Inu.Userland.Desktop;
public delegate Boolean DesktopReadFile(String path,out Byte[] bytes);
public sealed class DesktopApp
{
    public const String DefaultWallpaper="/System/Wallpapers/INU-HEX.BMP";
    private readonly GuiApplication _gui; private readonly DesktopReadFile _read; private readonly PixelCanvas _canvas; private UInt64 _surface;
    public DesktopApp(GuiApplication gui,DesktopReadFile read,Int32 width,Int32 height){_gui=gui;_read=read;_canvas=new PixelCanvas(width,height);}
    public Boolean Start(){Int64 id=_gui.CreateDesktop((UInt32)_canvas.Width,(UInt32)_canvas.Height,"Inu Desktop");if(id<=0)return false;_surface=(UInt64)id;Paint();return _gui.Present(_surface,_canvas.Pixels)&&_gui.Show(_surface);}
    private void Paint(){_canvas.Clear(0xFF101722U);if(_read!=null&&_read(DefaultWallpaper,out Byte[] bytes)&&BmpImage.TryDecode(bytes,out BmpImage image))_canvas.BlitCover(image.Bgra32,image.Width,image.Height);_canvas.Fill(0,0,_canvas.Width,32,0xD0101620U);_canvas.DrawAscii("Inu",14,12,0xFFFFFFFFU);_canvas.Fill(0,_canvas.Height-44,_canvas.Width,44,0xD0101620U);_canvas.DrawAscii("Applications",14,_canvas.Height-28,0xFFFFFFFFU);}
}
