using System;
using System.IO;
namespace Inu.Userland.Commands;
public static class Rmdir
{
    public static int Main(string[] args)
    {
        if(args.Length!=1){Console.WriteLine("Usage: rmdir <path>");return 1;}
        try{Directory.Delete(args[0]);return 0;}catch(Exception){Console.WriteLine("Could not remove directory.");return 1;}
    }
}
