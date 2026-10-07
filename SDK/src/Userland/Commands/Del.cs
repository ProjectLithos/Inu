using System;
using System.IO;
using Inu.Userland.Runtime;
namespace Inu.Userland.Commands;
public static class Del
{
    public static int Main(){String path=CommandLine.GetRawArguments();if(String.IsNullOrWhiteSpace(path)){Console.WriteLine("Usage: del <file>");return 1;}try{File.Delete(path);return 0;}catch(Exception){Console.WriteLine("Could not delete file.");return 1;}}
}
