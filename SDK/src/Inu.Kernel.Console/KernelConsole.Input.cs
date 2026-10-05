using System;
using Inu.Kernel.Internal.X64;
using Inu.Text;

namespace Inu.Kernel.Console;

public static partial class KernelConsole
{
    public static unsafe Boolean ReplaceEditableInput(Byte* text,UInt32 oldLength,UInt32 newLength,UInt32 cursorOffset)
    {
        if(!_initialized||_outputPaused)return false;if(!BeginAtomicPresentation())return false;Boolean ok=!_framebufferEnabled||_framebuffer.ReplaceEditableTail(text,oldLength,newLength,cursorOffset);return EndAtomicPresentation()&&ok;
    }

    /// <summary>Moves the caret inside the current shell-owned editable tail without changing text.</summary>
    public static Boolean SetEditableInputCursor(UInt32 editableLength,UInt32 cursorOffset)
    {
        if(!_initialized||_outputPaused)return false;if(!BeginAtomicPresentation())return false;Boolean ok=!_framebufferEnabled||_framebuffer.SetEditableTailCursor(editableLength,cursorOffset);return EndAtomicPresentation()&&ok;
    }

    /// <summary>Gets the active framebuffer font preset: 1 = 8 px, 2 = 16 px, 3 = 24 px.</summary>
    public static UInt32 GetFontPreset()
    {
        UInt32 size = _framebuffer.FontSize;
        if (size == FramebufferConsole.SmallFontSize) return 1U;
        if (size == FramebufferConsole.MediumFontSize) return 2U;
        if (size == FramebufferConsole.LargeFontSize) return 3U;
        return 0U;
    }

    /// <summary>Selects framebuffer font preset 1 (8 px), 2 (16 px), or 3 (24 px) and redraws retained output.</summary>
    public static Boolean SetFontPreset(UInt32 preset)
    {
        if (!_initialized) return false;
        return _framebuffer.SetFontPreset(preset);
    }

    /// <summary>Gets the active framebuffer font face independently of its rendered size.</summary>
    public static ConsoleFontInformation GetFontInformation() => ConsoleFont.GetInformation();

    /// <summary>Installs a PSF2 font from kernel-accessible memory and redraws retained QEMU/GOP framebuffer output.</summary>
    public static Boolean InstallPsf2Font(UInt64 address, UInt64 length)
    {
        if (!_initialized || !ConsoleFont.InstallPsf2(address, length)) return false;
        if (_framebuffer.ReloadFontFace()) return true;
        ConsoleFont.UseEmbedded();
        _framebuffer.ReloadFontFace();
        return false;
    }

    /// <summary>Restores the guaranteed embedded Inu console font and redraws retained framebuffer output.</summary>
    public static Boolean UseEmbeddedFont()
    {
        if (!_initialized || !ConsoleFont.UseEmbedded()) return false;
        return _framebuffer.ReloadFontFace();
    }

    /// <summary>Erases the last framebuffer-console character and mirrors a terminal backspace sequence to serial output.</summary>
    public static unsafe Boolean Backspace()
    {
        if (!_initialized || _outputPaused) return false;if(!BeginAtomicPresentation())return false;
        if (_serialEnabled && !_liveViewActive)
        {
            if (!Native.WriteSerial((Byte)'\b') || !Native.WriteSerial((Byte)' ') || !Native.WriteSerial((Byte)'\b')){EndAtomicPresentation();return false;}
            if (_secondarySerialWriter != null)
            {
                if (!_secondarySerialWriter((Byte)'\b') || !_secondarySerialWriter((Byte)' ') || !_secondarySerialWriter((Byte)'\b')){EndAtomicPresentation();return false;}
            }
        }
        Boolean ok=!_framebufferEnabled || _framebuffer.Backspace();return EndAtomicPresentation()&&ok;
    }


    /// <summary>Enables or disables the framebuffer command caret.</summary>
    public static Boolean SetCaretEnabled(Boolean enabled)
    {
        if (!_initialized) return false;if(!BeginAtomicPresentation())return false;Boolean ok=_framebuffer.SetCaretEnabled(enabled);return EndAtomicPresentation()&&ok;
    }

    /// <summary>Advances the visual caret blink timer; intended for the timer-dispatch service.</summary>
    public static Boolean TickCaret()
    {
        if (!_initialized) return false;
        // Timer/IRQ context must never race an in-progress GUI/shell framebuffer transaction.
        // A missed blink is harmless; mutating cursor/history state concurrently is not.
        if(!TryBeginInterruptPresentation())return true;
        Boolean ok=_framebuffer.TickCaret();
        return EndAtomicPresentation()&&ok;
    }

