#if INU_COMPONENT_ACPI_EMBEDDED_CONTROLLER
using System;
using Inu.Kernel.Internal.X64;

namespace Inu.Kernel.Acpi;

/// <summary>Implements the ACPI Embedded Controller byte command protocol for ECDT-described controllers.</summary>
public static unsafe class KernelAcpiEcProvider
{
    private const UInt32 EcdtSignature = 0x54444345U;
    private const Byte ReadCommand = 0x80;
    private const Byte WriteCommand = 0x81;
    private const Byte OutputBufferFull = 0x01;
    private const Byte InputBufferFull = 0x02;
    private const UInt32 WaitLimit = 1000000U;
    private static Boolean _initialized;
    private static AcpiEcInfo _info;
    public static Boolean Register()=>KernelAcpiEcServices.Register(&InitializeProvider,&GetInfoProvider,&TryReadProvider,&TryWriteProvider);

    /// <summary>Initializes the first ECDT-described embedded controller.</summary>
    private static Boolean InitializeProvider()
    {
        if (_initialized) return true;
        if (!KernelAcpi.TryGetTable(EcdtSignature, out UInt64 address, out UInt32 length) || length < 65U) return false;
        Byte* t = (Byte*)address;
        AcpiGenericAddress control = KernelAcpiRegisterServices.ReadGas(t + 36U);
        AcpiGenericAddress data = KernelAcpiRegisterServices.ReadGas(t + 48U);
        if (!control.IsPresent() || !data.IsPresent()) return false;
        _info = new AcpiEcInfo(control, data, Read32(t + 60U), t[64]);
        _initialized = true;
        return true;
    }
    /// <summary>Gets whether an ECDT embedded controller is available.</summary>
    /// <summary>Gets the ECDT controller information.</summary>
    private static Boolean GetInfoProvider(AcpiEcInfo* output){if(output==null||!_initialized)return false;*output=_info;return true;}
    /// <summary>Reads one byte from EC address space.</summary>
    private static Boolean TryReadProvider(Byte address, Byte* output)
    {
        if(output==null)return false; *output=(Byte)0; if (!_initialized || !WaitInputEmpty() || !KernelAcpiRegisterServices.Write(_info.Control, ReadCommand) || !WaitInputEmpty() || !KernelAcpiRegisterServices.Write(_info.Data, address) || !WaitOutputFull()) return false;
        if(!KernelAcpiRegisterServices.Read(_info.Data,out UInt64 raw))return false;*output=(Byte)raw;return true;
    }
    /// <summary>Writes one byte to EC address space.</summary>
    private static Boolean TryWriteProvider(Byte address, Byte value)
    {
        if (!_initialized || !WaitInputEmpty() || !KernelAcpiRegisterServices.Write(_info.Control, WriteCommand) || !WaitInputEmpty() || !KernelAcpiRegisterServices.Write(_info.Data, address) || !WaitInputEmpty()) return false;
        return KernelAcpiRegisterServices.Write(_info.Data, value);
    }
    private static Boolean WaitInputEmpty() { for (UInt32 i=0U;i<WaitLimit;i++) if (KernelAcpiRegisterServices.Read(_info.Control,out UInt64 s) && (((Byte)s & InputBufferFull)==0U)) return true; return false; }
    private static Boolean WaitOutputFull() { for (UInt32 i=0U;i<WaitLimit;i++) if (KernelAcpiRegisterServices.Read(_info.Control,out UInt64 s) && (((Byte)s & OutputBufferFull)!=0U)) return true; return false; }
    private static UInt32 Read32(Byte* p) => (UInt32)(p[0] | ((UInt32)p[1]<<8) | ((UInt32)p[2]<<16) | ((UInt32)p[3]<<24));
}

#endif
