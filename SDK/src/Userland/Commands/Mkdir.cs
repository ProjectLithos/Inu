using System;
using System.IO;
namespace Inu.Userland.Commands;
public static class Mkdir
{
    public static int Main(string[] args)
    {
        if(args.Length!=1){Console.WriteLine("Usage: mkdir <path>");return 1;}
        try{Directory.CreateDirectory(args[0]);return 0;}catch(Exception){Console.WriteLine("Could not create directory. No writable mounted filesystem accepted the path.");return 1;}
    }
}
