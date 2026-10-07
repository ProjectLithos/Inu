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
    internal const UInt32 MaximumCommandPaths=8U;
    private static String _readableUser=String.Empty;
    private static String _writableUser=String.Empty;
    private static String _visibleUser=String.Empty;
    private static String _readableFonts=String.Empty;
    private static String _writableFonts=String.Empty;
    private static String _visibleFonts=String.Empty;
    private static readonly String[] _commands=new String[(Int32)MaximumCommandPaths];
    private static UInt32 _commandCount=1U;
    private static String _applications=String.Empty;
    private static String _libraries=String.Empty;
    private static String _temporary=String.Empty;

    static FileSystemLogicalPaths(){_commands[0]="/System/Commands";}

    internal static Boolean TrySet(FileSystemLogicalPath kind,String externalPath)
    {
        if(kind==FileSystemLogicalPath.Commands)return TrySetCommands(new[]{externalPath});
        if(!FileSystemPathPolicyRuntime.TryNormalizeUserPath(externalPath,out String canonical)||canonical.Length==0)return false;
        switch(kind)
        {
            case FileSystemLogicalPath.ReadableUser:_readableUser=canonical;return true;
            case FileSystemLogicalPath.WritableUser:_writableUser=canonical;return true;
            case FileSystemLogicalPath.VisibleUser:_visibleUser=canonical;return true;
            case FileSystemLogicalPath.ReadableFonts:_readableFonts=canonical;return true;
            case FileSystemLogicalPath.WritableFonts:_writableFonts=canonical;return true;
            case FileSystemLogicalPath.VisibleFonts:_visibleFonts=canonical;return true;
            case FileSystemLogicalPath.Applications:_applications=canonical;return true;
            case FileSystemLogicalPath.Libraries:_libraries=canonical;return true;
            case FileSystemLogicalPath.Temporary:_temporary=canonical;return true;
            default:return false;
        }
    }

    internal static Boolean TrySetCommands(String[] externalPaths)
    {
        if(externalPaths==null||externalPaths.Length==0||(UInt32)externalPaths.Length>MaximumCommandPaths)return false;
        String[] canonical=new String[externalPaths.Length];
        for(Int32 i=0;i<externalPaths.Length;i++)
        {
            String candidate=externalPaths[i];
            if(!FileSystemPathPolicyRuntime.TryNormalizeUserPath(candidate,out String normalized)||normalized.Length==0)return false;
            canonical[i]=normalized;
        }
        for(Int32 i=0;i<_commands.Length;i++)_commands[i]=String.Empty;
        for(Int32 i=0;i<canonical.Length;i++)_commands[i]=canonical[i];
        _commandCount=(UInt32)canonical.Length;
        return true;
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
            case FileSystemLogicalPath.Commands:for(Int32 i=0;i<_commands.Length;i++)_commands[i]=String.Empty;_commandCount=0U;return true;
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
            case FileSystemLogicalPath.Commands:return _commandCount==0U?String.Empty:_commands[0];
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

    internal static UInt32 CommandCount=>_commandCount;
    internal static String ExternalCommand(UInt32 index)=>index>=_commandCount?String.Empty:FileSystemPathPolicyRuntime.ExternalizeCanonicalPath(_commands[(Int32)index]);

    internal static Boolean TryGetExternalAscii(FileSystemLogicalPath kind,Byte* output,UInt32 capacity,out UInt32 length)
    {
        if(kind==FileSystemLogicalPath.Commands)return TryGetCommandExternalAscii(0U,output,capacity,out length);
        length=0U;String canonical=Canonical(kind);if(canonical.Length==0||output==null)return false;
        return Externalize(canonical,output,capacity,out length);
    }

    internal static Boolean TryGetCommandExternalAscii(UInt32 index,Byte* output,UInt32 capacity,out UInt32 length)
    {
        length=0U;if(index>=_commandCount||output==null)return false;
        String canonical=_commands[(Int32)index];if(canonical==null||canonical.Length==0)return false;
        return Externalize(canonical,output,capacity,out length);
    }

    private static Boolean Externalize(String canonical,Byte* output,UInt32 capacity,out UInt32 length)
    {
        length=0U;if((UInt32)canonical.Length>capacity)return false;Char separator=FileSystemPathPolicyRuntime.Current.Separator;
        for(Int32 i=0;i<canonical.Length;i++){Char c=canonical[i]=='/'?separator:canonical[i];if(c>0x7F)return false;output[i]=(Byte)c;}
        length=(UInt32)canonical.Length;return true;
    }
}
