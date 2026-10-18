using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Console;
using Inu.Kernel.Bootstrap.Startup;
#if INU_KERNELAREA_GUI
using Inu.Kernel.Gui;
#endif
using Inu.Kernel.TimerDispatch;
using Inu.Kernel.Time;
using Inu.Kernel.Scheduler;
using Inu.Kernel.Smp;
using Inu.Kernel.Internal.X64;
#if INU_INPUT_PS2
using Inu.Kernel.Ps2;
#endif
#if INU_KERNELAREA_DRIVERS
using Inu.Kernel.InterruptBroker;
#endif
#if INU_INPUT_USB_HID
using Inu.Usb.Hid;
#endif

namespace Inu.Kernel.Bootstrap.HAL;

/// <summary>Owns only the selected keyboard/mouse providers and their event routing.</summary>
public static unsafe class InputHardwareStartup
{
    private static Boolean _initialized;
#if INU_KERNELAREA_INPUT
    private static UInt32 _inputTimerHandle;
    private static UInt64 _inputServicePending;
    private static UInt64 _inputTimerSignals;
    private static UInt64 _inputWorkerRuns;
    private static UInt64 _inputWorkerSuccesses;
    private static UInt64 _inputWorkerFailures;
    private const UInt64 KeyboardRepeatInitialDelayNanoseconds = 500000000UL;
    private const UInt64 KeyboardRepeatIntervalNanoseconds = 40000000UL;
#if INU_INPUT_PS2
    private static Boolean _ps2RepeatActive;
    private static Ps2Key _ps2RepeatKey;
    private static Char _ps2RepeatCharacter;
    private static UInt64 _ps2RepeatDeadline;
#endif
#if INU_INPUT_USB_HID
    private static Boolean _usbRepeatActive;
    private static Byte _usbRepeatUsage;
    private static Char _usbRepeatCharacter;
    private static UInt64 _usbRepeatDeadline;
    private static Boolean _usbFirstEventPublished;
    private static Boolean _usbFirstPressPublished;
    private static Boolean _usbFirstQueuedCharacterPublished;
#endif
#if INU_INPUT_PS2 && INU_KERNELAREA_DRIVERS
    private static UInt64 _keyboardIrqHandle;
    private static UInt64 _mouseIrqHandle;
#endif
#endif

    public static Boolean Initialize()
    {
        if (_initialized) return true;
#if INU_KERNELAREA_INPUT
#if INU_INPUT_PS2
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
#endif
        // Timer IRQs must never execute blocking USB/xHCI transfers. The 1 ms timer only
        // publishes input work; the Drivers role worker performs HID/PS2 service in normal
        // scheduler context where xHCI completion interrupts can be delivered.
        if (!KernelScheduler.IsInitialized()) return false;
        if (!KernelScheduler.RegisterRoleWorker(KernelCpuRole.Drivers, &ServiceInputWorker, KernelThreadPriority.High)) return false;
        if (!KernelTimerDispatch.Register(1000000UL, &QueueInputService, 0UL, out _inputTimerHandle)) return false;
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"input-detail","InputHardwareStartup.Initialize")) return false;
        if (!KernelConsole.Write("Input service worker thread / timer handle: ")) return false;
        if (!KernelConsole.WriteUInt64(KernelScheduler.GetRoleWorkerThreadId(KernelCpuRole.Drivers))) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(_inputTimerHandle)) return false;
        if (!KernelConsole.WriteLine(" (timer ISR only queues work; USB transfers run in Drivers worker)")) return false;
#endif
        _initialized = true;
        return true;
    }

    /// <summary>Ensures the selected text-input providers and timer-service drain are ready for an interactive shell.</summary>
    public static Boolean EnsureTextInputProviders()
    {
        if (!Initialize()) return false;
#if INU_INPUT_USB_HID
        if (!AttachSelectedUsbHandlers("TEXT_INPUT_USB")) return false;
#endif
        return true;
    }

    /// <summary>Attaches hardware IRQ delivery after the selected interrupt broker is online.</summary>
    public static Boolean EnableHardwareInterrupts()
    {
#if INU_INPUT_PS2 && INU_KERNELAREA_DRIVERS
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
#if INU_INPUT_PS2
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
#if INU_INPUT_USB_HID
        if (!AttachSelectedUsbHandlers("GUI_INPUT_USB"))
        {
            KernelConsole.WriteLine("NOKMAIN:GUI-INPUT:USB:FAIL");
            return false;
        }
        UsbHidCapabilities graphicalUsb = UsbHid.GetCapabilities();
        KernelConsole.WriteLine(graphicalUsb.Keyboards != 0U || graphicalUsb.Mice != 0U ? "NOKMAIN:GUI-INPUT:USB:OK" : "NOKMAIN:GUI-INPUT:USB:NO-DEVICE");
#endif
        KernelConsole.WriteLine("NOKMAIN:GUI-INPUT:READY");
        return true;
    }


