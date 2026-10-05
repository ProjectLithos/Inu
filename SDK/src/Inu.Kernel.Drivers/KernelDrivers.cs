using System;
using Inu.Kernel.Contracts;
using Inu.Kernel.Heap;

namespace Inu.Kernel.Drivers;

/// <summary>Provides the heap-backed Inu driver registry, matcher, lifecycle manager, resources, and interrupt broker.</summary>
public static unsafe partial class KernelDrivers
{
    private const Int32 ResourcesPerDevice=8;
    private const Int32 PropertiesPerDevice=8;
    private const Int32 GrantsPerDevice=16;
    private const Int32 InterruptsPerDevice=8;
    private const Int32 DriverNameBytes=48;
    private const UInt32 DeviceEventCapacity=64U;
    private struct DriverRecord { internal Byte Used,State,Bus,MatchBus,MatchVendor,MatchDevice,NameLength; internal UInt16 Vendor,Device; internal UInt32 ClassCode,ClassMask; internal UInt64 DeclaredCapabilities,Discover,Probe,Bind,Start,Stop,Reset,Suspend,Resume,Remove,Fail,Recover,Interrupt; internal fixed Byte Name[DriverNameBytes]; }
    private struct DeviceRecord
    {
        internal Byte Used,State,Bus,Revision,ResourceCount,PropertyCount,GrantCount,InterruptCount;
        internal UInt16 Vendor,Device,SubsystemVendor,Subsystem;
        internal UInt32 ClassCode,Location,BoundDriver,Parent,FirstChild,NextSibling,FailureCode;
        internal fixed Byte ResourceType[ResourcesPerDevice]; internal fixed UInt64 ResourceStart[ResourcesPerDevice]; internal fixed UInt64 ResourceLength[ResourcesPerDevice]; internal fixed UInt64 ResourceFlags[ResourcesPerDevice];
        internal fixed UInt32 PropertyKey[PropertiesPerDevice]; internal fixed UInt64 PropertyValue0[PropertiesPerDevice]; internal fixed UInt64 PropertyValue1[PropertiesPerDevice];
        internal fixed UInt64 GrantToken[GrantsPerDevice]; internal fixed UInt64 GrantCapability[GrantsPerDevice]; internal fixed UInt64 GrantStart[GrantsPerDevice]; internal fixed UInt64 GrantLength[GrantsPerDevice]; internal fixed Byte GrantAccess[GrantsPerDevice];
        internal fixed UInt64 InterruptHandle[InterruptsPerDevice];
    }
    private struct DeviceEventRecord
    {
        internal UInt64 Sequence; internal Byte Kind,PreviousState,CurrentState,Bus,Revision; internal UInt16 Vendor,Device,SubsystemVendor,Subsystem; internal UInt32 Handle,Parent,Driver,ClassCode,Location,Failure;
    }

    private static DriverRecord* _drivers; private static DeviceRecord* _devices; private static DeviceEventRecord* _deviceEvents;
    private static KernelHeapAllocation _driverAllocation,_deviceAllocation,_deviceEventAllocation;
    private static UInt32 _driverCapacity,_deviceCapacity,_maximumDrivers,_maximumDevices;
    private static KernelDriverRegistryMode _mode; private static Boolean _initialized;
    private static UInt32 _driverCount,_deviceCount,_boundCount,_startedCount;
    private static UInt32 _deviceEventHead,_deviceEventTail,_deviceEventCount; private static UInt64 _deviceEventsDropped,_deviceEventSequence=1UL;
    private static UInt64 _interruptRequestBroker,_interruptReleaseBroker; private static UInt64 _nextCapabilityGrantToken=1UL,_lifecycleSequence=1UL; private static UInt64 _lifecycleSink,_deviceTreeGeneration=1UL;

    /// <summary>Initializes a dynamically growing registry backed by the already-online kernel heap.</summary>
    public static Boolean Initialize() => Initialize(KernelDriverFrameworkOptions.DynamicDefault);

    /// <summary>Initializes dynamic or explicitly bounded registry storage from the kernel heap.</summary>
    public static Boolean Initialize(KernelDriverFrameworkOptions options)
    {
        if(_initialized)return true;
        if(!KernelHeap.IsInitialized()||!KernelDriverMath.IsValidOptions(options))return false;
        _mode=options.RegistryMode; _maximumDrivers=options.MaximumDriverCapacity; _maximumDevices=options.MaximumDeviceCapacity;
        if(!AllocateDriverTable(options.InitialDriverCapacity,out _driverAllocation,out _drivers))return false;
        if(!AllocateDeviceTable(options.InitialDeviceCapacity,out _deviceAllocation,out _devices)){KernelHeap.TryRelease(_driverAllocation);_drivers=null;_driverAllocation=default;return false;}
        if(!AllocateDeviceEventTable(out _deviceEventAllocation,out _deviceEvents)){KernelHeap.TryRelease(_deviceAllocation);KernelHeap.TryRelease(_driverAllocation);_devices=null;_drivers=null;_deviceAllocation=default;_driverAllocation=default;return false;}
        _driverCapacity=options.InitialDriverCapacity; _deviceCapacity=options.InitialDeviceCapacity;
        _driverCount=0U;_deviceCount=0U;_boundCount=0U;_startedCount=0U;_deviceTreeGeneration=1UL;_deviceEventHead=_deviceEventTail=_deviceEventCount=0U;_deviceEventsDropped=0UL;_deviceEventSequence=1UL;_interruptRequestBroker=0UL;_interruptReleaseBroker=0UL;_initialized=true;return true;
    }

    public static Boolean IsInitialized()=>_initialized;
    public static KernelDriverCapabilities GetCapabilities()=>new(_initialized,_mode,_driverCount,_deviceCount,_boundCount,_startedCount,_driverCapacity,_deviceCapacity,_maximumDrivers,_maximumDevices,ResourcesPerDevice,_interruptRequestBroker!=0UL&&_interruptReleaseBroker!=0UL);
}
