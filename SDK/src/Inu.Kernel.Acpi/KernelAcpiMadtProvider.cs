#if INU_COMPONENT_ACPI_MADT
using System;

namespace Inu.Kernel.Acpi;

public static unsafe class KernelAcpiMadtProvider
{
    /// <summary>Registers MADT topology discovery with the ACPI platform-service registry.</summary>
    public static Boolean Register() => KernelAcpiMadtServices.Register(
        &GetProcessorCount, &GetIoApicCount, &GetInterruptOverrideCount, &GetNmiSourceCount, &GetLocalNmiCount,
        &TryGetProcessorThunk, &TryGetIoApicThunk, &TryGetInterruptOverrideThunk, &TryGetNmiSourceThunk, &TryGetLocalNmiThunk, &TryGetLocalApicAddressThunk);

    private static Boolean TryGetProcessorThunk(UInt32 index, AcpiProcessorInfo* value) { if (value == null) return false; AcpiProcessorInfo local; if (!TryGetProcessor(index, out local)) return false; *value = local; return true; }
    private static Boolean TryGetIoApicThunk(UInt32 index, AcpiIoApicInfo* value) { if (value == null) return false; AcpiIoApicInfo local; if (!TryGetIoApic(index, out local)) return false; *value = local; return true; }
    private static Boolean TryGetInterruptOverrideThunk(UInt32 index, AcpiInterruptOverrideInfo* value) { if (value == null) return false; AcpiInterruptOverrideInfo local; if (!TryGetInterruptOverride(index, out local)) return false; *value = local; return true; }
    private static Boolean TryGetNmiSourceThunk(UInt32 index, AcpiNmiSourceInfo* value) { if (value == null) return false; AcpiNmiSourceInfo local; if (!TryGetNmiSource(index, out local)) return false; *value = local; return true; }
    private static Boolean TryGetLocalNmiThunk(UInt32 index, AcpiLocalNmiInfo* value) { if (value == null) return false; AcpiLocalNmiInfo local; if (!TryGetLocalNmi(index, out local)) return false; *value = local; return true; }
    private static Boolean TryGetLocalApicAddressThunk(UInt64* value) { if (value == null) return false; UInt64 local; if (!TryGetLocalApicAddress(out local)) return false; *value = local; return true; }


    /// <summary>Gets the count of enabled or online-capable processors in the MADT.</summary>
    public static UInt32 GetProcessorCount() => CountMadtEntries((Byte)0U, (Byte)9U, true);
    /// <summary>Gets the count of I/O APIC entries in the MADT.</summary>
    public static UInt32 GetIoApicCount() => CountMadtEntries((Byte)1U, (Byte)0xFFU, false);
    /// <summary>Gets the count of interrupt-source overrides in the MADT.</summary>
    public static UInt32 GetInterruptOverrideCount() => CountMadtEntries((Byte)2U, (Byte)0xFFU, false);
    /// <summary>Gets the count of global NMI-source entries in the MADT.</summary>
    public static UInt32 GetNmiSourceCount() => CountMadtEntries((Byte)3U, (Byte)0xFFU, false);
    /// <summary>Gets the count of processor-local LAPIC/x2APIC NMI entries in the MADT.</summary>
    public static UInt32 GetLocalNmiCount() => CountMadtEntries((Byte)4U, (Byte)10U, false);


    /// <summary>Gets one enabled processor record by logical discovery index.</summary>
    public static Boolean TryGetProcessor(UInt32 requestedIndex, out AcpiProcessorInfo processor)
    {
        processor = default;
        if (!KernelAcpi.TryGetTable(KernelAcpi.ApicSignature, out UInt64 address, out UInt32 length) || length < 44U) return false;
        Byte* table = (Byte*)address; UInt32 offset = 44U; UInt32 found = 0U;
        while (offset + 2U <= length)
        {
            Byte type = table[offset]; Byte entryLength = table[offset + 1U];
            if (entryLength < 2U || offset + entryLength > length) return false;
            if (type == 0U && entryLength >= 8U)
            {
                UInt32 flags = Read32(table + offset + 4U); Boolean enabled = (flags & 3U) != 0U;
                if (enabled && found++ == requestedIndex) { processor = new AcpiProcessorInfo(table[offset + 3U], table[offset + 2U], false, true); return true; }
            }
            else if (type == 9U && entryLength >= 16U)
            {
                UInt32 flags = Read32(table + offset + 8U); Boolean enabled = (flags & 3U) != 0U;
                if (enabled && found++ == requestedIndex) { processor = new AcpiProcessorInfo(Read32(table + offset + 4U), Read32(table + offset + 12U), true, true); return true; }
            }
            offset += entryLength;
        }
        return false;
    }


    /// <summary>Gets one I/O APIC record by discovery index.</summary>
    public static Boolean TryGetIoApic(UInt32 requestedIndex, out AcpiIoApicInfo ioApic)
    {
        ioApic = default;
        if (!KernelAcpi.TryGetTable(KernelAcpi.ApicSignature, out UInt64 address, out UInt32 length) || length < 44U) return false;
        Byte* table = (Byte*)address; UInt32 offset = 44U; UInt32 found = 0U;
        while (offset + 2U <= length)
        {
            Byte type = table[offset]; Byte entryLength = table[offset + 1U];
            if (entryLength < 2U || offset + entryLength > length) return false;
            if (type == 1U && entryLength >= 12U && found++ == requestedIndex)
            { ioApic = new AcpiIoApicInfo(table[offset + 2U], Read32(table + offset + 4U), Read32(table + offset + 8U)); return true; }
            offset += entryLength;
        }
        return false;
    }


