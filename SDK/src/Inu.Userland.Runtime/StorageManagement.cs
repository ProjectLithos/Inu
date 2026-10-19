using System;
using System.Text;

namespace Inu.Userland.Runtime;

public readonly struct DriveInfo
{
    internal DriveInfo(UInt32 ordinal,UInt32 kind,UInt32 blockSize,UInt32 flags,UInt64 blocks,UInt32 partitions,String name)
    { Ordinal=ordinal;Kind=kind;LogicalBlockSize=blockSize;ReadOnly=(flags&1U)!=0U;Removable=(flags&2U)!=0U;BlockCount=blocks;PartitionCount=partitions;Name=name??String.Empty; }
    public UInt32 Ordinal { get; }
    public UInt32 Kind { get; }
    public UInt32 LogicalBlockSize { get; }
    public Boolean ReadOnly { get; }
    public Boolean Removable { get; }
    public UInt64 BlockCount { get; }
    public UInt32 PartitionCount { get; }
    public String Name { get; }
}

public static unsafe class StorageManagement
{
    public static UInt32 GetDriveCount(){Int64 value=UserlandSystem.Call(UserlandOperation.Get,"storage.drive.count",null,0UL,null,0UL);return value<0L?0U:(UInt32)value;}
    public static Boolean TryGetDrive(UInt32 ordinal,out DriveInfo info)
    {
        info=default;Byte* value=stackalloc Byte[32];Int64 length=UserlandSystem.Call(UserlandOperation.Get,"storage.drive.info",null,0UL,value,32UL,ordinal);if(length!=32L)return false;
        UInt32 kind=Read32(value+4),blockSize=Read32(value+8),flags=Read32(value+12),partitions=Read32(value+24);UInt64 blocks=Read64(value+16);String name=GetDriveName(ordinal);
        info=new DriveInfo(ordinal,kind,blockSize,flags,blocks,partitions,name);return true;
    }
    public static String GetDriveName(UInt32 ordinal)
    {
        Byte* output=stackalloc Byte[32];Int64 length=UserlandSystem.Call(UserlandOperation.Get,"storage.drive.name",null,0UL,output,32UL,ordinal);if(length<=0L)return String.Empty;StringBuilder value=new StringBuilder((Int32)length);for(Int32 i=0;i<(Int32)length;i++)value.Append((Char)output[i]);return value.ToString();
    }
    public static Boolean SetDriveName(UInt32 ordinal,String name)
    {
        if(String.IsNullOrEmpty(name)||name.Length>31)return false;Byte* raw=stackalloc Byte[name.Length];for(Int32 i=0;i<name.Length;i++){Char c=name[i];if(c>0x7F)return false;raw[i]=(Byte)c;}return UserlandSystem.Call(UserlandOperation.Set,"storage.drive.name",raw,(UInt64)name.Length,null,0UL,ordinal)>=0L;
    }
    public static Int64 PartitionGpt(UInt32 ordinal,UInt32 partitionCount,Boolean replaceExisting)=>UserlandSystem.Call(UserlandOperation.Set,"storage.partition.gpt",null,0UL,null,0UL,ordinal,partitionCount,replaceExisting?1UL:0UL);
    public static Int64 FormatFat32(UInt32 ordinal,UInt32 partitionNumber)=>UserlandSystem.Call(UserlandOperation.Set,"storage.format.fat32",null,0UL,null,0UL,ordinal,partitionNumber);
    private static UInt32 Read32(Byte* p)=>(UInt32)(p[0]|((UInt32)p[1]<<8)|((UInt32)p[2]<<16)|((UInt32)p[3]<<24));
    private static UInt64 Read64(Byte* p)=>(UInt64)Read32(p)|((UInt64)Read32(p+4)<<32);
}
