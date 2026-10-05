using System;
using Inu.Arch.X64;
using Inu.Kernel.Acpi;
using Inu.Kernel.Drivers;
using Inu.Kernel.Smp;

namespace Inu.Kernel.Power;

/// <summary>System ACPI power states that Inu can coordinate.</summary>
public enum KernelSystemPowerState : Byte { Working=0, SleepS1=1, SleepS3=3, HibernateS4=4, SoftOffS5=5 }
/// <summary>CPU-local power/idle states. C1 is implemented generically; deeper states require an ACPI/architecture provider.</summary>
public enum KernelCpuPowerState : Byte { RunningC0=0, IdleC1=1, IdleC2=2, IdleC3=3 }
/// <summary>Power-management operation result.</summary>
public enum KernelPowerStatus : Byte { Success=0, NotInitialized=1, Unsupported=2, InvalidState=3, DeviceSuspendFailed=4, FirmwareRejected=5, DeviceResumeFailed=6 }
/// <summary>Immutable system power capabilities discovered from ACPI and the driver framework.</summary>
public readonly struct KernelPowerCapabilities
{
    public KernelPowerCapabilities(Boolean shutdown,Boolean reboot,Boolean s1,Boolean s3,Boolean s4,Boolean c1,Boolean deviceSuspend)
    { Shutdown=shutdown;Reboot=reboot;SleepS1=s1;SleepS3=s3;HibernateS4=s4;CpuC1=c1;DeviceSuspendResume=deviceSuspend; }
    public Boolean Shutdown{get;} public Boolean Reboot{get;} public Boolean SleepS1{get;} public Boolean SleepS3{get;} public Boolean HibernateS4{get;} public Boolean CpuC1{get;} public Boolean DeviceSuspendResume{get;}
}

/// <summary>
/// Coordinates ACPI system power transitions with the Inu driver and SMP layers.
/// Drivers own device-specific save/restore; ACPI owns platform sleep/off/reset semantics.
/// </summary>
public static class KernelPowerManagement
{
    private static Boolean _initialized;
    private static KernelPowerStatus _lastStatus=KernelPowerStatus.NotInitialized;
    private static KernelSystemPowerState _systemState=KernelSystemPowerState.Working;

    /// <summary>Initializes the ACPI fixed-feature power controller.</summary>
    public static Boolean Initialize()
    { if(_initialized)return true; if(!KernelAcpiPowerServices.Initialize()){_lastStatus=KernelPowerStatus.Unsupported;return false;} _initialized=true;_lastStatus=KernelPowerStatus.Success;return true; }
    public static Boolean IsInitialized()=>_initialized;
    public static KernelPowerStatus GetLastStatus()=>_lastStatus;
    public static KernelSystemPowerState GetSystemState()=>_systemState;
    public static KernelPowerCapabilities GetCapabilities()
    {
        if(!_initialized)Initialize(); AcpiPowerCapabilities a=KernelAcpiPowerServices.GetCapabilities();
        return new KernelPowerCapabilities(a.ShutdownAvailable,a.ResetAvailable,KernelAcpiPowerServices.IsSleepStateAvailable(1U),KernelAcpiPowerServices.IsSleepStateAvailable(3U),KernelAcpiPowerServices.IsSleepStateAvailable(4U),true,true);
    }

    /// <summary>Requests ACPI S5 soft-off after suspending started devices.</summary>
    public static Boolean Shutdown()
    { if(!Ensure())return false; if(!KernelDrivers.SuspendStartedDevices()){_lastStatus=KernelPowerStatus.DeviceSuspendFailed;return false;} _systemState=KernelSystemPowerState.SoftOffS5; if(KernelAcpiPowerServices.Shutdown()){_lastStatus=KernelPowerStatus.Success;return true;} _systemState=KernelSystemPowerState.Working; KernelDrivers.ResumeSuspendedDevices();_lastStatus=KernelPowerStatus.FirmwareRejected;return false; }

    /// <summary>Requests firmware reset after quiescing devices.</summary>
    public static Boolean Reboot()
    { if(!Ensure())return false; if(!KernelDrivers.SuspendStartedDevices()){_lastStatus=KernelPowerStatus.DeviceSuspendFailed;return false;} if(KernelAcpiPowerServices.Reboot()){_lastStatus=KernelPowerStatus.Success;return true;} KernelDrivers.ResumeSuspendedDevices();_lastStatus=KernelPowerStatus.FirmwareRejected;return false; }

