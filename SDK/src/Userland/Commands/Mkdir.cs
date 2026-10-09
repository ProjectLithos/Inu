using System;
using System.IO;
using Inu.Userland.Runtime;
namespace Inu.Userland.Commands;
public static class Mkdir
{
    public static int Main(){String path=CommandLine.GetRawArguments();if(String.IsNullOrWhiteSpace(path)){Console.WriteLine("Usage: mkdir <path>");return 1;}try{Directory.CreateDirectory(path);return 0;}catch(Exception){Console.WriteLine("Could not create directory. No writable mounted filesystem accepted the path.");return 1;}}
}
