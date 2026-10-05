using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Drivers;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Storage;

/// <summary>Heap-backed block-device and volume registry with MBR/GPT partition discovery.</summary>
public static unsafe partial class KernelStorage
{
    public static Boolean RegisterBlockDevice(KernelDeviceHandle driverDevice,KernelStorageDeviceKind kind,KernelStorageGeometry geometry,KernelBlockDeviceCallbacks callbacks,out KernelStorageDeviceHandle handle)
    { handle=default;if(!_initialized||driverDevice.Value==0||kind==KernelStorageDeviceKind.Unknown||!KernelStorageMath.IsValidGeometry(geometry)||callbacks.ReadBlocks==null)return false;Int32 slot=FreeDevice();if(slot<0){if(!GrowDevices())return false;slot=FreeDevice();if(slot<0)return false;}DeviceRecord* r=_devices+slot;Clear((Byte*)r,sizeof(DeviceRecord));r->Used=1;r->Kind=(Byte)kind;r->ReadOnly=geometry.ReadOnly?(Byte)1:(Byte)0;r->Removable=geometry.Removable?(Byte)1:(Byte)0;r->DriverDevice=driverDevice.Value;r->LogicalBlockSize=geometry.LogicalBlockSize;r->PhysicalBlockSize=geometry.PhysicalBlockSize;r->BlockCount=geometry.BlockCount;r->Read=(UInt64)(void*)callbacks.ReadBlocks;r->Write=(UInt64)(void*)callbacks.WriteBlocks;r->Flush=(UInt64)(void*)callbacks.Flush;_deviceCount++;handle=new KernelStorageDeviceHandle((UInt32)slot+1U);return true; }
    /// <summary>Registers a block device using callbacks that receive the owning generic device handle.</summary>
    public static Boolean RegisterBlockDevice(KernelDeviceHandle driverDevice,KernelStorageDeviceKind kind,KernelStorageGeometry geometry,KernelContextualBlockDeviceCallbacks callbacks,out KernelStorageDeviceHandle handle)
    { handle=default;if(!_initialized||driverDevice.Value==0||kind==KernelStorageDeviceKind.Unknown||!KernelStorageMath.IsValidGeometry(geometry)||callbacks.ReadBlocks==null)return false;Int32 slot=FreeDevice();if(slot<0){if(!GrowDevices())return false;slot=FreeDevice();if(slot<0)return false;}DeviceRecord* r=_devices+slot;Clear((Byte*)r,sizeof(DeviceRecord));r->Used=1;r->Contextual=1;r->Kind=(Byte)kind;r->ReadOnly=geometry.ReadOnly?(Byte)1:(Byte)0;r->Removable=geometry.Removable?(Byte)1:(Byte)0;r->DriverDevice=driverDevice.Value;r->LogicalBlockSize=geometry.LogicalBlockSize;r->PhysicalBlockSize=geometry.PhysicalBlockSize;r->BlockCount=geometry.BlockCount;r->Read=(UInt64)(void*)callbacks.ReadBlocks;r->Write=(UInt64)(void*)callbacks.WriteBlocks;r->Flush=(UInt64)(void*)callbacks.Flush;_deviceCount++;handle=new KernelStorageDeviceHandle((UInt32)slot+1U);return true; }
    /// <summary>Unregisters a driver-owned block device after all dependent mounts have quiesced.</summary>
    public static Boolean UnregisterBlockDevice(KernelStorageDeviceHandle handle)
    {
        if(!TryDevice(handle,out DeviceRecord* device))return false;
        for(Int32 i=0;i<(Int32)_volumeCapacity;i++){VolumeRecord* v=_volumes+i;if(v->Used==0||v->Device!=handle.Value)continue;if(KernelVfs.IsVolumeMounted(new KernelStorageVolumeHandle((UInt32)i+1U)))return false;}
        for(Int32 i=0;i<(Int32)_volumeCapacity;i++){VolumeRecord* v=_volumes+i;if(v->Used==0||v->Device!=handle.Value)continue;Clear((Byte*)v,sizeof(VolumeRecord));if(_volumeCount>0U)_volumeCount--;}
        Clear((Byte*)device,sizeof(DeviceRecord));if(_deviceCount>0U)_deviceCount--;return true;
    }

