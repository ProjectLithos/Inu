using System;

namespace Inu.Kernel.Drivers;

/// <summary>Canonical Inu device-tree classes. Every discoverable object belongs to one of these six models.</summary>
public enum KernelDeviceBus : Byte
{
    Unknown=0, Platform=1, Pci=2, Usb=3, Acpi=4, Virtual=5, Logical=6,
    /// <summary>Compatibility alias: VirtIO devices are virtual devices in the unified tree.</summary>
    Virtio=Virtual,
    /// <summary>Compatibility alias: synthetic devices are logical devices in the unified tree.</summary>
    Synthetic=Logical
}
public enum KernelDeviceResourceType : Byte { None=0, Memory=1, IoPort=2, Interrupt=3, Dma=4, BusSpecific=5 }
/// <summary>Stable numeric property keys stored on the authoritative device object without bus-specific side tables.</summary>
public enum KernelDevicePropertyKey : UInt32
{
    None=0, Segment=1, BusNumber=2, Slot=3, Function=4, UsbAddress=5, UsbPort=6,
    UsbInterface=7, UsbAlternateSetting=8, UsbEndpointCount=9, AcpiIdHash=10,
    LogicalBlockSize=11, PhysicalBlockSize=12, Mtu=13, FirmwareRevision=14, Flags=15
}

/// <summary>One bounded property attached to a device node. Two 64-bit values allow scalar or range-like properties without allocation.</summary>
public readonly struct KernelDeviceProperty
{
    public KernelDeviceProperty(KernelDevicePropertyKey key,UInt64 value0,UInt64 value1=0UL){Key=key;Value0=value0;Value1=value1;}
    public KernelDevicePropertyKey Key { get; }
    public UInt64 Value0 { get; }
    public UInt64 Value1 { get; }
}
public enum KernelDeviceState : Byte
{
    Registered=1, Discovered=2, Probing=3, Probed=4, Binding=5, Bound=6, Starting=7, Started=8, Running=Started,
    Stopping=9, Stopped=10, Resetting=11, Suspending=12, Suspended=13, Resuming=14,
    Failed=15, Recovering=16, Removing=17, Removed=18, Matched=19
}
public enum KernelDriverState : Byte { Registered=1, Active=2, Suspended=3, Failed=4, Recovering=5, Removing=6 }
public enum KernelDriverLifecycleStage : Byte { Discover=1, Probe=2, Bind=3, Start=4, Stop=5, Reset=6, Suspend=7, Resume=8, Remove=9, Fail=10, Recover=11 }
public enum KernelDriverFailureCode : UInt32 { None=0, ProbeFailed=1, BindFailed=2, StartFailed=3, StopFailed=4, ResetFailed=5, SuspendFailed=6, ResumeFailed=7, RemovedUnexpectedly=8, DeviceFault=9, Timeout=10, ResourceFailure=11, CapabilityFailure=12, InternalError=0xFFFF }


/// <summary>Selects whether driver/device tables grow from the kernel heap or stay at an explicitly fixed capacity.</summary>
public enum KernelDriverRegistryMode : Byte { Dynamic=0, Fixed=1 }

/// <summary>Configures driver/device registry storage without requiring record/runtime helper support.</summary>
public readonly struct KernelDriverFrameworkOptions
{
    public KernelDriverFrameworkOptions(KernelDriverRegistryMode registryMode, UInt32 initialDriverCapacity, UInt32 initialDeviceCapacity, UInt32 maximumDriverCapacity, UInt32 maximumDeviceCapacity)
    { RegistryMode=registryMode; InitialDriverCapacity=initialDriverCapacity; InitialDeviceCapacity=initialDeviceCapacity; MaximumDriverCapacity=maximumDriverCapacity; MaximumDeviceCapacity=maximumDeviceCapacity; }
    public KernelDriverRegistryMode RegistryMode { get; }
    public UInt32 InitialDriverCapacity { get; }
    public UInt32 InitialDeviceCapacity { get; }
    public UInt32 MaximumDriverCapacity { get; }
    public UInt32 MaximumDeviceCapacity { get; }
    public static KernelDriverFrameworkOptions DynamicDefault => new(KernelDriverRegistryMode.Dynamic, 64U, 128U, UInt32.MaxValue, UInt32.MaxValue);
    public static KernelDriverFrameworkOptions Fixed(UInt32 drivers, UInt32 devices) => new(KernelDriverRegistryMode.Fixed, drivers, devices, drivers, devices);
}

