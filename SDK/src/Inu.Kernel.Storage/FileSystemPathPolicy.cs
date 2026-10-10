using System;
using System.Text;

namespace Inu.Kernel.Storage;

/// <summary><inu.api>OS-author path-syntax policy applied by the VFS/userland boundary.</inu.api></summary>
public readonly struct FileSystemPathPolicy
{
    public FileSystemPathPolicy(Char separator, Boolean caseSensitive, UInt32 maximumComponentLength, Boolean allowSpaces, Boolean allowNumbers, String invalidCharacters)
    {
        Separator=separator;
        CaseSensitive=caseSensitive;
        MaximumComponentLength=maximumComponentLength;
        AllowSpaces=allowSpaces;
        AllowNumbers=allowNumbers;
        InvalidCharacters=invalidCharacters??String.Empty;
    }

    public Char Separator { get; }
    public Boolean CaseSensitive { get; }
    public UInt32 MaximumComponentLength { get; }
    public Boolean AllowSpaces { get; }
    public Boolean AllowNumbers { get; }
    public String InvalidCharacters { get; }
}

public static unsafe partial class FileSystemPathPolicyRuntime
{
    private static Char _separator='/';
    private static Boolean _caseSensitive;
    private static UInt32 _maximumComponentLength=255U;
    private static Boolean _allowSpaces=true;
    private static Boolean _allowNumbers=true;
    private static String _invalidCharacters=String.Empty;

    internal static FileSystemPathPolicy Current=>new(_separator,_caseSensitive,_maximumComponentLength,_allowSpaces,_allowNumbers,_invalidCharacters);
    internal static Boolean CaseSensitive=>_caseSensitive;

    internal static Boolean TrySet(FileSystemPathPolicy policy)
    {
        Char separator=policy.Separator;
        if(separator==0||separator>0x7F||separator==' '||separator=='\0')return false;
        UInt32 maximum=policy.MaximumComponentLength;
        if(maximum==0U||maximum>255U)return false;
        String invalid=policy.InvalidCharacters??String.Empty;
        for(Int32 i=0;i<invalid.Length;i++)
        {
            Char c=invalid[i];
            if(c==separator||c==0||c>0x7F)return false;
        }
        if(!KernelVfs.CanAdoptCaseSensitivity(policy.CaseSensitive))return false;
        _separator=separator;
        _caseSensitive=policy.CaseSensitive;
        _maximumComponentLength=maximum;
        _allowSpaces=policy.AllowSpaces;
        _allowNumbers=policy.AllowNumbers;
        _invalidCharacters=invalid;
        return true;
    }

    internal static Boolean ValidateCanonicalPath(String path)
    {
        if(path==null||path.Length==0||path[0]!='/')return false;
        if(path.Length==1)return true;
        UInt32 componentLength=0U;
        for(Int32 i=1;i<path.Length;i++)
        {
            Char c=path[i];
            if(c=='/')
            {
                if(componentLength==0U)return false;
                componentLength=0U;
                continue;
            }
            if(!ValidComponentChar(c)||++componentLength>_maximumComponentLength)return false;
        }
        return componentLength!=0U;
    }

    internal static Boolean ValidateCanonicalAscii(Byte* path,UInt32 length)
    {
        if(path==null||length==0U||path[0]!='/')return false;
        if(length==1U)return true;
        UInt32 componentLength=0U;
        for(UInt32 i=1U;i<length;i++)
        {
            Byte b=path[i];
            if(b==0U||b>0x7FU)return false;
            if(b==(Byte)'/')
            {
                if(componentLength==0U)return false;
                componentLength=0U;
                continue;
            }
            if(!ValidComponentChar((Char)b)||++componentLength>_maximumComponentLength)return false;
        }
        return componentLength!=0U;
    }


    internal static Boolean TryNormalizeUserPath(String input,out String canonical)
    {
        canonical=String.Empty;if(input==null||input.Length==0)return false;
        Char separator=_separator;if(input[0]!=separator)return false;
        StringBuilder value=new StringBuilder(input.Length);
        UInt32 componentLength=0U;
        for(Int32 i=0;i<input.Length;i++)
        {
            Char c=input[i];
            if(c==separator)
            {
                if(i==0){value.Append('/');continue;}
                if(componentLength==0U||i+1==input.Length)return false;
                componentLength=0U;value.Append('/');continue;
            }
            if((c=='/'||c=='\\'||c==':')&&c!=separator)return false;
            if(!ValidComponentChar(c)||++componentLength>_maximumComponentLength)return false;
            value.Append(c);
        }
        if(input.Length==1){canonical="/";return true;}
        if(componentLength==0U)return false;
        canonical=value.ToString();return ValidateCanonicalPath(canonical);
    }

    internal static String ExternalizeCanonicalPath(String canonical)
    {
        if(canonical==null||canonical.Length==0||!ValidateCanonicalPath(canonical))return String.Empty;
        if(_separator=='/')return canonical;
        StringBuilder value=new StringBuilder(canonical.Length);
        for(Int32 i=0;i<canonical.Length;i++)value.Append(canonical[i]=='/'?_separator:canonical[i]);
        return value.ToString();
    }

    public static Boolean TryNormalizeUserAscii(Byte* input,UInt32 inputLength,Byte* output,UInt32 capacity,out UInt32 outputLength,out Boolean absolute)
    {
        outputLength=0U;absolute=false;
        if(input==null||inputLength==0U||output==null||capacity==0U||inputLength>capacity)return false;
        Byte separator=(Byte)_separator;
        absolute=input[0]==separator;
        UInt32 componentLength=0U;
        for(UInt32 i=0U;i<inputLength;i++)
        {
            Byte b=input[i];
            if(b==0U||b>0x7FU)return false;
            if(b==separator)
            {
                if(i==0U)
                {
                    if(!absolute)return false;
                    output[i]=(Byte)'/';
                    continue;
                }
                if(componentLength==0U||i+1U==inputLength)return false;
                componentLength=0U;
                output[i]=(Byte)'/';
                continue;
            }
            // The selected separator is the only external joiner. Other common path joiners
            // are rejected rather than being accepted as ordinary component characters.
            if((b==(Byte)'/'||b==(Byte)'\\'||b==(Byte)':')&&b!=separator)return false;
            if(!ValidComponentChar((Char)b)||++componentLength>_maximumComponentLength)return false;
            output[i]=b;
        }
        if(absolute&&inputLength==1U){output[0]=(Byte)'/';outputLength=1U;return true;}
        if(componentLength==0U)return false;
        outputLength=inputLength;return true;
    }

    public static Boolean TryExternalizeCanonicalAscii(Byte* canonical,UInt32 canonicalLength,Byte* output,UInt32 capacity,out UInt32 outputLength)
    {
        outputLength=0U;if(canonical==null||canonicalLength==0U||output==null||canonicalLength>capacity||!ValidateCanonicalAscii(canonical,canonicalLength))return false;
        Byte separator=(Byte)_separator;
        for(UInt32 i=0U;i<canonicalLength;i++)output[i]=canonical[i]==(Byte)'/'?separator:canonical[i];
        outputLength=canonicalLength;return true;
    }

    private static Boolean ValidComponentChar(Char c)
    {
        if(c==0||c=='/')return false;
        if(!_allowSpaces&&c==' ')return false;
        if(!_allowNumbers&&c>='0'&&c<='9')return false;
        String invalid=_invalidCharacters;
        for(Int32 i=0;i<invalid.Length;i++)if(c==invalid[i])return false;
        return true;
    }
}
