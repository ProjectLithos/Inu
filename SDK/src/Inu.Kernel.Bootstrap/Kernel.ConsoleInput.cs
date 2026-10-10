using System;
using Inu.Kernel.Console;
using Inu.Kernel.Bootstrap.Startup;
using Inu.Kernel.Contracts;
using Inu.Kernel.Platform.X64;
using Inu.Kernel.Memory;
using Inu.Kernel.VirtualMemory;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Heap;
using Inu.Runtime.NativeAot;
using Inu.Runtime.Conformance;
using Inu.Kernel.Acpi;
using Inu.Kernel.TimerDispatch;
using Inu.Kernel.InterruptDispatch;
using Inu.Kernel.InterruptBroker;
using Inu.Kernel.Time;
using Inu.Kernel.Smp;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Protection;
using Inu.Kernel.Security;
using Inu.Kernel.SystemCalls;
using Inu.Kernel.Ps2;
using Inu.Kernel.Processes;
using Inu.Kernel.Drivers;
using Inu.Kernel.Storage;
using Inu.Kernel.Networking;
using Inu.Kernel.Pci;
using Inu.Kernel.Nvme;
using Inu.Kernel.Ahci;
using Inu.Kernel.Virtio;
using Inu.Kernel.Virtio.Gpu;
using Inu.Kernel.Graphics;
using Inu.Kernel.Gui;
using Inu.Kernel.Audio;
using Inu.Kernel.E1000;
using Inu.Kernel.Rtl8168;
using Inu.Bus.Usb;
using Inu.Usb.Xhci;
using Inu.Usb.Hid;
using Inu.Usb.MassStorage;
using Inu.Usb.Hub;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Bootstrap;

