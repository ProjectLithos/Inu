using System;
using System.IO;

namespace Inu.Userland.Commands;

public static class Pwd
{
    public static void Main()
    {
        Console.WriteLine(Directory.GetCurrentDirectory());
    }
}
