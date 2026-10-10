using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace Inu.Userland.Commands;
public static class Dir
{
    public static int Main(string[] args)
    {
        if(args.Length>1){Console.WriteLine("Usage: dir [path]");return 1;}
        String path=args.Length==0?Directory.GetCurrentDirectory():args[0];
        try
        {
            String[] entries=Directory.GetFileSystemEntries(path);
            List<String> directories=new List<String>();
            List<String> files=new List<String>();
            for(Int32 i=0;i<entries.Length;i++)
            {
                String entry=entries[i];
                if(Directory.Exists(entry))directories.Add(GetName(entry));
                else files.Add(GetName(entry));
            }
            Sort(directories);Sort(files);
            StringBuilder output=new StringBuilder();
            output.Append("Directory of ");output.Append(path);output.Append('\n');output.Append('\n');
            for(Int32 i=0;i<directories.Count;i++){output.Append("<DIR>  ");output.Append(directories[i]);output.Append('\n');}
            for(Int32 i=0;i<files.Count;i++){output.Append("       ");output.Append(files[i]);output.Append('\n');}
            Console.Write(output.ToString());
            return 0;
        }
        catch(Exception)
        {
            Console.WriteLine("Directory not found or could not be read: "+path);
            return 1;
        }
    }

    private static String GetName(String path)
    {
        if(String.IsNullOrEmpty(path))return String.Empty;
        Char separator=FileSystemPaths.GetPathSeparator();
        Int32 end=path.Length;
        while(end>1&&path[end-1]==separator)end--;
        Int32 start=end-1;
        while(start>=0&&path[start]!=separator)start--;
        start++;
        return path.Substring(start,end-start);
    }

    private static void Sort(List<String> values)
    {
        for(Int32 i=1;i<values.Count;i++)
        {
            String value=values[i];Int32 j=i-1;
            while(j>=0&&Compare(values[j],value)>0){values[j+1]=values[j];j--;}
            values[j+1]=value;
        }
    }

    private static Int32 Compare(String left,String right)
    {
        Int32 length=left.Length<right.Length?left.Length:right.Length;
        for(Int32 i=0;i<length;i++)
        {
            Char a=left[i],b=right[i];
            if(a>='a'&&a<='z')a=(Char)(a-('a'-'A'));
            if(b>='a'&&b<='z')b=(Char)(b-('a'-'A'));
            if(a<b)return -1;if(a>b)return 1;
        }
        if(left.Length<right.Length)return -1;if(left.Length>right.Length)return 1;return 0;
    }
}
