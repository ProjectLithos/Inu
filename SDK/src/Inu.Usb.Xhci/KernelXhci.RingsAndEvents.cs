using System;
using Inu.Bus.Usb;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Memory;
using Inu.Kernel.Pci;
namespace Inu.Usb.Xhci;
/// <summary>PCI xHCI host-controller implementation with command, event and endpoint transfer rings.</summary>
public static unsafe partial class KernelXhci
{
 static Boolean Command(Controller* c,UInt64 parameter,UInt32 status,UInt32 controlLow,UInt32 control,out Byte slot){slot=0;UInt32* ring=(UInt32*)Direct(c->Command);UInt32 i=(UInt32)c->CommandEnqueue*4U;ring[i]=(UInt32)parameter;ring[i+1]=(UInt32)(parameter>>32);ring[i+2]=status;ring[i+3]=control|c->CommandCycle;c->CommandEnqueue++;if(c->CommandEnqueue==255){c->CommandEnqueue=0;c->CommandCycle=(Byte)(c->CommandCycle==0?1:0);}W32(c->Doorbells,0);return WaitCommand(c,out slot);}
 static Boolean WaitCommand(Controller* c,out Byte slot){slot=0;for(UInt32 spin=0;spin<SpinLimit;spin++){if(!NextEvent(c,out UInt32 d0,out UInt32 d1,out UInt32 d2,out UInt32 d3))continue;Byte type=(Byte)((d3>>10)&63U);if(type!=33)continue;Byte code=(Byte)(d2>>24);slot=(Byte)(d3>>24);return code==1;}return false;}
 static Boolean WaitTransfer(Controller* c,Byte slot,Byte dci,UInt32 requested,out UInt32 actual){actual=0;for(UInt32 spin=0;spin<SpinLimit;spin++){if(!NextEvent(c,out UInt32 d0,out UInt32 d1,out UInt32 d2,out UInt32 d3))continue;Byte type=(Byte)((d3>>10)&63U);if(type!=32)continue;if((Byte)(d3>>24)!=slot||(Byte)((d3>>16)&31U)!=dci)continue;Byte code=(Byte)(d2>>24);UInt32 remain=d2&0x00FFFFFFU;actual=remain>requested?0:requested-remain;return code==1||code==13;}return false;}
 static Boolean NextEvent(Controller* c,out UInt32 d0,out UInt32 d1,out UInt32 d2,out UInt32 d3){UInt32* ring=(UInt32*)Direct(c->Event);UInt32 i=(UInt32)c->EventIndex*4U;d0=ring[i];d1=ring[i+1];d2=ring[i+2];d3=ring[i+3];if((d3&1U)!=c->EventCycle)return false;c->EventIndex++;if(c->EventIndex==256){c->EventIndex=0;c->EventCycle=(Byte)(c->EventCycle==0?1:0);}W64(c->Runtime+0x20+0x18,c->Event.StartAddress+(UInt64)c->EventIndex*16UL);return true;}
 static Boolean Enqueue(Ring* r,UInt64 parameter,UInt32 statusHi,UInt32 length,UInt32 control){UInt32* ring=(UInt32*)Direct(r->Memory);UInt32 i=(UInt32)r->Enqueue*4U;ring[i]=(UInt32)parameter;ring[i+1]=(UInt32)(parameter>>32);ring[i+2]=length|statusHi;ring[i+3]=control|r->Cycle;r->Enqueue++;if(r->Enqueue==255){r->Enqueue=0;r->Cycle=(Byte)(r->Cycle==0?1:0);}return true;}
 static void SetupTransferRing(Ring* r){UInt32* ring=(UInt32*)Direct(r->Memory);UInt64 p=r->Memory.StartAddress;UInt32 i=255U*4U;ring[i]=(UInt32)p;ring[i+1]=(UInt32)(p>>32);ring[i+2]=0;ring[i+3]=(6U<<10)|(1U<<1)|1U;r->Enqueue=0;r->Cycle=1;}
 static void RingDoorbell(Controller* c,Byte slot,Byte dci)=>W32(c->Doorbells+(UInt64)slot*4UL,dci);
}
