using System;
namespace Inu.Bus.Usb;
public static unsafe class UsbDescriptorParser
{
 public static Boolean TryParseDevice(Byte* p,UInt32 n,out UsbDeviceDescriptor d){d=default;if(p==null||n<18||p[0]<18||p[1]!=(Byte)UsbDescriptorType.Device)return false;d=new UsbDeviceDescriptor(R16(p+2),p[4],p[5],p[6],p[7],R16(p+8),R16(p+10),R16(p+12),p[14],p[15],p[16],p[17]);return true;}
 public static Boolean TryParseInterface(Byte* p,UInt32 n,out UsbInterfaceDescriptor d){d=default;if(p==null||n<9||p[0]<9||p[1]!=(Byte)UsbDescriptorType.Interface)return false;d=new UsbInterfaceDescriptor(p[2],p[3],p[4],p[5],p[6],p[7],p[8]);return true;}
 public static Boolean TryParseEndpoint(Byte* p,UInt32 n,out UsbEndpointDescriptor d){d=default;if(p==null||n<7||p[0]<7||p[1]!=(Byte)UsbDescriptorType.Endpoint)return false;d=new UsbEndpointDescriptor(p[2],p[3],R16(p+4),p[6]);return true;}
 public static UInt16 Read16(Byte* p)=>R16(p); private static UInt16 R16(Byte* p)=>(UInt16)(p[0]|((UInt16)p[1]<<8));
}
