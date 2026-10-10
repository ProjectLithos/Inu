using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Console;
using Inu.Kernel.Bootstrap.Startup;
#if INU_KERNELAREA_GUI
using Inu.Kernel.Gui;
#endif
using Inu.Kernel.TimerDispatch;
#if INU_KERNELAREA_INPUT
using Inu.Kernel.Ps2;
#endif
#if INU_KERNELAREA_DRIVERS
using Inu.Kernel.InterruptBroker;
#endif
#if INU_KERNELAREA_USB
using Inu.Usb.Hid;
#endif

namespace Inu.Kernel.Bootstrap.HAL;

/// <summary>Owns only the selected keyboard/mouse providers and their event routing.</summary>
public static unsafe class InputHardwareStartup
{
    private static Boolean _initialized;
#if INU_KERNELAREA_INPUT
    private static UInt32 _inputTimerHandle;
#if INU_KERNELAREA_DRIVERS
    private static UInt64 _keyboardIrqHandle;
    private static UInt64 _mouseIrqHandle;
#endif
#endif

    public static Boolean Initialize()
    {
        if (_initialized) return true;
#if INU_KERNELAREA_INPUT
        // The transport consumes scan codes through the decoder contract. Register
        // the supplied decoder only when the OS has not installed its own provider.
        if (!KernelKeyboardDecoderServices.IsAvailable && !KernelKeyboardDecoder.Initialize()) return false;
        if (!KernelPs2.Initialize()) return false;
        Ps2Capabilities ps2 = KernelPs2.GetCapabilities();
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"hal-detail","InputHardwareStartup.Initialize")) return false;
        if (!KernelConsole.Write("PS/2 keyboard/mouse: ")) return false;
        if (!KernelConsole.Write(ps2.Keyboard ? "keyboard" : "no keyboard")) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteLine(ps2.Mouse ? "mouse" : "no mouse")) return false;
        if (!KernelPs2.SetKeyboardEventHandler(&HandlePs2KeyboardEvent)) return false;
        if (!KernelPs2.SetMouseEventHandler(&HandlePs2MouseEvent)) return false;
        if (!KernelTimerDispatch.Register(1000000UL, &ServiceInput, 0UL, out _inputTimerHandle)) return false;
#endif
        _initialized = true;
        return true;
    }

    /// <summary>Ensures the selected text-input providers and timer-service drain are ready for an interactive shell.</summary>
    public static Boolean EnsureTextInputProviders() => Initialize();

    /// <summary>Attaches hardware IRQ delivery after the selected interrupt broker is online.</summary>
    public static Boolean EnableHardwareInterrupts()
    {
#if INU_KERNELAREA_INPUT && INU_KERNELAREA_DRIVERS
        Boolean keyboardIrq = KernelInterruptBroker.RegisterLegacyGsi(1U, false, false, &HandlePs2Interrupt, 0UL, out _keyboardIrqHandle);
        Boolean mouseIrq = !KernelPs2.GetCapabilities().Mouse || KernelInterruptBroker.RegisterLegacyGsi(12U, false, false, &HandlePs2Interrupt, 0UL, out _mouseIrqHandle);
        Boolean ps2Irqs = keyboardIrq && mouseIrq && KernelPs2.SetHardwareInterrupts(true);
        if (!KernelConsole.WriteLine(ps2Irqs ? "PS/2 input: hardware IRQ delivery active (timer drain retained as safety net)." : "PS/2 input: timer-dispatch drain active.")) return false;
#endif
        return true;
    }

    /// <summary>Ensures selected input providers are connected before a graphical session begins.</summary>
    public static Boolean EnsureGraphicalInputProviders()
    {
        if (!Initialize()) return false;
#if INU_KERNELAREA_INPUT
        if (!KernelKeyboardDecoderServices.IsAvailable && !KernelKeyboardDecoder.Initialize()) return false;
        if (!KernelPs2.IsInitialized() && !KernelPs2.Initialize())
        {
            KernelConsole.WriteLine("NOKMAIN:GUI-INPUT:PS2:FAIL");
            return false;
        }
        if (!KernelPs2.SetKeyboardEventHandler(&HandlePs2KeyboardEvent) || !KernelPs2.SetMouseEventHandler(&HandlePs2MouseEvent))
        {
            KernelConsole.WriteLine("NOKMAIN:GUI-INPUT:PS2:FAIL");
            return false;
        }
        Ps2Capabilities graphicalPs2 = KernelPs2.GetCapabilities();
        KernelConsole.WriteLine(graphicalPs2.Keyboard || graphicalPs2.Mouse ? "NOKMAIN:GUI-INPUT:PS2:OK" : "NOKMAIN:GUI-INPUT:PS2:NO-DEVICE");
#endif
#if INU_KERNELAREA_USB
        if (!UsbHid.IsInitialized())
        {
            KernelConsole.WriteLine("NOKMAIN:GUI-INPUT:USB:FAIL");
            return false;
        }
#if INU_KERNELAREA_INPUT
        if (!UsbHid.SetKeyboardEventHandler(&HandleUsbKeyboardEvent) || !UsbHid.SetMouseEventHandler(&HandleUsbMouseEvent))
        {
            KernelConsole.WriteLine("NOKMAIN:GUI-INPUT:USB:FAIL");
            return false;
        }
#endif
        UsbHidCapabilities graphicalUsb = UsbHid.GetCapabilities();
        KernelConsole.WriteLine(graphicalUsb.Keyboards != 0U || graphicalUsb.Mice != 0U ? "NOKMAIN:GUI-INPUT:USB:OK" : "NOKMAIN:GUI-INPUT:USB:NO-DEVICE");
#endif
        KernelConsole.WriteLine("NOKMAIN:GUI-INPUT:READY");
        return true;
    }

