using System;
using System.IO;

namespace Inu.Userland.Commands;

public static class Mkdir
{
    public static void Main(string[] args)
    {
        if (args is null) throw new ArgumentNullException(nameof(args));
        if (args.Length != 1) throw new ArgumentException("Usage: mkdir <path>", nameof(args));
        Directory.CreateDirectory(args[0]);
    }
}
