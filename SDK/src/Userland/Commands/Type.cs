using System;
using System.IO;
namespace Inu.Userland.Commands;
public static class Type
{
    public static int Main(string[] args)
    {
        if(args.Length!=1){Console.WriteLine("Usage: type <file>");return 1;}
        try{Console.Write(File.ReadAllText(args[0]));return 0;}
        catch(Exception){Console.WriteLine("Could not read file: "+args[0]);return 1;}
    }
}
