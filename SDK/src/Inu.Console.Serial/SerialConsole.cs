using Inu.Architecture.X64;
using Inu.Console.Contracts;

namespace Inu.Console.Serial;

public readonly record struct SerialConfiguration(ushort Port, uint BaudRate)
{
    public static SerialConfiguration Com1(uint baudRate = 115200) => new(0x3F8, baudRate);
}

public sealed class SerialConsole : IConsole
{
    private SerialConfiguration _configuration;
    private bool _initialized;

    public bool Configure(SerialConfiguration configuration)
    {
        if (configuration.Port == 0 || configuration.BaudRate == 0)
            throw new ArgumentOutOfRangeException(nameof(configuration));
        _configuration = configuration;
        return true;
    }

    public bool Initialize()
    {
        if (_configuration.Port == 0 && !Configure(SerialConfiguration.Com1())) return false;
        ushort divisor = checked((ushort)(115200 / _configuration.BaudRate));
        ushort port = _configuration.Port;
        if (!Port.Write8((ushort)(port + 1), 0x00)) return false;
        if (!Port.Write8((ushort)(port + 3), 0x80)) return false;
        if (!Port.Write8(port, (byte)(divisor & 0xFF))) return false;
        if (!Port.Write8((ushort)(port + 1), (byte)(divisor >> 8))) return false;
        if (!Port.Write8((ushort)(port + 3), 0x03)) return false;
        if (!Port.Write8((ushort)(port + 2), 0xC7)) return false;
        if (!Port.Write8((ushort)(port + 4), 0x0B)) return false;
        _initialized = true;
        return true;
    }

    public bool Write(ReadOnlySpan<char> text)
    {
        if (!_initialized) return false;
        foreach (char character in text)
        {
            if (character > 0x7F || !WriteByte((byte)character)) return false;
        }
        return true;
    }

    public bool WriteLine(ReadOnlySpan<char> text) => Write(text) && WriteLine();
    public bool WriteLine() => Write("\r\n");

    private bool WriteByte(byte value)
    {
        for (uint attempt = 0; attempt < 1_000_000; attempt++)
        {
            if (!Port.TryRead8((ushort)(_configuration.Port + 5), out byte status)) return false;
            if ((status & 0x20) != 0) return Port.Write8(_configuration.Port, value);
        }
        return false;
    }
}
