using System;

namespace Inu.Kernel.Storage;

/// <summary><inu.api>Coder-facing drive discovery and partition-layout policy used from the OS-owned Kernel.cs.</inu.api></summary>
public static class Storage
{
    /// <summary><inu.api>Gets a selected drive by controller kind and zero-based drive index.</inu.api></summary>
    public static Boolean TryGetDrive(KernelStorageDeviceKind kind,UInt32 driveIndex,out KernelStorageDeviceHandle drive)
        =>KernelStorage.TryGetDevice(kind,driveIndex,out drive);

    /// <summary><inu.api>Creates an equal-sized GPT layout on a blank drive. Existing partition tables are preserved.</inu.api></summary>
    public static Boolean EnsureGptPartitions(KernelStorageDeviceHandle drive,UInt32 partitionCount)
        =>KernelDiskProvisioning.EnsureGptPartitions(drive,partitionCount,out _);

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
