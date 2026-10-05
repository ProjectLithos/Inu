using System;

namespace Inu.Kernel.Ps2;

/// <summary>Transport-neutral Set-1 keyboard decoder. Owns decode state independently from the i8042 transport.</summary>
public static unsafe class KernelKeyboardDecoder
{
    private static Boolean _initialized,_extended,_shiftL,_shiftR,_controlL,_controlR,_altL,_altR,_caps;
    private static KeyboardLayout _layout;
    private static UInt64 _events,_pressedLow,_pressedHigh;
    private static Ps2KeyboardEvent _last;
    private static delegate*<Ps2KeyboardEvent,Boolean> _handler;

    public static Boolean Initialize()
    {
        if(_initialized)return true;
        _events=0UL;_last=default;Reset();
        if(!KernelKeyboardDecoderServices.Register(&Decode,&Reset,&GetLayout,&SetLayout,&GetLastEvent,&IsPressed,&SetEventHandler,&GetEventCount))return false;
        _initialized=true;
        return true;
    }

    public static void Reset()
    {
        _layout=KeyboardLayout.English_UK;_extended=false;_shiftL=false;_shiftR=false;_controlL=false;_controlR=false;_altL=false;_altR=false;_caps=false;
        _pressedLow=0UL;_pressedHigh=0UL;
    }

    public static KeyboardLayout GetLayout()=>_layout;
    public static Boolean SetLayout(KeyboardLayout layout){if(!KeyboardLayouts.IsInstalled(layout))return false;_layout=layout;return true;}
    public static Ps2KeyboardEvent GetLastEvent()=>_last;
    public static Boolean IsPressed(Ps2Key key)=>IsKeyDown(key);
    public static Boolean SetEventHandler(delegate*<Ps2KeyboardEvent,Boolean> handler){_handler=handler;return true;}
    public static UInt64 GetEventCount()=>_events;

    public static Boolean Decode(Byte code)
    {
        if(code==0xE0){_extended=true;return true;}
        Boolean released=(code&0x80)!=0;Byte make=(Byte)(code&0x7F);Ps2Key key=MapKey(make,_extended);_extended=false;if(key==Ps2Key.None)return true;
        Boolean pressed=!released;Boolean wasPressed=IsKeyDown(key);if(pressed==wasPressed)return true;if(!SetKeyDown(key,pressed))return true;
        if(key==Ps2Key.LeftShift)_shiftL=pressed;else if(key==Ps2Key.RightShift)_shiftR=pressed;else if(key==Ps2Key.LeftControl)_controlL=pressed;else if(key==Ps2Key.RightControl)_controlR=pressed;else if(key==Ps2Key.LeftAlt)_altL=pressed;else if(key==Ps2Key.RightAlt)_altR=pressed;else if(key==Ps2Key.CapsLock&&pressed)_caps=!_caps;
        Char ch=pressed?KeyboardLayouts.Translate(_layout,key,_shiftL||_shiftR,_caps,_altR):'\0';
        _last=new Ps2KeyboardEvent(key,pressed,ch,_shiftL||_shiftR,_controlL||_controlR,_altL||_altR,_caps);_events++;
        return _handler==null||_handler(_last);
    }