    /// <summary>Confirms that the console is ready for decoded input delivered by the active input driver.</summary>
    public static Boolean ServiceInput() => _initialized;

    private static unsafe delegate*<Boolean> _inputService;
    private static unsafe delegate*<Boolean> _interactiveDeferredService;
    private static unsafe delegate*<Boolean> _idleEnterService;
    private static unsafe delegate*<Boolean> _idleExitService;

    /// <summary>Installs the SDK-owned decoded-input service used by the interactive shell.</summary>
    public static unsafe Boolean SetInputService(delegate*<Boolean> service)
    {
        _inputService = service;
        return true;
    }

    /// <summary>Installs work that must run only from the post-interrupt interactive loop, never from the keyboard/timer input callback.</summary>
    public static unsafe Boolean SetInteractiveDeferredService(delegate*<Boolean> service)
    {
        _interactiveDeferredService = service;
        return true;
    }

    /// <summary>Installs optional scheduler accounting hooks around the hardware interrupt-wait instruction without coupling Console to Scheduler.</summary>
    public static unsafe Boolean SetIdleAccountingServices(delegate*<Boolean> enterIdle, delegate*<Boolean> exitIdle)
    { _idleEnterService=enterIdle;_idleExitService=exitIdle;return true; }

    /// <summary>Writes one IDE control record to the primary serial/debug channel without rendering it on the guest framebuffer.</summary>
    public static Boolean WriteHostControl(String command)
    {
        if (!_initialized || command == null || !Native.BeginSerialRecord()) return false;
        const String prefix = "[[INU:";
        const String suffix = "]]\r\n";
        Boolean ok=true;
        for (Int32 i = 0; ok && i < prefix.Length; i++) ok=Native.WriteSerial((Byte)prefix[i]);
        for (Int32 i = 0; ok && i < command.Length; i++) ok=Native.WriteSerial((Byte)command[i]);
        for (Int32 i = 0; ok && i < suffix.Length; i++) ok=Native.WriteSerial((Byte)suffix[i]);
        return Native.EndSerialRecord()&&ok;
    }

    /// <summary>Runs the post-boot interrupt-driven idle loop while servicing the SDK-owned input bridge before each halt.</summary>
    public static unsafe Boolean RunInteractive()
    {
        if (!_initialized || _outputPaused) return false;
        while (true)
        {
            // Interactive helpers may report a transient presentation/service failure while a
            // different CPU is completing a foreground process.  That is a shell-level failure,
            // not a reason to panic the kernel.  Keep the interactive loop alive and retry on
            // the next pass.  Only failure of the architectural interrupt wait itself is fatal.
            if (_inputService != null && !_inputService())
            {
                TraceInteractiveRetry("input");
                Native.Pause();
                continue;
            }
            // Deferred interactive work executes only here, after any input callback/IRQ has
            // returned.  This is the safe place to run the shell command manager while
            // leaving hardware interrupts live for Ctrl-C during a foreground command.
            if (_interactiveDeferredService != null && !_interactiveDeferredService())
            {
                TraceInteractiveRetry("deferred");
                Native.Pause();
                continue;
            }
            if (_idleEnterService != null && !_idleEnterService()) TraceInteractiveRetry("idle-enter");
            if (!Native.WaitForInterrupt()) return false;
            if (_idleExitService != null && !_idleExitService()) TraceInteractiveRetry("idle-exit");
        }
    }

    private static void TraceInteractiveRetry(String stage)
    {
        if(!Native.BeginSerialRecord())return;
        const String prefix="[INU:INTERACTIVE] recoverable ";
        const String suffix=" failure; retrying\r\n";
        for(Int32 i=0;i<prefix.Length;i++)Native.WriteSerial((Byte)prefix[i]);
        if(stage!=null)for(Int32 i=0;i<stage.Length;i++)Native.WriteSerial((Byte)stage[i]);
        for(Int32 i=0;i<suffix.Length;i++)Native.WriteSerial((Byte)suffix[i]);
        Native.EndSerialRecord();
    }

    /// <summary>Attaches an optional post-boot serial mirror while preserving COM1 as the primary debug transport.</summary>
    public static unsafe Boolean SetSecondarySerialWriter(delegate*<Byte, Boolean> writer)
    {
        _secondarySerialWriter = writer;
        return true;
    }

    /// <summary>Writes one character to every configured console target and presents the completed glyph/line region.</summary>
}