#if INU_INPUT_USB_HID
    private static Boolean AttachSelectedUsbHandlers(String markerPrefix)
    {
        Boolean usbKeyboardSelected = HasConfiguredInput("usb-hid-keyboard");
        Boolean usbMouseSelected = HasConfiguredInput("usb-hid-mouse");
        if (!usbKeyboardSelected && !usbMouseSelected) return true;

        if (!KernelConsole.WriteHostControl(markerPrefix + "_BIND_ENTER")) return false;
        if (!UsbHid.IsInitialized())
        {
            KernelConsole.WriteHostControl(markerPrefix + "_HID_NOT_INITIALIZED");
            return false;
        }

        UsbHidCapabilities capabilities = UsbHid.GetCapabilities();
        if (!KernelStructuredLogging.Begin(KernelLogLevel.Info,"input-detail","InputHardwareStartup.AttachSelectedUsbHandlers")) return false;
        if (!KernelConsole.Write("USB HID keyboards/mice/reports: ")) return false;
        if (!KernelConsole.WriteUInt64(capabilities.Keyboards)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(capabilities.Mice)) return false;
        if (!KernelConsole.Write(" / ")) return false;
        if (!KernelConsole.WriteUInt64(capabilities.KeyboardReports + capabilities.MouseReports)) return false;
        if (!KernelConsole.WriteLine(String.Empty)) return false;

        if (usbKeyboardSelected)
        {
            if (capabilities.Keyboards == 0U)
            {
                KernelConsole.WriteHostControl(markerPrefix + "_KEYBOARD_NO_DEVICE");
                return false;
            }
            if (!UsbHid.SetKeyboardEventHandler(&HandleUsbKeyboardEvent))
            {
                KernelConsole.WriteHostControl(markerPrefix + "_KEYBOARD_HANDLER_FAIL");
                return false;
            }
            if (!KernelConsole.WriteHostControl(markerPrefix + "_KEYBOARD_PRESENT")) return false;
        }

        if (usbMouseSelected)
        {
            if (capabilities.Mice == 0U)
            {
                KernelConsole.WriteHostControl(markerPrefix + "_MOUSE_NO_DEVICE");
                return false;
            }
            if (!UsbHid.SetMouseEventHandler(&HandleUsbMouseEvent))
            {
                KernelConsole.WriteHostControl(markerPrefix + "_MOUSE_HANDLER_FAIL");
                return false;
            }
            if (!KernelConsole.WriteHostControl(markerPrefix + "_MOUSE_PRESENT")) return false;
        }

        return KernelConsole.WriteHostControl(markerPrefix + "_BIND_OK");
    }
#endif

#if INU_KERNELAREA_INPUT
    private static Boolean HasConfiguredInput(String value)
    {
        String[] configured = Inu.Kernel.Bootstrap.GeneratedConfiguration.Input();
        for (Int32 i = 0; i < configured.Length; i++) if (configured[i] == value) return true;
        return false;
    }

    private static Boolean QueueInputService(UInt64 cookie)
    {
        UInt64 signal=0UL;
        fixed(UInt64* p=&_inputTimerSignals)
        {
            UInt64 previous=0UL;
            Native.AtomicFetchAdd64(p,1UL,&previous);
            signal=previous+1UL;
        }
        fixed(UInt64* p=&_inputServicePending) Native.AtomicStore64(p,1UL);
        if(signal<=32UL){Native.WritePort8(0xE9,(Byte)'Q');Native.WritePort8(0xE9,(Byte)signal);}
        Boolean woke=KernelScheduler.NotifyRoleWork(KernelCpuRole.Drivers);
        if(signal<=32UL)Native.WritePort8(0xE9,woke?(Byte)'q':(Byte)'!');
        return woke;
    }

    private static Boolean ServiceInputWorker()
    {
        UInt64 pending=0UL;
        fixed(UInt64* p=&_inputServicePending)
            if(!Native.AtomicExchange64(p,0UL,&pending)||pending==0UL)return false;
        UInt64 run=0UL;
        fixed(UInt64* p=&_inputWorkerRuns){UInt64 previous=0UL;Native.AtomicFetchAdd64(p,1UL,&previous);run=previous+1UL;}
        if(run<=32UL){Native.WritePort8(0xE9,(Byte)'W');Native.WritePort8(0xE9,(Byte)run);}
        Boolean ok = ServiceInputNow(run);
        if(ok){fixed(UInt64* p=&_inputWorkerSuccesses){UInt64 ignored=0UL;Native.AtomicFetchAdd64(p,1UL,&ignored);}}
        else{fixed(UInt64* p=&_inputWorkerFailures){UInt64 ignored=0UL;Native.AtomicFetchAdd64(p,1UL,&ignored);}}
        if(run<=32UL)Native.WritePort8(0xE9,ok?(Byte)'w':(Byte)'X');
        return true;
    }

    private static Boolean ServiceInputNow(UInt64 run)
    {
        Boolean ok = true;
#if INU_INPUT_PS2
        if(run<=16UL)Native.WritePort8(0xE9,(Byte)'P');
        ok = KernelPs2.Service();
        if(run<=16UL)Native.WritePort8(0xE9,ok?(Byte)'p':(Byte)'!');
#endif
#if INU_INPUT_USB_HID
        if (UsbHid.IsInitialized())
        {
            if(run<=32UL)Native.WritePort8(0xE9,(Byte)'H');
            Boolean usb=UsbHid.Service();
            if(run<=32UL)Native.WritePort8(0xE9,usb?(Byte)'h':(Byte)'!');
            ok = usb & ok;
        }
#endif
        ok = ServiceKeyboardRepeat() & ok;
        return ok;
    }

    public static UInt64 GetInputTimerSignalCount()=>_inputTimerSignals;
    public static UInt64 GetInputWorkerRunCount()=>_inputWorkerRuns;
    public static UInt64 GetInputWorkerSuccessCount()=>_inputWorkerSuccesses;
    public static UInt64 GetInputWorkerFailureCount()=>_inputWorkerFailures;

