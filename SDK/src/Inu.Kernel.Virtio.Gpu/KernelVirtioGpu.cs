using System;
using Inu.Kernel.AddressSpace;
using Inu.Kernel.Drivers;
using Inu.Kernel.Graphics;
using Inu.Kernel.Heap;
using Inu.Kernel.Memory;
using Inu.Kernel.Pci;
using Inu.Kernel.Time;

namespace Inu.Kernel.Virtio.Gpu;

/// <summary>Implements the modern VirtIO GPU 2D command set as Inu's first driver-owned graphics adapter.</summary>
public static unsafe partial class KernelVirtioGpu
{
    private const UInt16 VirtioVendorId=0x1AF4,ModernGpuDeviceId=0x1050,TransitionalGpuDeviceId=0x1010;
    private const Byte VendorCapabilityId=0x09,CommonConfigurationType=1,NotifyConfigurationType=2,IsrConfigurationType=3,DeviceConfigurationType=4;
    private const UInt64 FeatureVersion1=1UL<<32,SynchronousTimeoutNanoseconds=1000000000UL;
    private const UInt16 DescriptorNext=1,DescriptorWrite=2;
    private const UInt32 CommandGetDisplayInfo=0x0100U,CommandResourceCreate2D=0x0101U,CommandResourceUnref=0x0102U,CommandSetScanout=0x0103U,CommandResourceFlush=0x0104U,CommandTransferToHost2D=0x0105U,CommandResourceAttachBacking=0x0106U;
    private const UInt32 ResponseOkNoData=0x1100U,ResponseOkDisplayInfo=0x1101U,FormatB8G8R8X8Unorm=2U;
    private const UInt32 MaximumDimension=8192U;

    private struct QueueRecord { internal UInt16 Index,Size,LastUsed,Ready;internal UInt32 NotifyOffset;internal UInt64 AllocationToken,AllocationPages,PhysicalBase,VirtualBase,AvailableOffset,UsedOffset; }
    private struct DeviceRecord
    {
        internal Byte Used,Started,ScanoutActive;internal UInt16 Segment;internal Byte Bus,PciDevice,Function;internal UInt32 DeviceHandle,Scanout,ResourceId,Width,Height,Pitch;internal UInt64 Common,Notify,Isr,DeviceConfig,NotifyMultiplier,DeviceFeatures,NegotiatedFeatures;internal QueueRecord Control;
        internal UInt64 FrameToken,FramePages,FramePhysical,FrameVirtual,FrameBytes;internal UInt32 GraphicsDisplay;
    }
    private static DeviceRecord* _devices;private static KernelHeapAllocation _allocation;private static UInt32 _capacity,_count,_displayCount,_detectedPciDevices,_startFailures;private static Boolean _initialized;private static KernelDriverHandle _driver;

    /// <summary>Registers the VirtIO GPU PCI driver, binds GPU device type 16, and initializes scan-out resources.</summary>
    public static Boolean Initialize()
    {
        if(_initialized)return true;if(!KernelPci.IsInitialized()||!KernelDrivers.IsInitialized()||!KernelHeap.IsInitialized()||!KernelGraphics.IsInitialized())return false;
        if(!AllocateRecords(4U,out _allocation,out _devices))return false;_capacity=4U;
        KernelDriverMatchRule rule=new(KernelDeviceBus.Pci,true,VirtioVendorId,true,0,false,0U,0U);KernelDriverCallbacks callbacks=new(&Probe,&Start,&Stop,&Remove,&Interrupt);KernelDriverCapabilityDeclaration declaration=new(KernelDriverCapability.Mmio|KernelDriverCapability.Dma|KernelDriverCapability.PciConfig);if(!KernelDrivers.RegisterDriver("VirtIO GPU",rule,callbacks,declaration,out _driver))return false;
        UInt32 pciCount=KernelPci.GetDeviceCount();for(UInt32 i=0;i<pciCount;i++)
        {
            if(!KernelPci.TryGetDevice(i,out PciDeviceInfo pci)||!IsGpu(pci))continue;_detectedPciDevices++;
            if(KernelDrivers.TryGetDevice(pci.DeviceHandle,out _,out _,out KernelDriverHandle bound)&&bound.Value!=0U)
            {if(bound.Value!=_driver.Value)_startFailures++;continue;}
            if(!KernelDrivers.TryBindDevice(pci.DeviceHandle,out KernelDriverHandle driver)||driver.Value!=_driver.Value||!KernelDrivers.StartDevice(pci.DeviceHandle))_startFailures++;
        }
        KernelGraphics.ReportVirtioGpuDriverState(_detectedPciDevices,_count,_startFailures);
        _initialized=true;return true;
    }
    /// <summary>Gets whether the VirtIO GPU driver family is installed.</summary>
    public static Boolean IsInitialized()=>_initialized;
    /// <summary>Reports current VirtIO GPU controller and display counts.</summary>
    public static VirtioGpuCapabilities GetCapabilities()=>new(_initialized,_count,_displayCount,true,true);
    /// <summary>Reports how many VirtIO-GPU PCI functions were discovered during driver initialization.</summary>
    public static UInt32 GetDetectedPciDeviceCount()=>_detectedPciDevices;
    /// <summary>Reports how many discovered VirtIO-GPU devices failed bind/start.</summary>
    public static UInt32 GetStartFailureCount()=>_startFailures;
    /// <summary>Gets one started VirtIO GPU controller by zero-based index.</summary>
    public static Boolean TryGetController(UInt32 index,out VirtioGpuInfo info){info=default;if(index>=_count)return false;UInt32 found=0;for(UInt32 i=0;i<_capacity;i++){DeviceRecord* r=_devices+i;if(r->Used==0)continue;if(found++==index){info=Info(r);return true;}}return false;}
    /// <summary>Presents a rectangle for a specific VirtIO GPU device.</summary>
    public static Boolean Present(KernelDeviceHandle device,UInt32 x,UInt32 y,UInt32 width,UInt32 height){if(!TryRecord(device,out DeviceRecord* r)||r->Started==0)return false;return PresentRecord(r,x,y,width,height);}
    /// <summary>Changes the 2D resource and scan-out dimensions for a specific VirtIO GPU device.</summary>
    public static Boolean SetMode(KernelDeviceHandle device,UInt32 width,UInt32 height){if(!TryRecord(device,out DeviceRecord* r)||r->Started==0)return false;return ChangeMode(r,width,height);}
    /// <summary>Activates a prepared VirtIO-GPU display only after its backing resource has been populated.</summary>
    public static Boolean ActivateDisplay(KernelGraphicsDisplayHandle display)
    {
        if(!TryDisplay(display,out DeviceRecord* r)||r->Started==0)return false;
        if(r->ScanoutActive!=0)return PresentRecord(r,0U,0U,r->Width,r->Height);
        // Populate the host resource while firmware GOP is still visible. SET_SCANOUT is deliberately
        // last so a failed transfer/flush cannot replace a working firmware console with a black frame.
        if(!TransferToHost(r,0U,0U,r->Width,r->Height)||!Flush(r,0U,0U,r->Width,r->Height))return false;
        if(!SetScanout(r))return false;
        // RESOURCE_FLUSH updates scanouts that are already bound to this resource. A pre-bind flush
        // validates/populates the host resource but is not sufficient to guarantee a visible QEMU
        // update. Re-transfer and flush the complete frame after SET_SCANOUT so the newly bound
        // scanout receives an explicit first damage/update event.
        if(!TransferToHost(r,0U,0U,r->Width,r->Height)||!Flush(r,0U,0U,r->Width,r->Height))return false;
        r->ScanoutActive=1;
        return true;
    }

}
