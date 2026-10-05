using System;

namespace Inu.Text;

/// <summary>Provides allocation-free value-to-text helpers for freestanding Inu code.</summary>
public static class StringFormatter
{
    /// <summary>Returns the normal .NET text for a Boolean value without allocating.</summary>
    public static String Format(Boolean value) => value ? "True" : "False";

    /// <summary>Formats an unsigned 64-bit integer into a caller-owned character buffer.</summary>
    public static unsafe Boolean TryFormat(UInt64 value, Char* destination, UInt32 capacity, out UInt32 written)
    {
        written = 0U;
        if (destination == null || capacity == 0U) return false;
        Char* reverse = stackalloc Char[20];
        UInt32 count = 0U;
        do
        {
            reverse[count++] = (Char)('0' + (Char)(value % 10UL));
            value /= 10UL;
        }
        while (value != 0UL);
        if (count > capacity) return false;
        UInt32 index = 0U;
        while (index < count)
        {
            destination[index] = reverse[count - index - 1U];
            index++;
        }
        written = count;
        return true;
    }

    /// <summary>Formats a signed 64-bit integer into a caller-owned character buffer.</summary>
    public static unsafe Boolean TryFormat(Int64 value, Char* destination, UInt32 capacity, out UInt32 written)
    {
        written = 0U;
        if (destination == null || capacity == 0U) return false;
        Boolean negative = value < 0L;
        UInt64 magnitude = negative ? (UInt64)(-(value + 1L)) + 1UL : (UInt64)value;
        UInt32 offset = negative ? 1U : 0U;
        if (offset >= capacity) return false;
        UInt32 digits;
        if (!TryFormat(magnitude, destination + offset, capacity - offset, out digits)) return false;
        if (negative) destination[0] = '-';
        written = digits + offset;
        return true;
    }
}
