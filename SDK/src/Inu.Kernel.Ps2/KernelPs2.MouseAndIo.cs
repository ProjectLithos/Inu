using System;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Drivers;
using Inu.Kernel.SystemCalls;

namespace Inu.Kernel.Ps2;

/// <summary>Owns the legacy i8042 controller, PS/2 keyboard, mouse, and keyboard layout state.</summary>
public static unsafe partial class KernelPs2
{
    private static void DecodeMouse(Byte b){if(_mouseIndex==0){if((b&0x08)==0)return;_m0=b;_mouseIndex=1;return;}if(_mouseIndex==1){_m1=b;_mouseIndex=2;return;}_m2=b;_mouseIndex=0;Int32 dx=(SByte)_m1,dy=(SByte)_m2;_mouseState=new Ps2MouseState(_mouseState.X+dx,_mouseState.Y-dy,(_m0&1)!=0,(_m0&2)!=0,(_m0&4)!=0,_mouseState.Wheel);_mousePackets++;if(_mouseEventHandler!=null)_mouseEventHandler(_mouseState);}
    private static Boolean SendKeyboard(Byte command){if(!WriteData(command)||!ReadData(out Byte ack))return false;return ack==0xFA;}
    private static Boolean SendMouse(Byte command){if(!WriteCommand(0xD4)||!WriteData(command)||!ReadData(out Byte ack))return false;return ack==0xFA;}
    private static Boolean WriteCommand(Byte value){if(!WaitInputEmpty())return false;return Native.WritePort8(CommandPort,value);}
    private static Boolean WriteData(Byte value){if(!WaitInputEmpty())return false;return Native.WritePort8(DataPort,value);}
    private static Boolean ReadData(out Byte value){for(UInt32 i=0;i<100000U;i++){if(!Native.ReadPort8(StatusPort,out Byte s)){value=0;return false;}if((s&1)!=0)return Native.ReadPort8(DataPort,out value);}value=0;return false;}
    private static Boolean WaitInputEmpty(){for(UInt32 i=0;i<100000U;i++){if(!Native.ReadPort8(StatusPort,out Byte s))return false;if((s&2)==0)return true;}return false;}
    private static void Drain(){for(UInt32 i=0;i<32U;i++){if(!Native.ReadPort8(StatusPort,out Byte s)||(s&1)==0)return;Native.ReadPort8(DataPort,out _);}}
}
