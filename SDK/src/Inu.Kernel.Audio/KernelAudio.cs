using System;

namespace Inu.Kernel.Audio;

public readonly struct KernelAudioCapabilities
{
    public KernelAudioCapabilities(Boolean initialized, UInt32 devices, UInt32 outputDevices, UInt32 inputDevices, UInt32 maximumStreams)
    { Initialized=initialized; Devices=devices; OutputDevices=outputDevices; InputDevices=inputDevices; MaximumStreams=maximumStreams; }
    public Boolean Initialized { get; } public UInt32 Devices { get; } public UInt32 OutputDevices { get; } public UInt32 InputDevices { get; } public UInt32 MaximumStreams { get; }
}

/// <summary>Kernel audio framework. Hardware drivers register discovered playback/capture endpoints here.</summary>
public static class KernelAudio
{
    private const UInt32 MaximumAudioDevices=32U;
    private static Boolean _initialized; private static UInt32 _devices,_outputs,_inputs;
    public static Boolean Initialize(){_initialized=true;return true;}
    public static Boolean IsInitialized()=>_initialized;
    public static KernelAudioCapabilities GetCapabilities()=>new KernelAudioCapabilities(_initialized,_devices,_outputs,_inputs,64U);
    public static Boolean RegisterDevice(Boolean playback,Boolean capture)
    {
        if(!_initialized||_devices>=MaximumAudioDevices||(!playback&&!capture))return false;
        _devices++; if(playback)_outputs++; if(capture)_inputs++; return true;
    }
    public static Boolean UnregisterAllDevices(){_devices=0U;_outputs=0U;_inputs=0U;return true;}
}
