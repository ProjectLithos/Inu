using System;

namespace Inu.Kernel.Console;

internal unsafe partial struct FramebufferConsole
{
internal Boolean PrepareWord(UInt32 characterCount)
    {
        // Live views own their row geometry explicitly and raw/ANSI output retains the existing
        // character-edge wrapping behaviour. Ordinary console strings use this method to move a
        // complete word to the next visual line when it will not fit in the remaining columns.
        if (_liveView || characterCount == 0U) return true;
        UInt32 columns = GetColumnCount();
        if (columns <= 1U || characterCount >= columns) return true;
        if (_cursorX <= _margin) return true;

        UInt32 usedColumns = (_cursorX - _margin) / _characterAdvance;
        if (usedColumns >= columns) usedColumns = columns;
        UInt32 remainingColumns = columns - usedColumns;
        if (characterCount <= remainingColumns) return true;

        if (!HideCaret()) return false;
        UInt32 linesBefore = _scrollLinesFromBottom == 0U ? 0U : CountVisualLines();
        AppendHistory(SoftWrapMarker);
        if (_scrollLinesFromBottom != 0U)
        {
            UInt32 linesAfter = CountVisualLines();
            if (linesAfter > linesBefore) _scrollLinesFromBottom += linesAfter - linesBefore;
            return true;
        }
        return MoveToNextLine();
    }

internal Boolean Write(Byte value)
    {
        if (value == (Byte)'\r') return true;
        if (value == (Byte)'\b') return Backspace();
        if (!HideCaret()) return false;
        UInt32 linesBefore = _scrollLinesFromBottom == 0U ? 0U : CountVisualLines();
        if (!_liveView)
        {
            AppendHistory(value);
            if (_scrollLinesFromBottom != 0U)
            {
                UInt32 linesAfter = CountVisualLines();
                if (linesAfter > linesBefore) _scrollLinesFromBottom += linesAfter - linesBefore;
                return true;
            }
        }

        UInt32 previousX = _cursorX;
        UInt32 previousY = _cursorY;
        if (!RenderLive(value)) return false;

        // Text is rendered glyph-by-glyph into the active render buffer, but presentation is
        // deliberately line/region based. Present once when a newline/wrap completes a line;
        // otherwise keep accumulating the dirty rectangle for the current line.
        Boolean completedLine = value == (Byte)'\n' || _cursorY != previousY || _cursorX < previousX;
        return !completedLine || _batchUpdate || Present();
    }

internal Boolean Backspace()
    {
        if (!HideCaret()) return false;
        if (_historyLength == 0U) return Flush();
        Byte last = GetHistoryByte(_historyLength - 1U);
        if (last == (Byte)'\n') return true;
        // A soft-wrap marker is not a user character. When editing reaches one, remove it and
        // reconstruct the exact pre-wrap cursor before deleting the preceding visible cell.
        Boolean removedSoftWrap = false;
        while (last == SoftWrapMarker)
        {
            removedSoftWrap = true;
            _historyLength--;
            if (_historyLength == 0U) return RedrawHistory() && Flush();
            last = GetHistoryByte(_historyLength - 1U);
            if (last == (Byte)'\n') return RedrawHistory() && Flush();
        }
        if (removedSoftWrap && !RedrawHistory()) return false;
        _historyLength--;

        // Editing the current command must not redraw the entire retained console. Move to
        // the previous visual cell, erase only that cell, then present only its dirty rectangle.
        if(_scrollLinesFromBottom==0U)
        {
            if(_cursorX>_margin) _cursorX-=_characterAdvance;
            else if(_cursorY>=_margin+_lineHeight)
            {
                _cursorY-=_lineHeight;
                UInt32 columns=GetColumnCount();
                _cursorX=_margin+(columns>0U?(columns-1U)*_characterAdvance:0U);
            }
            UInt32 eraseWidth=_characterAdvance;
            UInt32 right=GetTextRight();
            if(_cursorX>=right)return true;
            if(eraseWidth>right-_cursorX)eraseWidth=right-_cursorX;
            if(!FillRectangle(_cursorX,_cursorY,eraseWidth,_lineHeight,_background))return false;
            if(_caretEnabled)
            {
                if(!DrawCaret(_foreground))return false;
                _caretVisible=true;_caretTicks=0U;
            }
            // Do not call Flush() here: Flush redraws the scrollbar and would expand a
            // one-cell edit into a much larger dirty rectangle. Present only the erased cell
            // (plus the immediately adjacent old/new caret cells tracked by HideCaret).
            return Present();
        }

        // Backspace while the user is looking at older scrollback is uncommon; preserve the
        // historical viewport semantics there rather than moving the off-screen edit cursor.
        return true;
    }

internal Boolean Flush()
    {
        // A batch/live frame owns presentation. Write(String), WriteUInt64 and other formatted
        // helpers call Flush after each fragment; honoring those calls here would present and
        // reset the dirty state in the middle of a CPU-monitor frame. In triple buffering it
        // can also rotate the draw buffer while the frame is still being composed. Keep all
        // drawing in the current back buffer until EndBatchUpdate/EndLiveFrame performs the
        // single authoritative present.
        if (_batchUpdate) return true;
        if (!DrawScrollbar()) return false;
        if (_caretEnabled && _scrollLinesFromBottom == 0U && !_caretVisible)
        {
            if (!DrawCaret(_foreground)) return false;
            _caretVisible = true;
        }
        return Present();
    }

internal Boolean SetCaretEnabled(Boolean enabled)
    {
        if (!HideCaret()) return false;
        _caretEnabled = enabled;
        _caretTicks = 0U;
        return Flush();
    }

internal Boolean TickCaret()
    {
        if (!_caretEnabled || _scrollLinesFromBottom != 0U) return true;
        _caretTicks++;
        if (_caretTicks < CaretBlinkTicks) return true;
        _caretTicks = 0U;
        if (_caretVisible)
        {
            if (!DrawCaret(_background)) return false;
            _caretVisible = false;
        }
        else
        {
            if (!DrawCaret(_foreground)) return false;
            _caretVisible = true;
        }
        if (!DrawScrollbar()) return false;
        return Present();
    }

internal Boolean ConfigureBuffers(UInt64 backBufferA, UInt64 backBufferB, UInt64 bufferByteCount)
    {
        if (_address == 0UL || _frameByteCount == 0UL) return false;
        if (backBufferA == 0UL || backBufferB == 0UL || backBufferA == backBufferB) return false;
        if ((backBufferA & 3UL) != 0UL || (backBufferB & 3UL) != 0UL) return false;
        if (backBufferA == _address || backBufferB == _address) return false;
        if (bufferByteCount < _frameByteCount) return false;
        _backBufferA = backBufferA;
        _backBufferB = backBufferB;
        _bufferStorageBytes = bufferByteCount;
        _availableBufferCount = 3U;
        if (!CopyFrame(_address, _backBufferA)) return false;
        if (!CopyFrame(_address, _backBufferB)) return false;
        return SetBufferCount(0U);
    }

internal Boolean SetBufferCount(UInt32 bufferCount)
    {
        // 0 = automatic. The framebuffer text console benefits from one software render buffer
        // without the extra synchronization copy required by triple buffering.
        Boolean automatic = bufferCount == 0U;
        if (automatic) bufferCount = _availableBufferCount >= 2U ? 2U : 1U;
        if (bufferCount < 1U || bufferCount > 3U) return false;
        if (bufferCount > _availableBufferCount) return false;
        if (_bufferCount > 1U && !Present()) return false;
        if (bufferCount == 1U)
        {
            _bufferCount = 1U;
            _drawBuffer = _address;
            _automaticBuffering = automatic;
            return true;
        }
        if (_backBufferA == 0UL) return false;
        if (!CopyFrame(_address, _backBufferA)) return false;
        if (bufferCount == 3U)
        {
            if (_backBufferB == 0UL) return false;
            if (!CopyFrame(_address, _backBufferB)) return false;
        }
        _bufferCount = bufferCount;
        _drawBuffer = _backBufferA;
        _automaticBuffering = automatic;
        return true;
    }

internal Boolean ScrollUp()
    {
        if (!HideCaret()) return false;
        UInt32 totalLines = CountVisualLines();
        UInt32 visibleLines = GetVisibleLineCount();
        UInt32 maximumOffset = totalLines > visibleLines ? totalLines - visibleLines : 0U;
        if (_scrollLinesFromBottom < maximumOffset) _scrollLinesFromBottom++;
        if (!RedrawHistory()) return false;
        return Flush();
    }

internal Boolean ScrollDown()
    {
        if (!HideCaret()) return false;
        if (_scrollLinesFromBottom != 0U) _scrollLinesFromBottom--;
        if (!RedrawHistory()) return false;
        return Flush();
    }

internal Boolean ScrollPageUp()
    {
        if (!HideCaret()) return false;
        UInt32 totalLines=CountVisualLines();
        UInt32 visibleLines=GetVisibleLineCount();
        UInt32 maximumOffset=totalLines>visibleLines?totalLines-visibleLines:0U;
        UInt32 step=visibleLines>1U?visibleLines-1U:1U;
        UInt32 remaining=maximumOffset-_scrollLinesFromBottom;
        _scrollLinesFromBottom+=step<remaining?step:remaining;
        if(!RedrawHistory())return false;
        return Flush();
    }

internal Boolean ScrollPageDown()
    {
        if (!HideCaret()) return false;
        UInt32 visibleLines=GetVisibleLineCount();
        UInt32 step=visibleLines>1U?visibleLines-1U:1U;
        _scrollLinesFromBottom=_scrollLinesFromBottom>step?_scrollLinesFromBottom-step:0U;
        if(!RedrawHistory())return false;
        return Flush();
    }

internal Boolean ReplaceEditableTail(Byte* text,UInt32 oldLength,UInt32 newLength,UInt32 cursorOffset)
    {
        if(_liveView||newLength>0U&&text==null||oldLength>_historyLength||cursorOffset>newLength)return false;
        if(!HideCaret())return false;
        _scrollLinesFromBottom=0U;
        _historyLength-=oldLength;
        for(UInt32 i=0U;i<newLength;i++)AppendHistory(text[i]);
        if(!RedrawHistory())return false;
        if(!SetCaretAtHistoryIndex(_historyLength-newLength+cursorOffset))return false;
        return Flush();
    }

internal Boolean SetEditableTailCursor(UInt32 editableLength,UInt32 cursorOffset)
    {
        if(_liveView||editableLength>_historyLength||cursorOffset>editableLength)return false;
        if(!HideCaret())return false;
        _scrollLinesFromBottom=0U;
        if(!SetCaretAtHistoryIndex(_historyLength-editableLength+cursorOffset))return false;
        return Flush();
    }

internal Boolean SetFontPreset(UInt32 preset)
    {
        UInt32 size;
        if (preset == 1U) size = SmallFontSize;
        else if (preset == 2U) size = MediumFontSize;
        else if (preset == 3U) size = LargeFontSize;
        else return false;
        if (!ConfigureFont(size)) return false;
        UInt32 totalLines = CountVisualLines();
        UInt32 visibleLines = GetVisibleLineCount();
        UInt32 maximumOffset = totalLines > visibleLines ? totalLines - visibleLines : 0U;
        if (_scrollLinesFromBottom > maximumOffset) _scrollLinesFromBottom = maximumOffset;
        if (!RedrawHistory()) return false;
        return Present();
    }

internal Boolean EnableTrueTypeFont()
    {
        if (!TrueTypeConsoleFont.TryEnable(_fontSize)) return false;
        if (!ConfigureFont(_fontSize)) return false;
        if (!RedrawHistory()) return false;
        return Present();
    }

internal Boolean ReloadFontFace()
    {
        if (!ConfigureFont(_fontSize)) return false;
        UInt32 totalLines = CountVisualLines();
        UInt32 visibleLines = GetVisibleLineCount();
        UInt32 maximumOffset = totalLines > visibleLines ? totalLines - visibleLines : 0U;
        if (_scrollLinesFromBottom > maximumOffset) _scrollLinesFromBottom = maximumOffset;
        if (!RedrawHistory()) return false;
        return Present();
    }
}
