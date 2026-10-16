using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Console;
using Inu.Kernel.Bootstrap.Startup;
#if INU_KERNELAREA_GUI
using Inu.Kernel.Gui;
#endif
using Inu.Kernel.TimerDispatch;
using Inu.Kernel.Time;
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
    private const UInt64 KeyboardRepeatInitialDelayNanoseconds = 500000000UL;
    private const UInt64 KeyboardRepeatIntervalNanoseconds = 40000000UL;
    private static Boolean _ps2RepeatActive;
    private static Ps2Key _ps2RepeatKey;
    private static Char _ps2RepeatCharacter;
    private static UInt64 _ps2RepeatDeadline;
#if INU_KERNELAREA_USB
    private static Boolean _usbRepeatActive;
    private static Byte _usbRepeatUsage;
    private static Char _usbRepeatCharacter;
    private static UInt64 _usbRepeatDeadline;
#endif
#if INU_KERNELAREA_DRIVERS
    private static UInt64 _keyboardIrqHandle;
    private static UInt64 _mouseIrqHandle;
#endif
#endif

    public static Boolean Initialize()
    {
        if (_initialized) return true;
#if INU_KERNELAREA_INPUT
        Boolean ps2KeyboardSelected = HasConfiguredInput("ps2-keyboard");
        Boolean ps2MouseSelected = HasConfiguredInput("ps2-mouse");
        Boolean ps2Selected = ps2KeyboardSelected || ps2MouseSelected;
        if (ps2Selected)
        {
            // The transport consumes scan codes through the decoder contract. Register
            // the supplied decoder only when the OS has not installed its own provider.
            if (ps2KeyboardSelected && !KernelKeyboardDecoderServices.IsAvailable && !KernelKeyboardDecoder.Initialize()) return false;
            if (!KernelPs2.Initialize()) return false;
            Ps2Capabilities ps2 = KernelPs2.GetCapabilities();
            if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"hal-detail","InputHardwareStartup.Initialize")) return false;
            if (!KernelConsole.Write("Selected PS/2 keyboard/mouse: ")) return false;
            if (!KernelConsole.Write(ps2KeyboardSelected && ps2.Keyboard ? "keyboard" : "not selected")) return false;
            if (!KernelConsole.Write(" / ")) return false;
            if (!KernelConsole.WriteLine(ps2MouseSelected && ps2.Mouse ? "mouse" : "not selected")) return false;
            if (ps2KeyboardSelected && !KernelPs2.SetKeyboardEventHandler(&HandlePs2KeyboardEvent)) return false;
            if (ps2MouseSelected && !KernelPs2.SetMouseEventHandler(&HandlePs2MouseEvent)) return false;
        }
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
        Boolean ps2KeyboardSelected = HasConfiguredInput("ps2-keyboard");
        Boolean ps2MouseSelected = HasConfiguredInput("ps2-mouse");
        if (ps2KeyboardSelected || ps2MouseSelected)
        {
            Boolean keyboardIrq = !ps2KeyboardSelected || KernelInterruptBroker.RegisterLegacyGsi(1U, false, false, &HandlePs2Interrupt, 0UL, out _keyboardIrqHandle);
            Boolean mouseIrq = !ps2MouseSelected || KernelInterruptBroker.RegisterLegacyGsi(12U, false, false, &HandlePs2Interrupt, 0UL, out _mouseIrqHandle);
            Boolean ps2Irqs = keyboardIrq && mouseIrq && KernelPs2.SetHardwareInterrupts(true);
            if (!KernelConsole.WriteLine(ps2Irqs ? "PS/2 input: selected hardware IRQ delivery active (timer drain retained as safety net)." : "PS/2 input: selected devices remain on timer-dispatch drain.")) return false;
        }
        else if (!KernelConsole.WriteLine("PS/2 input: not selected; hardware IRQ delivery skipped.")) return false;
