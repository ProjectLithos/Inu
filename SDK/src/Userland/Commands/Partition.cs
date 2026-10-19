using System;
using Inu.Userland.Runtime;
namespace Inu.Userland.Commands;
public static class Partition
{
    public static void Main(String[] args)
    {
        if(args.Length<2||args.Length>4)throw new ArgumentException("Usage: partition <drive-number> <count> [--replace --yes]",nameof(args));if(!TryUInt(args[0],out UInt32 drive)||!TryUInt(args[1],out UInt32 count)||count==0U||count>16U)throw new ArgumentException("Drive number and partition count (1-16) are required.",nameof(args));
        Boolean replace=false,yes=false;for(Int32 i=2;i<args.Length;i++){if(Eq(args[i],"--replace"))replace=true;else if(Eq(args[i],"--yes"))yes=true;else throw new ArgumentException("Unknown option: "+args[i],nameof(args));}
        if(replace&&!yes)throw new InvalidOperationException("Replacing a partition table destroys existing data. Repeat with --replace --yes to confirm.");
        Int64 result=StorageManagement.PartitionGpt(drive,count,replace);if(result==UserlandError.Busy)throw new InvalidOperationException("A partition on this drive is mounted; unmount/reboot before repartitioning.");if(result<0L)throw new InvalidOperationException("The GPT partition operation failed. Use DRIVES to verify the drive and ensure it is writable.");
        Console.Write("Drive");Console.Write(drive);Console.Write(" now has a GPT layout with ");Console.Write(count);Console.WriteLine(count==1U?" partition.":" partitions.");
    }
    private static Boolean Eq(String a,String b){if(a==null||b==null||a.Length!=b.Length)return false;for(Int32 i=0;i<a.Length;i++){Char x=a[i],y=b[i];if(x>='a'&&x<='z')x=(Char)(x-32);if(y>='a'&&y<='z')y=(Char)(y-32);if(x!=y)return false;}return true;}
    private static Boolean TryUInt(String text,out UInt32 value){value=0U;if(String.IsNullOrEmpty(text))return false;for(Int32 i=0;i<text.Length;i++){Char c=text[i];if(c<'0'||c>'9')return false;UInt32 digit=(UInt32)(c-'0');if(value>(UInt32.MaxValue-digit)/10U)return false;value=value*10U+digit;}return true;}
}