public readonly struct KernelDeviceIdentifier
{
    public KernelDeviceIdentifier(KernelDeviceBus bus, UInt16 vendorId, UInt16 deviceId, UInt16 subsystemVendorId, UInt16 subsystemId, UInt32 classCode, Byte revision, UInt32 location)
    { Bus=bus; VendorId=vendorId; DeviceId=deviceId; SubsystemVendorId=subsystemVendorId; SubsystemId=subsystemId; ClassCode=classCode; Revision=revision; Location=location; }
    public KernelDeviceBus Bus { get; }
    public UInt16 VendorId { get; }
    public UInt16 DeviceId { get; }
    public UInt16 SubsystemVendorId { get; }
    public UInt16 SubsystemId { get; }
    public UInt32 ClassCode { get; }
    public Byte Revision { get; }
    public UInt32 Location { get; }
}

public readonly struct KernelDeviceResource
{
    public KernelDeviceResource(KernelDeviceResourceType type, UInt64 start, UInt64 length, UInt64 flags) { Type=type; Start=start; Length=length; Flags=flags; }
    public KernelDeviceResourceType Type { get; }
    public UInt64 Start { get; }
    public UInt64 Length { get; }
    public UInt64 Flags { get; }
}

public readonly struct KernelDriverMatchRule
{
    public KernelDriverMatchRule(KernelDeviceBus bus, Boolean matchBus, UInt16 vendorId, Boolean matchVendor, UInt16 deviceId, Boolean matchDevice, UInt32 classCode, UInt32 classMask)
    { Bus=bus; MatchBus=matchBus; VendorId=vendorId; MatchVendor=matchVendor; DeviceId=deviceId; MatchDevice=matchDevice; ClassCode=classCode; ClassMask=classMask; }
    public KernelDeviceBus Bus { get; }
    public Boolean MatchBus { get; }
    public UInt16 VendorId { get; }
    public Boolean MatchVendor { get; }
    public UInt16 DeviceId { get; }
    public Boolean MatchDevice { get; }
    public UInt32 ClassCode { get; }
    public UInt32 ClassMask { get; }
}

public readonly struct KernelDriverHandle { public KernelDriverHandle(UInt32 value) { Value=value; } public UInt32 Value { get; } }
public readonly struct KernelDeviceHandle { public KernelDeviceHandle(UInt32 value) { Value=value; } public UInt32 Value { get; } }

/// <summary>Immutable runtime view of one registered driver.</summary>
public readonly struct KernelDriverInfo
{
    public KernelDriverInfo(KernelDriverHandle handle,KernelDriverState state,KernelDriverMatchRule matchRule,KernelDriverCapability capabilities,UInt32 nameLength)
    { Handle=handle;State=state;MatchRule=matchRule;Capabilities=capabilities;NameLength=nameLength; }
    public KernelDriverHandle Handle { get; }
    public KernelDriverState State { get; }
    public KernelDriverMatchRule MatchRule { get; }
    public KernelDriverCapability Capabilities { get; }
    public UInt32 NameLength { get; }
}
public readonly struct KernelDriverInterruptHandle { public KernelDriverInterruptHandle(UInt64 value) { Value=value; } public UInt64 Value { get; } }

[Flags]
public enum KernelDriverCapability : UInt64
{
    None=0UL,
    Mmio=1UL<<0,
    PortIo=1UL<<1,
    Interrupt=1UL<<2,
    Msi=1UL<<3,
    MsiX=1UL<<4,
    Dma=1UL<<5,
    PciConfig=1UL<<6,
    PhysicalMemory=1UL<<7,
    Timers=1UL<<8,
    Networking=1UL<<9,
    Filesystem=1UL<<10
}

