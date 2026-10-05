using System; using Inu.Userland.Gui;
namespace Inu.Userland.Login;
public delegate Boolean LoginAuthenticate(String username,String password); public delegate void LoginAction();
public sealed class LoginApp
{
    private readonly GuiApplication _gui;private readonly LoginAuthenticate _authenticate;private readonly LoginAction _sessionStarted,_shutdown;private readonly PixelCanvas _canvas=new(440,420);private UInt64 _surface;private String _username=String.Empty,_password=String.Empty;private Boolean _passwordFocus;
    public LoginApp(GuiApplication gui,LoginAuthenticate authenticate,LoginAction sessionStarted,LoginAction shutdown){_gui=gui;_authenticate=authenticate;_sessionStarted=sessionStarted;_shutdown=shutdown;}
    public Boolean Start(Int32 displayWidth,Int32 displayHeight){Int64 id=_gui.CreateWindow(440,420,"Log in");if(id<=0)return false;_surface=(UInt64)id;_gui.Move(_surface,(displayWidth-440)/2,(displayHeight-420)/2);Paint();return _gui.Present(_surface,_canvas.Pixels)&&_gui.Show(_surface)&&_gui.Focus(_surface);}
    public void Handle(GuiEvent e){if(e.SurfaceId!=_surface)return;if((GuiEventKind)e.Kind==GuiEventKind.KeyDown)HandleKey((UInt32)e.Value0);else if((GuiEventKind)e.Kind==GuiEventKind.PointerDown){if(e.Y>=185&&e.Y<225)_passwordFocus=false;else if(e.Y>=245&&e.Y<285)_passwordFocus=true;else if(e.Y>=315&&e.Y<355)TryLogin();else if(e.Y>=365&&e.Y<405)_shutdown?.Invoke();}Paint();_gui.Present(_surface,_canvas.Pixels);}
    private void HandleKey(UInt32 key){if(key==13U){TryLogin();return;}if(key==9U){_passwordFocus=!_passwordFocus;return;}if(key==8U){if(_passwordFocus&&_password.Length>0)_password=_password.Substring(0,_password.Length-1);else if(!_passwordFocus&&_username.Length>0)_username=_username.Substring(0,_username.Length-1);return;}if(key>=32U&&key<=126U){Char c=(Char)key;if(_passwordFocus){if(_password.Length<64)_password+=c;}else if(_username.Length<64)_username+=c;}}
    private void TryLogin(){if(_authenticate!=null&&_authenticate(_username,_password)){_gui.Hide(_surface);_sessionStarted?.Invoke();}else _password=String.Empty;}
    private void Paint(){_canvas.Clear(0xF018202BU);_canvas.Frame(0,0,440,420,0xFF4A6078U);_canvas.DrawAscii("INU",190,42,0xFFFFFFFFU);_canvas.DrawAscii("Sign in",184,92,0xFFCFE7FFU);Field(40,185,360,40,_username,false,!_passwordFocus);Field(40,245,360,40,_password,true,_passwordFocus);Button(40,315,360,40,"Log in",0xFF2E80D1U);Button(120,365,200,36,"Shut down",0xFF34404EU);}
    private void Field(Int32 x,Int32 y,Int32 w,Int32 h,String text,Boolean secret,Boolean focus){_canvas.Fill(x,y,w,h,0xFF111821U);_canvas.Frame(x,y,w,h,focus?0xFF50A8FFU:0xFF536273U);String shown=secret?new String('*',text.Length):text;_canvas.DrawAscii(shown,x+12,y+15,0xFFFFFFFFU);}
    private void Button(Int32 x,Int32 y,Int32 w,Int32 h,String text,UInt32 colour){_canvas.Fill(x,y,w,h,colour);_canvas.Frame(x,y,w,h,0xFFFFFFFFU);_canvas.DrawAscii(text,x+16,y+14,0xFFFFFFFFU);}
}
