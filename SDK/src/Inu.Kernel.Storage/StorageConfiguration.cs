using System;

namespace Inu.Kernel.Storage;

/// <summary><inu.api>Coder-facing drive discovery and partition-layout policy used from the OS-owned Kernel.cs.</inu.api></summary>
public static class Storage
{

    private static UInt32 _systemDrive;

    /// <summary><inu.api>Marks the block drive that contains the booted system image. Platform boot code or the OS may override the default discovery.</inu.api></summary>
    public static Boolean SetSystemDrive(KernelStorageDeviceHandle drive)
    {
        if(!KernelStorage.TryGetGeometry(drive,out _))return false;_systemDrive=drive.Value;return true;
    }

    /// <summary><inu.api>Returns the drive that contains the booted system image. The current UEFI/QEMU profile identifies the first virtual block drive as the boot/system drive unless the OS explicitly overrides it.</inu.api></summary>
    public static Boolean TryGetSystemDrive(out KernelStorageDeviceHandle drive)
    {
        drive=default;if(_systemDrive!=0U){KernelStorageDeviceHandle configured=new(_systemDrive);if(KernelStorage.TryGetGeometry(configured,out _)){drive=configured;return true;}_systemDrive=0U;}
        UInt32 count=GetDriveCount();for(UInt32 i=0U;i<count;i++){if(!TryGetDrive(i,out KernelStorageDeviceHandle candidate,out KernelStorageDeviceKind kind,out _))continue;if(kind!=KernelStorageDeviceKind.Virtual)continue;_systemDrive=candidate.Value;drive=candidate;return true;}return false;
    }

    /// <summary><inu.api>Reports whether a drive is the current system drive.</inu.api></summary>
    public static Boolean IsSystemDrive(KernelStorageDeviceHandle drive)=>TryGetSystemDrive(out KernelStorageDeviceHandle system)&&system.Value==drive.Value;

    /// <summary><inu.api>Returns the number of currently discovered storage volumes/partitions.</inu.api></summary>
    public static UInt32 GetVolumeCount()=>KernelStorage.GetCapabilities().Volumes;

    /// <summary><inu.api>Gets a discovered volume by zero-based ordinal together with its owning drive and partition metadata.</inu.api></summary>
    public static Boolean TryGetVolume(UInt32 volumeOrdinal,out KernelStorageVolumeHandle volume,out KernelStorageDeviceHandle drive,out KernelPartitionInfo partition)
    {
        volume=default;drive=default;partition=default;if(!KernelStorage.TryGetVolumeByOrdinal(volumeOrdinal,out volume))return false;return KernelStorage.TryGetVolume(volume,out drive,out partition);
    }
    /// <summary><inu.api>Returns the number of block drives currently detected by the kernel.</inu.api></summary>
    public static UInt32 GetDriveCount()=>KernelStorage.GetCapabilities().Devices;

    /// <summary><inu.api>Gets a detected drive by zero-based ordinal, regardless of controller type.</inu.api></summary>
    public static Boolean TryGetDrive(UInt32 driveOrdinal,out KernelStorageDeviceHandle drive,out KernelStorageDeviceKind kind,out KernelStorageGeometry geometry)
        =>KernelStorage.TryGetDeviceByOrdinal(driveOrdinal,out drive,out kind,out geometry);

    /// <summary><inu.api>Gets a selected drive by controller kind and zero-based drive index.</inu.api></summary>
    public static Boolean TryGetDrive(KernelStorageDeviceKind kind,UInt32 driveIndex,out KernelStorageDeviceHandle drive)
        =>KernelStorage.TryGetDevice(kind,driveIndex,out drive);

    /// <summary><inu.api>Creates an equal-sized GPT layout on a blank drive. Existing partition tables are preserved.</inu.api></summary>
    public static Boolean EnsureGptPartitions(KernelStorageDeviceHandle drive,UInt32 partitionCount)
        =>KernelDiskProvisioning.EnsureGptPartitions(drive,partitionCount,out _);

    /// <summary><inu.api>Creates an equal-sized GPT layout on a blank drive and reports whether a new partition table was written. Existing partition tables are preserved.</inu.api></summary>
    public static Boolean EnsureGptPartitions(KernelStorageDeviceHandle drive,UInt32 partitionCount,out Boolean created)
        =>KernelDiskProvisioning.EnsureGptPartitions(drive,partitionCount,out created);

    /// <summary><inu.api>Explicitly replaces the current partition table with an equal-sized GPT layout. This destroys existing data.</inu.api></summary>
    public static Boolean RepartitionGpt(KernelStorageDeviceHandle drive,UInt32 partitionCount)
        =>KernelDiskProvisioning.CreateGptPartitions(drive,partitionCount,true,out _);

    /// <summary><inu.api>Gets a one-based partition number belonging to a drive.</inu.api></summary>
    public static Boolean TryGetPartition(KernelStorageDeviceHandle drive,UInt32 partitionNumber,out KernelStorageVolumeHandle volume)
    {
        volume=default;if(partitionNumber==0U)return false;
        for(UInt32 ordinal=0U;KernelStorage.TryGetVolumeByOrdinal(ordinal,out KernelStorageVolumeHandle candidate);ordinal++)
        {
            if(!KernelStorage.TryGetVolume(candidate,out KernelStorageDeviceHandle owner,out KernelPartitionInfo partition))continue;
            if(owner.Value==drive.Value&&partition.Index+1U==partitionNumber){volume=candidate;return true;}
        }
        return false;
    }
}
