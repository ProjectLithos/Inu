using System;
using Inu.Kernel.Internal.X64;
using Inu.Text;

namespace Inu.Kernel.Console;

public static partial class KernelConsole
{
    public static Boolean ClearScreen()
    {
        if (!_initialized) return false;if(!BeginAtomicPresentation())return false;Boolean ok=_framebuffer.Clear();return EndAtomicPresentation()&&ok;
    }

    /// <summary>Begins a batched live framebuffer update. Completed lines are not presented until EndLiveUpdate.</summary>
    public static Boolean BeginLiveUpdate()
    { if(!_initialized)return false;return !_framebufferEnabled||_framebuffer.BeginBatchUpdate(); }

    /// <summary>Finishes a batched live framebuffer update and presents the completed frame once.</summary>
    public static Boolean EndLiveUpdate()
    { if(!_initialized)return false;return !_framebufferEnabled||_framebuffer.EndBatchUpdate(); }


    /// <summary>Starts a transient framebuffer-only live dashboard without changing retained shell scrollback.</summary>
    public static Boolean BeginLiveView()
    {
        if (!_initialized || _outputPaused || !_framebufferEnabled || _liveViewActive) return false;
        if (!_framebuffer.BeginLiveView()) return false;
        _liveViewActive = true;
        return true;
    }

    /// <summary>Begins one buffered dashboard frame. Dirty rows are accumulated without presenting until EndLiveFrame.</summary>
    public static Boolean BeginLiveFrame(UInt32 visualLineCount)
    { return _initialized && _liveViewActive && _framebuffer.BeginLiveFrame(visualLineCount); }

    /// <summary>Presents one completed buffered dashboard frame exactly once, copying only its dirty rectangles.</summary>
    public static Boolean EndLiveFrame()
    { return _initialized && _liveViewActive && _framebuffer.EndLiveFrame(); }

    /// <summary>Discards an in-progress live-dashboard frame without presenting it.</summary>
    public static Boolean CancelLiveFrame()
    { return !_initialized||!_liveViewActive||_framebuffer.CancelLiveFrame(); }

    /// <summary>Reports whether the framebuffer console currently owns a transient live view.</summary>
    public static Boolean IsLiveViewActive() => _initialized && _liveViewActive;

    /// <summary>Begins an in-place update of one live-dashboard row only.</summary>
    public static Boolean BeginLiveRow(UInt32 visualRow)
    { return _initialized && _liveViewActive && _framebuffer.BeginLiveRow(visualRow); }

    /// <summary>Completes one live-dashboard row. Inside a buffered live frame the row remains queued until EndLiveFrame.</summary>
    public static Boolean EndLiveRow()
    { return _initialized && _liveViewActive && _framebuffer.EndLiveRow(); }

    /// <summary>Ends the live dashboard and restores the retained shell viewport.</summary>
    public static Boolean EndLiveView()
    {
        if (!_initialized || !_liveViewActive) return false;
        Boolean result = _framebuffer.EndLiveView();
        _liveViewActive = false;
        return result;
    }

    /// <summary>Scrolls the framebuffer console upward by one visual line of retained output.</summary>
    public static Boolean ScrollUp()
    {
        if (!_initialized) return false;if(!BeginAtomicPresentation())return false;Boolean ok=_framebuffer.ScrollUp();return EndAtomicPresentation()&&ok;
    }

    /// <summary>Scrolls the framebuffer console downward by one visual line toward live output.</summary>
    public static Boolean ScrollDown()
    {
        if (!_initialized) return false;if(!BeginAtomicPresentation())return false;Boolean ok=_framebuffer.ScrollDown();return EndAtomicPresentation()&&ok;
    }

    /// <summary>Scrolls the framebuffer console backward by approximately one visible page.</summary>
    public static Boolean ScrollPageUp()
    {
        if(!_initialized)return false;if(!BeginAtomicPresentation())return false;Boolean ok=_framebuffer.ScrollPageUp();return EndAtomicPresentation()&&ok;
    }

    /// <summary>Scrolls the framebuffer console forward by approximately one visible page.</summary>
    public static Boolean ScrollPageDown()
    {
        if(!_initialized)return false;if(!BeginAtomicPresentation())return false;Boolean ok=_framebuffer.ScrollPageDown();return EndAtomicPresentation()&&ok;
    }

    /// <summary>Replaces the current shell-owned editable tail and positions its caret.</summary>
}