public enum KernelDriverCapabilityAccess : Byte { None=0, Read=1, Write=2, ReadWrite=3, Execute=4 }

/// <summary>Declares the complete privilege set a driver is allowed to request.</summary>
public readonly struct KernelDriverCapabilityDeclaration
{
    public KernelDriverCapabilityDeclaration(KernelDriverCapability capabilities) { Capabilities=capabilities; }
    public KernelDriverCapability Capabilities { get; }
    public Boolean Contains(KernelDriverCapability capability) => capability!=KernelDriverCapability.None && (Capabilities&capability)==capability;
    public static KernelDriverCapabilityDeclaration None => new(KernelDriverCapability.None);
}

/// <summary>Requests one concrete capability. Range is mandatory for MMIO, port I/O and physical-memory grants.</summary>
public readonly struct KernelDriverCapabilityRequest
{
    public KernelDriverCapabilityRequest(KernelDriverCapability capability, UInt64 start, UInt64 length, KernelDriverCapabilityAccess access, UInt64 flags=0UL)
    { Capability=capability; Start=start; Length=length; Access=access; Flags=flags; }
    public KernelDriverCapability Capability { get; }
    public UInt64 Start { get; }
    public UInt64 Length { get; }
    public KernelDriverCapabilityAccess Access { get; }
    public UInt64 Flags { get; }
}

/// <summary>Opaque kernel-issued proof that a driver/device binding was granted one capability.</summary>
public readonly struct KernelDriverCapabilityGrant
{
    public KernelDriverCapabilityGrant(UInt64 token, KernelDeviceHandle device, KernelDriverHandle driver, KernelDriverCapability capability, UInt64 start, UInt64 length, KernelDriverCapabilityAccess access)
    { Token=token; Device=device; Driver=driver; Capability=capability; Start=start; Length=length; Access=access; }
    public UInt64 Token { get; }
    public KernelDeviceHandle Device { get; }
    public KernelDriverHandle Driver { get; }
    public KernelDriverCapability Capability { get; }
    public UInt64 Start { get; }
    public UInt64 Length { get; }
    public KernelDriverCapabilityAccess Access { get; }
    public Boolean IsValid => Token!=0UL;
}


public readonly struct KernelDriverDeviceContext
{
    public KernelDriverDeviceContext(KernelDeviceHandle device, KernelDriverHandle driver, KernelDeviceIdentifier identifier) { Device=device; Driver=driver; Identifier=identifier; }
    public KernelDeviceHandle Device { get; }
    public KernelDriverHandle Driver { get; }
    public KernelDeviceIdentifier Identifier { get; }
}

public readonly struct KernelDriverInterruptRequest
{
    public KernelDriverInterruptRequest(KernelDeviceHandle device, UInt32 source, Byte priority, UInt32 targetProcessor, Boolean levelTriggered, Boolean activeLow, UInt64 driverCookie)
    { Device=device; Source=source; Priority=priority; TargetProcessor=targetProcessor; LevelTriggered=levelTriggered; ActiveLow=activeLow; DriverCookie=driverCookie; }
    public KernelDeviceHandle Device { get; }
    public UInt32 Source { get; }
    public Byte Priority { get; }
    public UInt32 TargetProcessor { get; }
    public Boolean LevelTriggered { get; }
    public Boolean ActiveLow { get; }
    public UInt64 DriverCookie { get; }
}

