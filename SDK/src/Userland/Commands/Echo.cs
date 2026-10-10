using System;
namespace Inu.Userland.Commands;
public static class Echo
{
    public static int Main(string[] args)
    {
        for(Int32 i=0;i<args.Length;i++){if(i!=0)Console.Write(" ");Console.Write(args[i]);}
        Console.WriteLine();
        return 0;
    }
}
