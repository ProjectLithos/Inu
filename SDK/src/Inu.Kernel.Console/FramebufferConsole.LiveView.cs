using System;

namespace Inu.Kernel.Console;

internal unsafe partial struct FramebufferConsole
{
internal Boolean BeginBatchUpdate()
    { _batchUpdate=true; return true; }

internal Boolean EndBatchUpdate()
    { if(!_batchUpdate)return true;_batchUpdate=false;return Flush(); }

internal Boolean BeginLiveView()
    {
        if (_liveView) return true;
        if (!HideCaret()) return false;
        _liveView = true;
        _scrollLinesFromBottom = 0U;
        _cursorX = _margin;
        _cursorY = _margin;
        if (!ClearPixels()) { _liveView = false; return false; }
        return Present();
    }

internal Boolean BeginLiveFrame(UInt32 visualLineCount)
    {
        if (!_liveView || _batchUpdate || visualLineCount == 0U) return false;
        _batchUpdate = true;
        _liveFrameBatch = true;
        _cursorX = _margin;
        _cursorY = _margin;
        ResetDirty();
        ResetLiveDirty();
        return true;
    }

internal Boolean CancelLiveFrame()
    {
        // Cancellation is deliberately non-presenting. Discard any partially composed monitor
        // frame and let EndLiveView redraw retained shell history into the current render buffer.
        // This is safe for single/double/triple buffering because draw-buffer rotation happens
        // only after a successful completed-frame present.
        if(!_liveFrameBatch)return true;
        _liveFrameBatch=false;
        _batchUpdate=false;
        ResetDirty();
        ResetLiveDirty();
        return true;
    }

internal Boolean EndLiveFrame()
    {
        if (!_liveView || !_batchUpdate || !_liveFrameBatch) return false;
        _liveFrameBatch = false;
        _batchUpdate = false;

        // Live dashboards use one dedicated buffered present. The generic console dirty-region
        // list is intentionally not trusted here: glyph-level merging is useful for ordinary
        // console output, but a transient monitor only needs the bounded union of the rows it
        // actually changed. This keeps a monitor frame small and prevents dirty-list bookkeeping
        // from aborting the dashboard.
        Boolean presented = PresentLiveFrame();
        ResetDirty();
        ResetLiveDirty();
        return presented;
    }

internal Boolean BeginLiveRow(UInt32 visualRow)
    {
        if (!_liveView || _lineHeight == 0U) return false;
        if (_batchUpdate && !_liveFrameBatch) return false;
        UInt64 top64=(UInt64)_margin+((UInt64)visualRow*(UInt64)_lineHeight);
        if(top64>=_height)return false;
        UInt32 top=(UInt32)top64;
        UInt32 rowHeight=_lineHeight;
        if(rowHeight>_height-top)rowHeight=_height-top;
        UInt32 right=GetTextRight();
        if(right<=_margin)return false;
        if(!_liveFrameBatch)_batchUpdate=true;
        _cursorX=_margin;_cursorY=top;
        if(!FillRectangle(_margin,top,right-_margin,rowHeight,_background)){if(!_liveFrameBatch)_batchUpdate=false;return false;}
        return true;
    }

internal Boolean EndLiveRow()
    {
        if(!_liveView||!_batchUpdate)return false;
        if(_liveFrameBatch)return true;
        _batchUpdate=false;
        return Present();
    }

internal Boolean EndLiveView()
    {
        if (!_liveView) return true;
        if(_liveFrameBatch&&!CancelLiveFrame())return false;
        _batchUpdate = true;
        _liveView = false;
        Boolean redrawn = RedrawHistory();
        _batchUpdate = false;
        if (!redrawn) return false;
        return Flush();
    }
}
