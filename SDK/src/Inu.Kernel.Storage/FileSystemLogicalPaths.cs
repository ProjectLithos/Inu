using System;
using System.Text;

namespace Inu.Kernel.Storage;

/// <summary><inu.api>Names stable logical filesystem locations without imposing a concrete OS directory layout.</inu.api></summary>
public enum FileSystemLogicalPath : UInt64
{
    ReadableUser = 1UL,
    WritableUser = 2UL,
    VisibleUser = 3UL,
    ReadableFonts = 4UL,
    WritableFonts = 5UL,
    VisibleFonts = 6UL,
    Commands = 7UL,
    Applications = 8UL,
    Libraries = 9UL,
    Temporary = 10UL
}

internal static unsafe class FileSystemLogicalPaths
{
    private static String _readableUser=String.Empty;
    private static String _writableUser=String.Empty;
    private static String _visibleUser=String.Empty;
    private static String _readableFonts=String.Empty;
    private static String _writableFonts=String.Empty;
    private static String _visibleFonts=String.Empty;
    // Compatibility default only. OS authors can replace it from Kernel.cs.
    private static String _commands="/System/Commands";
    private static String _applications=String.Empty;
    private static String _libraries=String.Empty;
    private static String _temporary=String.Empty;

    internal static Boolean TrySet(FileSystemLogicalPath kind,String externalPath)
    {
        if(!FileSystemPathPolicyRuntime.TryNormalizeUserPath(externalPath,out String canonical)||canonical.Length==0)return false;
        switch(kind)
        {
            case FileSystemLogicalPath.ReadableUser:_readableUser=canonical;return true;
            case FileSystemLogicalPath.WritableUser:_writableUser=canonical;return true;
            case FileSystemLogicalPath.VisibleUser:_visibleUser=canonical;return true;
            case FileSystemLogicalPath.ReadableFonts:_readableFonts=canonical;return true;
            case FileSystemLogicalPath.WritableFonts:_writableFonts=canonical;return true;
            case FileSystemLogicalPath.VisibleFonts:_visibleFonts=canonical;return true;
            case FileSystemLogicalPath.Commands:_commands=canonical;return true;
            case FileSystemLogicalPath.Applications:_applications=canonical;return true;
            case FileSystemLogicalPath.Libraries:_libraries=canonical;return true;
            case FileSystemLogicalPath.Temporary:_temporary=canonical;return true;
            default:return false;
        }
    }

    internal static Boolean Clear(FileSystemLogicalPath kind)
    {
        switch(kind)
        {
            case FileSystemLogicalPath.ReadableUser:_readableUser=String.Empty;return true;
            case FileSystemLogicalPath.WritableUser:_writableUser=String.Empty;return true;
            case FileSystemLogicalPath.VisibleUser:_visibleUser=String.Empty;return true;
            case FileSystemLogicalPath.ReadableFonts:_readableFonts=String.Empty;return true;
            case FileSystemLogicalPath.WritableFonts:_writableFonts=String.Empty;return true;
            case FileSystemLogicalPath.VisibleFonts:_visibleFonts=String.Empty;return true;
            case FileSystemLogicalPath.Commands:_commands=String.Empty;return true;
            case FileSystemLogicalPath.Applications:_applications=String.Empty;return true;
            case FileSystemLogicalPath.Libraries:_libraries=String.Empty;return true;
            case FileSystemLogicalPath.Temporary:_temporary=String.Empty;return true;
            default:return false;
        }
    }

    internal static String Canonical(FileSystemLogicalPath kind)
    {
        switch(kind)
        {
            case FileSystemLogicalPath.ReadableUser:return _readableUser;
            case FileSystemLogicalPath.WritableUser:return _writableUser;
            case FileSystemLogicalPath.VisibleUser:return _visibleUser;
            case FileSystemLogicalPath.ReadableFonts:return _readableFonts;
            case FileSystemLogicalPath.WritableFonts:return _writableFonts;
            case FileSystemLogicalPath.VisibleFonts:return _visibleFonts;
            case FileSystemLogicalPath.Commands:return _commands;
            case FileSystemLogicalPath.Applications:return _applications;
            case FileSystemLogicalPath.Libraries:return _libraries;
            case FileSystemLogicalPath.Temporary:return _temporary;
            default:return String.Empty;
        }
    }

    internal static String External(FileSystemLogicalPath kind)
    {
        String canonical=Canonical(kind);
        return canonical.Length==0?String.Empty:FileSystemPathPolicyRuntime.ExternalizeCanonicalPath(canonical);
    }

    internal static Boolean TryGetExternalAscii(FileSystemLogicalPath kind,Byte* output,UInt32 capacity,out UInt32 length)
    {
        length=0U;String canonical=Canonical(kind);if(canonical.Length==0||output==null)return false;
        if((UInt32)canonical.Length>capacity)return false;
        Char separator=FileSystemPathPolicyRuntime.Current.Separator;
        for(Int32 i=0;i<canonical.Length;i++)
        {
            Char c=canonical[i]=='/'?separator:canonical[i];
            if(c>0x7F)return false;
            output[i]=(Byte)c;
        }
        length=(UInt32)canonical.Length;return true;
    }
}