    public static Boolean TryGetGeometry(KernelStorageDeviceHandle handle,out KernelStorageGeometry geometry)
    { geometry=default;if(!TryDevice(handle,out DeviceRecord* r))return false;geometry=new KernelStorageGeometry(r->LogicalBlockSize,r->PhysicalBlockSize,r->BlockCount,r->ReadOnly!=0,r->Removable!=0);return true; }
    public static Boolean ReadBlocks(KernelStorageDeviceHandle handle,UInt64 firstBlock,UInt32 blockCount,Byte* buffer,UInt32 bufferBytes)
    { if((KernelFaultInjection.ShouldInject(KernelFaultKind.IoTimeout,"storage",out _)||KernelFaultInjection.ShouldInject(KernelFaultKind.StorageError,"storage",out _)))return false;if(!TryDevice(handle,out DeviceRecord* r)||buffer==null||blockCount==0||firstBlock>=r->BlockCount||blockCount>r->BlockCount-firstBlock||(UInt64)bufferBytes<(UInt64)blockCount*r->LogicalBlockSize)return false;if(r->Contextual!=0){delegate*<KernelDeviceHandle,UInt64,UInt32,Byte*,UInt32,Boolean> read=(delegate*<KernelDeviceHandle,UInt64,UInt32,Byte*,UInt32,Boolean>)(void*)r->Read;return read(new KernelDeviceHandle(r->DriverDevice),firstBlock,blockCount,buffer,bufferBytes);}delegate*<UInt64,UInt32,Byte*,UInt32,Boolean> readLegacy=(delegate*<UInt64,UInt32,Byte*,UInt32,Boolean>)(void*)r->Read;return readLegacy(firstBlock,blockCount,buffer,bufferBytes); }
    public static Boolean WriteBlocks(KernelStorageDeviceHandle handle,UInt64 firstBlock,UInt32 blockCount,Byte* buffer,UInt32 bufferBytes)
    { if((KernelFaultInjection.ShouldInject(KernelFaultKind.IoTimeout,"storage",out _)||KernelFaultInjection.ShouldInject(KernelFaultKind.StorageError,"storage",out _)))return false;if(!TryDevice(handle,out DeviceRecord* r)||r->ReadOnly!=0||r->Write==0||buffer==null||blockCount==0||firstBlock>=r->BlockCount||blockCount>r->BlockCount-firstBlock||(UInt64)bufferBytes<(UInt64)blockCount*r->LogicalBlockSize)return false;if(r->Contextual!=0){delegate*<KernelDeviceHandle,UInt64,UInt32,Byte*,UInt32,Boolean> write=(delegate*<KernelDeviceHandle,UInt64,UInt32,Byte*,UInt32,Boolean>)(void*)r->Write;return write(new KernelDeviceHandle(r->DriverDevice),firstBlock,blockCount,buffer,bufferBytes);}delegate*<UInt64,UInt32,Byte*,UInt32,Boolean> writeLegacy=(delegate*<UInt64,UInt32,Byte*,UInt32,Boolean>)(void*)r->Write;return writeLegacy(firstBlock,blockCount,buffer,bufferBytes); }
    public static Boolean Flush(KernelStorageDeviceHandle handle)
    { if((KernelFaultInjection.ShouldInject(KernelFaultKind.IoTimeout,"storage",out _)||KernelFaultInjection.ShouldInject(KernelFaultKind.StorageError,"storage",out _)))return false;if(!TryDevice(handle,out DeviceRecord* r))return false;if(r->Flush==0)return true;if(r->Contextual!=0){delegate*<KernelDeviceHandle,Boolean> flush=(delegate*<KernelDeviceHandle,Boolean>)(void*)r->Flush;return flush(new KernelDeviceHandle(r->DriverDevice));}delegate*<Boolean> flushLegacy=(delegate*<Boolean>)(void*)r->Flush;return flushLegacy(); }
}
