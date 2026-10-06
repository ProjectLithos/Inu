using System;

namespace Inu.Kernel.Console;

/// <summary><inu.api>Selects the operating policy for the coder-facing kernel Console subsystem.</inu.api></summary>
public enum ConsoleType : UInt32
{
    /// <summary>Selects framebuffer text when available and falls back to the serial console.</summary>
    Auto = 0,
    /// <summary>Uses the Inu text console with the ASCII character set and mirrors output to the debug serial transport.</summary>
    TextAscii = 1,
    /// <summary>Uses the text console with ANSI control-sequence handling and mirrors the original ANSI stream to serial.</summary>
    TextAnsi = 2,
    /// <summary>Uses the GOP/framebuffer text console and preserves the serial debug mirror.</summary>
    FramebufferText = 3,
    /// <summary>Uses the graphics framebuffer console. When a TrueType face is available, glyphs are rasterised by the Inu TrueType renderer.</summary>
    Graphics = 4,
    /// <summary>Uses only the primary serial/debug console.</summary>
    Serial = 5
}

/// <summary><inu.api>Describes the lifecycle state of the coder-facing kernel Console subsystem.</inu.api></summary>
public enum ConsoleState : UInt32
{
    Unloaded = 0,
    Starting = 1,
    Running = 2,
    Stopping = 3,
    Stopped = 4,
    Resuming = 5,
    Unloading = 6,
    Failed = 7
}

/// <summary>
/// <inu.api>Coder-facing kernel console lifecycle and output policy.</inu.api> Run selects policy; KernelConsole remains an implementation/advanced mechanism.
/// </summary>
public static class Console
{
    private static ConsoleType _requestedMode = ConsoleType.Auto;
    private static ConsoleType _activeMode = ConsoleType.Auto;
    private static ConsoleState _state = ConsoleState.Unloaded;

    /// <summary>Gets the current lifecycle state.</summary>
    public static ConsoleState State => _state;

    /// <summary>Gets the resolved active console mode. Auto starts on framebuffer text when available and can promote to Graphics when the TrueType renderer becomes available.</summary>
    public static ConsoleType Mode => _activeMode;

    /// <summary>Gets the mode most recently requested by the caller.</summary>
    public static ConsoleType RequestedMode => _requestedMode;

    /// <summary>Gets whether the Console subsystem is currently running.</summary>
    public static Boolean IsRunning => _state == ConsoleState.Running;

    /// <summary>Starts serial/automatic bootstrap console policy without requiring any boot capability.</summary>
    public static Boolean Run(ConsoleType type)
    {
        if (type != ConsoleType.Serial && type != ConsoleType.Auto) return Fail();
        if (!KernelConsole.IsInitialized() && !KernelConsole.InitializeSerialOnly()) return Fail();
        _requestedMode = type;
        _activeMode = ConsoleType.Serial;
        _state = ConsoleState.Running;
        return KernelConsole.ConfigureOutput(true, false, false);
    }

    /// <summary>Starts a framebuffer-capable console using only the framebuffer boot capability.</summary>
    public static Boolean Run<TBoot>(ConsoleType type, TBoot framebufferBoot) where TBoot : IBootFramebufferContext
    {
        if (!framebufferBoot.IsAvailable()) return Fail();
        if ((UInt32)type > (UInt32)ConsoleType.Serial) return Fail();
        _requestedMode = type;
        _state = ConsoleState.Starting;
        if (!KernelConsole.IsInitialized())
        {
            if (type == ConsoleType.Serial || type == ConsoleType.Auto)
            {
                if (!KernelConsole.InitializeSerialOnly()) return Fail();
            }
            else if (!KernelConsole.Initialize(framebufferBoot)) return Fail();
        }
        ConsoleType resolved = type == ConsoleType.Auto ? ConsoleType.Serial : type;
        if (!KernelConsole.ConfigureOutput(true, UsesFramebuffer(resolved), resolved == ConsoleType.TextAnsi)) return Fail();
        _activeMode = resolved;
        _state = ConsoleState.Running;
        return true;
    }


    /// <summary>Promotes a running framebuffer console to the graphics/TrueType console when the font and graphics path are available.</summary>
    public static Boolean TryPromoteGraphics()
    {
        if (_state != ConsoleState.Running) return false;
        if (!KernelConsole.HasFramebuffer()) return false;
        if (!KernelConsole.TryEnableTrueTypeFont()) return false;
        if (!KernelConsole.ConfigureOutput(true, true, false)) return Fail();
        _activeMode = ConsoleType.Graphics;
        return true;
    }

    /// <summary>Stops console output while retaining its mode, framebuffer history and boot configuration for Resume.</summary>
    public static Boolean Stop()
    {
        if (_state != ConsoleState.Running) return false;
        _state = ConsoleState.Stopping;
        if (!KernelConsole.StopOutput()) return Fail();
        _state = ConsoleState.Stopped;
        return true;
    }

    /// <summary>Resumes a console previously stopped with Stop.</summary>
    public static Boolean Resume()
    {
        if (_state != ConsoleState.Stopped) return false;
        _state = ConsoleState.Resuming;
        if (!KernelConsole.ResumeOutput()) return Fail();
        _state = ConsoleState.Running;
        return true;
    }

    /// <summary>Unloads the logical Console subsystem and releases its active lifecycle bindings.</summary>
    public static Boolean Unload()
    {
        if (_state == ConsoleState.Unloaded) return true;
        _state = ConsoleState.Unloading;
        if (KernelConsole.IsInitialized() && !KernelConsole.Unload()) return Fail();
        _activeMode = ConsoleType.Auto;
        _requestedMode = ConsoleType.Auto;
        _state = ConsoleState.Unloaded;
        return true;
    }

    /// <summary>Writes text through the active console policy.</summary>
    public static Boolean Write(String value) => _state == ConsoleState.Running && KernelConsole.Write(value);

    /// <summary>Writes a line through the active console policy.</summary>
    public static Boolean WriteLine(String value) => _state == ConsoleState.Running && KernelConsole.WriteLine(value);

    /// <summary>Clears the active framebuffer text console when one is enabled.</summary>
    public static Boolean Clear() => _state == ConsoleState.Running && UsesFramebuffer(_activeMode) && KernelConsole.ClearScreen();

    private static Boolean UsesFramebuffer(ConsoleType type)
    {
        return type == ConsoleType.TextAscii || type == ConsoleType.TextAnsi || type == ConsoleType.FramebufferText || type == ConsoleType.Graphics;
    }

    private static Boolean Fail()
    {
        _state = ConsoleState.Failed;
        return false;
    }
}
