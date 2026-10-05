using System;
using Inu.Kernel.Internal.X64;
using Inu.Text;

namespace Inu.Kernel.Console;

public static partial class KernelConsole
{
    public static unsafe Boolean Write(Byte value)
    {
        if(!BeginAtomicPresentation())return false;Boolean ok=WriteRaw(value)&&(_serialDiagnosticLine||FlushConfigured());return EndAtomicPresentation()&&ok;
    }

    private static Boolean FlushConfigured() => _captureActive || !_framebufferEnabled || _framebuffer.Flush();

    private static unsafe Boolean WriteRaw(Byte value)
    {
        if (!_initialized || _outputPaused) return false;
        if(_captureActive)
        {
            if(_captureBuffer==null||_captureLength>=_captureCapacity)return false;
            _captureBuffer[_captureLength++]=value;return true;
        }
        if (_serialEnabled)
        {
            if (!Native.WriteSerial(value)) return false;
            if (_secondarySerialWriter != null && !_secondarySerialWriter(value)) return false;
        }
        if (_serialDiagnosticLine)
        {
            if (value == (Byte)'\n') _serialDiagnosticLine = false;
            return true;
        }
        if (!_framebufferEnabled) return true;
        if (!_ansiEnabled) return _framebuffer.Write(value);
        return WriteAnsiFramebuffer(value);
    }

    private static Boolean WriteAnsiFramebuffer(Byte value)
    {
        if (_ansiState == 0)
        {
            if (value == 0x1BU)
            {
                _ansiState = 1;
                _ansiParameter = 0U;
                _ansiPrivate = false;
                return true;
            }
            return _framebuffer.Write(value);
        }
        if (_ansiState == 1)
        {
            if (value == (Byte)'[')
            {
                _ansiState = 2;
                return true;
            }
            _ansiState = 0;
            return true;
        }
        if (value == (Byte)'?')
        {
            _ansiPrivate = true;
            return true;
        }
        if (value >= (Byte)'0' && value <= (Byte)'9')
        {
            UInt32 digit = (UInt32)(value - (Byte)'0');
            if (_ansiParameter <= 1000000U) _ansiParameter = (_ansiParameter * 10U) + digit;
            return true;
        }
        if (value == (Byte)';') return true;
        Boolean result = true;
        if (value == (Byte)'J' && _ansiParameter == 2U) result = _framebuffer.Clear();
        else if (_ansiPrivate && _ansiParameter == 25U && value == (Byte)'h') result = _framebuffer.SetCaretEnabled(true);
        else if (_ansiPrivate && _ansiParameter == 25U && value == (Byte)'l') result = _framebuffer.SetCaretEnabled(false);
        // SGR and cursor-position sequences are deliberately consumed when the framebuffer
        // backend cannot represent them yet; the original ANSI bytes are still mirrored to serial.
        _ansiState = 0;
        _ansiParameter = 0U;
        _ansiPrivate = false;
        return result;
    }
}
