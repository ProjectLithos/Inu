using System;
using System.IO;
using Inu.Userland.Runtime;
namespace Inu.Userland.Commands;
public static class Cd
{
    public static int Main(){String path=CommandLine.GetRawArguments();if(String.IsNullOrWhiteSpace(path)){Console.WriteLine(Directory.GetCurrentDirectory());return 0;}try{Directory.SetCurrentDirectory(path);return 0;}catch(Exception){Console.WriteLine("Directory not found.");return 1;}}
}
