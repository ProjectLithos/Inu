using System;

namespace Inu.Kernel.Ps2;

/// <summary>Dependency-resolved contract between keyboard transports and an optional scan-code decoder.</summary>
public static unsafe class KernelKeyboardDecoderServices
{
    private static delegate*<Byte,Boolean> _decode;
    private static delegate*<void> _reset;
    private static delegate*<KeyboardLayout> _getLayout;
    private static delegate*<KeyboardLayout,Boolean> _setLayout;
    private static delegate*<Ps2KeyboardEvent> _getLastEvent;
    private static delegate*<Ps2Key,Boolean> _isPressed;
    private static delegate*<delegate*<Ps2KeyboardEvent,Boolean>,Boolean> _setHandler;
    private static delegate*<UInt64> _getEventCount;

    public static Boolean IsAvailable=>_decode!=null;

    public static Boolean Register(
        delegate*<Byte,Boolean> decode,
        delegate*<void> reset,
        delegate*<KeyboardLayout> getLayout,
        delegate*<KeyboardLayout,Boolean> setLayout,
        delegate*<Ps2KeyboardEvent> getLastEvent,
        delegate*<Ps2Key,Boolean> isPressed,
        delegate*<delegate*<Ps2KeyboardEvent,Boolean>,Boolean> setHandler,
        delegate*<UInt64> getEventCount)
    {
        if(decode==null||reset==null||getLayout==null||setLayout==null||getLastEvent==null||isPressed==null||setHandler==null||getEventCount==null)return false;
        if(_decode!=null)return false;
        _decode=decode;_reset=reset;_getLayout=getLayout;_setLayout=setLayout;_getLastEvent=getLastEvent;_isPressed=isPressed;_setHandler=setHandler;_getEventCount=getEventCount;
        return true;
    }

    internal static Boolean Decode(Byte code)=>_decode==null||_decode(code);
    internal static void Reset(){if(_reset!=null)_reset();}
    public static KeyboardLayout GetLayout()=>_getLayout==null?KeyboardLayout.English_UK:_getLayout();
    public static Boolean SetLayout(KeyboardLayout layout)=>_setLayout!=null&&_setLayout(layout);
    public static Ps2KeyboardEvent GetLastEvent()=>_getLastEvent==null?default:_getLastEvent();
    public static Boolean IsPressed(Ps2Key key)=>_isPressed!=null&&_isPressed(key);
    public static Boolean SetEventHandler(delegate*<Ps2KeyboardEvent,Boolean> handler)=>_setHandler==null||_setHandler(handler);
    public static UInt64 EventCount()=>_getEventCount==null?0UL:_getEventCount();
}
