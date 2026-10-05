using System;

namespace Inu.Kernel.SystemCalls;

/// <summary>Provides stable encoding and validation rules for Inu syscall ABI namespaces.</summary>
public static class KernelSystemCallMath
{
    public const UInt64 NamespaceMask = 0xFFFF000000000000UL;
    public const UInt64 GetSetEventNamespace = 0x4E4F000000000000UL;
    public const UInt64 LinuxNamespace = 0x4C58000000000000UL;
    public const UInt64 NtNamespace = 0x4E54000000000000UL;
    public const UInt64 ServiceMask = 0x00000000FFFFFFFFUL;
    public const UInt32 MaximumRegisteredService = 127U;

    /// <summary>Encodes a native Inu syscall class. Native Inu routing is carried by Message, never by a numeric service ID.</summary>
    public static UInt64 EncodeGetSetEvent(KernelSystemCallOperation operation)
    {
        return GetSetEventNamespace | ((UInt64)operation << 32);
    }

    /// <summary>Encodes one Linux-style syscall number without changing the original numeric ID.</summary>
    public static UInt64 EncodeLinux(UInt32 syscallNumber) => LinuxNamespace | syscallNumber;

    /// <summary>Encodes one NT-style service number without assuming a Windows-version-specific table.</summary>
    public static UInt64 EncodeNt(UInt32 serviceNumber) => NtNamespace | serviceNumber;

    /// <summary>Attempts to decode the ABI namespace carried by an encoded x64 syscall number.</summary>
    public static Boolean TryDecodeAbi(UInt64 encoded, out KernelSystemCallAbi abi)
    {
        UInt64 ns = encoded & NamespaceMask;
        if (ns == GetSetEventNamespace) { abi = KernelSystemCallAbi.GetSetEvent; return true; }
        if (ns == LinuxNamespace) { abi = KernelSystemCallAbi.Linux; return true; }
        if (ns == NtNamespace) { abi = KernelSystemCallAbi.Nt; return true; }
        abi = KernelSystemCallAbi.Unknown;
        return false;
    }

    /// <summary>Gets the 32-bit compatibility service number. Native Inu requires this field to be zero.</summary>
    public static UInt32 GetServiceNumber(UInt64 encoded) => (UInt32)(encoded & ServiceMask);

    /// <summary>Gets the Get/Set/Event operation class.</summary>
    public static KernelSystemCallOperation GetOperation(UInt64 encoded) => (KernelSystemCallOperation)((encoded >> 32) & 0xFFUL);

    public static Boolean IsNativeEncoding(UInt64 encoded)
    {
        if(!TryDecodeAbi(encoded,out KernelSystemCallAbi abi)||abi!=KernelSystemCallAbi.GetSetEvent||GetServiceNumber(encoded)!=0U)return false;
        KernelSystemCallOperation operation=GetOperation(encoded);return operation==KernelSystemCallOperation.Get||operation==KernelSystemCallOperation.Set||operation==KernelSystemCallOperation.Event;
    }

    /// <summary>Validates all allocation-free structural rules for a native Inu message before any user buffer is dereferenced.</summary>
    public static Boolean IsValidNativeEnvelopeShape(KernelSystemCallMessage message)
    {
        if(message.Version!=KernelSystemCallMessage.CurrentVersion||message.ByteSize!=KernelSystemCallMessage.SerializedBytes)return false;
        if(message.AppNameAddress==0UL||message.AppNameLength==0UL||message.AppNameLength>KernelSystemCallMessage.MaximumAppNameBytes)return false;
        if(message.MessageAddress==0UL||message.MessageLength==0UL||message.MessageLength>KernelSystemCallMessage.MaximumMessageBytes)return false;
        if(message.DataLength>KernelSystemCallMessage.MaximumPayloadBytes||message.OutputCapacity>KernelSystemCallMessage.MaximumPayloadBytes)return false;
        if(message.DataLength!=0UL&&message.DataAddress==0UL)return false;
        if(message.OutputCapacity!=0UL&&message.OutputAddress==0UL)return false;
        return true;
    }

    /// <summary>Determines whether a Linux/NT compatibility ID fits the bounded bootstrap registry.</summary>
    public static Boolean IsRegistrableService(UInt32 service) => service <= MaximumRegisteredService;
}
