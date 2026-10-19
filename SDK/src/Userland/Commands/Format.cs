using System;
using Inu.Userland.Runtime;
namespace Inu.Userland.Commands;
public static class Format
{
    public static void Main(String[] args)
    {
        if(args.Length!=4||!Eq(args[2],"fat32")||!Eq(args[3],"--yes"))throw new ArgumentException("Usage: format <drive-number> <partition-number> fat32 --yes",nameof(args));if(!TryUInt(args[0],out UInt32 drive)||!TryUInt(args[1],out UInt32 partition)||partition==UInt32.MaxValue)throw new ArgumentException("Drive and partition numbers must be non-negative numeric values.",nameof(args));
        if(!StorageManagement.TryGetDrive(drive,out DriveInfo info))throw new InvalidOperationException("That drive does not exist. Use DRIVES to see valid zero-based drive numbers.");if(partition>=info.PartitionCount)throw new InvalidOperationException(info.PartitionCount==0U?"That drive has no partitions. Create one first, for example: partition 0 1":"That partition index does not exist. Use DRIVES to see the valid zero-based partition range.");
        Int64 result=StorageManagement.FormatFat32(drive,partition);if(result==UserlandError.Busy)throw new InvalidOperationException("That partition is mounted. Reboot with it unmounted before formatting.");if(result<0L)throw new InvalidOperationException("The partition could not be formatted as FAT32. Use DRIVES and PARTITION to verify the target.");
        Console.Write("Formatted Drive");Console.Write(drive);Console.Write(" partition ");Console.Write(partition);Console.WriteLine(" as FAT32. Reboot or remount it to expose the new filesystem.");
    }
    private static Boolean Eq(String a,String b){if(a==null||b==null||a.Length!=b.Length)return false;for(Int32 i=0;i<a.Length;i++){Char x=a[i],y=b[i];if(x>='a'&&x<='z')x=(Char)(x-32);if(y>='a'&&y<='z')y=(Char)(y-32);if(x!=y)return false;}return true;}
    private static Boolean TryUInt(String text,out UInt32 value){value=0U;if(String.IsNullOrEmpty(text))return false;for(Int32 i=0;i<text.Length;i++){Char c=text[i];if(c<'0'||c>'9')return false;UInt32 digit=(UInt32)(c-'0');if(value>(UInt32.MaxValue-digit)/10U)return false;value=value*10U+digit;}return true;}
}
