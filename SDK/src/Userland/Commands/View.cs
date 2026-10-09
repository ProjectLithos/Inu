using System;
using System.IO;
using Inu.Userland.Runtime;

namespace Inu.Userland.Commands;

/// <summary>Displays a text file from the process working directory or from an explicit path.</summary>
public static class View
{
    public static int Main()
    {
        String path = CommandLine.GetRawArguments();
        if (String.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("Usage: view <file>");
            return 1;
        }

        Int32 start = 0;
        Int32 end = path.Length;
        while (start < end && path[start] == ' ') start++;
        while (end > start && path[end - 1] == ' ') end--;
        path = path.Substring(start, end - start);
        if (path.Length >= 2 && path[0] == '"' && path[path.Length - 1] == '"')
            path = path.Substring(1, path.Length - 2);

        try
        {
            // File.ReadAllText deliberately receives the path exactly as supplied.
            // Relative names are resolved by the kernel against this process's current
            // working directory; absolute/explicit paths are resolved by filesystem policy.
            Console.Write(File.ReadAllText(path));
            return 0;
        }
        catch (Exception)
        {
            Console.WriteLine("Could not read text file: " + path);
            return 1;
        }
    }
}