/// <summary>Reports registry mode, current heap-backed capacity, configured limits, and current use.</summary>
public readonly struct KernelDriverCapabilities
{
    public KernelDriverCapabilities(Boolean initialized, KernelDriverRegistryMode registryMode, UInt32 registeredDrivers, UInt32 registeredDevices, UInt32 boundDevices, UInt32 startedDevices, UInt32 driverCapacity, UInt32 deviceCapacity, UInt32 maximumDrivers, UInt32 maximumDevices, UInt32 maximumResourcesPerDevice, Boolean interruptBrokerInstalled)
    { Initialized=initialized; RegistryMode=registryMode; RegisteredDrivers=registeredDrivers; RegisteredDevices=registeredDevices; BoundDevices=boundDevices; StartedDevices=startedDevices; DriverCapacity=driverCapacity; DeviceCapacity=deviceCapacity; MaximumDrivers=maximumDrivers; MaximumDevices=maximumDevices; MaximumResourcesPerDevice=maximumResourcesPerDevice; InterruptBrokerInstalled=interruptBrokerInstalled; }
    public Boolean Initialized { get; }
    public KernelDriverRegistryMode RegistryMode { get; }
    public UInt32 RegisteredDrivers { get; }
    public UInt32 RegisteredDevices { get; }
    public UInt32 BoundDevices { get; }
    public UInt32 StartedDevices { get; }
    public UInt32 DriverCapacity { get; }
    public UInt32 DeviceCapacity { get; }
    public UInt32 MaximumDrivers { get; }
    public UInt32 MaximumDevices { get; }
    public UInt32 MaximumResourcesPerDevice { get; }
    public Boolean InterruptBrokerInstalled { get; }
}

/// <summary>One immutable view of a node in Inu's authoritative device tree. The IDE Hardware Tree consumes this same model.</summary>
public readonly struct KernelDeviceNode
{
    public KernelDeviceNode(KernelDeviceHandle handle,KernelDeviceHandle parent,KernelDeviceHandle firstChild,KernelDeviceHandle nextSibling,KernelDeviceIdentifier identifier,KernelDeviceState state,KernelDriverHandle driver,KernelDriverFailureCode failure)
    { Handle=handle;Parent=parent;FirstChild=firstChild;NextSibling=nextSibling;Identifier=identifier;State=state;Driver=driver;Failure=failure; }
    public KernelDeviceHandle Handle { get; }
    public KernelDeviceHandle Parent { get; }
    public KernelDeviceHandle FirstChild { get; }
    public KernelDeviceHandle NextSibling { get; }
    public KernelDeviceIdentifier Identifier { get; }
    public KernelDeviceState State { get; }
    public KernelDriverHandle Driver { get; }
    public KernelDriverFailureCode Failure { get; }
}


/// <summary>Summary of the authoritative unified device tree used by the kernel and IDE Hardware Tree.</summary>
public readonly struct KernelDeviceTreeSnapshot
{
    public KernelDeviceTreeSnapshot(UInt64 generation,UInt32 totalNodes,UInt32 rootNodes,UInt32 pciNodes,UInt32 usbNodes,UInt32 acpiNodes,UInt32 platformNodes,UInt32 virtualNodes,UInt32 logicalNodes)
    { Generation=generation;TotalNodes=totalNodes;RootNodes=rootNodes;PciNodes=pciNodes;UsbNodes=usbNodes;AcpiNodes=acpiNodes;PlatformNodes=platformNodes;VirtualNodes=virtualNodes;LogicalNodes=logicalNodes; }
    public UInt64 Generation { get; } public UInt32 TotalNodes { get; } public UInt32 RootNodes { get; }
    public UInt32 PciNodes { get; } public UInt32 UsbNodes { get; } public UInt32 AcpiNodes { get; } public UInt32 PlatformNodes { get; } public UInt32 VirtualNodes { get; } public UInt32 LogicalNodes { get; }
}

public enum KernelDriverArchitecture : Byte { Any=0, X64=1, Arm64=2 }
public enum KernelDriverSigningState : Byte { Unsigned=0, Development=1, Signed=2, Trusted=3, Revoked=4 }
public enum KernelDriverPackageKind : Byte { Platform=1, Pci=2, Usb=3, Virtio=4 }
public enum KernelDriverBindingPolicy : Byte { Automatic=1, Manual=2, Disabled=3 }
public enum KernelDriverPayloadKind : Byte { KernelModule=1, UserlandService=2 }

