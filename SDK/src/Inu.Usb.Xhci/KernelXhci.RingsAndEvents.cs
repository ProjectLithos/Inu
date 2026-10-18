using System;
using Inu.Bus.Usb;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Internal.X64;
using Inu.Kernel.Memory;
using Inu.Kernel.Pci;
namespace Inu.Usb.Xhci;
/// <summary>PCI xHCI host-controller implementation with command, event and endpoint transfer rings.</summary>
public static unsafe partial class KernelXhci
{
 static Boolean Command(Controller* c,UInt64 parameter,UInt32 status,UInt32 controlLow,UInt32 control,out Byte slot)
 {
  slot=0;if(!PrepareCommandCompletion(c))return false;UInt32* ring=(UInt32*)Direct(c->Command);UInt32 i=(UInt32)c->CommandEnqueue*4U;ring[i]=(UInt32)parameter;ring[i+1]=(UInt32)(parameter>>32);ring[i+2]=status;ring[i+3]=control|c->CommandCycle;c->CommandEnqueue++;if(c->CommandEnqueue==255){c->CommandEnqueue=0;c->CommandCycle=(Byte)(c->CommandCycle==0?1:0);}W32(c->Doorbells,0);return WaitCommand(c,out slot);
 }
 static Boolean PrepareCommandCompletion(Controller* c){c->CommandCompletionCode=0;c->CommandCompletionSlot=0;return c->CommandCompletionEvent.Reset();}
 static Boolean PrepareTransferCompletion(Controller* c){c->TransferCompletionCode=0;c->TransferCompletionSlot=0;c->TransferCompletionDci=0;c->TransferCompletionRemaining=0;return c->TransferCompletionEvent.Reset();}
 static Boolean WaitCommand(Controller* c,out Byte slot)
 {
  slot=0;
  if(c->InterruptDriven!=0)
  {
   if(!c->CommandCompletionEvent.Wait(InterruptWaitTimeoutNanoseconds))return false;
   slot=c->CommandCompletionSlot;return c->CommandCompletionCode==1;
  }
  for(UInt32 spin=0;spin<SpinLimit;spin++){if(!ServiceHardwareEvents(c))continue;if(!c->CommandCompletionEvent.TryWait())continue;slot=c->CommandCompletionSlot;return c->CommandCompletionCode==1;}return false;
 }
 static Boolean WaitTransfer(Controller* c,Byte slot,Byte dci,UInt32 requested,out UInt32 actual)
 {
  actual=0;
  if(c->InterruptDriven!=0)
  {
   const UInt64 watchdogSliceNanoseconds=10000000UL;UInt64 waited=0UL;UInt32 recoveryChecks=0U;
   while(waited<InterruptWaitTimeoutNanoseconds)
   {
    if(c->TransferCompletionEvent.Wait(watchdogSliceNanoseconds))
    {
     if(c->TransferCompletionSlot!=slot||c->TransferCompletionDci!=dci)return false;UInt32 remain=c->TransferCompletionRemaining;actual=remain>requested?0:requested-remain;return c->TransferCompletionCode==1||c->TransferCompletionCode==13;
    }
    waited+=watchdogSliceNanoseconds;recoveryChecks++;UInt32 status=R32(c->Operational+4);UInt32 iman=R32(c->Runtime+0x20);
    if((status&(1U<<3))==0U&&(iman&1U)==0U)continue;
    if(recoveryChecks<=32U){Native.WritePort8(0xE9,(Byte)'V');Native.WritePort8(0xE9,(Byte)recoveryChecks);Native.WritePort8(0xE9,(Byte)status);Native.WritePort8(0xE9,(Byte)iman);Native.WritePort8(0xE9,(Byte)c->EventIndex);Native.WritePort8(0xE9,c->EventCycle);}
    Boolean recovered=ServiceHardwareEvents(c);AcknowledgeEventInterrupt(c);
    if(recoveryChecks<=32U)Native.WritePort8(0xE9,recovered?(Byte)'v':(Byte)'-');
    if(c->TransferCompletionEvent.TryWait())
    {
     if(c->TransferCompletionSlot!=slot||c->TransferCompletionDci!=dci)return false;UInt32 remain=c->TransferCompletionRemaining;actual=remain>requested?0:requested-remain;return c->TransferCompletionCode==1||c->TransferCompletionCode==13;
    }
   }
   return false;
  }
  for(UInt32 spin=0;spin<SpinLimit;spin++){if(!ServiceHardwareEvents(c))continue;if(!c->TransferCompletionEvent.TryWait())continue;if(c->TransferCompletionSlot!=slot||c->TransferCompletionDci!=dci)continue;UInt32 remain=c->TransferCompletionRemaining;actual=remain>requested?0:requested-remain;return c->TransferCompletionCode==1||c->TransferCompletionCode==13;}return false;
 }
 static Boolean ServiceHardwareEvents(Controller* c)
 {
  Boolean serviced=false;
  while(TryReadHardwareEvent(c,out UInt32 d0,out UInt32 d1,out UInt32 d2,out UInt32 d3))
  {
   serviced=true;Byte type=(Byte)((d3>>10)&63U);Native.WritePort8(0xE9,(Byte)'E');Native.WritePort8(0xE9,type);Native.WritePort8(0xE9,(Byte)(d2>>24));Native.WritePort8(0xE9,(Byte)(d3>>24));Native.WritePort8(0xE9,(Byte)((d3>>16)&31U));Native.WritePort8(0xE9,(Byte)(d2&0xFFU));
   if(type==33){c->CommandCompletionCode=(Byte)(d2>>24);c->CommandCompletionSlot=(Byte)(d3>>24);if(!c->CommandCompletionEvent.Set())return false;}
   else if(type==32){c->TransferCompletionCode=(Byte)(d2>>24);c->TransferCompletionRemaining=d2&0x00FFFFFFU;c->TransferCompletionSlot=(Byte)(d3>>24);c->TransferCompletionDci=(Byte)((d3>>16)&31U);if(!c->TransferCompletionEvent.Set())return false;}
  }
  return serviced;
 }
 static Boolean TryReadHardwareEvent(Controller* c,out UInt32 d0,out UInt32 d1,out UInt32 d2,out UInt32 d3)
 {
  UInt32* ring=(UInt32*)Direct(c->Event);UInt32 i=(UInt32)c->EventIndex*4U;d0=ring[i];d1=ring[i+1];d2=ring[i+2];d3=ring[i+3];if((d3&1U)!=c->EventCycle)return false;c->EventIndex++;if(c->EventIndex==256){c->EventIndex=0;c->EventCycle=(Byte)(c->EventCycle==0?1:0);}W64(c->Runtime+0x20+0x18,(c->Event.StartAddress+(UInt64)c->EventIndex*16UL)|(1UL<<3));return true;
 }
 static Boolean Enqueue(Ring* r,UInt64 parameter,UInt32 statusHi,UInt32 length,UInt32 control){UInt32* ring=(UInt32*)Direct(r->Memory);UInt32 i=(UInt32)r->Enqueue*4U;ring[i]=(UInt32)parameter;ring[i+1]=(UInt32)(parameter>>32);ring[i+2]=length|statusHi;ring[i+3]=control|r->Cycle;r->Enqueue++;if(r->Enqueue==255){r->Enqueue=0;r->Cycle=(Byte)(r->Cycle==0?1:0);}return true;}
 static void SetupTransferRing(Ring* r){UInt32* ring=(UInt32*)Direct(r->Memory);UInt64 p=r->Memory.StartAddress;UInt32 i=255U*4U;ring[i]=(UInt32)p;ring[i+1]=(UInt32)(p>>32);ring[i+2]=0;ring[i+3]=(6U<<10)|(1U<<1)|1U;r->Enqueue=0;r->Cycle=1;}
 static void RingDoorbell(Controller* c,Byte slot,Byte dci)=>W32(c->Doorbells+(UInt64)slot*4UL,dci);
}
