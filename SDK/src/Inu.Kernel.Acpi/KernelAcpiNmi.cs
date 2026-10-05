using System;
using System.Runtime.InteropServices;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Acpi;

/// <summary>Snapshot of the firmware NMI state captured before Inu takes ownership of x64 interrupt routing.</summary>
public readonly struct AcpiNmiDiagnostics
{
    internal AcpiNmiDiagnostics(Boolean available, UInt64 count, UInt64 apicBase, UInt32 lint0, UInt32 lint1, UInt32 thermal, UInt32 performance, Byte port61,
        UInt32 lastLint0, UInt32 lastLint1, UInt32 lastThermal, UInt32 lastPerformance, UInt32 lastEsr, Byte lastPort61, Boolean firmwareNmiArmed, Boolean legacyFaultAsserted)
    { Available=available; Count=count; InitialApicBase=apicBase; InitialLint0=lint0; InitialLint1=lint1; InitialThermal=thermal; InitialPerformance=performance; InitialPort61=port61;
      LastLint0=lastLint0; LastLint1=lastLint1; LastThermal=lastThermal; LastPerformance=lastPerformance; LastErrorStatus=lastEsr; LastPort61=lastPort61;
      FirmwareNmiArmed=firmwareNmiArmed; LegacyParityOrChannelCheckAsserted=legacyFaultAsserted; }
    public Boolean Available { get; }
    public UInt64 Count { get; }
    public UInt64 InitialApicBase { get; }
    public UInt32 InitialLint0 { get; }
    public UInt32 InitialLint1 { get; }
    public UInt32 InitialThermal { get; }
    public UInt32 InitialPerformance { get; }
    public Byte InitialPort61 { get; }
    public UInt32 LastLint0 { get; }
    public UInt32 LastLint1 { get; }
    public UInt32 LastThermal { get; }
    public UInt32 LastPerformance { get; }
    public UInt32 LastErrorStatus { get; }
    public Byte LastPort61 { get; }
    public Boolean FirmwareNmiArmed { get; }
    public Boolean LegacyParityOrChannelCheckAsserted { get; }
}

/// <summary>Owns MADT-defined NMI routing after the native entry has quiesced firmware-owned NMI sources.</summary>
public static unsafe class KernelAcpiNmi
{
    private const UInt32 ApicBaseMsr=0x1BU;
    private const UInt64 ApicEnabled=1UL<<11, X2ApicEnabled=1UL<<10;
    private const UInt64 LocalApicLint0=0x350UL,LocalApicLint1=0x360UL,LocalApicThermal=0x330UL,LocalApicPerformance=0x340UL;
    private const UInt32 X2ApicLint0Msr=0x835U,X2ApicLint1Msr=0x836U,X2ApicThermalMsr=0x833U,X2ApicPerformanceMsr=0x834U;
    private const UInt32 LvtDeliveryMask=7U<<8,LvtDeliveryNmi=4U<<8,LvtPolarityLow=1U<<13,LvtTriggerLevel=1U<<15,LvtMasked=1U<<16;
    private const UInt64 DiagnosticMagic=0x0031494D4E554E49UL;

    [StructLayout(LayoutKind.Sequential,Pack=8)]
    private struct NativeNmiState
    {
        internal UInt64 Magic,Count,InitialApicBase,InitialLint0,InitialLint1,InitialThermal,InitialPerformance,InitialPort61;
        internal UInt64 LastApicBase,LastLint0,LastLint1,LastThermal,LastPerformance,LastEsr,LastPort61,Flags;
    }

    private static Boolean _initialized,_legacyGateUnmasked;
    private static UInt32 _appliedLocalEntries;

