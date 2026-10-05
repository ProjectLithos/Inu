using System;
using Inu.Kernel.Internal.X64;
using Inu.Text;

namespace Inu.Kernel.Console;

public static partial class KernelConsole
{
    public static Boolean BeginSerialDiagnosticLine()
    {
        if (!_initialized || _outputPaused || _serialDiagnosticLine) return false;
        _serialDiagnosticLine = true;
        return true;
    }

    /// <summary>Gets whether a serial-only structured diagnostic line is currently being emitted.</summary>
    public static Boolean IsSerialDiagnosticLineActive() => _serialDiagnosticLine;

    /// <summary>Writes a managed string without appending a line terminator.</summary>
    public static Boolean Write(String value)
    {
        if (!_initialized || _outputPaused || value == null) return false;
        if(!BeginAtomicPresentation())return false;
        Boolean ok=WriteStringRaw(value)&&(_serialDiagnosticLine||FlushConfigured());
        return EndAtomicPresentation()&&ok;
    }

    /// <summary>Writes a managed string followed by a carriage return and line feed as one framebuffer batch.</summary>
    public static Boolean WriteLine(String value)
    {
        if (!_initialized || _outputPaused || value == null) return false;
        if(!BeginAtomicPresentation())return false;
        Boolean ok=WriteStringRaw(value)&&WriteRaw((Byte)'\r')&&WriteRaw((Byte)'\n')&&FlushConfigured();
        return EndAtomicPresentation()&&ok;
    }

    private static Boolean WriteStringRaw(String value)
    {
        Int32 length = value.Length;
        Int32 index = 0;
        while (index < length)
        {
            Char character = value[index];
            if (ShouldPrepareWord(character))
            {
                Int32 wordEnd = index;
                while (wordEnd < length && ShouldPrepareWord(value[wordEnd])) wordEnd++;
                if (_framebufferEnabled && !_serialDiagnosticLine && !_ansiEnabled && !_liveViewActive)
                {
                    if (!_framebuffer.PrepareWord((UInt32)(wordEnd - index))) return false;
                }
                while (index < wordEnd)
                {
                    character = value[index];
                    if ((UInt32)character > 0x7FU) character = (Char)'?';
                    if (!WriteRaw((Byte)character)) return false;
                    index++;
                }
                continue;
            }
            if ((UInt32)character > 0x7FU) character = (Char)'?';
            if (!WriteRaw((Byte)character)) return false;
            index++;
        }
        return true;
    }

    private static Boolean ShouldPrepareWord(Char character)
    {
        return character != (Char)' ' && character != (Char)'\t' && character != (Char)'\r' && character != (Char)'\n';
    }

    /// <summary>Writes a Boolean using normal .NET True/False text without allocating a managed string.</summary>
    public static Boolean Write(Boolean value) => Write(StringFormatter.Format(value));

    /// <summary>Writes a Boolean using normal .NET True/False text followed by a line terminator.</summary>
    public static Boolean WriteLine(Boolean value) => WriteLine(StringFormatter.Format(value));

    /// <summary>Writes a text prefix followed by a Boolean without runtime string concatenation.</summary>
    public static Boolean Write(String prefix, Boolean value)
    {
        if (!Write(prefix)) return false;
        return Write(value);
    }

    /// <summary>Writes a text prefix followed by a Boolean and a line terminator without runtime string concatenation.</summary>
    public static Boolean WriteLine(String prefix, Boolean value)
    {
        if (!Write(prefix)) return false;
        return WriteLine(value);
    }


    /// <summary>Writes one unsigned 64-bit value as an ordinary base-10 number.</summary>
    public static unsafe Boolean WriteUInt64(UInt64 value)
    {
        if(!BeginAtomicPresentation())return false;
        Byte* digits = stackalloc Byte[20];
        Int32 count = 0;
        do
        {
            digits[count] = (Byte)('0' + (Byte)(value % 10UL));
            value /= 10UL;
            count++;
        }
        while (value != 0UL);

        while (count > 0)
        {
            count--;
            if (!WriteRaw(digits[count])){EndAtomicPresentation();return false;}
        }
        Boolean ok=_serialDiagnosticLine ? true : FlushConfigured();return EndAtomicPresentation()&&ok;
    }

