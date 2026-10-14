using System;
using System.IO;

namespace Inu.Userland.Commands;

public static class View
{
    public static void Main(string[] args)
    {
        if (args is null) throw new ArgumentNullException(nameof(args));
        if (args.Length != 1) throw new ArgumentException("Usage: view <file>", nameof(args));
        Console.Write(File.ReadAllText(args[0]));
    }
}
