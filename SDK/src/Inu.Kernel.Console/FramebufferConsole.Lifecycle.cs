using System;

namespace Inu.Kernel.Console;

internal unsafe partial struct FramebufferConsole
{
internal Boolean Initialize<TBoot>(TBoot boot, UInt32 fontSize) where TBoot : IBootFramebufferContext
    {
        if (!boot.IsAvailable()) return false;
        UInt64 framebufferAddress = boot.GetFramebufferAddress();
        UInt64 framebufferSize = boot.GetFramebufferSize();
        UInt32 width = boot.GetFramebufferWidth();
        UInt32 height = boot.GetFramebufferHeight();
        UInt32 pixelsPerScanLine = boot.GetFramebufferPitchInPixels();
        UInt32 pixelFormat = boot.GetFramebufferPixelFormat();
        UInt32 redMask = boot.GetFramebufferRedMask();
        UInt32 greenMask = boot.GetFramebufferGreenMask();
        UInt32 blueMask = boot.GetFramebufferBlueMask();
        UInt32 reservedMask = boot.GetFramebufferReservedMask();
        if (framebufferAddress == 0 || (framebufferAddress & 3UL) != 0) return false;
        if (framebufferSize == 0 || width == 0 || height == 0) return false;
        if (pixelsPerScanLine < width) return false;
        if (pixelFormat > 2U) return false;

        UInt64 bytesPerScanLine = (UInt64)pixelsPerScanLine * 4UL;
        if (bytesPerScanLine == 0 || (UInt64)height > framebufferSize / bytesPerScanLine) return false;
        if (pixelFormat == 2U)
        {
            if (redMask == 0 || greenMask == 0 || blueMask == 0) return false;
            if ((redMask & greenMask) != 0 || (redMask & blueMask) != 0 || (greenMask & blueMask) != 0) return false;
            if (reservedMask != 0U && ((reservedMask & redMask) != 0 || (reservedMask & greenMask) != 0 || (reservedMask & blueMask) != 0)) return false;
            if (!IsContiguousMask(redMask) || !IsContiguousMask(greenMask) || !IsContiguousMask(blueMask)) return false;
        }

        _address = framebufferAddress;
        _presenter = null;
        _size = framebufferSize;
        _width = width;
        _height = height;
        _pitch = pixelsPerScanLine;
        _pixelFormat = pixelFormat;
        _redMask = redMask;
        _greenMask = greenMask;
        _blueMask = blueMask;
        _foreground = PackColor(232, 240, 248);
        _background = PackColor(9, 16, 24);
        _historyStart = 0U;
        _historyLength = 0U;
        _scrollLinesFromBottom = 0U;
        _frameByteCount = bytesPerScanLine * (UInt64)height;
        _backBufferA = 0UL;
        _backBufferB = 0UL;
        _bufferStorageBytes = 0UL;
        _drawBuffer = _address;
        _availableBufferCount = 1U;
        _bufferCount = 1U;
        _automaticBuffering = true;
        _batchUpdate = false;
        _liveView = false;
        _liveFrameBatch = false;
        ResetLiveDirty();
        _caretEnabled = false;
        _caretVisible = false;
        _caretTicks = 0U;
        ResetDirty();
        return ConfigureFont(fontSize);
    }

internal Boolean ReconfigureFramebuffer(UInt64 address,UInt64 size,UInt32 width,UInt32 height,UInt32 pitch,UInt32 pixelFormat)
    { return ReconfigureFramebuffer(address,size,width,height,pitch,pixelFormat,null); }

internal Boolean ReconfigureFramebuffer(UInt64 address,UInt64 size,UInt32 width,UInt32 height,UInt32 pitch,UInt32 pixelFormat,delegate*<UInt32,UInt32,UInt32,UInt32,Boolean> presenter)
    {
        if(_liveView||address==0UL||(address&3UL)!=0UL||size==0UL||width==0U||height==0U||pitch<width||pixelFormat>1U)return false;
        UInt64 bytesPerScanLine=(UInt64)pitch*4UL;if(bytesPerScanLine==0UL||(UInt64)height>size/bytesPerScanLine)return false;
        if(!HideCaret())return false;
        UInt32 oldRequested=_automaticBuffering?0U:_bufferCount;UInt64 required=bytesPerScanLine*(UInt64)height;
        _address=address;_presenter=presenter;_size=size;_width=width;_height=height;_pitch=pitch;_pixelFormat=pixelFormat;_redMask=0U;_greenMask=0U;_blueMask=0U;
        _foreground=PackColor(232,240,248);_background=PackColor(9,16,24);
        _frameByteCount=required;_drawBuffer=_address;_availableBufferCount=1U;_bufferCount=1U;_automaticBuffering=oldRequested==0U;_batchUpdate=false;ResetDirty();ResetLiveDirty();
        if(!ConfigureFont(_fontSize))return false;
        if(_backBufferA!=0UL&&_backBufferB!=0UL&&_bufferStorageBytes>=required)
        {
            _availableBufferCount=3U;if(!CopyFrame(_address,_backBufferA)||!CopyFrame(_address,_backBufferB))return false;if(!SetBufferCount(oldRequested))return false;
        }
        if(!RedrawHistory())return false;return Flush();
    }

internal Boolean SetReadOnlyShade(Boolean enabled)
    {
        if(_caretVisible&&!HideCaret())return false;
        _foreground=enabled?PackColor(138,151,164):PackColor(232,240,248);
        return true;
    }

internal Boolean Clear()
    {
        _historyStart = 0U;
        _historyLength = 0U;
        _scrollLinesFromBottom = 0U;
        _caretVisible = false;
        _caretTicks = 0U;
        if (!ClearPixels()) return false;
        _cursorX = _margin;
        _cursorY = _margin;
        return _batchUpdate || Present();
    }
}
