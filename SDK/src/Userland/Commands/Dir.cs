using System;
using System.IO;
using Inu.Userland.Runtime;
namespace Inu.Userland.Commands;
public static class Dir
{
    public static int Main()
    {
        String path=CommandLine.GetRawArguments();
        if(String.IsNullOrWhiteSpace(path))path=Directory.GetCurrentDirectory();
        try
        {
            String[] entries=Directory.GetFileSystemEntries(path);
            for(Int32 i=0;i<entries.Length;i++)Console.WriteLine(entries[i]);
            return 0;
        }
        catch(Exception)
        {
            Console.WriteLine("Directory not found or could not be read: "+path);
            return 1;
        }
    }
}
