using System;
using System.Text;
using Inu.Userland.Runtime;
namespace Inu.Userland.Commands;
public static class DriveName
{
    public static void Main(String[] args)
    {
        if(args.Length<2)throw new ArgumentException("Usage: drivename <drive-number> <name>",nameof(args));if(!TryUInt(args[0],out UInt32 drive))throw new ArgumentException("Drive number must be a non-negative integer.",nameof(args));
        StringBuilder name=new StringBuilder();for(Int32 i=1;i<args.Length;i++){if(i>1)name.Append(' ');name.Append(args[i]);}if(!StorageManagement.SetDriveName(drive,name.ToString()))throw new InvalidOperationException("The drive name could not be changed. Use DRIVES to see valid drive numbers; names may contain letters, digits, spaces, '-' and '_'.");
        Console.Write("Drive");Console.Write(drive);Console.Write(" is now named ");Console.WriteLine(name.ToString());
    }
    private static Boolean TryUInt(String text,out UInt32 value){value=0U;if(String.IsNullOrEmpty(text))return false;for(Int32 i=0;i<text.Length;i++){Char c=text[i];if(c<'0'||c>'9')return false;UInt32 digit=(UInt32)(c-'0');if(value>(UInt32.MaxValue-digit)/10U)return false;value=value*10U+digit;}return true;}
}
