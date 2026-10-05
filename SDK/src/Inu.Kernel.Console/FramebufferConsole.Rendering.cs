using System;

namespace Inu.Kernel.Console;

internal unsafe partial struct FramebufferConsole
{
private Boolean ClearPixels()
    {
        UInt64 pixelCount = (UInt64)_pitch * (UInt64)_height;
        if (pixelCount > _size / 4UL) return false;
        UInt32* pixel = (UInt32*)GetRenderAddress();
        while (pixelCount != 0UL)
        {
            *pixel = _background;
            pixel++;
            pixelCount--;
        }
        MarkDirtyRectangle(0U, 0U, _width, _height);
        return true;
    }

private Boolean MoveToNextLine()
    {
        _cursorX = _margin;
        if (_cursorY <= _height - _fontSize && _lineHeight <= (_height - _fontSize) - _cursorY)
        {
            _cursorY += _lineHeight;
            return true;
        }
        return ScrollUpOneLinePixels();
    }

private Boolean ScrollUpOneLinePixels()
    {
        if (_lineHeight == 0U || _margin >= _height || _lineHeight >= _height - _margin) return false;
        UInt32 sourceY = _margin + _lineHeight;
        UInt32 destinationY = _margin;
        UInt32 rowsToMove = _height - sourceY;
        UInt64 framebufferPixels = _size / 4UL;
        UInt32* pixels = (UInt32*)GetRenderAddress();

        UInt32 row = 0U;
        while (row < rowsToMove)
        {
            UInt64 sourceIndex = ((UInt64)(sourceY + row) * (UInt64)_pitch);
            UInt64 destinationIndex = ((UInt64)(destinationY + row) * (UInt64)_pitch);
            if (sourceIndex + _pitch > framebufferPixels || destinationIndex + _pitch > framebufferPixels) return false;
            UInt32 column = 0U;
            while (column < _pitch)
            {
                pixels[destinationIndex + column] = pixels[sourceIndex + column];
                column++;
            }
            row++;
        }

        UInt32 clearStartY = _height - _lineHeight;
        row = clearStartY;
        while (row < _height)
        {
            UInt64 destinationIndex = ((UInt64)row * (UInt64)_pitch);
            if (destinationIndex + _pitch > framebufferPixels) return false;
            UInt32 column = 0U;
            while (column < _pitch)
            {
                pixels[destinationIndex + column] = _background;
                column++;
            }
            row++;
        }
        MarkDirtyRectangle(0U, _margin, _width, _height - _margin);
        return _cursorY <= _height - _fontSize;
    }

private UInt32 GetTextRight()
    {
        UInt32 reserve = ScrollbarWidth + 2U;
        return _width > reserve ? _width - reserve : _width;
    }

private Boolean HideCaret()
    {
        if (!_caretVisible) return true;
        if (!DrawCaret(_background)) return false;
        _caretVisible = false;
        _caretTicks = 0U;
        return true;
    }

private Boolean DrawCaret(UInt32 color)
    {
        if (_cursorX >= GetTextRight() || _cursorY >= _height) return true;
        UInt32 caretWidth = _glyphWidth >= 12U ? 2U : 1U;
        UInt32 caretHeight = _fontSize;
        if (_cursorX + caretWidth > GetTextRight()) caretWidth = 1U;
        return FillRectangle(_cursorX, _cursorY, caretWidth, caretHeight, color);
    }

private Boolean DrawScrollbar()
    {
        if (_width <= ScrollbarWidth || _height == 0U) return true;
        UInt32 left = _width - ScrollbarWidth;
        UInt32 track = PackColor(27, 38, 49);
        UInt32 thumb = PackColor(112, 132, 150);
        if (!FillRectangle(left, 0U, ScrollbarWidth, _height, track)) return false;
        UInt32 totalLines = CountVisualLines();
        UInt32 visibleLines = GetVisibleLineCount();
        if (totalLines <= visibleLines) return FillRectangle(left + 2U, 2U, ScrollbarWidth - 4U, _height > 4U ? _height - 4U : 1U, thumb);
        UInt32 usable = _height > 4U ? _height - 4U : 1U;
        UInt64 scaled = ((UInt64)visibleLines * usable) / totalLines;
        UInt32 thumbHeight = scaled < 16UL ? 16U : (scaled > usable ? usable : (UInt32)scaled);
        UInt32 maximumOffset = totalLines - visibleLines;
        UInt32 offset = _scrollLinesFromBottom > maximumOffset ? maximumOffset : _scrollLinesFromBottom;
        UInt32 travel = usable > thumbHeight ? usable - thumbHeight : 0U;
        UInt32 fromTop = maximumOffset == 0U ? 0U : (UInt32)(((UInt64)(maximumOffset - offset) * travel) / maximumOffset);
        return FillRectangle(left + 2U, 2U + fromTop, ScrollbarWidth - 4U, thumbHeight, thumb);
    }

private Boolean FillRectangle(UInt32 left, UInt32 top, UInt32 width, UInt32 height, UInt32 color)
    {
        if (width == 0U || height == 0U || left >= _width || top >= _height) return true;
        UInt32 right = left + width > _width ? _width : left + width;
        UInt32 bottom = top + height > _height ? _height : top + height;
        UInt32* pixels = (UInt32*)GetRenderAddress();
        UInt32 y = top;
        while (y < bottom)
        {
            UInt64 row = (UInt64)y * _pitch;
            UInt32 x = left;
            while (x < right)
            {
                UInt64 index = row + x;
                if (index >= _size / 4UL) return false;
                pixels[index] = color;
                x++;
            }
            y++;
        }
        MarkDirtyRectangle(left, top, right - left, bottom - top);
        return true;
    }

private Boolean DrawGlyph(Byte value, UInt32 originX, UInt32 originY)
    {
        if (TrueTypeConsoleFont.IsActive())
        {
            if (TrueTypeConsoleFont.Draw(value, GetRenderAddress(), _width, _height, _pitch, originX, originY, _foreground))
            {
                MarkDirtyRectangle(originX, originY, _characterAdvance, _lineHeight);
                return true;
            }
            // A single unsupported/malformed glyph must not tear down the graphics console.
            // Render that cell with the built-in bitmap face and keep TrueType active for the rest.
        }
        UInt32 renderedRow = 0U;
        while (renderedRow < _fontSize)
        {
            UInt32 sourceRow = ConsoleFont.GetSourceRow(renderedRow, _fontSize);
            UInt32 bits = ConsoleFont.GetGlyphRow(value, sourceRow);
            UInt32 renderedColumn = 0U;
            while (renderedColumn < _glyphWidth)
            {
                UInt32 sourceColumn = ConsoleFont.GetSourceColumn(renderedColumn, _glyphWidth);
                UInt32 sourceWidth = ConsoleFont.GetSourceWidth();
                UInt32 mask = 1U << (Int32)((sourceWidth - 1U) - sourceColumn);
                if ((bits & mask) != 0U)
                {
                    if (!DrawPixel(originX + renderedColumn, originY + renderedRow)) return false;
                }
                renderedColumn++;
            }
            renderedRow++;
        }
        MarkDirtyRectangle(originX, originY, _glyphWidth, _fontSize);
        return true;
    }

private Boolean DrawPixel(UInt32 pixelX, UInt32 pixelY)
    {
        if (pixelX >= _width || pixelY >= _height) return false;
        UInt64 index = ((UInt64)pixelY * (UInt64)_pitch) + pixelX;
        if (index >= _size / 4UL) return false;
        *((UInt32*)GetRenderAddress() + index) = _foreground;
        return true;
    }

private UInt64 GetRenderAddress()
    {
        return _bufferCount == 1U ? _address : _drawBuffer;
    }

private Boolean Present()
    {
        if (!_dirty) return true;
        UInt32 presentLeft=_dirtyLeft,presentTop=_dirtyTop,presentRight=_dirtyRight,presentBottom=_dirtyBottom;
        if (_bufferCount == 1U)
        {
            Boolean presented=PresentScanout(presentLeft,presentTop,presentRight,presentBottom);
            if(presented)ResetDirty();
            return presented;
        }
        if (_drawBuffer == 0UL) return false;
        UInt32 regionCount=_dirtyRegionCount;
        if(regionCount==0U)regionCount=1U;
        UInt32 region=0U;
        while(region<regionCount)
        {
            UInt32 left,top,right,bottom;
            if(_dirtyRegionCount==0U){left=_dirtyLeft;top=_dirtyTop;right=_dirtyRight;bottom=_dirtyBottom;}
            else{UInt32 offset=region*4U;left=_dirtyRegions[offset];top=_dirtyRegions[offset+1U];right=_dirtyRegions[offset+2U];bottom=_dirtyRegions[offset+3U];}
            if(!CopyRegion(_drawBuffer,_address,left,top,right,bottom))return false;
            region++;
        }
        if(!PresentScanout(presentLeft,presentTop,presentRight,presentBottom))return false;
        if (_bufferCount == 2U)
        {
            ResetDirty();
            return true;
        }
        UInt64 next = _drawBuffer == _backBufferA ? _backBufferB : _backBufferA;
        if (next == 0UL) return false;
        region=0U;
        while(region<regionCount)
        {
            UInt32 left,top,right,bottom;
            if(_dirtyRegionCount==0U){left=_dirtyLeft;top=_dirtyTop;right=_dirtyRight;bottom=_dirtyBottom;}
            else{UInt32 offset=region*4U;left=_dirtyRegions[offset];top=_dirtyRegions[offset+1U];right=_dirtyRegions[offset+2U];bottom=_dirtyRegions[offset+3U];}
            if(!CopyRegion(_drawBuffer,next,left,top,right,bottom))return false;
            region++;
        }
        _drawBuffer = next;
        ResetDirty();
        return true;
    }
}
