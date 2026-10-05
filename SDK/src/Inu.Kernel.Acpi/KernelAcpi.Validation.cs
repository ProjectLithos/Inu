using System;
using Inu.Kernel.Console;

namespace Inu.Kernel.Acpi;

public static unsafe partial class KernelAcpi
{

    private static Boolean TryValidateTable(Byte* table, out UInt32 length)
    {
        length = 0U; if (table == null) return false;
        UInt32 candidateLength = Read32(table + 4U);
        if (candidateLength < 36U || candidateLength > MaximumTableLength) return false;
        if (!ChecksumIsZero(table, candidateLength)) return false;
        length = candidateLength; return true;
    }


    private static Boolean HasRsdpSignature(Byte* value)
    {
        return value != null && value[0] == 0x52U && value[1] == 0x53U && value[2] == 0x44U && value[3] == 0x20U &&
            value[4] == 0x50U && value[5] == 0x54U && value[6] == 0x52U && value[7] == 0x20U;
    }


    private static Boolean ChecksumIsZero(Byte* value, UInt32 length)
    {
        Byte sum = (Byte)0U; for (UInt32 index = 0U; index < length; index++) sum = (Byte)(sum + value[index]); return sum == (Byte)0U;
    }
    private static UInt16 Read16(Byte* value) => (UInt16)(value[0] | ((UInt16)value[1] << 8));
    private static UInt32 Read32(Byte* value) => (UInt32)(value[0] | ((UInt32)value[1] << 8) | ((UInt32)value[2] << 16) | ((UInt32)value[3] << 24));
    private static UInt64 Read64(Byte* value) => (UInt64)Read32(value) | ((UInt64)Read32(value + 4) << 32);
}
