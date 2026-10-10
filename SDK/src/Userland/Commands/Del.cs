using System;
using System.IO;
namespace Inu.Userland.Commands;
public static class Del
{
    public static int Main(string[] args)
    {
        if(args.Length!=1){Console.WriteLine("Usage: del <file>");return 1;}
        try{File.Delete(args[0]);return 0;}catch(Exception){Console.WriteLine("Could not delete file.");return 1;}
    }
}
