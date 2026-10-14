using System;
using System.IO;

namespace Inu.Userland.Commands;

public static class Cd
{
    public static void Main(string[] args)
    {
        if (args is null) throw new ArgumentNullException(nameof(args));
        if (args.Length == 0)
        {
            Console.WriteLine(Directory.GetCurrentDirectory());
            return;
        }
        if (args.Length != 1) throw new ArgumentException("Usage: cd <path>", nameof(args));
        string path = args[0];
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Directory not found: " + path);
        Directory.SetParentCurrentDirectory(path);
    }
}
