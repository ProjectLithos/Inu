using System;
using Inu.Userland.Runtime;
namespace Inu.Userland.Commands;
public static class Drives
{
    public static void Main()
    {
        UInt32 count=StorageManagement.GetDriveCount();Console.WriteLine("Manageable data drives:");
        if(count==0U){Console.WriteLine("  No additional writable drives detected.");return;}
        for(UInt32 i=0U;i<count;i++)
        {
            if(!StorageManagement.TryGetDrive(i,out DriveInfo d))continue;Console.Write("  Drive");Console.Write(i);if(!String.IsNullOrEmpty(d.Name)){Console.Write(" [");Console.Write(d.Name);Console.Write("]");}
            Console.Write("  ");Console.Write(KindName(d.Kind));Console.Write("  ");WriteSize(d.BlockCount*(UInt64)d.LogicalBlockSize);Console.Write("  partitions=");Console.Write(d.PartitionCount);if(d.ReadOnly)Console.Write("  read-only");Console.WriteLine();
        }
    }
    private static String KindName(UInt32 kind)=>kind==5U?"USB":kind==6U?"NVMe":kind==7U?"AHCI":kind==2U?"Virtual":kind==3U?"RAM disk":kind==4U?"Optical":"Physical";
    private static void WriteSize(UInt64 bytes){UInt64 mib=bytes/(1024UL*1024UL);if(mib>=1024UL){Console.Write(mib/1024UL);Console.Write(" GiB");}else{Console.Write(mib);Console.Write(" MiB");}}
}
