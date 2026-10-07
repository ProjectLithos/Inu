using System;
using System.Text;

namespace Inu.Userland.Runtime;

/// <summary><inu.api>High-level filesystem path policy for ordinary Inu applications. Native Get/Set/Event transport remains an SDK implementation detail.</inu.api></summary>
public static unsafe class FileSystemPaths
{
    private const UInt32 MaximumPathBytes=1536U;
    private const UInt32 MaximumCommandPaths=8U;

    /// <summary><inu.api>Gets the OS-selected path separator.</inu.api></summary>
    public static Char GetPathSeparator()
    {
        Int64 value=UserlandSystem.Call(UserlandOperation.Get,"filesystem.path-separator",null,0UL,null,0UL);
        return value>0L&&value<=0x7FL?(Char)value:'/';
    }

    /// <summary><inu.api>Changes the OS-selected external path separator while preserving the rest of the active path policy.</inu.api></summary>
    public static Boolean SetPathSeparator(Char separator)
    {
        if(separator==0||separator>0x7F)return false;
        return UserlandSystem.Call(UserlandOperation.Set,"filesystem.path-separator",null,0UL,null,0UL,(UInt64)separator)>=0L;
    }

    /// <summary><inu.api>Gets the first command search directory, or an empty string when none is configured.</inu.api></summary>
    public static String GetCommandsPath()
    {
        String[] paths=GetCommandsPaths();return paths.Length==0?String.Empty:paths[0];
    }

    /// <summary><inu.api>Gets every command search directory in search order.</inu.api></summary>
    public static String[] GetCommandsPaths()
    {
        Int64 rawCount=UserlandSystem.Call(UserlandOperation.Get,"filesystem.commands-path.count",null,0UL,null,0UL);
        if(rawCount<=0L)return Array.Empty<String>();
        UInt32 count=(UInt32)rawCount;if(count>MaximumCommandPaths)count=MaximumCommandPaths;
        String[] result=new String[(Int32)count];Byte* path=stackalloc Byte[(Int32)MaximumPathBytes];
        for(UInt32 i=0U;i<count;i++)
        {
            Int64 length=UserlandSystem.Call(UserlandOperation.Get,"filesystem.commands-path",null,0UL,path,MaximumPathBytes,i);
            if(length<=0L){result[(Int32)i]=String.Empty;continue;}
            StringBuilder value=new StringBuilder((Int32)length);
            for(Int32 j=0;j<(Int32)length;j++)value.Append((Char)path[j]);
            result[(Int32)i]=value.ToString();
        }
        return result;
    }

    /// <summary><inu.api>Replaces the command search list with one directory.</inu.api></summary>
    public static Boolean SetCommandsPath(String path)=>SetCommandsPaths(new[]{path});

    /// <summary><inu.api>Replaces the command search list. Directories are searched in the supplied order.</inu.api></summary>
    public static Boolean SetCommandsPaths(String[] paths)
    {
        if(paths==null||paths.Length==0||(UInt32)paths.Length>MaximumCommandPaths)return false;
        UInt32 bytes=0U;
        for(Int32 i=0;i<paths.Length;i++)
        {
            String path=paths[i];if(path==null||path.Length==0||(UInt32)path.Length>MaximumPathBytes)return false;
            bytes+=(UInt32)path.Length;if(i+1<paths.Length)bytes++;
        }
        Byte* payload=stackalloc Byte[(Int32)bytes];UInt32 offset=0U;
        for(Int32 i=0;i<paths.Length;i++)
        {
            String path=paths[i];
            for(Int32 j=0;j<path.Length;j++){Char c=path[j];if(c>0x7F)return false;payload[offset++]=(Byte)c;}
            if(i+1<paths.Length)payload[offset++]=(Byte)0;
        }
        return UserlandSystem.Call(UserlandOperation.Set,"filesystem.commands-path",payload,bytes,null,0UL)>=0L;
    }

    /// <summary><inu.api>Builds every executable candidate for a command using the configured command search directories. Each directory contributes an .EXE candidate followed by the extensionless candidate.</inu.api></summary>
    public static String[] BuildCommandsPath(String command)
    {
        if(command==null||command.Length==0)return Array.Empty<String>();
        Char separator=GetPathSeparator();
        if(command[0]==separator)return new[]{command};
        String[] roots=GetCommandsPaths();String[] result=new String[roots.Length*2];Int32 output=0;
        for(Int32 i=0;i<roots.Length;i++)
        {
            String root=roots[i];if(root==null||root.Length==0)continue;
            StringBuilder path=new StringBuilder(root.Length+command.Length+5);path.Append(root);
            if(root[root.Length-1]!=separator)path.Append(separator);path.Append(command);
            String plain=path.ToString();StringBuilder executable=new StringBuilder(plain.Length+4);executable.Append(plain);executable.Append(".EXE");result[output++]=executable.ToString();result[output++]=plain;
        }
        if(output==result.Length&&output!=0)return result;
        if(output==0)return new[]{command};
        String[] exact=new String[output];for(Int32 i=0;i<output;i++)exact[i]=result[i];return exact;
    }

    /// <summary><inu.api>Plural spelling alias for BuildCommandsPath.</inu.api></summary>
    public static String[] BuildCommandsPaths(String command)=>BuildCommandsPath(command);
}
