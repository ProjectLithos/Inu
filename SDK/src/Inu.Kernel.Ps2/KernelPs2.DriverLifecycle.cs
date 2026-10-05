using System;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Drivers;
using Inu.Kernel.SystemCalls;

namespace Inu.Kernel.Ps2;

/// <summary>Owns the legacy i8042 controller, PS/2 keyboard, mouse, and keyboard layout state.</summary>
public static unsafe partial class KernelPs2
{
    private static Boolean Probe(KernelDriverDeviceContext* context)=>context!=null&&context->Identifier.Bus==KernelDeviceBus.Platform&&context->Identifier.VendorId==0x1D1D&&context->Identifier.DeviceId==0x8042;
    private static Boolean Start(KernelDriverDeviceContext* context)
    {
        if(context==null)return false;
        _controller=false; _keyboard=false; _mouse=false;
        KernelKeyboardDecoderServices.Reset();
        Drain();
        if(!WriteCommand(0xAD)||!WriteCommand(0xA7)) return false;
        if(!WriteCommand(0x20)||!ReadData(out Byte config)) return false;
        config=(Byte)(config & ~0x03);
        if(!WriteCommand(0x60)||!WriteData(config)) return false;
        if(!WriteCommand(0xAA)||!ReadData(out Byte self)||self!=0x55) return false;
        _controller=true;
        if(WriteCommand(0xAB)&&ReadData(out Byte ktest)&&ktest==0x00)
        {
            if(WriteCommand(0xAE)&&SendKeyboard(0xF6)&&SendKeyboard(0xF4)) _keyboard=true;
        }
        if(WriteCommand(0xA9)&&ReadData(out Byte mtest)&&mtest==0x00)
        {
            if(WriteCommand(0xA8)&&SendMouse(0xF6)&&SendMouse(0xF4)) _mouse=true;
        }
        if(!WriteCommand(0x20)||!ReadData(out config)) return false;
        config=(Byte)((config | 0x40) & ~0x03);
        if(!WriteCommand(0x60)||!WriteData(config)) return false;
        return true;
    }
    private static Boolean Stop(KernelDriverDeviceContext* context)
    {
        if(context==null)return false;
        if(_controller){SetHardwareInterrupts(false);WriteCommand(0xAD);WriteCommand(0xA7);}
        _controller=false;_keyboard=false;_mouse=false;KernelKeyboardDecoderServices.Reset();_mouseIndex=0;
        return true;
    }
    private static Boolean Remove(KernelDriverDeviceContext* context)=>context!=null&&Stop(context);

    /// <summary>Services all currently buffered i8042 keyboard and mouse bytes without blocking.</summary>
}
