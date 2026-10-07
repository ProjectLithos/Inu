using System;

namespace Inu.Userland.Runtime;

/// <summary><inu.api>Snapshot of one device reported by the kernel device inventory.</inu.api></summary>
public readonly struct DeviceInfo
{
    public DeviceInfo(UInt64 bus,UInt64 vendor,UInt64 device,UInt64 classCode,UInt64 state,UInt64 driver,UInt64 failure,UInt64 handle)
    { Bus=bus;Vendor=vendor;Device=device;ClassCode=classCode;State=state;Driver=driver;Failure=failure;Handle=handle; }
    public UInt64 Bus { get; }
    public UInt64 Vendor { get; }
    public UInt64 Device { get; }
    public UInt64 ClassCode { get; }
    public UInt64 State { get; }
    public UInt64 Driver { get; }
    public UInt64 Failure { get; }
    public UInt64 Handle { get; }
}

/// <summary><inu.api>High-level system information queries for ordinary ring-3 applications.</inu.api></summary>
public static unsafe class SystemInformation
{
    public static Int32 GetOnlineProcessorCount(){Int64 value=UserlandSystem.Call(UserlandOperation.Get,"cpu.online.count",null,0UL,null,0UL);return value<0L?0:(Int32)value;}
    public static Int64 GetSchedulerQuantumNanoseconds()=>UserlandSystem.Call(UserlandOperation.Get,"scheduler.quantum",null,0UL,null,0UL);
    public static Boolean TryGetDevice(UInt32 index,out DeviceInfo device)
    {
        device=default;UInt64* record=stackalloc UInt64[8];Int64 result=UserlandSystem.Call(UserlandOperation.Get,"system.device.inspect",null,0UL,(Byte*)record,64UL,index);
        if(result!=64L)return false;device=new DeviceInfo(record[0],record[1],record[2],record[3],record[4],record[5],record[6],record[7]);return true;
    }
}