    private static Boolean IsKeyDown(Ps2Key key){UInt32 index=(UInt32)key;if(index==0U||index>=128U)return false;UInt64 mask=1UL<<(Int32)(index&63U);return index<64U?(_pressedLow&mask)!=0UL:(_pressedHigh&mask)!=0UL;}
    private static Boolean SetKeyDown(Ps2Key key,Boolean pressed){UInt32 index=(UInt32)key;if(index==0U||index>=128U)return false;UInt64 mask=1UL<<(Int32)(index&63U);if(index<64U){if(pressed)_pressedLow|=mask;else _pressedLow&=~mask;}else{if(pressed)_pressedHigh|=mask;else _pressedHigh&=~mask;}return true;}
    private static Ps2Key MapKey(Byte c,Boolean e)
    {
        if(e){switch(c){case 0x1C:return Ps2Key.KeypadEnter;case 0x1D:return Ps2Key.RightControl;case 0x35:return Ps2Key.KeypadDivide;case 0x38:return Ps2Key.RightAlt;case 0x47:return Ps2Key.Home;case 0x48:return Ps2Key.Up;case 0x49:return Ps2Key.PageUp;case 0x4B:return Ps2Key.Left;case 0x4D:return Ps2Key.Right;case 0x4F:return Ps2Key.End;case 0x50:return Ps2Key.Down;case 0x51:return Ps2Key.PageDown;case 0x52:return Ps2Key.Insert;case 0x53:return Ps2Key.Delete;default:return Ps2Key.None;}}
        switch(c){case 0x01:return Ps2Key.Escape;case 0x02:return Ps2Key.D1;case 0x03:return Ps2Key.D2;case 0x04:return Ps2Key.D3;case 0x05:return Ps2Key.D4;case 0x06:return Ps2Key.D5;case 0x07:return Ps2Key.D6;case 0x08:return Ps2Key.D7;case 0x09:return Ps2Key.D8;case 0x0A:return Ps2Key.D9;case 0x0B:return Ps2Key.D0;case 0x0C:return Ps2Key.Minus;case 0x0D:return Ps2Key.Equals;case 0x0E:return Ps2Key.Backspace;case 0x0F:return Ps2Key.Tab;case 0x10:return Ps2Key.Q;case 0x11:return Ps2Key.W;case 0x12:return Ps2Key.E;case 0x13:return Ps2Key.R;case 0x14:return Ps2Key.T;case 0x15:return Ps2Key.Y;case 0x16:return Ps2Key.U;case 0x17:return Ps2Key.I;case 0x18:return Ps2Key.O;case 0x19:return Ps2Key.P;case 0x1A:return Ps2Key.LeftBracket;case 0x1B:return Ps2Key.RightBracket;case 0x1C:return Ps2Key.Enter;case 0x1D:return Ps2Key.LeftControl;case 0x1E:return Ps2Key.A;case 0x1F:return Ps2Key.S;case 0x20:return Ps2Key.D;case 0x21:return Ps2Key.F;case 0x22:return Ps2Key.G;case 0x23:return Ps2Key.H;case 0x24:return Ps2Key.J;case 0x25:return Ps2Key.K;case 0x26:return Ps2Key.L;case 0x27:return Ps2Key.Semicolon;case 0x28:return Ps2Key.Apostrophe;case 0x29:return Ps2Key.Grave;case 0x2A:return Ps2Key.LeftShift;case 0x2B:return Ps2Key.Backslash;case 0x2C:return Ps2Key.Z;case 0x2D:return Ps2Key.X;case 0x2E:return Ps2Key.C;case 0x2F:return Ps2Key.V;case 0x30:return Ps2Key.B;case 0x31:return Ps2Key.N;case 0x32:return Ps2Key.M;case 0x33:return Ps2Key.Comma;case 0x34:return Ps2Key.Period;case 0x35:return Ps2Key.Slash;case 0x36:return Ps2Key.RightShift;case 0x37:return Ps2Key.KeypadMultiply;case 0x38:return Ps2Key.LeftAlt;case 0x39:return Ps2Key.Space;case 0x3A:return Ps2Key.CapsLock;case 0x3B:return Ps2Key.F1;case 0x3C:return Ps2Key.F2;case 0x3D:return Ps2Key.F3;case 0x3E:return Ps2Key.F4;case 0x3F:return Ps2Key.F5;case 0x40:return Ps2Key.F6;case 0x41:return Ps2Key.F7;case 0x42:return Ps2Key.F8;case 0x43:return Ps2Key.F9;case 0x44:return Ps2Key.F10;case 0x45:return Ps2Key.NumLock;case 0x46:return Ps2Key.ScrollLock;case 0x47:return Ps2Key.Keypad7;case 0x48:return Ps2Key.Keypad8;case 0x49:return Ps2Key.Keypad9;case 0x4A:return Ps2Key.KeypadMinus;case 0x4B:return Ps2Key.Keypad4;case 0x4C:return Ps2Key.Keypad5;case 0x4D:return Ps2Key.Keypad6;case 0x4E:return Ps2Key.KeypadPlus;case 0x4F:return Ps2Key.Keypad1;case 0x50:return Ps2Key.Keypad2;case 0x51:return Ps2Key.Keypad3;case 0x52:return Ps2Key.Keypad0;case 0x53:return Ps2Key.KeypadDecimal;case 0x56:return Ps2Key.Oem102;case 0x57:return Ps2Key.F11;case 0x58:return Ps2Key.F12;default:return Ps2Key.None;}
    }
}
