using System;
using Inu.Userland.Runtime;

namespace Inu.Userland.Commands;

/// <summary>Lets the end user inspect and change text-console colours and caret presentation.</summary>
public static class ConsoleSettings
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Show();
            return;
        }

        byte first;
        byte second;
        byte third;
        byte fourth;
        byte fifth;
        byte sixth;

        if (Equal(args[0], "fg") &&
            args.Length == 4 &&
            TryByte(args[1], out first) &&
            TryByte(args[2], out second) &&
            TryByte(args[3], out third))
        {
            if (!ConsolePresentation.SetForegroundRgb(first, second, third)) throw new InvalidOperationException("Console presentation rejected the requested change.");
            return;
        }

        if (Equal(args[0], "bg") &&
            args.Length == 4 &&
            TryByte(args[1], out first) &&
            TryByte(args[2], out second) &&
            TryByte(args[3], out third))
        {
            if (!ConsolePresentation.SetBackgroundRgb(first, second, third)) throw new InvalidOperationException("Console presentation rejected the requested change.");
            return;
        }

        if (Equal(args[0], "colors") &&
            args.Length == 7 &&
            TryByte(args[1], out first) &&
            TryByte(args[2], out second) &&
            TryByte(args[3], out third) &&
            TryByte(args[4], out fourth) &&
            TryByte(args[5], out fifth) &&
            TryByte(args[6], out sixth))
        {
            bool foregroundChanged = ConsolePresentation.SetForegroundRgb(first, second, third);
            bool backgroundChanged = ConsolePresentation.SetBackgroundRgb(fourth, fifth, sixth);
            if (!foregroundChanged || !backgroundChanged) throw new InvalidOperationException("Console presentation rejected the requested colour change.");
            return;
        }

        if (Equal(args[0], "caret") && args.Length == 2)
        {
            if (Equal(args[1], "blink") || Equal(args[1], "blinking"))
            {
                if (!ConsolePresentation.SetCaretMode(ConsoleCaretMode.Blinking)) throw new InvalidOperationException("Console presentation rejected the requested change.");
            return;
            }

            if (Equal(args[1], "visible") || Equal(args[1], "on"))
            {
                if (!ConsolePresentation.SetCaretMode(ConsoleCaretMode.Visible)) throw new InvalidOperationException("Console presentation rejected the requested change.");
            return;
            }

            if (Equal(args[1], "off"))
            {
                if (!ConsolePresentation.SetCaretMode(ConsoleCaretMode.Off)) throw new InvalidOperationException("Console presentation rejected the requested change.");
            return;
            }
        }

        uint percent;
        if (Equal(args[0], "caret") &&
            args.Length == 3 &&
            Equal(args[1], "size") &&
            TryUInt(args[2], out percent) &&
            percent >= 1U &&
            percent <= 100U)
        {
            if (!ConsolePresentation.SetCaretHeightPercent(percent)) throw new InvalidOperationException("Console presentation rejected the requested change.");
            return;
        }

        Usage();
        throw new ArgumentException("Invalid console command arguments.", nameof(args));
    }

    private static void Show()
    {
        uint foreground = ConsolePresentation.GetForegroundRgb();
        uint background = ConsolePresentation.GetBackgroundRgb();

        Console.Write("Foreground RGB: ");
        WriteRgb(foreground);
        Console.WriteLine();
        Console.Write("Background RGB: ");
        WriteRgb(background);
        Console.WriteLine();
        Console.Write("Caret mode: ");
        Console.WriteLine(ModeName(ConsolePresentation.GetCaretMode()));
        Console.Write("Caret height: ");
        Console.Write(ConsolePresentation.GetCaretHeightPercent());
        Console.WriteLine("%");
    }

    private static void Usage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  console fg <red> <green> <blue>");
        Console.WriteLine("  console bg <red> <green> <blue>");
        Console.WriteLine("  console colors <fr> <fg> <fb> <br> <bg> <bb>");
        Console.WriteLine("  console caret <blink|visible|off>");
        Console.WriteLine("  console caret size <1-100>");
    }

    private static string ModeName(ConsoleCaretMode mode)
    {
        if (mode == ConsoleCaretMode.Blinking)
        {
            return "Blinking";
        }

        if (mode == ConsoleCaretMode.Visible)
        {
            return "Visible";
        }

        return "Off";
    }

    private static void WriteRgb(uint rgb)
    {
        Console.Write((rgb >> 16) & 255U);
        Console.Write(" ");
        Console.Write((rgb >> 8) & 255U);
        Console.Write(" ");
        Console.Write(rgb & 255U);
    }

    private static bool Equal(string left, string right)
    {
        return String.Equals(left, right) || AsciiLowerEquals(left, right);
    }

    private static bool AsciiLowerEquals(string left, string right)
    {
        if (left == null || right == null || left.Length != right.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            char leftCharacter = left[index];
            char rightCharacter = right[index];

            if (leftCharacter >= 'A' && leftCharacter <= 'Z')
            {
                leftCharacter = (char)(leftCharacter + 32);
            }

            if (rightCharacter >= 'A' && rightCharacter <= 'Z')
            {
                rightCharacter = (char)(rightCharacter + 32);
            }

            if (leftCharacter != rightCharacter)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryByte(string text, out byte value)
    {
        value = 0;

        uint parsed;
        if (!TryUInt(text, out parsed) || parsed > 255U)
        {
            return false;
        }

        value = (byte)parsed;
        return true;
    }

    private static bool TryUInt(string text, out uint value)
    {
        value = 0;

        if (String.IsNullOrEmpty(text))
        {
            return false;
        }

        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character < '0' || character > '9')
            {
                return false;
            }

            uint digit = (uint)(character - '0');
            if (value > (UInt32.MaxValue - digit) / 10U)
            {
                return false;
            }

            value = (value * 10U) + digit;
        }

        return true;
    }
}
