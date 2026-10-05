using System;
namespace Inu.Kernel.Networking;

public static unsafe class KernelNetworkMath
{
    public static Boolean IsValidOptions(KernelNetworkOptions o)=>o.InitialInterfaces>0&&o.InitialRoutes>0&&o.InitialNeighbors>0&&o.InitialSockets>0&&o.InitialPackets>0&&o.InitialInterfaces<=o.MaximumInterfaces&&o.InitialRoutes<=o.MaximumRoutes&&o.InitialNeighbors<=o.MaximumNeighbors&&o.InitialSockets<=o.MaximumSockets&&o.InitialPackets<=o.MaximumPackets;
    public static UInt32 NextCapacity(UInt32 current,UInt32 maximum){if(current>=maximum)return current;UInt64 next=(UInt64)current*2UL;if(next>maximum)next=maximum;if(next>UInt32.MaxValue)next=UInt32.MaxValue;return (UInt32)next;}
    public static KernelIpv6Address ReadIpv6Address(Byte* p){if(p==null)return default;UInt64 high=((UInt64)ReadUInt32Network(p)<<32)|ReadUInt32Network(p+4);UInt64 low=((UInt64)ReadUInt32Network(p+8)<<32)|ReadUInt32Network(p+12);return new KernelIpv6Address(high,low);}
    public static UInt16 ReadUInt16Network(Byte* p)=> (UInt16)(((UInt16)p[0]<<8)|p[1]);
    public static UInt32 ReadUInt32Network(Byte* p)=>((UInt32)p[0]<<24)|((UInt32)p[1]<<16)|((UInt32)p[2]<<8)|p[3];
    public static Boolean WriteUInt16Network(Byte* p,UInt32 length,UInt16 value){if(p==null||length<2)return false;p[0]=(Byte)(value>>8);p[1]=(Byte)value;return true;}
    public static Boolean WriteUInt32Network(Byte* p,UInt32 length,UInt32 value){if(p==null||length<4)return false;p[0]=(Byte)(value>>24);p[1]=(Byte)(value>>16);p[2]=(Byte)(value>>8);p[3]=(Byte)value;return true;}
    public static UInt16 InternetChecksum(Byte* data,UInt32 length){if(data==null)return 0;UInt32 sum=0;UInt32 i=0;while(i+1<length){sum+=ReadUInt16Network(data+i);while((sum>>16)!=0)sum=(sum&0xFFFFU)+(sum>>16);i+=2;}if(i<length){sum+=(UInt32)data[i]<<8;while((sum>>16)!=0)sum=(sum&0xFFFFU)+(sum>>16);}return (UInt16)~sum;}
    public static Boolean SameSubnet(KernelIpv4Address a,KernelIpv4Address b,KernelIpv4Address mask)=>(a.Value&mask.Value)==(b.Value&mask.Value);
    public static Boolean RouteMatches(KernelNetworkRoute route,KernelIpv4Address destination)=>(destination.Value&route.Mask.Value)==(route.Network.Value&route.Mask.Value);
    public static UInt32 PrefixLength(KernelIpv4Address mask){UInt32 v=mask.Value,count=0;for(Int32 i=31;i>=0;i--){if((v&(1U<<i))==0)break;count++;}return count;}
}