/// <summary>Versioned package metadata required for every distributable Inu driver.</summary>
public readonly struct KernelDriverPackageManifest
{
    /// <summary>Compatibility constructor for schema-v3 manifests created before install/binding and payload metadata became first-class.</summary>
    public KernelDriverPackageManifest(UInt32 schemaVersion,String id,String name,String version,KernelDriverArchitecture architecture,String minimumInuVersion,String sdkApiVersion,String driverAbiVersion,String[] deviceIds,String[] dependencies,KernelDriverCapability permissions,KernelDriverSigningState signingState,String signingAlgorithm,String signerId,String signatureDigest)
        : this(schemaVersion,id,name,version,KernelDriverPackageKind.Platform,architecture,minimumInuVersion,sdkApiVersion,driverAbiVersion,deviceIds,dependencies,permissions,KernelDriverPayloadKind.KernelModule,"Driver.dll",KernelDriverBindingPolicy.Automatic,"/System/Drivers",signingState,signingAlgorithm,signerId,signatureDigest) { }

    public KernelDriverPackageManifest(UInt32 schemaVersion,String id,String name,String version,KernelDriverPackageKind kind,KernelDriverArchitecture architecture,String minimumInuVersion,String sdkApiVersion,String driverAbiVersion,String[] deviceIds,String[] dependencies,KernelDriverCapability permissions,KernelDriverPayloadKind payloadKind,String payloadPath,KernelDriverBindingPolicy bindingPolicy,String installRoot,KernelDriverSigningState signingState,String signingAlgorithm,String signerId,String signatureDigest)
    { SchemaVersion=schemaVersion;Id=id;Name=name;Version=version;Kind=kind;Architecture=architecture;MinimumInuVersion=minimumInuVersion;SdkApiVersion=sdkApiVersion;DriverAbiVersion=driverAbiVersion;DeviceIds=deviceIds;Dependencies=dependencies;Permissions=permissions;PayloadKind=payloadKind;PayloadPath=payloadPath;BindingPolicy=bindingPolicy;InstallRoot=installRoot;SigningState=signingState;SigningAlgorithm=signingAlgorithm;SignerId=signerId;SignatureDigest=signatureDigest; }
    public UInt32 SchemaVersion { get; }
    public String Id { get; }
    public String Name { get; }
    public String Version { get; }
    public KernelDriverPackageKind Kind { get; }
    public KernelDriverArchitecture Architecture { get; }
    public String MinimumInuVersion { get; }
    public String SdkApiVersion { get; }
    public String DriverAbiVersion { get; }
    public String[] DeviceIds { get; }
    public String[] Dependencies { get; }
    public KernelDriverCapability Permissions { get; }
    public KernelDriverPayloadKind PayloadKind { get; }
    public String PayloadPath { get; }
    public KernelDriverBindingPolicy BindingPolicy { get; }
    public String InstallRoot { get; }
    public KernelDriverSigningState SigningState { get; }
    public String SigningAlgorithm { get; }
    public String SignerId { get; }
    public String SignatureDigest { get; }
}

public readonly struct KernelDriverLifecycleEvent
{
    public KernelDriverLifecycleEvent(KernelDeviceHandle device,KernelDriverHandle driver,KernelDriverLifecycleStage stage,KernelDeviceState previousState,KernelDeviceState currentState,KernelDriverFailureCode failureCode,UInt64 sequence)
    { Device=device;Driver=driver;Stage=stage;PreviousState=previousState;CurrentState=currentState;FailureCode=failureCode;Sequence=sequence; }
    public KernelDeviceHandle Device { get; }
    public KernelDriverHandle Driver { get; }
    public KernelDriverLifecycleStage Stage { get; }
    public KernelDeviceState PreviousState { get; }
    public KernelDeviceState CurrentState { get; }
    public KernelDriverFailureCode FailureCode { get; }
    public UInt64 Sequence { get; }
}

public enum KernelDeviceStateEventKind : Byte { Arrived=1, StateChanged=2, DriverBound=3, DriverUnbound=4, Removed=5 }