public static unsafe partial class Kernel
{
private static Int64 GetFontPresetSyscall(KernelSystemCallFrame* frame) => (Int64)KernelConsole.GetFontPreset();

private static Int64 SetFontPresetSyscall(KernelSystemCallFrame* frame) => KernelConsole.SetFontPreset((UInt32)frame->NativeMessage.Value0) ? 0L : (Int64)KernelSystemCallError.InvalidArgument;

private static Int64 GetBufferingPresetSyscall(KernelSystemCallFrame* frame) => (Int64)KernelConsole.GetFramebufferBufferSetting();

private static Int64 SetBufferingPresetSyscall(KernelSystemCallFrame* frame) => KernelConsole.SetFramebufferBufferCount((UInt32)frame->NativeMessage.Value0) ? 0L : (Int64)KernelSystemCallError.InvalidArgument;

private static Boolean ServiceConsoleInput(UInt64 cookie)
    {
        // USB HID currently uses interrupt endpoints serviced by the timer dispatcher.
        // PS/2 is hardware-IRQ driven; Service remains harmless as a drain fallback.
        Boolean ok=KernelConsole.TickCaret();
        // Drain physical transitions before repeat work. A matching key-up therefore
        // cancels repeat before this service tick can emit another repeated action.
        ok=KernelPs2.Service()&ok;
        if (UsbHid.IsInitialized()) ok=UsbHid.Service()&ok;
        ok=KernelGui.ServiceInput()&ok;
        ok=ServiceKeyboardRepeat()&ok;
        return ok;
    }

private static Boolean HandlePs2Interrupt(Byte vector,UInt64 cookie) => KernelPs2.Service();

private static Boolean HandlePs2MouseEvent(Ps2MouseState input) => !KernelGui.IsInitialized() || KernelGui.HandlePs2Mouse(input);

private static Boolean HandleUsbMouseEvent(UsbHidMouseState input) => !KernelGui.IsInitialized() || KernelGui.HandleUsbMouse(input);

private static Boolean HandleKeyboardEvent(Ps2KeyboardEvent input)
    {
        if (KernelGui.HandleKeyboardEvent(input))
        {
            if (!input.Pressed && _ps2RepeatActive && _ps2RepeatKey==input.Key) _ps2RepeatActive=false;
            return true;
        }
        if (!input.Pressed)
        {
            if (_ps2RepeatActive && _ps2RepeatKey==input.Key) _ps2RepeatActive=false;
            return true;
        }

        Boolean ok=DispatchPs2Press(input);
        if (ok && IsPs2Repeatable(input))
        {
            _ps2RepeatActive=true;
            _ps2RepeatKey=input.Key;
            _ps2RepeatCharacter=input.Character;
            _ps2RepeatDeadline=NextRepeatDeadline(KeyboardRepeatInitialDelayNanoseconds);
        }
        return ok;
    }

private static Boolean HandleUsbKeyboardEvent(UsbHidKeyboardEvent input)
    {
        UInt32 guiModifiers=(UInt32)(((input.Modifiers&0x22U)!=0U?1U:0U)|((input.Modifiers&0x11U)!=0U?2U:0U)|((input.Modifiers&0x44U)!=0U?4U:0U));
        if (KernelGui.HandleKeyboard(input.Usage,input.Character,input.Pressed,guiModifiers))
        {
            if (!input.Pressed && _usbRepeatActive && _usbRepeatUsage==input.Usage) _usbRepeatActive=false;
            return true;
        }
        if (!input.Pressed)
        {
            if (_usbRepeatActive && _usbRepeatUsage==input.Usage) _usbRepeatActive=false;
            return true;
        }

        Boolean ok=DispatchUsbPress(input);
        if (ok && IsUsbRepeatable(input))
        {
            _usbRepeatActive=true;
            _usbRepeatUsage=input.Usage;
            _usbRepeatCharacter=input.Character;
            _usbRepeatDeadline=NextRepeatDeadline(KeyboardRepeatInitialDelayNanoseconds);
        }
        return ok;
    }

private static Boolean DispatchPs2Press(Ps2KeyboardEvent input)
    {
        if (input.Control && input.Key == Ps2Key.C) return UserlandRuntimeStartup.HandleControlC();
        if (input.Key == Ps2Key.PageUp) return KernelConsole.ScrollPageUp();
        if (input.Key == Ps2Key.PageDown) return KernelConsole.ScrollPageDown();
        if (TryMapPs2Navigation(input.Key,out Byte navigationCode)) return UserlandRuntimeStartup.QueueInputCode(navigationCode);
        if (input.Control && input.Key == Ps2Key.D1) return KernelConsole.SetFramebufferBufferCount(1U);
        if (input.Control && input.Key == Ps2Key.D2) return KernelConsole.SetFramebufferBufferCount(2U);
        if (input.Control && input.Key == Ps2Key.D3) return KernelConsole.SetFramebufferBufferCount(3U);
        if (input.Alt && input.Key == Ps2Key.D1) return KernelConsole.SetFontPreset(1U);
        if (input.Alt && input.Key == Ps2Key.D2) return KernelConsole.SetFontPreset(2U);
        if (input.Alt && input.Key == Ps2Key.D3) return KernelConsole.SetFontPreset(3U);
        return UserlandRuntimeStartup.QueueCharacter(input.Character);
    }

private static Boolean DispatchUsbPress(UsbHidKeyboardEvent input)
    {
        if (input.Usage==75U) return KernelConsole.ScrollPageUp();
        if (input.Usage==78U) return KernelConsole.ScrollPageDown();
        if (TryMapUsbNavigation(input.Usage,out Byte navigationCode)) return UserlandRuntimeStartup.QueueInputCode(navigationCode);
        Boolean control=(input.Modifiers&0x11U)!=0;
        Boolean alt=(input.Modifiers&0x44U)!=0;
        if(control&&input.Usage==6U)return UserlandRuntimeStartup.HandleControlC();
        if (control && input.Usage == 30U) return KernelConsole.SetFramebufferBufferCount(1U);
        if (control && input.Usage == 31U) return KernelConsole.SetFramebufferBufferCount(2U);
        if (control && input.Usage == 32U) return KernelConsole.SetFramebufferBufferCount(3U);
        if (alt && input.Usage == 30U) return KernelConsole.SetFontPreset(1U);
        if (alt && input.Usage == 31U) return KernelConsole.SetFontPreset(2U);
        if (alt && input.Usage == 32U) return KernelConsole.SetFontPreset(3U);
        return UserlandRuntimeStartup.QueueCharacter(input.Character);
    }

private static Boolean IsPs2Repeatable(Ps2KeyboardEvent input)
    {
        if(input.Control||input.Alt)return false;
        return input.Key==Ps2Key.Up||input.Key==Ps2Key.Down||input.Key==Ps2Key.Left||input.Key==Ps2Key.Right||IsRepeatableCharacter(input.Character);
    }

private static Boolean IsUsbRepeatable(UsbHidKeyboardEvent input)
    {
        if((input.Modifiers&0x55U)!=0U)return false;
        return input.Usage==82U||input.Usage==81U||input.Usage==80U||input.Usage==79U||IsRepeatableCharacter(input.Character);
    }

private static Boolean TryMapPs2Navigation(Ps2Key key,out Byte code)
    {
        code=0U;if(key==Ps2Key.Up)code=0x80U;else if(key==Ps2Key.Down)code=0x81U;else if(key==Ps2Key.Left)code=0x82U;else if(key==Ps2Key.Right)code=0x83U;else if(key==Ps2Key.Home)code=0x84U;else if(key==Ps2Key.End)code=0x85U;else if(key==Ps2Key.Delete)code=0x86U;return code!=0U;
    }

private static Boolean TryMapUsbNavigation(Byte usage,out Byte code)
    {
        code=0U;if(usage==82U)code=0x80U;else if(usage==81U)code=0x81U;else if(usage==80U)code=0x82U;else if(usage==79U)code=0x83U;else if(usage==74U)code=0x84U;else if(usage==77U)code=0x85U;else if(usage==76U)code=0x86U;return code!=0U;
    }

private static Boolean IsRepeatableCharacter(Char character)=>character=='\b'||(character>=' '&&character<='~');

private static UInt64 NextRepeatDeadline(UInt64 delay)
    {
        UInt64 now=KernelTime.GetMonotonicNanoseconds();
        return UInt64.MaxValue-now<delay?UInt64.MaxValue:now+delay;
    }

private static Boolean ServiceKeyboardRepeat()
    {
        UInt64 now=KernelTime.GetMonotonicNanoseconds();
        Boolean ok=true;

        if(_ps2RepeatActive&&now>=_ps2RepeatDeadline)
        {
            // Schedule from now rather than catching up missed periods. Slow framebuffer
            // work cannot accumulate queued repeat actions that continue after key-up.
            _ps2RepeatDeadline=UInt64.MaxValue-now<KeyboardRepeatIntervalNanoseconds?UInt64.MaxValue:now+KeyboardRepeatIntervalNanoseconds;
            Boolean repeated;
            if(_ps2RepeatKey==Ps2Key.PageUp)repeated=KernelConsole.ScrollPageUp();else if(_ps2RepeatKey==Ps2Key.PageDown)repeated=KernelConsole.ScrollPageDown();else if(TryMapPs2Navigation(_ps2RepeatKey,out Byte ps2Code))repeated=UserlandRuntimeStartup.QueueInputCode(ps2Code);else repeated=UserlandRuntimeStartup.QueueCharacter(_ps2RepeatCharacter);
            if(!repeated){_ps2RepeatActive=false;ok=false;}
        }

        if(_usbRepeatActive&&now>=_usbRepeatDeadline)
        {
            _usbRepeatDeadline=UInt64.MaxValue-now<KeyboardRepeatIntervalNanoseconds?UInt64.MaxValue:now+KeyboardRepeatIntervalNanoseconds;
            Boolean repeated;
            if(_usbRepeatUsage==75U)repeated=KernelConsole.ScrollPageUp();else if(_usbRepeatUsage==78U)repeated=KernelConsole.ScrollPageDown();else if(TryMapUsbNavigation(_usbRepeatUsage,out Byte usbCode))repeated=UserlandRuntimeStartup.QueueInputCode(usbCode);else repeated=UserlandRuntimeStartup.QueueCharacter(_usbRepeatCharacter);
            if(!repeated){_usbRepeatActive=false;ok=false;}
        }

        return ok;
    }
}