#if INU_INPUT_PS2
    private static Boolean HandlePs2Interrupt(Byte vector, UInt64 cookie) => KernelPs2.Service();
    private static Boolean HandlePs2MouseEvent(Ps2MouseState input)
    {
#if INU_KERNELAREA_GUI
        return !KernelGui.IsInitialized() || KernelGui.HandlePs2Mouse(input);
#else
        return true;
#endif
    }
#endif
#if INU_INPUT_USB_HID
    private static Boolean HandleUsbMouseEvent(UsbHidMouseState input)
    {
#if INU_KERNELAREA_GUI
        return !KernelGui.IsInitialized() || KernelGui.HandleUsbMouse(input);
#else
        return true;
#endif
    }
#endif

#if INU_INPUT_PS2
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
#endif

#if INU_INPUT_USB_HID
    private static Boolean HandleUsbKeyboardEvent(UsbHidKeyboardEvent input)
    {
        if (!_usbFirstEventPublished)
        {
            _usbFirstEventPublished = true;
            KernelConsole.WriteHostControl("USB_KEYBOARD_FIRST_EVENT");
        }
        if (input.Pressed && !_usbFirstPressPublished)
        {
            _usbFirstPressPublished = true;
            KernelConsole.WriteHostControl("USB_KEYBOARD_FIRST_PRESS");
        }
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
        Boolean queued = UserlandRuntimeStartup.QueueCharacter(input.Character);
        if (queued && input.Character != '\0' && !_usbFirstQueuedCharacterPublished)
        {
            _usbFirstQueuedCharacterPublished = true;
            KernelConsole.WriteHostControl("USB_KEYBOARD_FIRST_CHARACTER_QUEUED");
        }
        return queued;
    }

    private static Boolean TryMapUsbNavigation(Byte usage, out Byte code)
    {
        code=0;
        if(usage==82U)code=0x80;else if(usage==81U)code=0x81;else if(usage==80U)code=0x82;else if(usage==79U)code=0x83;
        else if(usage==74U)code=0x84;else if(usage==77U)code=0x85;else if(usage==76U)code=0x86;else if(usage==75U)code=0x87;else if(usage==78U)code=0x88;
        return code!=0U;
    }
#endif

#if INU_INPUT_PS2
    private static Boolean IsPs2Repeatable(Ps2KeyboardEvent input)
    {
        if (input.Control || input.Alt) return false;
        return input.Key == Ps2Key.Up || input.Key == Ps2Key.Down || input.Key == Ps2Key.Left || input.Key == Ps2Key.Right ||
               input.Key == Ps2Key.PageUp || input.Key == Ps2Key.PageDown || IsRepeatableCharacter(input.Character);
    }
#endif

#if INU_INPUT_USB_HID
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
#if INU_INPUT_PS2
        if (_ps2RepeatActive && now >= _ps2RepeatDeadline)
        {
            _ps2RepeatDeadline = NextRepeatDeadline(KeyboardRepeatIntervalNanoseconds);
            Boolean repeated = TryMapPs2Navigation(_ps2RepeatKey, out Byte code) ?
                UserlandRuntimeStartup.QueueInputCode(code) : UserlandRuntimeStartup.QueueCharacter(_ps2RepeatCharacter);
            if (!repeated) { _ps2RepeatActive = false; ok = false; }
        }
#endif
#if INU_INPUT_USB_HID
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