/// <summary>Bounded hotplug/state event consumable by kernel services and the native Event(device.state.next) bridge.</summary>
public readonly struct KernelDeviceStateEvent
{
    public KernelDeviceStateEvent(UInt64 sequence,KernelDeviceStateEventKind kind,KernelDeviceHandle device,KernelDeviceHandle parent,KernelDriverHandle driver,KernelDeviceIdentifier identifier,KernelDeviceState previousState,KernelDeviceState currentState,KernelDriverFailureCode failure)
    { Sequence=sequence;Kind=kind;Device=device;Parent=parent;Driver=driver;Identifier=identifier;PreviousState=previousState;CurrentState=currentState;Failure=failure; }
    public UInt64 Sequence { get; }
    public KernelDeviceStateEventKind Kind { get; }
    public KernelDeviceHandle Device { get; }
    public KernelDeviceHandle Parent { get; }
    public KernelDriverHandle Driver { get; }
    public KernelDeviceIdentifier Identifier { get; }
    public KernelDeviceState PreviousState { get; }
    public KernelDeviceState CurrentState { get; }
    public KernelDriverFailureCode Failure { get; }
}

public readonly struct KernelDeviceEventQueueStatus
{
    public KernelDeviceEventQueueStatus(UInt32 queued,UInt64 dropped,UInt64 nextSequence){Queued=queued;Dropped=dropped;NextSequence=nextSequence;}
    public UInt32 Queued { get; }
    public UInt64 Dropped { get; }
    public UInt64 NextSequence { get; }
}

public readonly unsafe struct KernelDriverCallbacks
{
    public readonly delegate*<KernelDriverDeviceContext*, Boolean> Discover;
    public readonly delegate*<KernelDriverDeviceContext*, Boolean> Probe;
    public readonly delegate*<KernelDriverDeviceContext*, Boolean> Bind;
    public readonly delegate*<KernelDriverDeviceContext*, Boolean> Start;
    public readonly delegate*<KernelDriverDeviceContext*, Boolean> Stop;
    public readonly delegate*<KernelDriverDeviceContext*, Boolean> Reset;
    public readonly delegate*<KernelDriverDeviceContext*, Boolean> Suspend;
    public readonly delegate*<KernelDriverDeviceContext*, Boolean> Resume;
    public readonly delegate*<KernelDriverDeviceContext*, Boolean> Remove;
    public readonly delegate*<KernelDriverDeviceContext*, KernelDriverFailureCode, Boolean> Fail;
    public readonly delegate*<KernelDriverDeviceContext*, Boolean> Recover;
    public readonly delegate*<KernelDriverDeviceContext*, UInt64, Boolean> Interrupt;

    /// <summary>Compatibility constructor for existing Inu drivers. New lifecycle callbacks remain optional.</summary>
    public KernelDriverCallbacks(delegate*<KernelDriverDeviceContext*, Boolean> probe, delegate*<KernelDriverDeviceContext*, Boolean> start, delegate*<KernelDriverDeviceContext*, Boolean> stop, delegate*<KernelDriverDeviceContext*, Boolean> remove, delegate*<KernelDriverDeviceContext*, UInt64, Boolean> interrupt)
    { Discover=null;Probe=probe;Bind=null;Start=start;Stop=stop;Reset=null;Suspend=null;Resume=null;Remove=remove;Fail=null;Recover=null;Interrupt=interrupt; }

    /// <summary>Complete professional driver lifecycle contract.</summary>
    public KernelDriverCallbacks(delegate*<KernelDriverDeviceContext*, Boolean> discover,delegate*<KernelDriverDeviceContext*, Boolean> probe,delegate*<KernelDriverDeviceContext*, Boolean> bind,delegate*<KernelDriverDeviceContext*, Boolean> start,delegate*<KernelDriverDeviceContext*, Boolean> stop,delegate*<KernelDriverDeviceContext*, Boolean> reset,delegate*<KernelDriverDeviceContext*, Boolean> suspend,delegate*<KernelDriverDeviceContext*, Boolean> resume,delegate*<KernelDriverDeviceContext*, Boolean> remove,delegate*<KernelDriverDeviceContext*, KernelDriverFailureCode, Boolean> fail,delegate*<KernelDriverDeviceContext*, Boolean> recover,delegate*<KernelDriverDeviceContext*, UInt64, Boolean> interrupt)
    { Discover=discover;Probe=probe;Bind=bind;Start=start;Stop=stop;Reset=reset;Suspend=suspend;Resume=resume;Remove=remove;Fail=fail;Recover=recover;Interrupt=interrupt; }
}