    /// <summary>Applies MADT processor-local NMI routing to the bootstrap processor and re-enables the PC NMI gate only after Inu owns the IDT.</summary>
    public static Boolean InitializeBootstrapProcessor()
    {
        if(_initialized)return true;
        if(!KernelAcpi.IsInitialized()||!ConfigureCurrentProcessor())return false;
        // Native entry deliberately selected CMOS register zero while masking the PC/AT NMI gate.
        // Re-enable it only when the inherited chipset status does not already report a
        // parity/channel-check condition. An asserted source remains quarantined for diagnosis.
        AcpiNmiDiagnostics diagnostics=GetDiagnostics();
        if(!diagnostics.LegacyParityOrChannelCheckAsserted)
        {
            if(!Native.WritePort8((UInt16)0x70,(Byte)0x00))return false;
            _legacyGateUnmasked=true;
        }
        _initialized=true;
        return true;
    }

    /// <summary>Applies MADT processor-local NMI routing to the processor executing this call. Used again after INIT/SIPI resets each AP's LAPIC.</summary>
    public static Boolean ConfigureCurrentProcessor()
    {
        if(!KernelAcpi.IsInitialized())return false;
        UInt64 apicBaseMsr=Native.ReadModelSpecificRegister(ApicBaseMsr);
        if((apicBaseMsr&ApicEnabled)==0UL)
        {
            if(!Native.WriteModelSpecificRegister(ApicBaseMsr,apicBaseMsr|ApicEnabled))return false;
            apicBaseMsr=Native.ReadModelSpecificRegister(ApicBaseMsr);
            if((apicBaseMsr&ApicEnabled)==0UL)return false;
        }
        Boolean x2=(apicBaseMsr&X2ApicEnabled)!=0UL;
        UInt64 mmioBase=apicBaseMsr&0x0000000FFFFFF000UL;
        if(!x2&&mmioBase==0UL&&!KernelAcpi.TryGetLocalApicAddress(out mmioBase))return false;
        UInt32 currentApicId=Native.GetCurrentApicId();
        if(!TryFindCurrentProcessor(currentApicId,out AcpiProcessorInfo processor))return false;

        // Start from a known safe state. Firmware/AP reset policy is not Inu policy.
        if(!WriteLvt(x2,mmioBase,0,ReadLvt(x2,mmioBase,0)|LvtMasked))return false;
        if(!WriteLvt(x2,mmioBase,1,ReadLvt(x2,mmioBase,1)|LvtMasked))return false;
        if(!WriteAuxiliaryLvtMasked(x2,mmioBase,true)||!WriteAuxiliaryLvtMasked(x2,mmioBase,false))return false;

        UInt32 applied=0U,count=KernelAcpi.GetLocalNmiCount();
        for(UInt32 index=0U;index<count;index++)
        {
            if(!KernelAcpi.TryGetLocalNmi(index,out AcpiLocalNmiInfo entry))return false;
            if(entry.Lint>1U||entry.IsX2Apic!=processor.IsX2Apic||!AppliesToProcessor(entry,processor.AcpiUid))continue;
            UInt32 current=ReadLvt(x2,mmioBase,entry.Lint);
            UInt32 configured=current&~(LvtDeliveryMask|LvtPolarityLow|LvtTriggerLevel|LvtMasked);
            configured|=LvtDeliveryNmi;
            UInt16 polarity=(UInt16)(entry.Flags&3U),trigger=(UInt16)((entry.Flags>>2)&3U);
            if(polarity==3U)configured|=LvtPolarityLow; else if(polarity==2U)return false;
            if(trigger==3U)configured|=LvtTriggerLevel; else if(trigger==2U)return false;
            if(!WriteLvt(x2,mmioBase,entry.Lint,configured))return false;
            applied++;
        }
        if(_appliedLocalEntries<applied)_appliedLocalEntries=applied;
        return true;
    }

    /// <summary>Gets the number of MADT local-NMI declarations applied to a processor so far.</summary>
    public static UInt32 GetAppliedLocalNmiCount()=>_appliedLocalEntries;
    /// <summary>Gets whether bootstrap NMI takeover completed.</summary>
    public static Boolean IsInitialized()=>_initialized;
    /// <summary>Gets whether Inu safely reopened the legacy PC/AT NMI gate after takeover.</summary>
    public static Boolean IsLegacyNmiGateUnmasked()=>_legacyGateUnmasked;

