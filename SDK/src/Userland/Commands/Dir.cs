using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Inu.Userland.Runtime;

namespace Inu.Userland.Commands;

public static class Dir
{
    public static void Main(string[] args)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException("Usage: dir [path]", nameof(args));
        }

        string path = args.Length == 0 ? Directory.GetCurrentDirectory() : args[0];

            string[] entries = Directory.GetFileSystemEntries(path);
            List<string> directories = new();
            List<string> files = new();

            for (int index = 0; index < entries.Length; index++)
            {
                string entry = entries[index];
                if (Directory.Exists(entry))
                {
                    directories.Add(GetName(entry));
                }
                else
                {
                    files.Add(GetName(entry));
                }
            }

            Sort(directories);
            Sort(files);

            StringBuilder output = new();
            output.Append("Directory of ");
            output.Append(path);
            output.Append('\n');
            output.Append('\n');

            for (int index = 0; index < directories.Count; index++)
            {
                output.Append("<DIR>  ");
                output.Append(directories[index]);
                output.Append('\n');
            }

            for (int index = 0; index < files.Count; index++)
            {
                output.Append("       ");
                output.Append(files[index]);
                output.Append('\n');
            }

        Console.Write(output.ToString());
    }

    private static string GetName(string path)
    {
        if (String.IsNullOrEmpty(path))
        {
            return String.Empty;
        }

        char separator = FileSystemPaths.GetPathSeparator();
        int end = path.Length;

        while (end > 1 && path[end - 1] == separator)
        {
            end--;
        }

        int start = end - 1;
        while (start >= 0 && path[start] != separator)
        {
            start--;
        }

        start++;
        return path.Substring(start, end - start);
    }

    private static void Sort(List<string> values)
    {
        for (int index = 1; index < values.Count; index++)
        {
            string value = values[index];
            int insertAt = index - 1;

            while (insertAt >= 0 && Compare(values[insertAt], value) > 0)
            {
                values[insertAt + 1] = values[insertAt];
                insertAt--;
            }

            values[insertAt + 1] = value;
        }
    }

    private static int Compare(string left, string right)
    {
        int length = left.Length < right.Length ? left.Length : right.Length;

        for (int index = 0; index < length; index++)
        {
            char leftCharacter = left[index];
            char rightCharacter = right[index];

            if (leftCharacter >= 'a' && leftCharacter <= 'z')
            {
                leftCharacter = (char)(leftCharacter - ('a' - 'A'));
            }

            if (rightCharacter >= 'a' && rightCharacter <= 'z')
            {
                rightCharacter = (char)(rightCharacter - ('a' - 'A'));
            }

            if (leftCharacter < rightCharacter)
            {
                return -1;
            }

            if (leftCharacter > rightCharacter)
            {
                return 1;
            }
        }

        if (left.Length < right.Length)
        {
            return -1;
        }

        if (left.Length > right.Length)
        {
            return 1;
        }

        return 0;
    }
}
