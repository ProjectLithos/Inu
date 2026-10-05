#if INU_COMPONENT_ACPI_POWER
using System;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Acpi;

/// <summary>Implements ACPI fixed-feature power-button, reset and S5 shutdown services.</summary>
public static unsafe class KernelAcpiPowerProvider
{
    private const UInt16 PowerButtonBit = (UInt16)(1U << 8);
    private const UInt16 SleepEnableBit = (UInt16)(1U << 13);
    private static Boolean _initialized;
    private static Boolean _hasS5;
    private static Byte _s5a;
    private static Byte _s5b;
    private static Boolean _hasS1; private static Byte _s1a; private static Byte _s1b;
    private static Boolean _hasS3; private static Byte _s3a; private static Byte _s3b;
    private static Boolean _hasS4; private static Byte _s4a; private static Byte _s4b;
    private static UInt32 _resumeVector;
    public static Boolean Register() => KernelAcpiPowerServices.Register(&InitializeProvider, &GetCapabilitiesProvider, &TryConsumePowerButtonProvider, &IsSleepStateAvailableProvider, &HasResumeVectorProvider, &ConfigureResumeVectorProvider, &SleepProvider, &RebootProvider, &ShutdownProvider);

    /// <summary>Initializes FADT power management and discovers AML sleep types.</summary>
    private static Boolean InitializeProvider()
    {
        if (_initialized) return true;
        if (!KernelAcpiFadtServices.Initialize() || !KernelAcpiFadtServices.TryGetInfo(out AcpiFadtInfo f)) return false;
        if (f.SmiCommandPort != 0U && f.AcpiEnableValue != 0U && f.SmiCommandPort <= 0xFFFFU) Native.WritePort8((UInt16)f.SmiCommandPort, f.AcpiEnableValue);
        _hasS1 = TryFindSleepState(f.DsdtAddress, (Byte)'1', out _s1a, out _s1b);
        _hasS3 = TryFindSleepState(f.DsdtAddress, (Byte)'3', out _s3a, out _s3b);
        _hasS4 = TryFindSleepState(f.DsdtAddress, (Byte)'4', out _s4a, out _s4b);
        _hasS5 = TryFindSleepState(f.DsdtAddress, (Byte)'5', out _s5a, out _s5b);
        _initialized = true;
        EnablePowerButton();
        return true;
    }
    /// <summary>Gets fixed-feature power-management capabilities.</summary>
    private static Boolean GetCapabilitiesProvider(AcpiPowerCapabilities* output)
    {
        if (output == null || !KernelAcpiFadtServices.TryGetInfo(out AcpiFadtInfo f)) return false;
        *output = new AcpiPowerCapabilities(_initialized, _initialized && f.Pm1aEvent.IsPresent(), _initialized && f.SupportsReset(), _initialized && _hasS5 && f.Pm1aControl.IsPresent(), _s5a, _s5b); return true;
    }
    /// <summary>Gets whether the fixed-feature power button has asserted its status bit and clears it when set.</summary>
    private static Boolean TryConsumePowerButtonProvider(Boolean* output)
    {
        if(output==null)return false;*output=false;if(!_initialized||!KernelAcpiFadtServices.TryGetInfo(out AcpiFadtInfo f)) return false; if(!f.Pm1aEvent.IsPresent() || f.Pm1EventLength<4U) return false;
        AcpiGenericAddress status=new AcpiGenericAddress(f.Pm1aEvent.AddressSpace,(Byte)16,(Byte)0,(Byte)2,f.Pm1aEvent.Address);
        if(!KernelAcpiRegisterServices.Read(status,out UInt64 raw)) return false; Boolean pressed=(((UInt16)raw)&PowerButtonBit)!=0U; *output=pressed;
        if(pressed && !KernelAcpiRegisterServices.Write(status,PowerButtonBit)) return false; return true;
    }

    /// <summary>Gets whether the requested ACPI sleep package (_S1/_S3/_S4/_S5) was discovered.</summary>
    private static Boolean IsSleepStateAvailableProvider(UInt32 state)
    { return _initialized && (state==1U?_hasS1:state==3U?_hasS3:state==4U?_hasS4:state==5U?_hasS5:false); }
    /// <summary>Gets whether a firmware waking vector has been configured for context-losing sleep states.</summary>
    private static Boolean HasResumeVectorProvider()=>_resumeVector!=0U;
    /// <summary>Programs the FACS 32-bit firmware waking vector used by S3/S4 resume.</summary>
    private static Boolean ConfigureResumeVectorProvider(UInt32 physicalAddress)
    {
        if(!_initialized||physicalAddress==0U)return false;if(!KernelAcpiFadtServices.TryGetInfo(out AcpiFadtInfo f))return false;if(f.FirmwareControlAddress==0UL)return false;Byte* facs=(Byte*)f.FirmwareControlAddress;
        if(Read32(facs)!=0x53434146U)return false;UInt32 length=Read32(facs+4U);if(length<16U)return false;*(UInt32*)(facs+12U)=physicalAddress;_resumeVector=physicalAddress;return true;
    }
    /// <summary>Requests ACPI sleep using AML-discovered sleep types. S3/S4 require a configured waking vector.</summary>
    private static Boolean SleepProvider(UInt32 state)
    {
        if(!_initialized)return false;Byte a=0,b=0;Boolean available=false;
        if(state==1U){available=_hasS1;a=_s1a;b=_s1b;}else if(state==3U){available=_hasS3;a=_s3a;b=_s3b;if(_resumeVector==0U)return false;}else if(state==4U){available=_hasS4;a=_s4a;b=_s4b;if(_resumeVector==0U)return false;}else return false;
        if(!available)return false;return WriteSleepControl(a,b);
    }

