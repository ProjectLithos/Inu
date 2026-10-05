using System;

namespace Inu.Kernel.Console;

public static partial class KernelConsole
{
    /// <summary>Writes a length-delimited byte buffer as one console presentation, without allocation.</summary>
    public static unsafe Boolean WriteAscii(Byte* buffer, UInt32 count)
    {
        if (!_initialized || _outputPaused || (buffer == null && count != 0U)) return false;
        if (!BeginAtomicPresentation()) return false;
        Boolean ok = true;
        for (UInt32 index = 0U; index < count; index++)
        {
            if (!WriteRaw(buffer[index])) { ok = false; break; }
        }
        if (ok) ok = _serialDiagnosticLine || FlushConfigured();
        Boolean released = EndAtomicPresentation();
        return released && ok;
    }
}
