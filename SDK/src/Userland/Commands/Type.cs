using System;
using System.IO;
using Inu.Userland.Runtime;
namespace Inu.Userland.Commands;
public static class Type
{
    public static int Main(){String path=CommandLine.GetRawArguments();if(String.IsNullOrWhiteSpace(path)){Console.WriteLine("Usage: type <file>");return 1;}try{Console.Write(File.ReadAllText(path));return 0;}catch(Exception){Console.WriteLine("Could not read file.");return 1;}}
}