    /// <summary>Writes a byte quantity using B, KiB, MiB, GiB, or TiB as appropriate.</summary>
    public static Boolean WriteByteSize(UInt64 bytes)
    {
        if (bytes >= 1099511627776UL) return WriteScaled(bytes, 1099511627776UL, " TiB");
        if (bytes >= 1073741824UL) return WriteScaled(bytes, 1073741824UL, " GiB");
        if (bytes >= 1048576UL) return WriteScaled(bytes, 1048576UL, " MiB");
        if (bytes >= 1024UL) return WriteScaled(bytes, 1024UL, " KiB");
        if (!WriteUInt64(bytes)) return false;
        return Write(" B");
    }

    /// <summary>Writes a frequency using Hz, kHz, MHz, or GHz as appropriate.</summary>
    public static Boolean WriteFrequency(UInt64 hertz)
    {
        if (hertz >= 1000000000UL) return WriteScaled(hertz, 1000000000UL, " GHz");
        if (hertz >= 1000000UL) return WriteScaled(hertz, 1000000UL, " MHz");
        if (hertz >= 1000UL) return WriteScaled(hertz, 1000UL, " kHz");
        if (!WriteUInt64(hertz)) return false;
        return Write(" Hz");
    }

    /// <summary>Writes a duration expressed in nanoseconds using compact human-readable day/hour/minute/second units.</summary>
    public static Boolean WriteDurationNanoseconds(UInt64 nanoseconds)
    {
        const UInt64 second=1000000000UL, minute=60UL*second, hour=60UL*minute, day=24UL*hour;
        if(nanoseconds>=day) return WriteDurationParts(nanoseconds,day,"d",hour,"h",minute,"m",second,"s");
        if(nanoseconds>=hour) return WriteDurationParts(nanoseconds,hour,"h",minute,"m",second,"s",0UL,"");
        if(nanoseconds>=minute) return WriteDurationParts(nanoseconds,minute,"m",second,"s",0UL,"",0UL,"");
        if(nanoseconds>=second) return WriteScaled(nanoseconds,second," s");
        if(nanoseconds>=1000000UL) return WriteScaled(nanoseconds,1000000UL," ms");
        if(nanoseconds>=1000UL) return WriteScaled(nanoseconds,1000UL," us");
        if(!WriteUInt64(nanoseconds)) return false;
        return Write(" ns");
    }

    private static Boolean WriteDurationParts(UInt64 value,UInt64 first,String firstSuffix,UInt64 second,String secondSuffix,UInt64 third,String thirdSuffix,UInt64 fourth,String fourthSuffix)
    {
        UInt64 remaining=value;
        if(first!=0UL){UInt64 part=remaining/first;remaining%=first;if(!WriteUInt64(part)||!Write(firstSuffix))return false;}
        if(second!=0UL){UInt64 part=remaining/second;remaining%=second;if(!Write(" ")||!WriteUInt64(part)||!Write(secondSuffix))return false;}
        if(third!=0UL){UInt64 part=remaining/third;remaining%=third;if(!Write(" ")||!WriteUInt64(part)||!Write(thirdSuffix))return false;}
        if(fourth!=0UL){UInt64 part=remaining/fourth;if(!Write(" ")||!WriteUInt64(part)||!Write(fourthSuffix))return false;}
        return true;
    }

    private static Boolean WriteScaled(UInt64 value, UInt64 divisor, String suffix)
    {
        UInt64 whole = value / divisor;
        UInt64 remainder = value % divisor;
        UInt64 hundredths = (remainder * 100UL) / divisor;
        if (!WriteUInt64(whole)) return false;
        if (hundredths != 0UL)
        {
            if (!Write(".")) return false;
            if (hundredths < 10UL && !Write("0")) return false;
            if (!WriteUInt64(hundredths)) return false;
        }
        return Write(suffix);
    }

    /// <summary>Writes one unsigned 64-bit value as a fixed-width hexadecimal number prefixed with 0x.</summary>
    /// <returns><see langword="true"/> when every hexadecimal character was written.</returns>
    public static Boolean WriteHex(UInt64 value)
    {
        if(!BeginAtomicPresentation())return false;
        if (!WriteRaw((Byte)'0') || !WriteRaw((Byte)'x')){EndAtomicPresentation();return false;}
        Int32 shift = 60;
        while (shift >= 0)
        {
            UInt32 nibble = (UInt32)((value >> shift) & 0xFUL);
            Byte character = nibble < 10U ? (Byte)('0' + nibble) : (Byte)('A' + (nibble - 10U));
            if (!WriteRaw(character)){EndAtomicPresentation();return false;}
            shift -= 4;
        }
        Boolean ok=FlushConfigured();return EndAtomicPresentation()&&ok;
    }


    /// <summary>Rebinds the text console to a newly selected 32-bit RGB/BGR framebuffer and redraws retained output.</summary>
}
