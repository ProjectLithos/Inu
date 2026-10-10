using System;
using System.IO;
namespace Inu.Userland.Commands;
public static class Cd
{
    public static int Main(string[] args)
    {
        if(args.Length==0){Console.WriteLine(Directory.GetCurrentDirectory());return 0;}
        if(args.Length!=1){Console.WriteLine("Usage: cd <path>");return 1;}
        String path=args[0];
        try{if(!Directory.Exists(path))throw new DirectoryNotFoundException();Directory.SetParentCurrentDirectory(path);return 0;}
        catch(Exception){Console.WriteLine("Directory not found: "+path);return 1;}
    }
}
