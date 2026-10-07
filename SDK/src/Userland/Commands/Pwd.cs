using System;
using System.IO;
namespace Inu.Userland.Commands;
public static class Pwd
{
    public static int Main(){Console.WriteLine(Directory.GetCurrentDirectory());return 0;}
}
