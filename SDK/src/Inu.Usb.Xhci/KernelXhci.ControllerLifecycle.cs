using System;
using Inu.Bus.Usb;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Memory;
using Inu.Kernel.Pci;
using Inu.Kernel.Internal.X64;
namespace Inu.Usb.Xhci;
/// <summary>PCI xHCI host-controller implementation with command, event and endpoint transfer rings.</summary>
public static unsafe partial class KernelXhci
{
 static Boolean Probe(KernelDriverDeviceContext* context)=>context!=null&&context->Identifier.ClassCode==0x0C0330U&&KernelPci.TryGetDevice(context->Device,out _);
 static Boolean Start(KernelDriverDeviceContext* context){if(context==null||!KernelPci.TryGetDevice(context->Device,out PciDeviceInfo pci))return false;Int32 slot=FreeController();if(slot<0)return false;Controller* c=_controllers+slot;Clear((Byte*)c,(UInt64)sizeof(Controller));c->Used=1;c->Segment=pci.Location.Segment;c->Bus=pci.Location.Bus;c->Device=pci.Location.Device;c->Function=pci.Location.Function;c->DeviceHandle=context->Device.Value;c->State=(Byte)XhciControllerState.Discovered;c->CommandCompletionEvent.Initialize(false,false);c->TransferCompletionEvent.Initialize(false,false);if(!StartController(c)){ReleaseControllerResources(c);Clear((Byte*)c,(UInt64)sizeof(Controller));return false;}_controllerCount++;_running++;return true;}
 static Boolean Stop(KernelDriverDeviceContext* context){if(context==null)return false;Controller* c=FindController(context->Device);if(c==null)return true;if(c->Host!=0U&&!KernelUsbBus.UnregisterHost(new UsbHostHandle(c->Host)))return false;DisableInterruptDelivery(c);if(c->InterruptHandle!=0UL&&!KernelDrivers.ReleaseInterrupt(new KernelDriverInterruptHandle(c->InterruptHandle)))return false;c->InterruptHandle=0UL;c->InterruptConfigured=0;c->InterruptPrepared=0;c->InterruptDriven=0;if(c->Operational!=0UL){W32(c->Operational,R32(c->Operational)&~1U);Wait32(c->Operational+4,1U,1U);}if(!ReleaseControllerResources(c))return false;Clear((Byte*)c,(UInt64)sizeof(Controller));if(_controllerCount>0U)_controllerCount--;if(_running>0U)_running--;return true;}
 static Boolean Remove(KernelDriverDeviceContext* context)=>context!=null&&Stop(context);
 static Boolean Interrupt(KernelDriverDeviceContext* context,UInt64 cookie)
 {
  if(context==null)return false;Controller* c=FindController(context->Device);if(c==null||c->Runtime==0UL||c->InterruptPrepared==0)return false;
  UInt64 irq=0UL;fixed(UInt64* p=&_diagnosticInterrupts){UInt64 previous=0UL;Native.AtomicFetchAdd64(p,1UL,&previous);irq=previous+1UL;}
  UInt64 ir=c->Runtime+0x20;UInt32 iman=R32(ir);UInt32 status=R32(c->Operational+4);if(irq<=64UL){Native.WritePort8(0xE9,(Byte)'J');Native.WritePort8(0xE9,(Byte)irq);Native.WritePort8(0xE9,(Byte)status);Native.WritePort8(0xE9,(Byte)iman);Native.WritePort8(0xE9,(Byte)c->EventIndex);Native.WritePort8(0xE9,c->EventCycle);}
  if((iman&1U)==0U&&(status&(1U<<3))==0U){if(irq<=64UL)Native.WritePort8(0xE9,(Byte)'0');return true;}
  Boolean serviced=ServiceHardwareEvents(c);
  // xHCI has two event-interrupt latches that software must acknowledge: USBSTS.EINT and
  // IMAN.IP. Bootstrap polling can leave EINT latched even though the event ring was drained,
  // which suppresses a later fresh MSI/MSI-X transition. Always clear both after servicing.
  AcknowledgeEventInterrupt(c);
  if(irq<=64UL){Native.WritePort8(0xE9,serviced?(Byte)'j':(Byte)'s');Native.WritePort8(0xE9,(Byte)R32(c->Operational+4));Native.WritePort8(0xE9,(Byte)R32(ir));}
  return true;
 }
 static Boolean ScanControllerPorts(Controller* c){if(c==null||c->State!=(Byte)XhciControllerState.Running)return false;for(Byte p=1;p<=c->Ports;p++){UInt32 ps=R32(Port(c,p));if((ps&1U)==0)continue;_connected++;UsbSpeed speed=SpeedFromId((Byte)((ps>>10)&15U));if(!KernelUsbBus.EnumeratePort(new UsbHostHandle(c->Host),p,speed,out _))return false;}return true;}
 static Boolean StartController(Controller* c)
 {
  PciLocation loc=new(c->Segment,c->Bus,c->Device,c->Function);
  if(!KernelPci.TryMapBar(loc,0,out PciBarInfo bar,out UInt64 mmio)||bar.Type==PciBarType.Io||bar.Length<0x2000UL)return false;
  if(!KernelPci.TryRead16(loc,4,out UInt16 command)||!KernelPci.TryWrite16(loc,4,(UInt16)(command|0x0006U)))return false;
  c->Mmio=mmio;Byte cap=R8(mmio);UInt32 hcs1=R32(mmio+4),hcs2=R32(mmio+8),hcc1=R32(mmio+0x10);c->Slots=(Byte)(hcs1&0xFFU);c->Ports=(Byte)((hcs1>>24)&0xFFU);c->ContextSize=(Byte)(((hcc1&(1U<<2))!=0)?64:32);c->Operational=mmio+cap;c->Doorbells=mmio+(R32(mmio+0x14)&~3U);c->Runtime=mmio+(R32(mmio+0x18)&~31U);
  if(c->Slots==0||c->Ports==0||(R32(c->Operational+8)&1U)==0)return false;
  W32(c->Operational,R32(c->Operational)&~1U);if(!Wait32(c->Operational+4,1U,1U))return false;
  W32(c->Operational,R32(c->Operational)|2U);if(!Wait32(c->Operational,2U,0U)||!Wait32(c->Operational+4,1U<<11,0U))return false;
  if(!AllocateControllerMemory(c,hcs2))return false;SetupCommandRing(c);SetupEventRing(c);W64(c->Operational+0x30,c->Dcbaa.StartAddress);W64(c->Operational+0x18,c->Command.StartAddress|1UL);W32(c->Operational+0x38,c->Slots);
  UInt64 ir=c->Runtime+0x20;W32(ir+8,1U);W64(ir+0x10,c->Erst.StartAddress);W64(ir+0x18,c->Event.StartAddress|(1UL<<3));W32(ir+4,0U);W32(ir,0U);
  KernelDriverInterruptRequest interruptRequest=new(new KernelDeviceHandle(c->DeviceHandle),0U,8,0U,false,false,0UL);
  if(KernelDrivers.TryRequestInterrupt(interruptRequest,out KernelDriverInterruptHandle interrupt)){c->InterruptHandle=interrupt.Value;c->InterruptConfigured=1;}
  else if(!_allowPollingFallback)return false;
  // Maskable interrupts are enabled later, after synchronous hardware startup. Early xHCI
  // enumeration therefore drains completion TRBs synchronously; EnableInterruptDelivery()
  // switches this already-routed controller to MSI-X/MSI/INTx before the interactive session.
  W32(c->Operational,R32(c->Operational)|1U);if(!Wait32(c->Operational+4,1U,0U))return false;
  UsbHostCallbacks callbacks=new((UInt64)(nuint)c,&PrepareDevice,&ControlTransfer,&BulkTransfer,&InterruptTransfer,&ServiceInterruptTransfer,&ConfigureEndpoint,&ConfigureHub);if(!KernelUsbBus.RegisterHost(new KernelDeviceHandle(c->DeviceHandle),callbacks,out UsbHostHandle host))return false;c->Host=host.Value;c->State=(Byte)XhciControllerState.Running;return true;
 }
 static void DisableInterruptDelivery(Controller* c){if(c==null||c->Operational==0UL||c->Runtime==0UL)return;UInt64 ir=c->Runtime+0x20;W32(ir,1U);W32(c->Operational,R32(c->Operational)&~(1U<<2));c->InterruptDriven=0;c->InterruptPrepared=0;}
 static Boolean AllocateControllerMemory(Controller* c,UInt32 hcs2){if(!Page(out c->Dcbaa)||!Page(out c->Command)||!Page(out c->Event)||!Page(out c->Erst))return false;Zero(c->Dcbaa);Zero(c->Command);Zero(c->Event);Zero(c->Erst);if(!KernelHeap.TryAllocate((UInt64)MaxSlots*(UInt64)sizeof(Slot),64,true,out c->SlotAllocation)||!KernelHeap.TryAllocate((UInt64)MaxEndpoints*(UInt64)sizeof(Endpoint),64,true,out c->EndpointAllocation))return false;c->SlotTable=c->SlotAllocation.Address;c->EndpointTable=c->EndpointAllocation.Address;UInt32 scratch=((hcs2>>27)&31U)<<5|((hcs2>>21)&31U);if(scratch!=0){UInt64 arrayPages=((UInt64)scratch*8UL+4095UL)/4096UL;if(!KernelPhysicalMemory.TryAllocate(arrayPages,1,out c->ScratchArray)||!KernelPhysicalMemory.TryAllocate(scratch,1,out c->ScratchPages))return false;Zero(c->ScratchArray);Zero(c->ScratchPages);UInt64* a=(UInt64*)Direct(c->ScratchArray);for(UInt32 i=0;i<scratch;i++)a[i]=c->ScratchPages.StartAddress+(UInt64)i*4096UL;((UInt64*)Direct(c->Dcbaa))[0]=c->ScratchArray.StartAddress;}return true;}
 static void SetupCommandRing(Controller* c){UInt32* trb=(UInt32*)Direct(c->Command);UInt64 p=c->Command.StartAddress;UInt32 i=255U*4U;trb[i]=(UInt32)p;trb[i+1]=(UInt32)(p>>32);trb[i+2]=0;trb[i+3]=(6U<<10)|(1U<<1)|1U;c->CommandEnqueue=0;c->CommandCycle=1;}
 static void SetupEventRing(Controller* c){UInt64* erst=(UInt64*)Direct(c->Erst);erst[0]=c->Event.StartAddress;erst[1]=256UL;c->EventIndex=0;c->EventCycle=1;}
}
