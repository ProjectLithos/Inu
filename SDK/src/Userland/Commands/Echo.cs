using System;
using Inu.Userland.Runtime;
namespace Inu.Userland.Commands;
public static class Echo
{
    public static int Main(){Console.WriteLine(CommandLine.GetRawArguments());return 0;}
}