    /// <summary>
    /// Enters an ACPI sleep state. S1 is immediately resume-capable on the current execution context.
    /// S3/S4 are exposed only when firmware advertises them; platform resume-vector support must also be configured.
    /// </summary>
    public static Boolean Sleep(KernelSystemPowerState state)
    {
        if(!Ensure())return false; UInt32 acpiState=state==KernelSystemPowerState.SleepS1?1U:state==KernelSystemPowerState.SleepS3?3U:state==KernelSystemPowerState.HibernateS4?4U:0U;
        if(acpiState==0U){_lastStatus=KernelPowerStatus.InvalidState;return false;} if(!KernelAcpiPowerServices.IsSleepStateAvailable(acpiState)){_lastStatus=KernelPowerStatus.Unsupported;return false;}
        if((acpiState==3U||acpiState==4U)&&!KernelAcpiPowerServices.HasResumeVector()){_lastStatus=KernelPowerStatus.Unsupported;return false;}
        if(!KernelDrivers.SuspendStartedDevices()){_lastStatus=KernelPowerStatus.DeviceSuspendFailed;return false;}
        _systemState=state;
        if(!KernelAcpiPowerServices.Sleep(acpiState)){_systemState=KernelSystemPowerState.Working;KernelDrivers.ResumeSuspendedDevices();_lastStatus=KernelPowerStatus.FirmwareRejected;return false;}
        _systemState=KernelSystemPowerState.Working;
        if(!KernelDrivers.ResumeSuspendedDevices()){_lastStatus=KernelPowerStatus.DeviceResumeFailed;return false;}
        _lastStatus=KernelPowerStatus.Success;return true;
    }

    /// <summary>Enters one formal system state through the same coordinated policy used by Shutdown/Reboot/Sleep.</summary>
    public static Boolean TryEnterSystemState(KernelSystemPowerState state)
    { if(state==KernelSystemPowerState.SoftOffS5)return Shutdown();if(state==KernelSystemPowerState.SleepS1||state==KernelSystemPowerState.SleepS3||state==KernelSystemPowerState.HibernateS4)return Sleep(state);if(state==KernelSystemPowerState.Working){_systemState=state;_lastStatus=KernelPowerStatus.Success;return true;}_lastStatus=KernelPowerStatus.InvalidState;return false; }
    public static Boolean TryGetSystemPowerState(out KernelSystemPowerState state){state=_systemState;return _initialized;}

    /// <summary>Registers a firmware-visible wake vector required before S3/S4 may be attempted.</summary>
    public static Boolean ConfigureResumeVector(UInt32 physicalAddress)=>Ensure()&&KernelAcpiPowerServices.ConfigureResumeVector(physicalAddress);

    /// <summary>Gets one processor's currently observable CPU power state.</summary>
    public static Boolean TryGetCpuPowerState(UInt32 cpu,out KernelCpuPowerState state)
    { state=KernelCpuPowerState.RunningC0;return KernelSmp.TryGetProcessor(cpu,out _); }
    /// <summary>Requests a CPU power state. C0 is passive; C1 may be entered only by the calling CPU. C2/C3 require a future ACPI processor-power provider.</summary>
    public static Boolean TrySetCpuPowerState(UInt32 cpu,KernelCpuPowerState state)
    { if(!KernelSmp.TryGetCurrentProcessorIndex(out UInt32 current)||!KernelSmp.TryGetProcessor(cpu,out _))return false;if(state==KernelCpuPowerState.RunningC0)return true;if(cpu!=current){_lastStatus=KernelPowerStatus.Unsupported;return false;}return EnterCurrentCpuIdle(state); }

    /// <summary>Gets the current processor's logical identity and reports its requested CPU power state.</summary>
    public static Boolean TryGetCurrentCpuPowerState(out UInt32 cpu,out KernelCpuPowerState state)
    { cpu=0U;state=KernelCpuPowerState.RunningC0;if(!KernelSmp.TryGetCurrentProcessorIndex(out cpu))return false;return true; }

    /// <summary>Enters ACPI/architectural C1 on the current CPU until the next interrupt.</summary>
    public static Boolean EnterCurrentCpuIdle(KernelCpuPowerState state)
    { if(state!=KernelCpuPowerState.IdleC1){_lastStatus=KernelPowerStatus.Unsupported;return false;} if(!X64ArchitectureBoundary.Halt())return false;_lastStatus=KernelPowerStatus.Success;return true; }

    /// <summary>Suspends one device through its registered driver lifecycle callback.</summary>
    public static Boolean SuspendDevice(UInt64 deviceId)=>deviceId<=UInt32.MaxValue&&Ensure()&&KernelDrivers.SuspendDevice(new KernelDeviceHandle((UInt32)deviceId));
    /// <summary>Resumes one device through its registered driver lifecycle callback.</summary>
    public static Boolean ResumeDevice(UInt64 deviceId)=>deviceId<=UInt32.MaxValue&&Ensure()&&KernelDrivers.ResumeDevice(new KernelDeviceHandle((UInt32)deviceId));
    private static Boolean Ensure(){if(_initialized)return true;return Initialize();}
}
