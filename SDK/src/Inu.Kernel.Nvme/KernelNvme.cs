using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Memory;
using Inu.Kernel.Heap;
using Inu.Kernel.Pci;
using Inu.Kernel.Storage;
namespace Inu.Kernel.Nvme;
/// <summary>PCI NVMe 1.x controller driver with admin/I/O queues, namespace discovery and synchronous block I/O.</summary>
public static unsafe partial class KernelNvme
{
 const UInt16 RequestedAdminEntries=32,RequestedIoEntries=64; const UInt32 SpinLimit=40000000U;
 struct Controller { internal Byte Used,State,DbStride,InterruptMode,AdminPhase,IoPhase; internal UInt16 Segment,AdminTail,AdminHead,AdminCid,IoTail,IoHead,IoCid,AdminEntries,IoEntries; internal Byte Bus,Device,Function; internal UInt32 DeviceHandle,Version,NamespaceCount; internal UInt64 Mmio,InterruptHandle,InterruptEpoch; internal KernelPhysicalAllocation Asq,Acq,IoSq,IoCq,Identify; }
 struct Namespace { internal Byte Used; internal UInt32 Controller,Nsid,BlockSize,DeviceHandle,StorageHandle; internal UInt64 Blocks; }
 static Controller* _controllers; static Namespace* _namespaces; static KernelHeapAllocation _controllerAllocation,_namespaceAllocation; static UInt32 _controllerCapacity,_namespaceCapacity,_controllerCount,_namespaceCount; static KernelDriverHandle _driver; static Boolean _initialized;
 /// <summary>Discovers class 01/08/02 PCI functions and initializes every usable NVMe controller.</summary>
 public static Boolean Initialize(){if(_initialized)return true;if(!KernelPci.IsInitialized()||!KernelStorage.IsInitialized()||!KernelPhysicalMemory.IsInitialized()||!KernelAddressSpace.IsInitialized()||!KernelDrivers.IsInitialized())return false;if(!AllocateTables())return false;KernelDriverMatchRule rule=new(KernelDeviceBus.Pci,true,0,false,0,false,0x010802U,0xFFFFFFU);KernelDriverCallbacks cb=new(&Probe,&Start,&Stop,&Remove,&Interrupt);KernelDriverCapabilityDeclaration caps=new(KernelDriverCapability.Mmio|KernelDriverCapability.Dma|KernelDriverCapability.PciConfig|KernelDriverCapability.Interrupt|KernelDriverCapability.Msi|KernelDriverCapability.MsiX|KernelDriverCapability.Filesystem);if(!KernelDrivers.RegisterDriver("nvme",rule,cb,caps,out _driver))return false;_initialized=true;UInt32 count=KernelPci.GetDeviceCount();for(UInt32 i=0;i<count;i++){if(!KernelPci.TryGetDevice(i,out PciDeviceInfo pci)||!NvmeMath.IsNvmeClass(pci.ClassCode))continue;KernelDrivers.MatchAndStartDevice(pci.DeviceHandle);}return true;}
 public static Boolean IsInitialized()=>_initialized; public static NvmeCapabilities GetCapabilities()=>new(_initialized,_controllerCount,_namespaceCount);
 public static Boolean TryGetController(UInt32 index,out NvmeControllerInfo info){info=default;if(index>=_controllerCount)return false;UInt32 found=0;for(UInt32 i=0;i<_controllerCapacity;i++){Controller* c=_controllers+i;if(c->Used==0)continue;if(found++!=index)continue;info=new NvmeControllerInfo(new PciLocation(c->Segment,c->Bus,c->Device,c->Function),new KernelDeviceHandle(c->DeviceHandle),(NvmeControllerState)c->State,(NvmeInterruptMode)c->InterruptMode,c->AdminEntries,c->IoEntries,c->NamespaceCount,c->Version);return true;}return false;}
 public static Boolean TryGetNamespace(UInt32 index,out NvmeNamespaceInfo info){info=default;if(index>=_namespaceCount)return false;UInt32 found=0;for(UInt32 i=0;i<_namespaceCapacity;i++){Namespace* n=_namespaces+i;if(n->Used==0)continue;if(found++!=index)continue;info=new NvmeNamespaceInfo(n->Controller,n->Nsid,n->BlockSize,n->Blocks,new KernelDeviceHandle(n->DeviceHandle),new KernelStorageDeviceHandle(n->StorageHandle));return true;}return false;}
}
