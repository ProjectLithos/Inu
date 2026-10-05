using Inu.Userland.Runtime;

namespace System;

/// <summary><inu.api>Freestanding .NET-compatible text console for ordinary ring-3 Inu applications.</inu.api> The implementation crosses the Inu user/kernel boundary through Get/Set/Event and does not depend on the desktop .NET runtime.</summary>
public static class Console
{
    /// <summary><inu.api>Writes text without appending a line terminator.</inu.api></summary>
    public static void Write(String value) => UserlandConsole.Write(value);

    /// <summary><inu.api>Writes one character without appending a line terminator.</inu.api></summary>
    public static void Write(Char value) => UserlandConsole.WriteChar(value);

    /// <summary><inu.api>Writes a Boolean using the normal .NET True/False spelling.</inu.api></summary>
    public static void Write(Boolean value) => UserlandConsole.Write(value ? "True" : "False");

    /// <summary><inu.api>Writes a signed 32-bit integer in decimal form.</inu.api></summary>
    public static void Write(Int32 value) => UserlandConsole.WriteSigned(value);

    /// <summary><inu.api>Writes an unsigned 32-bit integer in decimal form.</inu.api></summary>
    public static void Write(UInt32 value) => UserlandConsole.WriteUnsigned(value);

    /// <summary><inu.api>Writes a signed 64-bit integer in decimal form.</inu.api></summary>
    public static void Write(Int64 value) => UserlandConsole.WriteSigned(value);

    /// <summary><inu.api>Writes an unsigned 64-bit integer in decimal form.</inu.api></summary>
    public static void Write(UInt64 value) => UserlandConsole.WriteUnsigned(value);

    /// <summary><inu.api>Writes only the current console line terminator.</inu.api></summary>
    public static void WriteLine() => UserlandConsole.Write("\n");

    /// <summary><inu.api>Writes text followed by the current console line terminator.</inu.api></summary>
    public static void WriteLine(String value) { UserlandConsole.Write(value); UserlandConsole.Write("\n"); }

    /// <summary><inu.api>Writes one character followed by the current console line terminator.</inu.api></summary>
    public static void WriteLine(Char value) { UserlandConsole.WriteChar(value); UserlandConsole.Write("\n"); }

    /// <summary><inu.api>Writes a Boolean followed by the current console line terminator.</inu.api></summary>
    public static void WriteLine(Boolean value) { Write(value); WriteLine(); }

    /// <summary><inu.api>Writes a signed 32-bit integer followed by the current console line terminator.</inu.api></summary>
    public static void WriteLine(Int32 value) { Write(value); WriteLine(); }

    /// <summary><inu.api>Writes an unsigned 32-bit integer followed by the current console line terminator.</inu.api></summary>
    public static void WriteLine(UInt32 value) { Write(value); WriteLine(); }

    /// <summary><inu.api>Writes a signed 64-bit integer followed by the current console line terminator.</inu.api></summary>
    public static void WriteLine(Int64 value) { Write(value); WriteLine(); }

    /// <summary><inu.api>Writes an unsigned 64-bit integer followed by the current console line terminator.</inu.api></summary>
    public static void WriteLine(UInt64 value) { Write(value); WriteLine(); }

    /// <summary><inu.api>Waits for and returns the next decoded console character as an integer, or -1 if the input service fails.</inu.api></summary>
    public static Int32 Read() => UserlandConsole.ReadChar();

    /// <summary><inu.api>Reads one editable line from the console and returns it as a managed string.</inu.api></summary>
    public static String ReadLine() => UserlandConsole.ReadLine();

    /// <summary><inu.api>Clears all visible text from the current text console.</inu.api></summary>
    public static void Clear() => UserlandConsole.Clear();
}
