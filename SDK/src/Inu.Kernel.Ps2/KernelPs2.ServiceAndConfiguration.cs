using System;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Drivers;
using Inu.Kernel.SystemCalls;

namespace Inu.Kernel.Ps2;

/// <summary>Owns the legacy i8042 controller, PS/2 keyboard, mouse, and keyboard layout state.</summary>
public static unsafe partial class KernelPs2
{
    public static Boolean Service()
    {
        for(UInt32 n=0;n<64U;n++)
        {
            if(!Native.ReadPort8(StatusPort,out Byte status)) return false;
            if((status&0x01)==0) return true;
            if(!Native.ReadPort8(DataPort,out Byte value)) return false;
            if((status&0x20)!=0) DecodeMouse(value); else if(!DecodeKeyboard(value)) return false;
        }
        return true;
    }
    /// <summary>Enables or disables the i8042 keyboard/mouse hardware IRQ lines after the kernel interrupt routes are installed.</summary>
    public static Boolean SetHardwareInterrupts(Boolean enabled)
    {
        if(!_controller||!WriteCommand(0x20)||!ReadData(out Byte config)) return false;
        if(enabled) config=(Byte)(config|(_keyboard?0x01:0x00)|(_mouse?0x02:0x00)); else config=(Byte)(config&~0x03);
        if(!WriteCommand(0x60)||!WriteData(config)) return false;
        return true;
    }

    /// <summary>Gets current device and layout capabilities.</summary>
    public static Boolean IsInitialized()=>_initialized;
    public static Ps2Capabilities GetCapabilities()=>new(_controller,_keyboard,_mouse,KernelKeyboardDecoderServices.GetLayout(),KernelKeyboardDecoderServices.EventCount(),_mousePackets);
    /// <summary>Gets the active installed keyboard layout.</summary>
    public static KeyboardLayout GetKeyboardLayout()=>KernelKeyboardDecoderServices.GetLayout();
    /// <summary>Sets the active installed keyboard layout immediately.</summary>
    public static Boolean SetKeyboardLayout(KeyboardLayout layout)=>KernelKeyboardDecoderServices.SetLayout(layout);
    /// <summary>Gets the most recently decoded keyboard transition.</summary>
    public static Ps2KeyboardEvent GetLastKeyboardEvent()=>KernelKeyboardDecoderServices.GetLastEvent();
    /// <summary>Gets whether one logical PS/2 key is currently held down.</summary>
    public static Boolean IsKeyPressed(Ps2Key key)=>KernelKeyboardDecoderServices.IsPressed(key);
    /// <summary>Gets the accumulated mouse position/button state.</summary>
    public static Ps2MouseState GetMouseState()=>_mouseState;
    /// <summary>Installs the decoded keyboard-event consumer. The PS/2 driver remains the sole owner of i8042 hardware reads.</summary>
    public static Boolean SetKeyboardEventHandler(delegate*<Ps2KeyboardEvent, Boolean> handler)=>KernelKeyboardDecoderServices.SetEventHandler(handler);
    /// <summary>Installs the decoded mouse-event consumer. One callback is issued for each complete PS/2 packet.</summary>
    public static Boolean SetMouseEventHandler(delegate*<Ps2MouseState, Boolean> handler){_mouseEventHandler=handler;return true;}

    private static Int64 GetLayoutSyscall(KernelSystemCallFrame* frame)=>unchecked((Int64)(UInt64)KernelKeyboardDecoderServices.GetLayout());
    private static Int64 SetLayoutSyscall(KernelSystemCallFrame* frame)=>SetKeyboardLayout((KeyboardLayout)(UInt32)frame->NativeMessage.Value0)?0L:(Int64)KernelSystemCallError.InvalidArgument;
}