    /// <summary>Gets one interrupt-source override by discovery index.</summary>
    public static Boolean TryGetInterruptOverride(UInt32 requestedIndex, out AcpiInterruptOverrideInfo interruptOverride)
    {
        interruptOverride = default;
        if (!KernelAcpi.TryGetTable(KernelAcpi.ApicSignature, out UInt64 address, out UInt32 length) || length < 44U) return false;
        Byte* table = (Byte*)address; UInt32 offset = 44U; UInt32 found = 0U;
        while (offset + 2U <= length)
        {
            Byte type = table[offset]; Byte entryLength = table[offset + 1U];
            if (entryLength < 2U || offset + entryLength > length) return false;
            if (type == 2U && entryLength >= 10U && found++ == requestedIndex)
            { interruptOverride = new AcpiInterruptOverrideInfo(table[offset + 2U], table[offset + 3U], Read32(table + offset + 4U), Read16(table + offset + 8U)); return true; }
            offset += entryLength;
        }
        return false;
    }


    /// <summary>Gets one global NMI-source declaration by MADT discovery index.</summary>
    public static Boolean TryGetNmiSource(UInt32 requestedIndex, out AcpiNmiSourceInfo source)
    {
        source=default;
        if(!KernelAcpi.TryGetTable(KernelAcpi.ApicSignature,out UInt64 address,out UInt32 length)||length<44U)return false;
        Byte* table=(Byte*)address;UInt32 offset=44U,found=0U;
        while(offset+2U<=length)
        {
            Byte type=table[offset],entryLength=table[offset+1U];
            if(entryLength<2U||offset+entryLength>length)return false;
            if(type==3U&&entryLength>=8U&&found++==requestedIndex)
            {source=new AcpiNmiSourceInfo(Read16(table+offset+2U),Read32(table+offset+4U));return true;}
            offset+=entryLength;
        }
        return false;
    }


    /// <summary>Gets one processor-local LAPIC/x2APIC NMI declaration in MADT table order.</summary>
    public static Boolean TryGetLocalNmi(UInt32 requestedIndex, out AcpiLocalNmiInfo localNmi)
    {
        localNmi=default;
        if(!KernelAcpi.TryGetTable(KernelAcpi.ApicSignature,out UInt64 address,out UInt32 length)||length<44U)return false;
        Byte* table=(Byte*)address;UInt32 offset=44U,found=0U;
        while(offset+2U<=length)
        {
            Byte type=table[offset],entryLength=table[offset+1U];
            if(entryLength<2U||offset+entryLength>length)return false;
            if(type==4U&&entryLength>=6U)
            {
                if(found++==requestedIndex){localNmi=new AcpiLocalNmiInfo(false,table[offset+2U],Read16(table+offset+3U),table[offset+5U]);return true;}
            }
            else if(type==10U&&entryLength>=12U)
            {
                if(found++==requestedIndex){localNmi=new AcpiLocalNmiInfo(true,Read32(table+offset+4U),Read16(table+offset+2U),table[offset+8U]);return true;}
            }
            offset+=entryLength;
        }
        return false;
    }


    /// <summary>Gets the Local APIC physical base advertised by MADT.</summary>
    public static Boolean TryGetLocalApicAddress(out UInt64 address)
    {
        address = 0UL;
        if (!KernelAcpi.TryGetTable(KernelAcpi.ApicSignature, out UInt64 tableAddress, out UInt32 length) || length < 44U) return false;
        address = Read32((Byte*)tableAddress + 36U);
        Byte* table = (Byte*)tableAddress; UInt32 offset = 44U;
        while (offset + 2U <= length)
        {
            Byte type = table[offset]; Byte entryLength = table[offset + 1U];
            if (entryLength < 2U || offset + entryLength > length) return false;
            if (type == 5U && entryLength >= 12U) address = Read64(table + offset + 4U);
            offset += entryLength;
        }
        return address != 0UL;
    }


    private static UInt32 CountMadtEntries(Byte firstType, Byte secondType, Boolean onlyEnabledProcessors)
    {
        if (!KernelAcpi.TryGetTable(KernelAcpi.ApicSignature, out UInt64 address, out UInt32 length) || length < 44U) return 0U;
        Byte* table = (Byte*)address; UInt32 offset = 44U; UInt32 count = 0U;
        while (offset + 2U <= length)
        {
            Byte type = table[offset]; Byte entryLength = table[offset + 1U];
            if (entryLength < 2U || offset + entryLength > length) return count;
            if (type == firstType || type == secondType)
            {
                if (!onlyEnabledProcessors) count++;
                else if (type == 0U && entryLength >= 8U && (Read32(table + offset + 4U) & 3U) != 0U) count++;
                else if (type == 9U && entryLength >= 16U && (Read32(table + offset + 8U) & 3U) != 0U) count++;
            }
            offset += entryLength;
        }
        return count;
    }


    private static UInt16 Read16(Byte* p) => (UInt16)(p[0] | ((UInt16)p[1] << 8));
    private static UInt32 Read32(Byte* p) => (UInt32)(p[0] | ((UInt32)p[1] << 8) | ((UInt32)p[2] << 16) | ((UInt32)p[3] << 24));
    private static UInt64 Read64(Byte* p) => (UInt64)Read32(p) | ((UInt64)Read32(p + 4) << 32);
}

#endif