#if INU_KERNELAREA_INPUT
    private static Boolean ServiceInput(UInt64 cookie)
    {
        Boolean ok = KernelPs2.Service();
#if INU_KERNELAREA_USB
        if (UsbHid.IsInitialized()) ok = UsbHid.Service() & ok;
#endif
        return ok;
    }

    private static Boolean HandlePs2Interrupt(Byte vector, UInt64 cookie) => KernelPs2.Service();
    private static Boolean HandlePs2MouseEvent(Ps2MouseState input)
    {
#if INU_KERNELAREA_GUI
        return !KernelGui.IsInitialized() || KernelGui.HandlePs2Mouse(input);
#else
        return true;
#endif
    }
#if INU_KERNELAREA_USB
    private static Boolean HandleUsbMouseEvent(UsbHidMouseState input)
    {
#if INU_KERNELAREA_GUI
        return !KernelGui.IsInitialized() || KernelGui.HandleUsbMouse(input);
#else
        return true;
#endif
    }
#endif

    private static Boolean HandlePs2KeyboardEvent(Ps2KeyboardEvent input)
    {
#if INU_KERNELAREA_GUI
        if (KernelGui.IsInitialized() && KernelGui.HandleKeyboardEvent(input)) return true;
#endif
        if (!input.Pressed) return true;
        if (input.Control && input.Key == Ps2Key.C) return UserlandRuntimeStartup.HandleControlC();
        if (input.Key == Ps2Key.PageUp) return KernelConsole.ScrollPageUp();
        if (input.Key == Ps2Key.PageDown) return KernelConsole.ScrollPageDown();
        if (TryMapPs2Navigation(input.Key, out Byte navigationCode)) return UserlandRuntimeStartup.QueueInputCode(navigationCode);
        if (input.Control && input.Key == Ps2Key.D1) return KernelConsole.SetFramebufferBufferCount(1U);
        if (input.Control && input.Key == Ps2Key.D2) return KernelConsole.SetFramebufferBufferCount(2U);
        if (input.Control && input.Key == Ps2Key.D3) return KernelConsole.SetFramebufferBufferCount(3U);
        if (input.Alt && input.Key == Ps2Key.D1) return KernelConsole.SetFontPreset(1U);
        if (input.Alt && input.Key == Ps2Key.D2) return KernelConsole.SetFontPreset(2U);
        if (input.Alt && input.Key == Ps2Key.D3) return KernelConsole.SetFontPreset(3U);
        return UserlandRuntimeStartup.QueueCharacter(input.Character);
    }

    private static Boolean TryMapPs2Navigation(Ps2Key key, out Byte code)
    {
        code=0U;
        if(key==Ps2Key.Up)code=0x80U;else if(key==Ps2Key.Down)code=0x81U;else if(key==Ps2Key.Left)code=0x82U;else if(key==Ps2Key.Right)code=0x83U;
        else if(key==Ps2Key.Home)code=0x84U;else if(key==Ps2Key.End)code=0x85U;else if(key==Ps2Key.Delete)code=0x86U;
        return code!=0U;
    }

#if INU_KERNELAREA_USB
    private static Boolean HandleUsbKeyboardEvent(UsbHidKeyboardEvent input)
    {
#if INU_KERNELAREA_GUI
        UInt32 guiModifiers=(UInt32)(((input.Modifiers&0x22U)!=0U?1U:0U)|((input.Modifiers&0x11U)!=0U?2U:0U)|((input.Modifiers&0x44U)!=0U?4U:0U));
        if (KernelGui.IsInitialized() && KernelGui.HandleKeyboard(input.Usage,input.Character,input.Pressed,guiModifiers)) return true;
#endif
        if (!input.Pressed) return true;
        if (input.Usage == 75U) return KernelConsole.ScrollPageUp();
        if (input.Usage == 78U) return KernelConsole.ScrollPageDown();
        if (TryMapUsbNavigation(input.Usage, out Byte navigationCode)) return UserlandRuntimeStartup.QueueInputCode(navigationCode);
        Boolean control = (input.Modifiers & 0x11U) != 0U;
        Boolean alt = (input.Modifiers & 0x44U) != 0U;
        if (control && input.Usage == 6U) return UserlandRuntimeStartup.HandleControlC();
        if (control && input.Usage == 30U) return KernelConsole.SetFramebufferBufferCount(1U);
        if (control && input.Usage == 31U) return KernelConsole.SetFramebufferBufferCount(2U);
        if (control && input.Usage == 32U) return KernelConsole.SetFramebufferBufferCount(3U);
        if (alt && input.Usage == 30U) return KernelConsole.SetFontPreset(1U);
        if (alt && input.Usage == 31U) return KernelConsole.SetFontPreset(2U);
        if (alt && input.Usage == 32U) return KernelConsole.SetFontPreset(3U);
        return UserlandRuntimeStartup.QueueCharacter(input.Character);
    }

    private static Boolean TryMapUsbNavigation(Byte usage, out Byte code)
    {
        code=0U;
        if(usage==82U)code=0x80U;else if(usage==81U)code=0x81U;else if(usage==80U)code=0x82U;else if(usage==79U)code=0x83U;
        else if(usage==74U)code=0x84U;else if(usage==77U)code=0x85U;else if(usage==76U)code=0x86U;
        return code!=0U;
    }

#endif
#endif
}