    /// <summary>Gets the native pre-takeover and most-recent NMI hardware snapshot without allocating.</summary>
    public static AcpiNmiDiagnostics GetDiagnostics()
    {
        UInt64 address=Native.GetNmiDiagnosticStateAddress();
        if(address==0UL)return default;
        NativeNmiState* state=(NativeNmiState*)(nuint)address;
        Boolean available=state->Magic==DiagnosticMagic;
        UInt32 lint0=(UInt32)state->InitialLint0,lint1=(UInt32)state->InitialLint1,thermal=(UInt32)state->InitialThermal,performance=(UInt32)state->InitialPerformance;
        Boolean armed=IsUnmaskedNmi(lint0)||IsUnmaskedNmi(lint1)||IsUnmaskedNmi(thermal)||IsUnmaskedNmi(performance);
        Byte initialPort=(Byte)state->InitialPort61,lastPort=(Byte)state->LastPort61;
        Boolean legacyFault=((((UInt32)initialPort|(UInt32)lastPort)&0xC0U)!=0U);
        return new AcpiNmiDiagnostics(available,state->Count,state->InitialApicBase,lint0,lint1,thermal,performance,initialPort,
            (UInt32)state->LastLint0,(UInt32)state->LastLint1,(UInt32)state->LastThermal,(UInt32)state->LastPerformance,(UInt32)state->LastEsr,lastPort,armed,legacyFault);
    }

    private static Boolean TryFindCurrentProcessor(UInt32 apicId,out AcpiProcessorInfo processor)
    {
        processor=default;UInt32 count=KernelAcpi.GetProcessorCount();
        for(UInt32 i=0U;i<count;i++)if(KernelAcpi.TryGetProcessor(i,out AcpiProcessorInfo candidate)&&candidate.ApicId==apicId){processor=candidate;return true;}
        return false;
    }
    private static Boolean AppliesToProcessor(AcpiLocalNmiInfo entry,UInt32 uid)
        => entry.IsX2Apic ? entry.ProcessorUid==0xFFFFFFFFU||entry.ProcessorUid==uid : entry.ProcessorUid==0xFFU||entry.ProcessorUid==uid;
    private static Boolean IsUnmaskedNmi(UInt32 value)=> (value&LvtMasked)==0U && (value&LvtDeliveryMask)==LvtDeliveryNmi;
    private static UInt32 ReadLvt(Boolean x2,UInt64 baseAddress,Byte lint)
        => x2?(UInt32)Native.ReadModelSpecificRegister(lint==0U?X2ApicLint0Msr:X2ApicLint1Msr):Native.ReadMmio32(baseAddress+(lint==0U?LocalApicLint0:LocalApicLint1));
    private static Boolean WriteLvt(Boolean x2,UInt64 baseAddress,Byte lint,UInt32 value)
        => x2?Native.WriteModelSpecificRegister(lint==0U?X2ApicLint0Msr:X2ApicLint1Msr,value):Native.WriteMmio32(baseAddress+(lint==0U?LocalApicLint0:LocalApicLint1),value);
    private static Boolean WriteAuxiliaryLvtMasked(Boolean x2,UInt64 baseAddress,Boolean thermal)
    {
        if(x2){UInt32 msr=thermal?X2ApicThermalMsr:X2ApicPerformanceMsr;UInt32 value=(UInt32)Native.ReadModelSpecificRegister(msr);return Native.WriteModelSpecificRegister(msr,value|LvtMasked);}
        UInt64 offset=thermal?LocalApicThermal:LocalApicPerformance;return Native.WriteMmio32(baseAddress+offset,Native.ReadMmio32(baseAddress+offset)|LvtMasked);
    }
}
