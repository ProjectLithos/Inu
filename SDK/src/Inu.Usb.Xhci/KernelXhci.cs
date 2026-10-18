using System;
using Inu.Bus.Usb;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;
using Inu.Kernel.Memory;
using Inu.Kernel.Pci;
using Inu.Kernel.Synchronization;
namespace Inu.Usb.Xhci;
/// <summary>PCI xHCI host-controller implementation with command, event and endpoint transfer rings.</summary>
public static unsafe partial class KernelXhci
{
 const UInt32 SpinLimit=40000000U;const UInt64 InterruptWaitTimeoutNanoseconds=2000000000UL;const Int32 MaxControllers=8,MaxSlots=256,MaxEndpoints=2048;
 struct Ring{internal KernelPhysicalAllocation Memory;internal UInt16 Enqueue;internal Byte Cycle;}
 struct Slot{internal Byte Used,SlotId,LogicalAddress,RootPort,Depth,SpeedId,HubPorts,ContextEntries,MultiTt,ParentSlot,TtPort;internal UInt32 Route;internal UInt16 Ep0Packet;internal KernelPhysicalAllocation Output,Input;internal Ring Ep0;}
 struct Endpoint{internal Byte Used,SlotId,Dci,Address,Type,Interval;internal UInt16 MaxPacket;internal Ring Ring;}
 struct Controller
 {
  internal Byte Used,Ports,Slots,ContextSize,State,EventCycle,InterruptConfigured,InterruptPrepared,InterruptDriven;
  internal UInt16 EventIndex;internal UInt16 Segment;internal Byte Bus,Device,Function,PendingSlot;
  internal UInt32 Host,DeviceHandle;internal UInt64 Mmio,Operational,Runtime,Doorbells,InterruptHandle;
  internal KernelPhysicalAllocation Dcbaa,Command,Event,Erst,ScratchArray,ScratchPages;internal KernelHeapAllocation SlotAllocation,EndpointAllocation;
  internal UInt64 SlotTable,EndpointTable;internal UInt16 CommandEnqueue;internal Byte CommandCycle;
  internal KernelEvent CommandCompletionEvent,TransferCompletionEvent;
  internal Byte CommandCompletionCode,CommandCompletionSlot,TransferCompletionCode,TransferCompletionSlot,TransferCompletionDci;
  internal UInt32 TransferCompletionRemaining;
 }
 static Controller* _controllers;static KernelHeapAllocation _controllersAllocation;static KernelDriverHandle _driver;static Boolean _initialized,_allowPollingFallback;static UInt32 _controllerCount,_running,_connected;
 public static Boolean Initialize(){if(_initialized)return true;if(!KernelUsbBus.Initialize()||!KernelPhysicalMemory.IsInitialized()||!KernelAddressSpace.IsInitialized()||!KernelHeap.IsInitialized()||!KernelDrivers.IsInitialized())return false;if(!KernelHeap.TryAllocate((UInt64)MaxControllers*(UInt64)sizeof(Controller),64,true,out _controllersAllocation))return false;_controllers=(Controller*)(nuint)_controllersAllocation.Address;KernelDriverMatchRule rule=new(KernelDeviceBus.Pci,true,0,false,0,false,0x0C0330U,0xFFFFFFU);KernelDriverCallbacks cb=new(&Probe,&Start,&Stop,&Remove,&Interrupt);KernelDriverCapabilityDeclaration caps=new(KernelDriverCapability.Mmio|KernelDriverCapability.Dma|KernelDriverCapability.PciConfig|KernelDriverCapability.Interrupt|KernelDriverCapability.Msi|KernelDriverCapability.MsiX);if(!KernelDrivers.RegisterDriver("xhci",rule,cb,caps,out _driver))return false;_initialized=true;for(UInt32 i=0;i<KernelPci.GetDeviceCount();i++){if(!KernelPci.TryGetDevice(i,out PciDeviceInfo pci)||pci.ClassCode!=0x0C0330U)continue;KernelDrivers.MatchAndStartDevice(pci.DeviceHandle);}return true;}
 /// <summary>Explicit bring-up/debug escape hatch. Normal Inu boots require broker-managed xHCI interrupts.</summary>
 public static Boolean SetPollingFallback(Boolean enabled){if(_running!=0U)return false;_allowPollingFallback=enabled;return true;}
 public static Boolean PrepareInterruptDelivery(){if(!_initialized)return false;for(Int32 i=0;i<MaxControllers;i++){Controller* c=_controllers+i;if(c->Used==0||c->State!=(Byte)XhciControllerState.Running)continue;if(c->InterruptPrepared!=0)continue;if(c->InterruptConfigured==0){if(_allowPollingFallback)continue;return false;}ServiceHardwareEvents(c);c->InterruptPrepared=1;}return true;}
 public static Boolean EnableInterruptDelivery(){if(!_initialized)return false;for(Int32 i=0;i<MaxControllers;i++){Controller* c=_controllers+i;if(c->Used==0||c->State!=(Byte)XhciControllerState.Running)continue;if(c->InterruptDriven!=0)continue;if(c->InterruptConfigured==0){if(_allowPollingFallback)continue;return false;}if(c->InterruptPrepared==0)return false;UInt64 ir=c->Runtime+0x20;W32(ir,3U);W32(c->Operational,R32(c->Operational)|(1U<<2));c->InterruptDriven=1;}return true;}
 public static XhciCapabilities GetCapabilities(){UInt32 configured=0,prepared=0,interruptDriven=0,polling=0;for(Int32 i=0;i<MaxControllers;i++){Controller* c=_controllers+i;if(c->Used==0||c->State!=(Byte)XhciControllerState.Running)continue;if(c->InterruptConfigured!=0)configured++;if(c->InterruptPrepared!=0)prepared++;if(c->InterruptDriven!=0)interruptDriven++;else if(c->InterruptConfigured==0)polling++;}return new(_initialized,_controllerCount,_running,_connected,true,configured,prepared,interruptDriven,polling);}
 public static UInt32 GetControllerCount()=>_controllerCount;
 public static Boolean TryGetController(UInt32 index,out XhciControllerInfo info){info=default;if(index>=_controllerCount)return false;UInt32 found=0;for(Int32 i=0;i<MaxControllers;i++){Controller* c=_controllers+i;if(c->Used==0)continue;if(found++!=index)continue;info=new XhciControllerInfo(new PciLocation(c->Segment,c->Bus,c->Device,c->Function),c->Mmio,c->Ports,c->Slots,(XhciControllerState)c->State);return true;}return false;}
 public static Boolean ScanRootPorts(){if(!_initialized&&_controllerCount==0)return false;_connected=0;for(Int32 ci=0;ci<MaxControllers;ci++){Controller* c=_controllers+ci;if(c->Used==0||c->State!=(Byte)XhciControllerState.Running)continue;if(!ScanControllerPorts(c))return false;}return true;}
}
