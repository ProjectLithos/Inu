using System;
using Inu.Userland.Runtime;

namespace Inu.Userland.Commands;

/// <summary>Reports live CPU/scheduler information and detected devices separately from driver state.</summary>
public static unsafe class SysInfo
{
    /// <summary>Queries the kernel through Get and writes the report through Event.</summary>
    public static int Main()
    {
        if(!UserlandConsole.WriteLine("System information"))return 1;
        if(!Metric("Online processors: ","cpu.online.count")||!Metric("Scheduler quantum (ns): ","scheduler.quantum"))return 1;
        if(!UserlandConsole.WriteLine("Detected devices (kernel registry):"))return 1;
        UInt64* record=stackalloc UInt64[8];
        UInt64 index=0UL;
        for(;;)
        {
            Int64 result=UserlandSystem.Call(UserlandOperation.Get,"system.device.inspect",null,0UL,(Byte*)record,64UL,index);
            if(result==UserlandError.NotFound){if(index==0UL&&!UserlandConsole.WriteLine("No registered devices."))return 1;return 0;}
            if(result==UserlandError.NotImplemented)return UserlandConsole.WriteLine("Device inventory service is unavailable in this OS.")?0:1;
            if(result!=64L)return UserlandConsole.WriteLine("Device inventory query failed.")?2:1;
            // Eight UInt64 values: bus, vendor, device, class, state, driver, failure, handle.
            if(!UserlandConsole.Write("  ")||!UserlandConsole.Write(DeviceName(record[0],record[1],record[2],record[3]))||
               !UserlandConsole.Write(" [")||!Number(record[1],16U)||!UserlandConsole.Write(":")||!Number(record[2],16U)||
               !UserlandConsole.Write("] Present; driver: ")||!UserlandConsole.Write(DriverState(record[4],record[5]))||
               !UserlandConsole.Write("; bus=")||!Number(record[0],10U)||!UserlandConsole.Write(" class=0x")||!Number(record[3],16U)||
               !UserlandConsole.Write(" state=")||!Number(record[4],10U)||!UserlandConsole.Write(" failure=")||!Number(record[6],10U)||!UserlandConsole.WriteLine(""))return 1;
            index++;
        }
    }
    private static Boolean Metric(String label,String message)
    {
        Int64 value=UserlandSystem.Call(UserlandOperation.Get,message,null,0UL,null,0UL);
        return UserlandConsole.Write(label)&&(value<0L?UserlandConsole.Write("Unavailable"):Number((UInt64)value,10U))&&UserlandConsole.WriteLine("");
    }
    private static Boolean Number(UInt64 value,UInt32 radix)
    {
        const String digits="0123456789ABCDEF";Byte* buffer=stackalloc Byte[32];UInt32 count=0U;
        do{buffer[count++]=(Byte)digits[(Int32)(value%radix)];value/=radix;}while(value!=0UL);
        for(UInt32 i=0U;i<count/2U;i++){Byte b=buffer[i];buffer[i]=buffer[count-1U-i];buffer[count-1U-i]=b;}
        return UserlandSystem.Call(UserlandOperation.Event,"console.output",buffer,count,null,0UL)>=0L;
    }
    private static String DriverState(UInt64 state,UInt64 driver)
    {
        if(state==15UL)return "Failed";
        if(driver==0UL)return "Not bound";
        if(state==8UL)return "Running";
        if(state==10UL||state==13UL)return "Stopped/suspended";
        return "Bound (not running)";
    }
    private static String DeviceName(UInt64 bus,UInt64 vendor,UInt64 device,UInt64 classCode)
    {
        if(bus==1UL&&vendor==0x1D1DUL&&device==0x8042UL)return "PS/2 controller";
        if(vendor==0x1AF4UL){if(device==0x1001UL||device==0x1042UL)return "VirtIO block";if(device==0x1050UL)return "VirtIO GPU";if(device==0x1000UL||device==0x1041UL)return "VirtIO network";return "VirtIO device";}
        if((classCode>>8)==0x0108UL)return "NVMe controller";
        if((classCode>>8)==0x0106UL)return "SATA/AHCI controller";
        if((classCode>>16)==2UL)return "Network controller";
        if((classCode>>16)==3UL)return "Display controller";
        if((classCode>>8)==0x0C03UL)return "USB controller";
        if(bus==3UL)return "USB device";
        if(bus==4UL)return "ACPI device";
        return "Device";
    }
}
