using System;
using Inu.Userland.Runtime;

namespace Inu.Userland.Commands;

/// <summary>Changes the system text-console foreground colour.</summary>
public static class FgColour
{
    public static void Main(string[] args)
    {
        if (!TryParseRgb(args, out byte red, out byte green, out byte blue))
        {
            throw new ArgumentException("Usage: fgcolour <red> <green> <blue>; each colour component must be between 0 and 255.", nameof(args));
        }

        if (!ConsolePresentation.SetForegroundRgb(red, green, blue)) throw new InvalidOperationException("Console presentation rejected the colour change.");
    }

    private static bool TryParseRgb(string[] args, out byte red, out byte green, out byte blue)
    {
        red = 0;
        green = 0;
        blue = 0;

        return args != null &&
               args.Length == 3 &&
               TryByte(args[0], out red) &&
               TryByte(args[1], out green) &&
               TryByte(args[2], out blue);
    }

    private static bool TryByte(string text, out byte value)
    {
        value = 0;
        if (String.IsNullOrEmpty(text))
        {
            return false;
        }

        uint parsed = 0;
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character < '0' || character > '9')
            {
                return false;
            }

            parsed = (parsed * 10U) + (uint)(character - '0');
            if (parsed > 255U)
            {
                return false;
            }
        }

        value = (byte)parsed;
        return true;
    }
}
