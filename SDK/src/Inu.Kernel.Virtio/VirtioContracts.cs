using System;
using Inu.Kernel.Drivers;
using Inu.Kernel.Networking;
using Inu.Kernel.Storage;

namespace Inu.Kernel.Virtio;

/// <summary>VirtIO device identifiers used by the PCI transport.</summary>
public enum VirtioDeviceType : UInt16 { Unknown=0, Network=1, Block=2, Console=3, EntropySource=4 }
/// <summary>VirtIO device status bits defined by the transport specification.</summary>
public enum VirtioDeviceStatus : Byte { Reset=0,Acknowledge=1,Driver=2,DriverOk=4,FeaturesOk=8,DeviceNeedsReset=64,Failed=128 }

/// <summary>Describes one initialized VirtIO PCI device.</summary>
public readonly struct VirtioDeviceInfo
{
    /// <summary>Creates VirtIO device metadata.</summary>
    public VirtioDeviceInfo(KernelDeviceHandle device,VirtioDeviceType type,UInt64 deviceFeatures,UInt64 negotiatedFeatures,UInt16 queueCount,Boolean started,KernelStorageDeviceHandle storageDevice,KernelNetworkInterfaceHandle networkInterface)
    {Device=device;Type=type;DeviceFeatures=deviceFeatures;NegotiatedFeatures=negotiatedFeatures;QueueCount=queueCount;Started=started;StorageDevice=storageDevice;NetworkInterface=networkInterface;}
    /// <summary>Generic device handle.</summary>
    public KernelDeviceHandle Device { get; }
    /// <summary>VirtIO device type.</summary>
    public VirtioDeviceType Type { get; }
    /// <summary>Features advertised by the device.</summary>
    public UInt64 DeviceFeatures { get; }
    /// <summary>Features accepted by the driver.</summary>
    public UInt64 NegotiatedFeatures { get; }
    /// <summary>Number of transport queues configured by this driver.</summary>
    public UInt16 QueueCount { get; }
    /// <summary>Whether DRIVER_OK was set.</summary>
    public Boolean Started { get; }
    /// <summary>Registered block-device handle for VirtIO block devices.</summary>
    public KernelStorageDeviceHandle StorageDevice { get; }
    /// <summary>Registered network interface for VirtIO network devices.</summary>
    public KernelNetworkInterfaceHandle NetworkInterface { get; }
}

/// <summary>Summarizes initialized VirtIO devices.</summary>
public readonly struct VirtioCapabilities
{
    /// <summary>Creates VirtIO capability counts.</summary>
    public VirtioCapabilities(Boolean initialized,UInt32 devices,UInt32 blockDevices,UInt32 networkDevices,UInt32 consoles,UInt32 entropySources){Initialized=initialized;Devices=devices;BlockDevices=blockDevices;NetworkDevices=networkDevices;Consoles=consoles;EntropySources=entropySources;}
    /// <summary>Whether the VirtIO driver family is installed.</summary>
    public Boolean Initialized { get; }
    /// <summary>Total started VirtIO devices.</summary>
    public UInt32 Devices { get; }
    /// <summary>Started block devices.</summary>
    public UInt32 BlockDevices { get; }
    /// <summary>Started network devices.</summary>
    public UInt32 NetworkDevices { get; }
    /// <summary>Started consoles.</summary>
    public UInt32 Consoles { get; }
    /// <summary>Started entropy sources.</summary>
    public UInt32 EntropySources { get; }
}
