using System;
using System.IO;

namespace Inu.Userland.Commands;

public static class Rmdir
{
    public static void Main(string[] args)
    {
        if (args is null) throw new ArgumentNullException(nameof(args));
        if (args.Length != 1) throw new ArgumentException("Usage: rmdir <path>", nameof(args));
        Directory.Delete(args[0]);
    }
}
