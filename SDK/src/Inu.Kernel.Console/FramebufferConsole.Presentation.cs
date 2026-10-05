using System;

namespace Inu.Kernel.Console;

internal unsafe partial struct FramebufferConsole
{
private Boolean PresentScanout(UInt32 left,UInt32 top,UInt32 right,UInt32 bottom)
    {
        if(left>=right||top>=bottom||right>_width||bottom>_height)return true;
        // Firmware GOP is directly CPU-visible and therefore requires no callback. Driver-owned
        // scan-outs (notably VirtIO-GPU) install a presenter from the bootstrap/HAL layer, keeping
        // the console independent of Graphics/Heap/Memory and avoiding a NuGet project cycle.
        if(_presenter==null)return true;
        return _presenter(left,top,right-left,bottom-top);
    }

private Boolean CopyRegion(UInt64 sourceAddress, UInt64 destinationAddress, UInt32 left, UInt32 top, UInt32 right, UInt32 bottom)
    {
        if (sourceAddress == 0UL || destinationAddress == 0UL) return false;
        if ((sourceAddress & 3UL) != 0UL || (destinationAddress & 3UL) != 0UL) return false;
        if (left >= right || top >= bottom || right > _width || bottom > _height) return false;
        UInt32* source = (UInt32*)sourceAddress;
        UInt32* destination = (UInt32*)destinationAddress;
        UInt32 row = top;
        while (row < bottom)
        {
            UInt64 rowStart = (UInt64)row * (UInt64)_pitch;
            UInt64 start = rowStart + left;
            UInt64 end = rowStart + right;
            if (end > _size / 4UL) return false;
            UInt64 index = start;
            while (index < end)
            {
                destination[index] = source[index];
                index++;
            }
            row++;
        }
        return true;
    }

private void MarkDirtyRectangle(UInt32 left, UInt32 top, UInt32 width, UInt32 height)
    {
        if (width == 0U || height == 0U || left >= _width || top >= _height) return;
        UInt64 right64 = (UInt64)left + width;
        UInt64 bottom64 = (UInt64)top + height;
        UInt32 right = right64 > _width ? _width : (UInt32)right64;
        UInt32 bottom = bottom64 > _height ? _height : (UInt32)bottom64;
        if (right <= left || bottom <= top) return;
        if (!_dirty)
        {
            _dirty = true;
            _dirtyLeft = left;
            _dirtyTop = top;
            _dirtyRight = right;
            _dirtyBottom = bottom;
        }
        else
        {
            if (left < _dirtyLeft) _dirtyLeft = left;
            if (top < _dirtyTop) _dirtyTop = top;
            if (right > _dirtyRight) _dirtyRight = right;
            if (bottom > _dirtyBottom) _dirtyBottom = bottom;
        }

        if (_liveFrameBatch) MarkLiveDirtyRectangle(left, top, right, bottom);

        // Keep independent regions for buffered frames. A row clear contains all glyph dirties
        // subsequently emitted for that row, so a live CPU frame normally stores one region
        // per row rather than one rectangle per glyph. If the bounded list fills, fall back to
        // the accumulated rectangle so correctness is preserved for arbitrary console output.
        UInt32 i=0U;
        while(i<_dirtyRegionCount)
        {
            UInt32 offset=i*4U;UInt32 l=_dirtyRegions[offset],t=_dirtyRegions[offset+1U],r=_dirtyRegions[offset+2U],b=_dirtyRegions[offset+3U];
            if(left>=l&&top>=t&&right<=r&&bottom<=b)return;
            if(l>=left&&t>=top&&r<=right&&b<=bottom){_dirtyRegions[offset]=left;_dirtyRegions[offset+1U]=top;_dirtyRegions[offset+2U]=right;_dirtyRegions[offset+3U]=bottom;return;}
            Boolean overlaps=left<r&&right>l&&top<b&&bottom>t;
            if(overlaps){if(l<left)left=l;if(t<top)top=t;if(r>right)right=r;if(b>bottom)bottom=b;_dirtyRegions[offset]=left;_dirtyRegions[offset+1U]=top;_dirtyRegions[offset+2U]=right;_dirtyRegions[offset+3U]=bottom;return;}
            i++;
        }
        if(_dirtyRegionCount<MaxDirtyRegions)
        {
            UInt32 offset=_dirtyRegionCount*4U;_dirtyRegions[offset]=left;_dirtyRegions[offset+1U]=top;_dirtyRegions[offset+2U]=right;_dirtyRegions[offset+3U]=bottom;_dirtyRegionCount++;return;
        }
        _dirtyRegions[0]=_dirtyLeft;_dirtyRegions[1]=_dirtyTop;_dirtyRegions[2]=_dirtyRight;_dirtyRegions[3]=_dirtyBottom;_dirtyRegionCount=1U;
    }

private void MarkLiveDirtyRectangle(UInt32 left, UInt32 top, UInt32 right, UInt32 bottom)
    {
        if (left >= right || top >= bottom) return;
        if (!_liveFrameDirty)
        {
            _liveFrameDirty = true;
            _liveDirtyLeft = left;
            _liveDirtyTop = top;
            _liveDirtyRight = right;
            _liveDirtyBottom = bottom;
            return;
        }
        if (left < _liveDirtyLeft) _liveDirtyLeft = left;
        if (top < _liveDirtyTop) _liveDirtyTop = top;
        if (right > _liveDirtyRight) _liveDirtyRight = right;
        if (bottom > _liveDirtyBottom) _liveDirtyBottom = bottom;
    }

private void ResetLiveDirty()
    {
        _liveFrameDirty = false;
        _liveDirtyLeft = 0U;
        _liveDirtyTop = 0U;
        _liveDirtyRight = 0U;
        _liveDirtyBottom = 0U;
    }

private Boolean PresentLiveFrame()
    {
        if (!_liveFrameDirty) return true;
        UInt32 left = _liveDirtyLeft;
        UInt32 top = _liveDirtyTop;
        UInt32 right = _liveDirtyRight > _width ? _width : _liveDirtyRight;
        UInt32 bottom = _liveDirtyBottom > _height ? _height : _liveDirtyBottom;
        if (left >= right || top >= bottom) return true;

        // Single buffering renders directly into the display-owned backing memory, but
        // driver-owned targets such as VirtIO-GPU still require an explicit present callback.
        if (_bufferCount == 1U) return PresentScanout(left,top,right,bottom);
        if (_drawBuffer == 0UL || _address == 0UL) return false;

        // Prefer the bounded live rectangle. If an implementation-specific partial blit ever
        // rejects it, fall back to synchronising the software frame once rather than terminating
        // the kernel command. The fallback is exceptional; normal monitor animation stays partial.
        if (!CopyRegion(_drawBuffer, _address, left, top, right, bottom))
        {
            if (!CopyFrame(_drawBuffer, _address)) return false;
        }
        if(!PresentScanout(left,top,right,bottom))return false;

        if (_bufferCount == 2U) return true;
        UInt64 next = _drawBuffer == _backBufferA ? _backBufferB : _backBufferA;
        if (next == 0UL) return false;
        if (!CopyRegion(_drawBuffer, next, left, top, right, bottom))
        {
            if (!CopyFrame(_drawBuffer, next)) return false;
        }
        _drawBuffer = next;
        return true;
    }

private void ResetDirty()
    {
        _dirty = false;
        _dirtyLeft = 0U;
        _dirtyTop = 0U;
        _dirtyRight = 0U;
        _dirtyBottom = 0U;
        _dirtyRegionCount = 0U;
    }

private Boolean CopyFrame(UInt64 sourceAddress, UInt64 destinationAddress)
    {
        if (sourceAddress == 0UL || destinationAddress == 0UL) return false;
        if ((sourceAddress & 3UL) != 0UL || (destinationAddress & 3UL) != 0UL) return false;
        UInt64 pixelCount = _frameByteCount / 4UL;
        if (pixelCount == 0UL || _frameByteCount > _size) return false;
        UInt32* source = (UInt32*)sourceAddress;
        UInt32* destination = (UInt32*)destinationAddress;
        UInt64 index = 0UL;
        while (index < pixelCount)
        {
            destination[index] = source[index];
            index++;
        }
        return true;
    }

private UInt32 PackColor(Byte red, Byte green, Byte blue)
    {
        if (_pixelFormat == 0U) return (UInt32)red | ((UInt32)green << 8) | ((UInt32)blue << 16);
        if (_pixelFormat == 1U) return (UInt32)blue | ((UInt32)green << 8) | ((UInt32)red << 16);
        return EncodeMask(red, _redMask) | EncodeMask(green, _greenMask) | EncodeMask(blue, _blueMask);
    }

private static Boolean IsContiguousMask(UInt32 mask)
    {
        while ((mask & 1U) == 0U) mask >>= 1;
        while ((mask & 1U) != 0U) mask >>= 1;
        return mask == 0U;
    }

private static UInt32 EncodeMask(Byte component, UInt32 mask)
    {
        UInt32 shift = 0U;
        while (((mask >> (Int32)shift) & 1U) == 0U && shift < 31U) shift++;
        UInt32 shiftedMask = mask >> (Int32)shift;
        UInt32 bits = 0U;
        while ((shiftedMask & 1U) != 0U)
        {
            bits++;
            shiftedMask >>= 1;
        }
        UInt64 maximum = bits == 32U ? 0xFFFFFFFFUL : ((1UL << (Int32)bits) - 1UL);
        UInt32 encoded = (UInt32)(((UInt64)component * maximum) / 255UL);
        return (encoded << (Int32)shift) & mask;
    }
}
