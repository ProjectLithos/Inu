using System;

namespace Inu.Kernel.Console;

internal unsafe partial struct FramebufferConsole
{
private Boolean ConfigureFont(UInt32 fontSize)
    {
        if (fontSize < BitmapFont.MinimumFontSize || fontSize > BitmapFont.MaximumFontSize) return false;
        UInt32 glyphWidth = TrueTypeConsoleFont.IsActive() ? TrueTypeConsoleFont.GlyphWidth : ConsoleFont.GetRenderedGlyphWidth(fontSize);
        UInt32 characterAdvance = TrueTypeConsoleFont.IsActive() ? TrueTypeConsoleFont.Advance : ConsoleFont.GetRenderedCharacterAdvance(fontSize);
        UInt32 lineHeight = TrueTypeConsoleFont.IsActive() ? TrueTypeConsoleFont.LineHeight : ConsoleFont.GetRenderedLineHeight(fontSize);
        UInt32 margin = fontSize / 2U;
        if (margin == 0U) margin = 1U;
        if (glyphWidth == 0U || characterAdvance < glyphWidth || lineHeight < fontSize) return false;
        if (margin >= _width || margin >= _height) return false;
        if (glyphWidth > _width - margin || fontSize > _height - margin) return false;
        _fontSize = fontSize;
        _glyphWidth = glyphWidth;
        _characterAdvance = characterAdvance;
        _lineHeight = lineHeight;
        _margin = margin;
        _cursorX = margin;
        _cursorY = margin;
        return true;
    }

private Boolean RenderLive(Byte value)
    {
        if (value == (Byte)'\n') return MoveToNextLine();
        UInt32 right = GetTextRight();
        if ((_cursorX >= right || _glyphWidth > right - _cursorX) && !MoveToNextLine()) return false;
        if (_cursorY > _height - _fontSize) return false;
        if (!DrawGlyph(value, _cursorX, _cursorY)) return false;
        _cursorX += _characterAdvance;
        return true;
    }

private void AppendHistory(Byte value)
    {
        fixed (Byte* history = _history)
        {
            if (_historyLength < ScrollbackCapacity)
            {
                UInt32 index = (_historyStart + _historyLength) % ScrollbackCapacity;
                history[index] = value;
                _historyLength++;
                return;
            }
            history[_historyStart] = value;
            _historyStart++;
            if (_historyStart == ScrollbackCapacity) _historyStart = 0U;
        }
    }

private Byte GetHistoryByte(UInt32 logicalIndex)
    {
        fixed (Byte* history = _history)
        {
            UInt32 index = (_historyStart + logicalIndex) % ScrollbackCapacity;
            return history[index];
        }
    }

private UInt32 GetColumnCount()
    {
        UInt32 right = GetTextRight();
        if (_characterAdvance == 0U || _glyphWidth == 0U || _margin >= right || _glyphWidth > right - _margin) return 1U;
        return ((right - _glyphWidth - _margin) / _characterAdvance) + 1U;
    }

private UInt32 GetVisibleLineCount()
    {
        if (_lineHeight == 0U || _margin > _height - _fontSize) return 1U;
        return ((_height - _fontSize - _margin) / _lineHeight) + 1U;
    }

private UInt32 CountVisualLines()
    {
        UInt32 columns = GetColumnCount();
        UInt32 lineCount = 1U;
        UInt32 column = 0U;
        UInt32 index = 0U;
        while (index < _historyLength)
        {
            Byte value = GetHistoryByte(index);
            if (value == (Byte)'\n' || value == SoftWrapMarker)
            {
                lineCount++;
                column = 0U;
            }
            else
            {
                if (column >= columns)
                {
                    lineCount++;
                    column = 0U;
                }
                column++;
            }
            index++;
        }
        return lineCount;
    }

private Boolean SetCaretAtHistoryIndex(UInt32 targetIndex)
    {
        if(targetIndex>_historyLength)return false;
        UInt32 columns=GetColumnCount();
        UInt32 targetLine=0U,targetColumn=0U;
        for(UInt32 index=0U;index<targetIndex;index++)
        {
            Byte value=GetHistoryByte(index);
            if(value==(Byte)'\n'||value==SoftWrapMarker){targetLine++;targetColumn=0U;continue;}
            if(targetColumn>=columns){targetLine++;targetColumn=0U;}
            targetColumn++;
        }
        // A caret immediately after the final cell belongs at the start of the next visual line.
        if(targetColumn>=columns){targetLine++;targetColumn=0U;}
        UInt32 totalLines=CountVisualLines();
        UInt32 visibleLines=GetVisibleLineCount();
        UInt32 firstLine=totalLines>visibleLines?totalLines-visibleLines:0U;
        if(targetLine<firstLine)return false;
        UInt32 relativeLine=targetLine-firstLine;
        UInt64 y=(UInt64)_margin+(UInt64)relativeLine*(UInt64)_lineHeight;
        UInt64 x=(UInt64)_margin+(UInt64)targetColumn*(UInt64)_characterAdvance;
        if(y>=_height||x>=GetTextRight())return false;
        _cursorX=(UInt32)x;_cursorY=(UInt32)y;
        return true;
    }

private UInt32 FindVisualLineStart(UInt32 targetLine)
    {
        if (targetLine == 0U) return 0U;
        UInt32 columns = GetColumnCount();
        UInt32 line = 0U;
        UInt32 column = 0U;
        UInt32 index = 0U;
        while (index < _historyLength)
        {
            Byte value = GetHistoryByte(index);
            if (value == (Byte)'\n' || value == SoftWrapMarker)
            {
                line++;
                column = 0U;
                index++;
                if (line == targetLine) return index;
                continue;
            }
            if (column >= columns)
            {
                line++;
                column = 0U;
                if (line == targetLine) return index;
            }
            column++;
            index++;
        }
        return _historyLength;
    }

private Boolean RedrawHistory()
    {
        if (!ClearPixels()) return false;
        UInt32 totalLines = CountVisualLines();
        UInt32 visibleLines = GetVisibleLineCount();
        UInt32 maximumOffset = totalLines > visibleLines ? totalLines - visibleLines : 0U;
        if (_scrollLinesFromBottom > maximumOffset) _scrollLinesFromBottom = maximumOffset;
        UInt32 firstLine = totalLines > visibleLines + _scrollLinesFromBottom
            ? totalLines - visibleLines - _scrollLinesFromBottom
            : 0U;
        UInt32 index = FindVisualLineStart(firstLine);
        UInt32 x = _margin;
        UInt32 y = _margin;
        while (index < _historyLength)
        {
            Byte value = GetHistoryByte(index);
            if (value == (Byte)'\n' || value == SoftWrapMarker)
            {
                x = _margin;
                if (y > _height - _fontSize || _lineHeight > (_height - _fontSize) - y) break;
                y += _lineHeight;
                index++;
                continue;
            }
            UInt32 right = GetTextRight();
            if (x >= right || _glyphWidth > right - x)
            {
                x = _margin;
                if (y > _height - _fontSize || _lineHeight > (_height - _fontSize) - y) break;
                y += _lineHeight;
            }
            if (y > _height - _fontSize) break;
            if (!DrawGlyph(value, x, y)) return false;
            x += _characterAdvance;
            index++;
        }
        if (_scrollLinesFromBottom == 0U)
        {
            _cursorX = x;
            _cursorY = y;
        }
        return true;
    }
}
