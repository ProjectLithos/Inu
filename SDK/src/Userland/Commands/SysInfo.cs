using System;
using Inu.Userland.Runtime;

namespace Inu.Userland.Commands;

/// <summary>Reports live CPU/scheduler information and detected devices separately from driver state.</summary>
public static class SysInfo
{
    public static int Main()
    {
        Console.WriteLine("System information");
        Console.Write("Online processors: ");Console.WriteLine(SystemInformation.GetOnlineProcessorCount());
        Console.Write("Scheduler quantum (ns): ");Console.WriteLine(SystemInformation.GetSchedulerQuantumNanoseconds());
        Console.WriteLine("Detected devices (kernel registry):");
        UInt32 index=0U;DeviceInfo device;
        while(SystemInformation.TryGetDevice(index,out device))
        {
            Console.Write("  ");Console.Write(DeviceName(device));Console.Write(" [");WriteHex(device.Vendor);Console.Write(":");WriteHex(device.Device);Console.Write("] Present; driver: ");Console.Write(DriverState(device));Console.WriteLine();index++;
        }
        if(index==0U)Console.WriteLine("No registered devices.");
        return 0;
    }
    private static void WriteHex(UInt64 value){const String digits="0123456789ABCDEF";Char[] chars=new Char[16];Int32 count=0;do{chars[count++]=digits[(Int32)(value&15UL)];value>>=4;}while(value!=0UL);for(Int32 i=count-1;i>=0;i--)Console.Write(chars[i]);}
    private static String DriverState(DeviceInfo device){if(device.State==15UL)return "Failed";if(device.Driver==0UL)return "Not bound";if(device.State==8UL)return "Running";if(device.State==10UL||device.State==13UL)return "Stopped/suspended";return "Bound (not running)";}
    private static String DeviceName(DeviceInfo device)
    {
        UInt64 bus=device.Bus,vendor=device.Vendor,id=device.Device,classCode=device.ClassCode;
        if(bus==1UL&&vendor==0x1D1DUL&&id==0x8042UL)return "PS/2 controller";
        if(vendor==0x1AF4UL){if(id==0x1001UL||id==0x1042UL)return "VirtIO block";if(id==0x1050UL)return "VirtIO GPU";if(id==0x1000UL||id==0x1041UL)return "VirtIO network";return "VirtIO device";}
        if((classCode>>8)==0x0108UL)return "NVMe controller";if((classCode>>8)==0x0106UL)return "SATA/AHCI controller";if((classCode>>16)==2UL)return "Network controller";if((classCode>>16)==3UL)return "Display controller";if((classCode>>8)==0x0C03UL)return "USB controller";if(bus==3UL)return "USB device";if(bus==4UL)return "ACPI device";return "Device";
    }
}
