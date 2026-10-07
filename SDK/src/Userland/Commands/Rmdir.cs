using System;
using System.IO;
using Inu.Userland.Runtime;
namespace Inu.Userland.Commands;
public static class Rmdir
{
    public static int Main(){String path=CommandLine.GetRawArguments();if(String.IsNullOrWhiteSpace(path)){Console.WriteLine("Usage: rmdir <path>");return 1;}try{Directory.Delete(path);return 0;}catch(Exception){Console.WriteLine("Could not remove directory.");return 1;}}
}