    /// <summary>Requests firmware-defined ACPI reset through the FADT reset register.</summary>
    private static Boolean RebootProvider()
    {
        if(!_initialized) return false; if(!KernelAcpiFadtServices.TryGetInfo(out AcpiFadtInfo f))return false; if(!f.SupportsReset()) return false; return KernelAcpiRegisterServices.Write(f.ResetRegister,f.ResetValue);
    }
    /// <summary>Requests ACPI S5 soft-off using AML-discovered sleep types.</summary>
    private static Boolean ShutdownProvider()
    {
        if(!_initialized || !_hasS5) return false; return WriteSleepControl(_s5a,_s5b);
    }
    private static Boolean EnablePowerButton()
    {
        if(!KernelAcpiFadtServices.TryGetInfo(out AcpiFadtInfo f))return false; if(!f.Pm1aEvent.IsPresent() || f.Pm1EventLength<4U) return false;
        AcpiGenericAddress enable=new AcpiGenericAddress(f.Pm1aEvent.AddressSpace,(Byte)16,(Byte)0,(Byte)2,f.Pm1aEvent.Address+(UInt64)(f.Pm1EventLength/2U));
        if(!KernelAcpiRegisterServices.Read(enable,out UInt64 raw)) return false; return KernelAcpiRegisterServices.Write(enable,(UInt16)((UInt16)raw|PowerButtonBit));
    }
    private static Boolean WriteSleepControl(Byte sleepTypeA,Byte sleepTypeB)
    {
        if(!KernelAcpiFadtServices.TryGetInfo(out AcpiFadtInfo f))return false;if(!f.Pm1aControl.IsPresent())return false;
        if(!KernelAcpiRegisterServices.Read(f.Pm1aControl,out UInt64 a))return false;UInt16 av=(UInt16)(((UInt16)a&~(UInt16)(7U<<10))|((UInt16)(sleepTypeA&7U)<<10)|SleepEnableBit);if(!KernelAcpiRegisterServices.Write(f.Pm1aControl,av))return false;
        if(f.Pm1bControl.IsPresent()){if(!KernelAcpiRegisterServices.Read(f.Pm1bControl,out UInt64 b))return false;UInt16 bv=(UInt16)(((UInt16)b&~(UInt16)(7U<<10))|((UInt16)(sleepTypeB&7U)<<10)|SleepEnableBit);if(!KernelAcpiRegisterServices.Write(f.Pm1bControl,bv))return false;}return true;
    }
    private static Boolean TryFindSleepState(UInt64 dsdtAddress,Byte stateDigit,out Byte a,out Byte b)
    {
        a=(Byte)0;b=(Byte)0;if(dsdtAddress==0UL)return false;Byte* t=(Byte*)dsdtAddress;UInt32 length=Read32(t+4U);if(length<36U||length>16U*1024U*1024U)return false;
        for(UInt32 i=36U;i+5U<length;i++)if(t[i]==0x5FU&&t[i+1U]==0x53U&&t[i+2U]==stateDigit&&t[i+3U]==0x5FU)
        {UInt32 p=i+4U;if(p<length&&t[p]==0x12U){p++;if(!SkipPkgLength(t,length,ref p)||p>=length)return false;p++;if(!ReadAmlInteger(t,length,ref p,out UInt64 x)||!ReadAmlInteger(t,length,ref p,out UInt64 y))return false;a=(Byte)x;b=(Byte)y;return true;}}return false;
    }
    private static Boolean SkipPkgLength(Byte* t,UInt32 length,ref UInt32 p){if(p>=length)return false;Byte lead=t[p++];UInt32 follow=(UInt32)(lead>>6);if(follow==0U)return true;if(p+follow>length)return false;p+=follow;return true;}
    private static Boolean ReadAmlInteger(Byte* t,UInt32 length,ref UInt32 p,out UInt64 v){v=0UL;if(p>=length)return false;Byte op=t[p++];if(op==0x00U){v=0;return true;}if(op==0x01U){v=1;return true;}if(op==0x0AU&&p<length){v=t[p++];return true;}if(op==0x0BU&&p+2U<=length){v=(UInt64)(t[p]|((UInt16)t[p+1U]<<8));p+=2U;return true;}if(op==0x0CU&&p+4U<=length){v=Read32(t+p);p+=4U;return true;}return false;}
    private static UInt32 Read32(Byte* p)=>(UInt32)(p[0]|((UInt32)p[1]<<8)|((UInt32)p[2]<<16)|((UInt32)p[3]<<24));
}

#endif