#endif
        return true;
    }

    /// <summary>Ensures selected input providers are connected before a graphical session begins.</summary>
    public static Boolean EnsureGraphicalInputProviders()
    {
        if (!Initialize()) return false;
#if INU_KERNELAREA_INPUT
        Boolean ps2KeyboardSelected = HasConfiguredInput("ps2-keyboard");
        Boolean ps2MouseSelected = HasConfiguredInput("ps2-mouse");
        if (ps2KeyboardSelected || ps2MouseSelected)
        {
            if (ps2KeyboardSelected && !KernelKeyboardDecoderServices.IsAvailable && !KernelKeyboardDecoder.Initialize()) return false;
            if (!KernelPs2.IsInitialized() && !KernelPs2.Initialize())
            {
                KernelConsole.WriteLine("NOKMAIN:GUI-INPUT:PS2:FAIL");
                return false;
            }
            if ((ps2KeyboardSelected && !KernelPs2.SetKeyboardEventHandler(&HandlePs2KeyboardEvent)) ||
                (ps2MouseSelected && !KernelPs2.SetMouseEventHandler(&HandlePs2MouseEvent)))
            {
                KernelConsole.WriteLine("NOKMAIN:GUI-INPUT:PS2:FAIL");
                return false;
            }
            Ps2Capabilities graphicalPs2 = KernelPs2.GetCapabilities();
            KernelConsole.WriteLine((ps2KeyboardSelected && graphicalPs2.Keyboard) || (ps2MouseSelected && graphicalPs2.Mouse) ? "NOKMAIN:GUI-INPUT:PS2:OK" : "NOKMAIN:GUI-INPUT:PS2:NO-DEVICE");
        }
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
    private static Boolean HasConfiguredInput(String value)
    {
        String[] configured = Inu.Kernel.Bootstrap.GeneratedConfiguration.Input();
        for (Int32 i = 0; i < configured.Length; i++) if (configured[i] == value) return true;
        return false;
    }

    private static Boolean ServiceInput(UInt64 cookie)
    {
        Boolean ok = !(HasConfiguredInput("ps2-keyboard") || HasConfiguredInput("ps2-mouse")) || KernelPs2.Service();
#if INU_KERNELAREA_USB
        if (UsbHid.IsInitialized()) ok = UsbHid.Service() & ok;
#endif
        ok = ServiceKeyboardRepeat() & ok;
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
        if (KernelGui.IsInitialized() && KernelGui.HandleKeyboardEvent(input))
        {
            if (!input.Pressed && _ps2RepeatActive && _ps2RepeatKey == input.Key) _ps2RepeatActive = false;
            return true;
        }
#endif
        if (!input.Pressed)
        {
            if (_ps2RepeatActive && _ps2RepeatKey == input.Key) _ps2RepeatActive = false;
            return true;
        }

        Boolean ok = DispatchPs2Press(input);
        if (ok && IsPs2Repeatable(input))
        {
            _ps2RepeatActive = true;
            _ps2RepeatKey = input.Key;
            _ps2RepeatCharacter = input.Character;
            _ps2RepeatDeadline = NextRepeatDeadline(KeyboardRepeatInitialDelayNanoseconds);
        }
        return ok;
    }

    private static Boolean DispatchPs2Press(Ps2KeyboardEvent input)
    {
        if (input.Control && input.Key == Ps2Key.C) return UserlandRuntimeStartup.HandleControlC();
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
        code=0;
        if(key==Ps2Key.Up)code=0x80;else if(key==Ps2Key.Down)code=0x81;else if(key==Ps2Key.Left)code=0x82;else if(key==Ps2Key.Right)code=0x83;
        else if(key==Ps2Key.Home)code=0x84;else if(key==Ps2Key.End)code=0x85;else if(key==Ps2Key.Delete)code=0x86;else if(key==Ps2Key.PageUp)code=0x87;else if(key==Ps2Key.PageDown)code=0x88;
        return code!=0U;
    }

#if INU_KERNELAREA_USB
    private static Boolean HandleUsbKeyboardEvent(UsbHidKeyboardEvent input)
    {
#if INU_KERNELAREA_GUI
        UInt32 guiModifiers=(UInt32)(((input.Modifiers&0x22U)!=0U?1U:0U)|((input.Modifiers&0x11U)!=0U?2U:0U)|((input.Modifiers&0x44U)!=0U?4U:0U));
        if (KernelGui.IsInitialized() && KernelGui.HandleKeyboard(input.Usage,input.Character,input.Pressed,guiModifiers))
        {
            if (!input.Pressed && _usbRepeatActive && _usbRepeatUsage == input.Usage) _usbRepeatActive = false;
            return true;
        }
#endif
        if (!input.Pressed)
        {
            if (_usbRepeatActive && _usbRepeatUsage == input.Usage) _usbRepeatActive = false;
            return true;
        }

        Boolean ok = DispatchUsbPress(input);
        if (ok && IsUsbRepeatable(input))
        {
            _usbRepeatActive = true;
            _usbRepeatUsage = input.Usage;
            _usbRepeatCharacter = input.Character;
            _usbRepeatDeadline = NextRepeatDeadline(KeyboardRepeatInitialDelayNanoseconds);
        }
        return ok;
    }

    private static Boolean DispatchUsbPress(UsbHidKeyboardEvent input)
    {
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
        code=0;
        if(usage==82U)code=0x80;else if(usage==81U)code=0x81;else if(usage==80U)code=0x82;else if(usage==79U)code=0x83;
        else if(usage==74U)code=0x84;else if(usage==77U)code=0x85;else if(usage==76U)code=0x86;else if(usage==75U)code=0x87;else if(usage==78U)code=0x88;
        return code!=0U;
    }
#endif

    private static Boolean IsPs2Repeatable(Ps2KeyboardEvent input)
    {
        if (input.Control || input.Alt) return false;
        return input.Key == Ps2Key.Up || input.Key == Ps2Key.Down || input.Key == Ps2Key.Left || input.Key == Ps2Key.Right ||
               input.Key == Ps2Key.PageUp || input.Key == Ps2Key.PageDown || IsRepeatableCharacter(input.Character);
    }

#if INU_KERNELAREA_USB
    private static Boolean IsUsbRepeatable(UsbHidKeyboardEvent input)
    {
        if ((input.Modifiers & 0x55U) != 0U) return false;
        return input.Usage == 82U || input.Usage == 81U || input.Usage == 80U || input.Usage == 79U ||
               input.Usage == 75U || input.Usage == 78U || IsRepeatableCharacter(input.Character);
    }
#endif

    private static Boolean IsRepeatableCharacter(Char character) => character == '\b' || (character >= ' ' && character <= '~');

    private static UInt64 NextRepeatDeadline(UInt64 delay)
    {
        UInt64 now = KernelTime.GetMonotonicNanoseconds();
        return UInt64.MaxValue - now < delay ? UInt64.MaxValue : now + delay;
    }

    private static Boolean ServiceKeyboardRepeat()
    {
        UInt64 now = KernelTime.GetMonotonicNanoseconds();
        Boolean ok = true;
        if (_ps2RepeatActive && now >= _ps2RepeatDeadline)
        {
            _ps2RepeatDeadline = NextRepeatDeadline(KeyboardRepeatIntervalNanoseconds);
            Boolean repeated = TryMapPs2Navigation(_ps2RepeatKey, out Byte code) ?
                UserlandRuntimeStartup.QueueInputCode(code) : UserlandRuntimeStartup.QueueCharacter(_ps2RepeatCharacter);
            if (!repeated) { _ps2RepeatActive = false; ok = false; }
        }
#if INU_KERNELAREA_USB
        if (_usbRepeatActive && now >= _usbRepeatDeadline)
        {
            _usbRepeatDeadline = NextRepeatDeadline(KeyboardRepeatIntervalNanoseconds);
            Boolean repeated = TryMapUsbNavigation(_usbRepeatUsage, out Byte code) ?
                UserlandRuntimeStartup.QueueInputCode(code) : UserlandRuntimeStartup.QueueCharacter(_usbRepeatCharacter);
            if (!repeated) { _usbRepeatActive = false; ok = false; }
        }
#endif
        return ok;
    }
#endif
}
