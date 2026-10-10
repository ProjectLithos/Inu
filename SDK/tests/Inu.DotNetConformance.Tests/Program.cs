using System;
using System.Collections.Generic;
using System.Text;

namespace Inu.DotNetConformance.Tests;

internal static class Program
{
    private const string TargetName = "Inu.BCL.Core.v1";
    private const int TargetItemCount = 37;
    private static int _passed;
    private static int _failed;

    private sealed class Probe { }

    private static int Main()
    {
        Console.WriteLine($"[INFO] Reference BCL target: {TargetName} ({TargetItemCount} items)");

        Check("System.Object", TestObject());
        Check("System.Boolean", TestBoolean());
        Check("System.Char", TestChar());
        Check("System.SByte", TestSByte());
        Check("System.Int16", TestInt16());
        Check("System.Int32", TestInt32());
        Check("System.Int64", TestInt64());
        Check("System.Byte", TestByte());
        Check("System.UInt16", TestUInt16());
        Check("System.UInt32", TestUInt32());
        Check("System.UInt64", TestUInt64());
        Check("System.Single", TestSingle());
        Check("System.Double", TestDouble());
        Check("System.IntPtr", TestIntPtr());
        Check("System.UIntPtr", TestUIntPtr());
        Check("System.Array", TestArray());
        Check("System.String", TestString());
        Check("System.Nullable<T>", TestNullable());
        Check("System.Type", TestType());
        Check("System.Collections.Generic.KeyValuePair<TKey,TValue>", TestKeyValuePair());
        Check("System.Collections.Generic.EqualityComparer<T>", TestEqualityComparer());
        Check("System.Collections.Generic.List<T>", TestList());
        Check("System.Collections.Generic.Dictionary<TKey,TValue>", TestDictionary());
        Check("System.Collections.Generic.Queue<T>", TestQueue());
        Check("System.Collections.Generic.Stack<T>", TestStack());
        Check("System.Text.StringBuilder", TestStringBuilder());
        Check("System.Text.Encoding", TestEncodingFactories());
        Check("System.Text.ASCIIEncoding", TestAsciiEncoding());
        Check("System.Text.UTF8Encoding", TestUtf8Encoding());
        Check("Primitive formatting", TestPrimitiveFormatting());
        Check("System.Math", TestMath());
        Check("System.Convert", TestConvert());
        Check("System.IComparable / IComparable<T>", TestComparables());
        Check("System.Delegate / Action / Func", TestDelegates());
        Check("System.Span<T> / ReadOnlySpan<T>", TestSpan());
        Check("System.Memory<T> / ReadOnlyMemory<T>", TestMemory());
        Check("Generic comparison/equality consistency", TestGenericComparisonEquality());

        int total = _passed + _failed;
        if (total != TargetItemCount)
        {
            Console.Error.WriteLine($"[FAIL] Target accounting mismatch: expected {TargetItemCount}, exercised {total}.");
            return 2;
        }

        Console.WriteLine($"[INFO] {TargetName}: {_passed} passed, {_failed} failed.");
        return _failed == 0 ? 0 : 1;
    }

    private static void Check(string item, bool ok)
    {
        if (ok)
        {
            _passed++;
            Console.WriteLine($"[ OK ] {item}");
        }
        else
        {
            _failed++;
            Console.Error.WriteLine($"[FAIL] {item}");
        }
    }

    private static bool TestObject()
    {
        object value = new object();
        object alias = value;
        object other = new object();
        object derived = new Probe();
        object generic = new List<int>();
        object array = new int[1];
        return ReferenceEquals(value, alias) && !ReferenceEquals(value, other)
            && value.Equals(alias)
            && value.GetHashCode() == alias.GetHashCode()
            && value.GetType() == typeof(object)
            && alias.GetType() == value.GetType()
            && value.ToString() == "System.Object"
            && derived.ToString() == "Inu.DotNetConformance.Tests.Program+Probe"
            && generic.ToString() == "System.Collections.Generic.List`1[System.Int32]"
            && array.ToString() == "System.Int32[]";
    }

    private static T ParseViaIParsable<T>(string text, IFormatProvider? provider) where T : IParsable<T>
        => T.Parse(text, provider);

    private static bool TryParseViaIParsable<T>(string? text, IFormatProvider? provider, out T? result) where T : IParsable<T>
        => T.TryParse(text, provider, out result);

    private static T ParseViaISpanParsable<T>(ReadOnlySpan<char> text, IFormatProvider? provider) where T : ISpanParsable<T>
        => T.Parse(text, provider);

    private static bool TryParseViaISpanParsable<T>(ReadOnlySpan<char> text, IFormatProvider? provider, out T? result) where T : ISpanParsable<T>
        => T.TryParse(text, provider, out result);

    private static bool TestBoolean()
    {
        bool parsed;
        bool invalid;
        bool nullParseThrows = false;
        bool formatThrows = false;
        bool wrongCompareThrows = false;
        bool charConvertThrows = false;
        bool dateConvertThrows = false;
        bool badTypeConvertThrows = false;
        bool nullTypeConvertThrows = false;
        try { _ = bool.Parse((string)null!); } catch (ArgumentNullException) { nullParseThrows = true; }
        try { _ = bool.Parse("not-a-boolean"); } catch (FormatException) { formatThrows = true; }
        try { _ = ((IComparable)true).CompareTo(1); } catch (ArgumentException) { wrongCompareThrows = true; }

        IConvertible convertibleTrue = true;
        IConvertible convertibleFalse = false;
        try { _ = convertibleTrue.ToChar(null); } catch (InvalidCastException) { charConvertThrows = true; }
        try { _ = convertibleTrue.ToDateTime(null); } catch (InvalidCastException) { dateConvertThrows = true; }
        try { _ = convertibleTrue.ToType(typeof(Program), null); } catch (InvalidCastException) { badTypeConvertThrows = true; }
        try { _ = convertibleTrue.ToType(null!, null); } catch (ArgumentNullException) { nullTypeConvertThrows = true; }

        char[] destinationArray = new char[5];
        Span<char> destination = destinationArray;
        bool formatted = false.TryFormat(destination, out int charsWritten);
        Span<char> tooSmall = new char[4];
        bool smallFormat = false.TryFormat(tooSmall, out int smallCharsWritten);
        char[] spanTrue = new[] { ' ', 'T', 'R', 'U', 'E', '\0' };
        ReadOnlySpan<char> spanFalse = " FALSE ".AsSpan();

        bool staticParsed = ParseViaIParsable<bool>(" true ", null);
        bool staticTryParsed = TryParseViaIParsable<bool>("FALSE", null, out bool staticTryValue);
        bool spanStaticParsed = ParseViaISpanParsable<bool>(new ReadOnlySpan<char>(spanTrue), null);
        bool spanStaticTryParsed = TryParseViaISpanParsable<bool>(spanFalse, null, out bool spanStaticTryValue);

        return bool.TrueString == "True" && bool.FalseString == "False"
            && true.ToString() == "True" && false.ToString() == "False"
            && true.ToString(null) == "True" && false.ToString(null) == "False"
            && formatted && charsWritten == 5 && new string(destinationArray) == "False"
            && !smallFormat && smallCharsWritten == 0
            && bool.Parse("true") && !bool.Parse(" FALSE ")
            && bool.Parse(new ReadOnlySpan<char>(spanTrue))
            && bool.TryParse("TrUe", out parsed) && parsed
            && bool.TryParse("\u3000false\u3000", out parsed) && !parsed
            && !bool.TryParse("yes", out invalid) && !invalid
            && !bool.TryParse((string?)null, out invalid) && !invalid
            && nullParseThrows && formatThrows
            && false.CompareTo(true) < 0 && true.CompareTo(false) > 0 && true.CompareTo(true) == 0
            && ((IComparable)true).CompareTo(null) > 0 && wrongCompareThrows
            && true.Equals(true) && !true.Equals(false)
            && ((object)true).Equals(true) && !((object)true).Equals(false)
            && true.GetHashCode() == 1 && false.GetHashCode() == 0
            && convertibleTrue.GetTypeCode() == TypeCode.Boolean
            && convertibleTrue.ToBoolean(null) && !convertibleFalse.ToBoolean(null)
            && convertibleTrue.ToSByte(null) == 1 && convertibleFalse.ToSByte(null) == 0
            && convertibleTrue.ToByte(null) == 1 && convertibleFalse.ToByte(null) == 0
            && convertibleTrue.ToInt16(null) == 1 && convertibleFalse.ToInt16(null) == 0
            && convertibleTrue.ToUInt16(null) == 1 && convertibleFalse.ToUInt16(null) == 0
            && convertibleTrue.ToInt32(null) == 1 && convertibleFalse.ToInt32(null) == 0
            && convertibleTrue.ToUInt32(null) == 1U && convertibleFalse.ToUInt32(null) == 0U
            && convertibleTrue.ToInt64(null) == 1L && convertibleFalse.ToInt64(null) == 0L
            && convertibleTrue.ToUInt64(null) == 1UL && convertibleFalse.ToUInt64(null) == 0UL
            && convertibleTrue.ToSingle(null) == 1F && convertibleFalse.ToSingle(null) == 0F
            && convertibleTrue.ToDouble(null) == 1D && convertibleFalse.ToDouble(null) == 0D
            && convertibleTrue.ToDecimal(null) == 1M && convertibleFalse.ToDecimal(null) == 0M
            && convertibleTrue.ToString(null) == "True" && convertibleFalse.ToString(null) == "False"
            && (bool)convertibleTrue.ToType(typeof(bool), null)
            && (int)convertibleTrue.ToType(typeof(int), null) == 1
            && (string)convertibleTrue.ToType(typeof(string), null) == "True"
            && (bool)convertibleTrue.ToType(typeof(object), null)
            && charConvertThrows && dateConvertThrows && badTypeConvertThrows && nullTypeConvertThrows
            && staticParsed && staticTryParsed && !staticTryValue
            && spanStaticParsed && spanStaticTryParsed && !spanStaticTryValue;
    }

    private static bool TestChar()
    {
        bool wrongCompareThrows = false;
        bool nullParseThrows = false;
        bool formatParseThrows = false;
        bool booleanConvertThrows = false;
        bool singleConvertThrows = false;
        bool doubleConvertThrows = false;
        bool decimalConvertThrows = false;
        bool dateConvertThrows = false;
        bool badTypeConvertThrows = false;
        bool nullTypeConvertThrows = false;
        bool sbyteOverflowThrows = false;
        bool nullStringConvertThrows = false;

        try { _ = ((IComparable)'B').CompareTo(66); } catch (ArgumentException) { wrongCompareThrows = true; }
        try { _ = char.Parse((string)null!); } catch (ArgumentNullException) { nullParseThrows = true; }
        try { _ = char.Parse("AB"); } catch (FormatException) { formatParseThrows = true; }
        try { _ = Convert.ToChar((string)null!); } catch (ArgumentNullException) { nullStringConvertThrows = true; }

        IConvertible convertible = 'A';
        try { _ = convertible.ToBoolean(null); } catch (InvalidCastException) { booleanConvertThrows = true; }
        try { _ = convertible.ToSingle(null); } catch (InvalidCastException) { singleConvertThrows = true; }
        try { _ = convertible.ToDouble(null); } catch (InvalidCastException) { doubleConvertThrows = true; }
        try { _ = convertible.ToDecimal(null); } catch (InvalidCastException) { decimalConvertThrows = true; }
        try { _ = convertible.ToDateTime(null); } catch (InvalidCastException) { dateConvertThrows = true; }
        try { _ = convertible.ToType(typeof(Program), null); } catch (InvalidCastException) { badTypeConvertThrows = true; }
        try { _ = convertible.ToType(null!, null); } catch (ArgumentNullException) { nullTypeConvertThrows = true; }
        try { _ = ((IConvertible)'\u0100').ToSByte(null); } catch (OverflowException) { sbyteOverflowThrows = true; }

        char parsed;
        char invalid;
        bool staticParsed = ParseViaIParsable<char>("Z", null) == 'Z';
        bool staticTryParsed = TryParseViaIParsable<char>("Q", null, out char staticTryValue) && staticTryValue == 'Q';
        bool spanStaticParsed = ParseViaISpanParsable<char>("M".AsSpan(), null) == 'M';
        bool spanStaticTryParsed = TryParseViaISpanParsable<char>("N".AsSpan(), null, out char spanTryValue) && spanTryValue == 'N';

        Span<char> formatDestination = stackalloc char[1];
        ISpanFormattable spanFormattable = 'K';
        bool formatted = spanFormattable.TryFormat(formatDestination, out int charsWritten, default, null);
        Span<char> emptyDestination = Span<char>.Empty;
        bool emptyFormatted = spanFormattable.TryFormat(emptyDestination, out int emptyCharsWritten, default, null);

        return char.MinValue == (char)0 && char.MaxValue == (char)0xFFFF
            && 'A'.CompareTo('B') == -1 && 'B'.CompareTo('A') == 1 && 'A'.CompareTo('A') == 0
            && ((IComparable)'B').CompareTo(null) > 0 && wrongCompareThrows
            && 'A'.Equals('A') && !'A'.Equals('B') && ((object)'A').Equals('A')
            && 'A'.GetHashCode() == ((int)'A' | ((int)'A' << 16))
            && char.IsAscii('A') && char.IsAscii((char)0x7F) && !char.IsAscii((char)0x80)
            && char.IsAsciiLetter('A') && char.IsAsciiLetter('z') && !char.IsAsciiLetter('4')
            && char.IsAsciiLetterLower('z') && !char.IsAsciiLetterLower('Z')
            && char.IsAsciiLetterUpper('Z') && !char.IsAsciiLetterUpper('z')
            && char.IsAsciiDigit('7') && !char.IsAsciiDigit('x')
            && char.IsAsciiLetterOrDigit('7') && char.IsAsciiLetterOrDigit('x') && !char.IsAsciiLetterOrDigit('-')
            && char.IsAsciiHexDigit('F') && char.IsAsciiHexDigit('f') && !char.IsAsciiHexDigit('G')
            && char.IsAsciiHexDigitLower('f') && !char.IsAsciiHexDigitLower('F')
            && char.IsAsciiHexDigitUpper('F') && !char.IsAsciiHexDigitUpper('f')
            && char.IsBetween('m', 'a', 'z') && !char.IsBetween('M', 'a', 'z')
            && char.IsControl('\0') && char.IsControl((char)0x009F) && !char.IsControl(' ')
            && char.IsWhiteSpace(' ') && char.IsWhiteSpace('\n') && char.IsWhiteSpace('\u3000') && !char.IsWhiteSpace('X')
            && 'K'.ToString() == "K" && 'K'.ToString(null) == "K" && char.ToString('K') == "K"
            && ((IFormattable)'K').ToString("ignored", null) == "K"
            && formatted && charsWritten == 1 && formatDestination[0] == 'K'
            && !emptyFormatted && emptyCharsWritten == 0
            && char.Parse("P") == 'P'
            && char.TryParse("R", out parsed) && parsed == 'R'
            && !char.TryParse("RR", out invalid) && invalid == '\0'
            && !char.TryParse((string?)null, out invalid) && invalid == '\0'
            && nullParseThrows && formatParseThrows
            && staticParsed && staticTryParsed && spanStaticParsed && spanStaticTryParsed
            && convertible.GetTypeCode() == TypeCode.Char && convertible.ToChar(null) == 'A'
            && convertible.ToSByte(null) == 65 && convertible.ToByte(null) == 65
            && convertible.ToInt16(null) == 65 && convertible.ToUInt16(null) == 65
            && convertible.ToInt32(null) == 65 && convertible.ToUInt32(null) == 65U
            && convertible.ToInt64(null) == 65L && convertible.ToUInt64(null) == 65UL
            && convertible.ToString(null) == "A"
            && (char)convertible.ToType(typeof(char), null) == 'A'
            && (int)convertible.ToType(typeof(int), null) == 65
            && (string)convertible.ToType(typeof(string), null) == "A"
            && (char)convertible.ToType(typeof(object), null) == 'A'
            && Convert.ToChar((byte)65) == 'A' && Convert.ToChar(65) == 'A' && Convert.ToChar("A") == 'A'
            && Convert.ToUInt16('A') == 65 && Convert.ToInt32('A') == 65 && Convert.ToString('A') == "A"
            && booleanConvertThrows && singleConvertThrows && doubleConvertThrows && decimalConvertThrows
            && dateConvertThrows && badTypeConvertThrows && nullTypeConvertThrows
            && sbyteOverflowThrows && nullStringConvertThrows;
    }

    private static bool TestSByte()
    {
        sbyte parsed;
        bool nullThrows = false, formatThrows = false, overflowThrows = false, checkedThrows = false, wrongCompareThrows = false;
        try { _ = sbyte.Parse((string)null!); } catch (ArgumentNullException) { nullThrows = true; }
        try { _ = sbyte.Parse("12x"); } catch (FormatException) { formatThrows = true; }
        try { _ = sbyte.Parse("128"); } catch (OverflowException) { overflowThrows = true; }
        int tooLarge = 128;
        try { _ = checked((sbyte)tooLarge); } catch (OverflowException) { checkedThrows = true; }
        try { _ = ((IComparable)(sbyte)4).CompareTo((short)4); } catch (ArgumentException) { wrongCompareThrows = true; }
        int wrap = 130;
        Span<char> destination = stackalloc char[4];
        bool formatted = ((sbyte)-12).TryFormat(destination, out int written, "D3", null);
        IConvertible convertible = (sbyte)-12;
        bool convertOverflow = false;
        try { _ = Convert.ToSByte(128); } catch (OverflowException) { convertOverflow = true; }
        return sbyte.MinValue == -128 && sbyte.MaxValue == 127
            && sbyte.Parse(" -128 ") == sbyte.MinValue && sbyte.Parse("+127") == sbyte.MaxValue
            && sbyte.TryParse("42", out parsed) && parsed == 42 && !sbyte.TryParse("129", out parsed) && parsed == 0
            && ParseViaIParsable<sbyte>("7", null) == 7
            && ParseViaISpanParsable<sbyte>("-8".AsSpan(), null) == -8
            && ((sbyte)-12).ToString() == "-12" && ((sbyte)12).ToString("D3", null) == "012" && ((sbyte)-1).ToString("X2", null) == "FF"
            && formatted && written == 4 && destination.SequenceEqual("-012")
            && ((sbyte)4).CompareTo((sbyte)5) < 0 && ((IComparable)(sbyte)4).CompareTo(null) > 0
            && ((sbyte)4).Equals((sbyte)4) && !((sbyte)4).Equals((sbyte)5) && !((sbyte)4).Equals((object)(short)4)
            && convertible.GetTypeCode() == TypeCode.SByte && convertible.ToInt64(null) == -12L && convertible.ToBoolean(null)
            && convertible.ToDecimal(null) == new decimal(-12) && convertOverflow
            && checkedThrows && unchecked((sbyte)wrap) == -126
            && nullThrows && formatThrows && overflowThrows && wrongCompareThrows;
    }

    private static bool TestInt16()
    {
        short parsed;
        bool overflowThrows = false, checkedThrows = false, wrongCompareThrows = false;
        try { _ = short.Parse("32768"); } catch (OverflowException) { overflowThrows = true; }
        int tooLarge = 32768;
        try { _ = checked((short)tooLarge); } catch (OverflowException) { checkedThrows = true; }
        try { _ = ((IComparable)(short)4).CompareTo(4); } catch (ArgumentException) { wrongCompareThrows = true; }
        int wrap = 65535;
        Span<char> destination = stackalloc char[6];
        bool formatted = ((short)-123).TryFormat(destination, out int written, "D5", null);
        IConvertible convertible = (short)-123;
        bool convertOverflow = false;
        try { _ = Convert.ToInt16(32768); } catch (OverflowException) { convertOverflow = true; }
        return short.MinValue == -32768 && short.MaxValue == 32767
            && short.Parse("-32768") == short.MinValue && short.Parse("32767") == short.MaxValue
            && short.TryParse("1234", out parsed) && parsed == 1234 && !short.TryParse("32768", out parsed) && parsed == 0
            && ParseViaIParsable<short>("-45", null) == -45
            && ParseViaISpanParsable<short>("46".AsSpan(), null) == 46
            && ((short)-123).ToString() == "-123" && ((short)12).ToString("D4", null) == "0012" && ((short)-1).ToString("X4", null) == "FFFF"
            && formatted && written == 6 && destination.SequenceEqual("-00123")
            && ((short)4).CompareTo((short)5) < 0 && ((short)4).Equals((short)4) && !((short)4).Equals((short)5)
            && convertible.GetTypeCode() == TypeCode.Int16 && convertible.ToInt64(null) == -123L && convertible.ToBoolean(null)
            && convertible.ToDecimal(null) == new decimal(-123) && convertOverflow
            && checkedThrows && unchecked((short)wrap) == -1 && overflowThrows && wrongCompareThrows;
    }

    private static bool TestInt32()
    {
        int parsed;
        bool overflowThrows = false, checkedThrows = false, wrongCompareThrows = false;
        try { _ = int.Parse("2147483648"); } catch (OverflowException) { overflowThrows = true; }
        long tooLarge = 2147483648L;
        try { _ = checked((int)tooLarge); } catch (OverflowException) { checkedThrows = true; }
        try { _ = ((IComparable)12345).CompareTo(12345L); } catch (ArgumentException) { wrongCompareThrows = true; }
        long wrap = 4294967295L;
        Span<char> destination = stackalloc char[7];
        bool formatted = (-123).TryFormat(destination, out int written, "D6", null);
        IConvertible convertible = -123;
        bool convertOverflow = false;
        try { _ = Convert.ToInt32(2147483648L); } catch (OverflowException) { convertOverflow = true; }
        int value = 12345;
        return int.MinValue == -2147483648 && int.MaxValue == 2147483647
            && int.Parse(" -2147483648 ") == int.MinValue && int.Parse("+2147483647") == int.MaxValue
            && int.TryParse("12345", out parsed) && parsed == 12345 && !int.TryParse("2147483648", out parsed) && parsed == 0
            && ParseViaIParsable<int>("-77", null) == -77
            && ParseViaISpanParsable<int>("78".AsSpan(), null) == 78
            && (-123).ToString() == "-123" && 12.ToString("D4", null) == "0012" && (-1).ToString("X8", null) == "FFFFFFFF"
            && formatted && written == 7 && destination.SequenceEqual("-000123")
            && value.Equals(12345) && !value.Equals(12346) && value.GetHashCode() == 12345
            && value.CompareTo(12346) < 0 && ((IComparable)value).CompareTo(null) > 0
            && convertible.GetTypeCode() == TypeCode.Int32 && convertible.ToInt64(null) == -123L && convertible.ToBoolean(null)
            && convertible.ToDecimal(null) == new decimal(-123) && convertOverflow
            && checkedThrows && unchecked((int)wrap) == -1 && overflowThrows && wrongCompareThrows;
    }

    private static bool TestInt64()
    {
        long parsed;
        bool overflowThrows = false, checkedThrows = false, wrongCompareThrows = false;
        try { _ = long.Parse("9223372036854775808"); } catch (OverflowException) { overflowThrows = true; }
        ulong tooLarge = 9223372036854775808UL;
        try { _ = checked((long)tooLarge); } catch (OverflowException) { checkedThrows = true; }
        try { _ = ((IComparable)4L).CompareTo(4); } catch (ArgumentException) { wrongCompareThrows = true; }
        ulong wrap = ulong.MaxValue;
        Span<char> destination = stackalloc char[8];
        bool formatted = (-123L).TryFormat(destination, out int written, "D7", null);
        IConvertible convertible = -123L;
        bool convertOverflow = false;
        try { _ = Convert.ToInt64(9223372036854775808UL); } catch (OverflowException) { convertOverflow = true; }
        return long.MinValue == -9223372036854775808L && long.MaxValue == 9223372036854775807L
            && long.Parse("-9223372036854775808") == long.MinValue && long.Parse("9223372036854775807") == long.MaxValue
            && long.TryParse("123456789", out parsed) && parsed == 123456789L && !long.TryParse("9223372036854775808", out parsed) && parsed == 0L
            && ParseViaIParsable<long>("-79", null) == -79L
            && ParseViaISpanParsable<long>("80".AsSpan(), null) == 80L
            && (-123L).ToString() == "-123" && 12L.ToString("D4", null) == "0012" && (-1L).ToString("X16", null) == "FFFFFFFFFFFFFFFF"
            && formatted && written == 8 && destination.SequenceEqual("-0000123")
            && 4L.CompareTo(5L) < 0 && 4L.Equals(4L) && !4L.Equals(5L)
            && convertible.GetTypeCode() == TypeCode.Int64 && convertible.ToInt64(null) == -123L && convertible.ToBoolean(null)
            && convertible.ToDecimal(null) == new decimal(-123L) && convertOverflow
            && checkedThrows && unchecked((long)wrap) == -1L && overflowThrows && wrongCompareThrows;
    }


    private static bool TestByte()
    {
        byte parsed;
        bool nullThrows = false, formatThrows = false, overflowThrows = false, checkedThrows = false, wrongCompareThrows = false;
        try { _ = byte.Parse((string)null!); } catch (ArgumentNullException) { nullThrows = true; }
        try { _ = byte.Parse("12x"); } catch (FormatException) { formatThrows = true; }
        try { _ = byte.Parse("256"); } catch (OverflowException) { overflowThrows = true; }
        int negative = -1;
        try { _ = checked((byte)negative); } catch (OverflowException) { checkedThrows = true; }
        try { _ = ((IComparable)(byte)4).CompareTo((ushort)4); } catch (ArgumentException) { wrongCompareThrows = true; }
        Span<char> destination = stackalloc char[3];
        bool formatted = ((byte)12).TryFormat(destination, out int written, "D3", null);
        IConvertible convertible = (byte)200;
        bool convertOverflow = false;
        try { _ = Convert.ToByte(256); } catch (OverflowException) { convertOverflow = true; }
        return byte.MinValue == 0 && byte.MaxValue == 255
            && byte.Parse(" 0 ") == byte.MinValue && byte.Parse("+255") == byte.MaxValue
            && byte.TryParse("42", out parsed) && parsed == 42 && !byte.TryParse("256", out parsed) && parsed == 0
            && ParseViaIParsable<byte>("7", null) == 7
            && ParseViaISpanParsable<byte>("8".AsSpan(), null) == 8
            && ((byte)12).ToString() == "12" && ((byte)12).ToString("D3", null) == "012" && byte.MaxValue.ToString("X2", null) == "FF"
            && formatted && written == 3 && destination.SequenceEqual("012")
            && ((byte)4).CompareTo((byte)5) < 0 && ((IComparable)(byte)4).CompareTo(null) > 0
            && ((byte)4).Equals((byte)4) && !((byte)4).Equals((byte)5) && !((byte)4).Equals((object)(ushort)4)
            && convertible.GetTypeCode() == TypeCode.Byte && convertible.ToUInt64(null) == 200UL && convertible.ToBoolean(null)
            && convertible.ToDecimal(null) == new decimal(200UL) && convertOverflow
            && checkedThrows && unchecked((byte)negative) == byte.MaxValue
            && nullThrows && formatThrows && overflowThrows && wrongCompareThrows;
    }

    private static bool TestUInt16()
    {
        ushort parsed;
        bool overflowThrows = false, checkedThrows = false, wrongCompareThrows = false;
        try { _ = ushort.Parse("65536"); } catch (OverflowException) { overflowThrows = true; }
        int negative = -1;
        try { _ = checked((ushort)negative); } catch (OverflowException) { checkedThrows = true; }
        try { _ = ((IComparable)(ushort)4).CompareTo(4U); } catch (ArgumentException) { wrongCompareThrows = true; }
        Span<char> destination = stackalloc char[5];
        bool formatted = ((ushort)123).TryFormat(destination, out int written, "D5", null);
        IConvertible convertible = (ushort)60000;
        bool convertOverflow = false;
        try { _ = Convert.ToUInt16(65536); } catch (OverflowException) { convertOverflow = true; }
        return ushort.MinValue == 0 && ushort.MaxValue == 65535
            && ushort.Parse("0") == ushort.MinValue && ushort.Parse("65535") == ushort.MaxValue
            && ushort.TryParse("1234", out parsed) && parsed == 1234 && !ushort.TryParse("65536", out parsed) && parsed == 0
            && ParseViaIParsable<ushort>("45", null) == 45
            && ParseViaISpanParsable<ushort>("46".AsSpan(), null) == 46
            && ((ushort)123).ToString() == "123" && ((ushort)12).ToString("D4", null) == "0012" && ushort.MaxValue.ToString("X4", null) == "FFFF"
            && formatted && written == 5 && destination.SequenceEqual("00123")
            && ((ushort)4).CompareTo((ushort)5) < 0 && ((ushort)4).Equals((ushort)4) && !((ushort)4).Equals((ushort)5)
            && convertible.GetTypeCode() == TypeCode.UInt16 && convertible.ToUInt64(null) == 60000UL && convertible.ToBoolean(null)
            && convertible.ToDecimal(null) == new decimal(60000UL) && convertOverflow
            && checkedThrows && unchecked((ushort)negative) == ushort.MaxValue && overflowThrows && wrongCompareThrows;
    }

    private static bool TestUInt32()
    {
        uint parsed;
        bool overflowThrows = false, checkedThrows = false, wrongCompareThrows = false;
        try { _ = uint.Parse("4294967296"); } catch (OverflowException) { overflowThrows = true; }
        long negative = -1L;
        try { _ = checked((uint)negative); } catch (OverflowException) { checkedThrows = true; }
        try { _ = ((IComparable)12345U).CompareTo(12345UL); } catch (ArgumentException) { wrongCompareThrows = true; }
        Span<char> destination = stackalloc char[6];
        bool formatted = 123U.TryFormat(destination, out int written, "D6", null);
        IConvertible convertible = 4000000000U;
        bool convertOverflow = false;
        try { _ = Convert.ToUInt32(4294967296L); } catch (OverflowException) { convertOverflow = true; }
        uint value = 12345U;
        return uint.MinValue == 0U && uint.MaxValue == 4294967295U
            && uint.Parse(" 0 ") == uint.MinValue && uint.Parse("+4294967295") == uint.MaxValue
            && uint.TryParse("12345", out parsed) && parsed == 12345U && !uint.TryParse("4294967296", out parsed) && parsed == 0U
            && ParseViaIParsable<uint>("77", null) == 77U
            && ParseViaISpanParsable<uint>("78".AsSpan(), null) == 78U
            && 123U.ToString() == "123" && 12U.ToString("D4", null) == "0012" && uint.MaxValue.ToString("X8", null) == "FFFFFFFF"
            && formatted && written == 6 && destination.SequenceEqual("000123")
            && value.Equals(12345U) && !value.Equals(12346U) && value.CompareTo(12346U) < 0 && ((IComparable)value).CompareTo(null) > 0
            && convertible.GetTypeCode() == TypeCode.UInt32 && convertible.ToUInt64(null) == 4000000000UL && convertible.ToBoolean(null)
            && convertible.ToDecimal(null) == new decimal(4000000000UL) && convertOverflow
            && checkedThrows && unchecked((uint)negative) == uint.MaxValue && overflowThrows && wrongCompareThrows;
    }

    private static bool TestUInt64()
    {
        ulong parsed;
        bool overflowThrows = false, checkedThrows = false, wrongCompareThrows = false;
        try { _ = ulong.Parse("18446744073709551616"); } catch (OverflowException) { overflowThrows = true; }
        long negative = -1L;
        try { _ = checked((ulong)negative); } catch (OverflowException) { checkedThrows = true; }
        try { _ = ((IComparable)4UL).CompareTo(4U); } catch (ArgumentException) { wrongCompareThrows = true; }
        Span<char> destination = stackalloc char[7];
        bool formatted = 123UL.TryFormat(destination, out int written, "D7", null);
        IConvertible convertible = ulong.MaxValue;
        bool convertOverflow = false;
        try { _ = Convert.ToUInt64(-1L); } catch (OverflowException) { convertOverflow = true; }
        return ulong.MinValue == 0UL && ulong.MaxValue == 18446744073709551615UL
            && ulong.Parse("0") == ulong.MinValue && ulong.Parse("18446744073709551615") == ulong.MaxValue
            && ulong.TryParse("123456789", out parsed) && parsed == 123456789UL && !ulong.TryParse("18446744073709551616", out parsed) && parsed == 0UL
            && ParseViaIParsable<ulong>("79", null) == 79UL
            && ParseViaISpanParsable<ulong>("80".AsSpan(), null) == 80UL
            && 123UL.ToString() == "123" && 12UL.ToString("D4", null) == "0012" && ulong.MaxValue.ToString("X16", null) == "FFFFFFFFFFFFFFFF"
            && formatted && written == 7 && destination.SequenceEqual("0000123")
            && 4UL.CompareTo(5UL) < 0 && 4UL.Equals(4UL) && !4UL.Equals(5UL)
            && convertible.GetTypeCode() == TypeCode.UInt64 && convertible.ToUInt64(null) == ulong.MaxValue && convertible.ToBoolean(null)
            && convertible.ToDecimal(null) == new decimal(ulong.MaxValue) && convertOverflow
            && checkedThrows && unchecked((ulong)negative) == ulong.MaxValue && overflowThrows && wrongCompareThrows;
    }

    private static bool TestSingle()
    {
        float parsed;
        float spanParsed;
        bool invalid = float.TryParse("not-a-number", out _);
        Span<char> destination = stackalloc char[32];
        bool formatted = 12.5f.TryFormat(destination, out int written, "F2", null);
        IComparable<float> comparable = 2.5f;
        IEquatable<float> equatableNaN = float.NaN;
        return float.MinValue < 0.0f && float.MaxValue > 0.0f && float.Epsilon > 0.0f
            && float.IsNaN(float.NaN) && float.IsPositiveInfinity(float.PositiveInfinity)
            && float.IsNegativeInfinity(float.NegativeInfinity) && float.IsInfinity(float.PositiveInfinity)
            && float.IsFinite(12.5f) && !float.IsFinite(float.NaN) && float.IsNegative(-0.0f)
            && equatableNaN.Equals(float.NaN) && comparable.CompareTo(3.0f) < 0 && float.NaN.CompareTo(0.0f) < 0
            && float.TryParse(" -12.5 ", out parsed) && parsed == -12.5f
            && float.TryParse("1.25e2".AsSpan(), out spanParsed) && spanParsed == 125.0f
            && ParseViaIParsable<float>("2.5", null) == 2.5f
            && ParseViaISpanParsable<float>("3.5".AsSpan(), null) == 3.5f
            && !invalid && formatted && new string(destination[..written]) == "12.50"
            && 12.5f.ToString("G", null) == "12.5" && 12.5f.ToString("E2", null) == "1.25E+001"
            && Convert.ToInt32(2.5f) == 2 && Convert.ToDouble(12.5f) == 12.5;
    }

    private static bool TestDouble()
    {
        double parsed;
        double spanParsed;
        bool invalid = double.TryParse("not-a-number", out _);
        Span<char> destination = stackalloc char[32];
        bool formatted = 12.5.TryFormat(destination, out int written, "F3", null);
        IComparable<double> comparable = 2.5;
        IEquatable<double> equatableNaN = double.NaN;
        return double.MinValue < 0.0 && double.MaxValue > 0.0 && double.Epsilon > 0.0
            && double.IsNaN(double.NaN) && double.IsPositiveInfinity(double.PositiveInfinity)
            && double.IsNegativeInfinity(double.NegativeInfinity) && double.IsInfinity(double.PositiveInfinity)
            && double.IsFinite(12.5) && !double.IsFinite(double.NaN) && double.IsNegative(-0.0)
            && equatableNaN.Equals(double.NaN) && comparable.CompareTo(3.0) < 0 && double.NaN.CompareTo(0.0) < 0
            && double.TryParse(" -12.5 ", out parsed) && parsed == -12.5
            && double.TryParse("1.25e2".AsSpan(), out spanParsed) && spanParsed == 125.0
            && ParseViaIParsable<double>("2.5", null) == 2.5
            && ParseViaISpanParsable<double>("3.5".AsSpan(), null) == 3.5
            && !invalid && formatted && new string(destination[..written]) == "12.500"
            && 12.5.ToString("G", null) == "12.5" && 12.5.ToString("E2", null) == "1.25E+001"
            && Convert.ToInt32(3.5) == 4 && Convert.ToSingle(12.5) == 12.5f;
    }

    private static bool TestIntPtr()
    {
        IntPtr value = new IntPtr(100);
        IntPtr advanced = IntPtr.Add(value, 23);
        IntPtr arithmetic = (new IntPtr(6) * new IntPtr(7)) + new IntPtr(2);
        IntPtr divided = arithmetic / new IntPtr(4);
        IntPtr remainder = arithmetic % new IntPtr(5);
        long converted = (long)new IntPtr(-55);
        bool wrongCompareThrows = false;
        try { _ = ((IComparable)value).CompareTo(100L); } catch (ArgumentException) { wrongCompareThrows = true; }
        bool narrowOverflow = false;
        if (IntPtr.Size == 8)
        {
            try { _ = IntPtr.MaxValue.ToInt32(); } catch (OverflowException) { narrowOverflow = true; }
        }
        else
        {
            narrowOverflow = true;
        }
        Span<char> destination = stackalloc char[5];
        bool formatted = new IntPtr(123).TryFormat(destination, out int written, "D5", null);
        long expectedMax = IntPtr.Size == 8 ? long.MaxValue : int.MaxValue;
        long expectedMin = IntPtr.Size == 8 ? long.MinValue : int.MinValue;
        return (IntPtr.Size == 4 || IntPtr.Size == 8)
            && IntPtr.Zero.ToInt64() == 0 && IntPtr.MaxValue.ToInt64() == expectedMax && IntPtr.MinValue.ToInt64() == expectedMin
            && advanced.ToInt64() == 123 && IntPtr.Subtract(advanced, 23) == value
            && (value + 23).ToInt64() == 123 && ((value + 23) - 23) == value
            && arithmetic.ToInt64() == 44 && divided.ToInt64() == 11 && remainder.ToInt64() == 4
            && value.CompareTo(advanced) < 0 && advanced.CompareTo(value) > 0 && value.CompareTo(value) == 0
            && value.Equals(new IntPtr(100)) && !value.Equals(advanced) && wrongCompareThrows
            && converted == -55 && narrowOverflow
            && new IntPtr(-123).ToString() == "-123" && new IntPtr(12).ToString("D4", null) == "0012"
            && formatted && written == 5 && destination.SequenceEqual("00123");
    }

    private static bool TestUIntPtr()
    {
        UIntPtr value = new UIntPtr(200UL);
        UIntPtr advanced = UIntPtr.Add(value, 17);
        UIntPtr arithmetic = (new UIntPtr(6UL) * new UIntPtr(7UL)) + new UIntPtr(2UL);
        UIntPtr divided = arithmetic / new UIntPtr(4UL);
        UIntPtr remainder = arithmetic % new UIntPtr(5UL);
        ulong converted = (ulong)new UIntPtr(55UL);
        bool wrongCompareThrows = false;
        try { _ = ((IComparable)value).CompareTo(200UL); } catch (ArgumentException) { wrongCompareThrows = true; }
        bool narrowOverflow = false;
        if (UIntPtr.Size == 8)
        {
            try { _ = UIntPtr.MaxValue.ToUInt32(); } catch (OverflowException) { narrowOverflow = true; }
        }
        else
        {
            narrowOverflow = true;
        }
        Span<char> destination = stackalloc char[5];
        bool formatted = new UIntPtr(123UL).TryFormat(destination, out int written, "D5", null);
        ulong expectedMax = UIntPtr.Size == 8 ? ulong.MaxValue : uint.MaxValue;
        return (UIntPtr.Size == 4 || UIntPtr.Size == 8)
            && UIntPtr.Zero.ToUInt64() == 0UL && UIntPtr.MinValue.ToUInt64() == 0UL && UIntPtr.MaxValue.ToUInt64() == expectedMax
            && advanced.ToUInt64() == 217UL && UIntPtr.Subtract(advanced, 17) == value
            && (value + 17).ToUInt64() == 217UL && ((value + 17) - 17) == value
            && arithmetic.ToUInt64() == 44UL && divided.ToUInt64() == 11UL && remainder.ToUInt64() == 4UL
            && value.CompareTo(advanced) < 0 && advanced.CompareTo(value) > 0 && value.CompareTo(value) == 0
            && value.Equals(new UIntPtr(200UL)) && !value.Equals(advanced) && wrongCompareThrows
            && converted == 55UL && narrowOverflow
            && new UIntPtr(123UL).ToString() == "123" && new UIntPtr(12UL).ToString("D4", null) == "0012"
            && formatted && written == 5 && destination.SequenceEqual("00123");
    }

    private static bool TestArray()
    {
        int[] values = new int[3];
        values[1] = 9;
        int[] empty = Array.Empty<int>();
        return values.Length == 3 && values.LongLength == 3L && values[1] == 9
            && empty is not null && empty.Length == 0;
    }

    private static bool TestString()
    {
        string text = "Inu kernel";
        string concatenated = string.Concat("Inu", " ", "kernel");
        return string.Empty.Length == 0 && text.Length == 10 && text[0] == 'I'
            && string.Equals(text, concatenated) && string.CompareOrdinal("abc", "abd") < 0
            && text.IndexOf('k') == 4 && text.Contains('u') && text.StartsWith("Inu") && text.EndsWith("kernel")
            && text.Substring(4, 6) == "kernel"
            && string.IsNullOrEmpty("") && string.IsNullOrWhiteSpace(" \t\r\n");
    }

    private static bool TestNullable()
    {
        int? present = 55;
        int? absent = null;
        return present.HasValue && present.Value == 55 && present.GetValueOrDefault() == 55
            && !absent.HasValue && absent.GetValueOrDefault() == 0 && absent.GetValueOrDefault(7) == 7;
    }

    private static bool TestType()
    {
        Type intType = typeof(int);
        Type arrayType = typeof(int[]);
        return intType.IsValueType && intType.IsPrimitive && !intType.IsArray
            && arrayType.IsArray && arrayType.IsSZArray && arrayType.GetElementType() == intType
            && arrayType.BaseType == typeof(Array)
            && typeof(object).IsAssignableFrom(typeof(Probe)) && typeof(Probe).IsSubclassOf(typeof(object));
    }

    private static bool TestKeyValuePair()
    {
        var pair = new KeyValuePair<string, int>("answer", 42);
        return pair.Key == "answer" && pair.Value == 42;
    }

    private static bool TestEqualityComparer()
    {
        EqualityComparer<int> ints = EqualityComparer<int>.Default;
        EqualityComparer<string> strings = EqualityComparer<string>.Default;
        return ints.Equals(7, 7) && !ints.Equals(7, 8)
            && strings.Equals("same", "same") && !strings.Equals("same", "other")
            && strings.GetHashCode("same") == strings.GetHashCode("same");
    }

    private static bool TestList()
    {
        var list = new List<int>();
        list.Add(1); list.Add(3); list.Insert(1, 2);
        int[] copy = list.ToArray();
        return list.Count == 3 && list[1] == 2 && list.Contains(3) && list.IndexOf(2) == 1
            && copy.Length == 3 && copy[2] == 3 && list.Remove(2) && list.Count == 2;
    }

    private static bool TestDictionary()
    {
        var dictionary = new Dictionary<string, int>();
        dictionary.Add("one", 1);
        dictionary["two"] = 2;
        return dictionary.Count == 2 && dictionary.ContainsKey("one")
            && dictionary.TryGetValue("two", out int value) && value == 2
            && !dictionary.TryAdd("one", 11) && dictionary.Remove("one") && !dictionary.ContainsKey("one");
    }

    private static bool TestQueue()
    {
        var queue = new Queue<int>();
        queue.Enqueue(4); queue.Enqueue(5); queue.Enqueue(6);
        return queue.Count == 3 && queue.Peek() == 4 && queue.Dequeue() == 4 && queue.Peek() == 5 && queue.Count == 2;
    }

    private static bool TestStack()
    {
        var stack = new Stack<int>();
        stack.Push(4); stack.Push(5); stack.Push(6);
        return stack.Count == 3 && stack.Peek() == 6 && stack.Pop() == 6 && stack.Peek() == 5 && stack.Count == 2;
    }

    private static bool TestStringBuilder()
    {
        var builder = new StringBuilder();
        builder.Append("Inu").Append(' ').Append(95).AppendLine();
        bool first = builder.Length == 8 && builder[0] == 'I' && builder.ToString() == "Inu 95\r\n";
        builder.Clear().Append(true);
        return first && builder.EnsureCapacity(32) >= 32 && builder.ToString() == "True";
    }

    private static bool TestEncodingFactories()
    {
        Encoding ascii = Encoding.ASCII;
        Encoding utf8 = Encoding.UTF8;
        return ascii is not null && utf8 is not null && ascii.GetByteCount("Inu") == 3 && utf8.GetByteCount("Inu") == 3;
    }

    private static bool TestAsciiEncoding()
    {
        var ascii = new ASCIIEncoding();
        byte[] bytes = ascii.GetBytes("Inu");
        return bytes.Length == 3 && bytes[0] == (byte)'I' && bytes[2] == (byte)'u' && ascii.GetString(bytes) == "Inu";
    }

    private static bool TestUtf8Encoding()
    {
        var utf8 = new UTF8Encoding();
        const string unicode = "\u00A3\u20AC";
        byte[] bytes = utf8.GetBytes(unicode);
        return bytes.Length == 5 && bytes[0] == 0xC2 && bytes[1] == 0xA3
            && bytes[2] == 0xE2 && bytes[3] == 0x82 && bytes[4] == 0xAC
            && utf8.GetString(bytes) == unicode;
    }
    private static bool TestPrimitiveFormatting()
        => 12345.ToString() == "12345" && (-42).ToString() == "-42"
            && 12345.ToString("G", null) == "12345" && 42.ToString("D5", null) == "00042"
            && 0x2A.ToString("X4", null) == "002A" && (-1).ToString("X", null) == "FFFFFFFF"
            && ((uint)99).ToString("D4", null) == "0099"
            && ((long)-9000000000L).ToString("D12", null) == "-009000000000"
            && 12.5.ToString("G", null) == "12.5" && (-0.25f).ToString("G", null) == "-0.25";

    private static bool TestMath()
        => Math.Abs(-17) == 17 && Math.Abs(-17L) == 17L && Math.Abs(-2.5) == 2.5
            && Math.Min(5, 9) == 5 && Math.Max(5, 9) == 9 && Math.Min(5L, 9L) == 5L
            && Math.Sign(-8) == -1 && Math.Sign(0L) == 0 && Math.Sign(8.0) == 1
            && Math.Clamp(15, 0, 10) == 10 && Math.Clamp(-4L, 0L, 10L) == 0L
            && Math.Floor(2.75) == 2.0 && Math.Ceiling(2.25) == 3.0 && Math.Truncate(-2.75) == -2.0
            && Math.Round(2.5) == 2.0 && Math.Round(3.5) == 4.0
            && Math.Abs(Math.Sqrt(81.0) - 9.0) < 0.000001;

    private static bool TestConvert()
    {
        bool intOverflow = false, uintOverflow = false, longOverflow = false, ulongOverflow = false;
        try { _ = Convert.ToInt32(2147483648.0); } catch (OverflowException) { intOverflow = true; }
        try { _ = Convert.ToUInt32(-1.0); } catch (OverflowException) { uintOverflow = true; }
        try { _ = Convert.ToInt64(9223372036854775808.0); } catch (OverflowException) { longOverflow = true; }
        try { _ = Convert.ToUInt64(-1.0); } catch (OverflowException) { ulongOverflow = true; }
        return Convert.ToInt32(true) == 1 && Convert.ToInt32(false) == 0
            && Convert.ToByte(255) == 255 && Convert.ToSByte(-12) == -12
            && Convert.ToInt16(-32000) == -32000 && Convert.ToUInt16(65000) == 65000
            && Convert.ToUInt32(123) == 123U && Convert.ToInt64(-123) == -123L
            && Convert.ToUInt64(123) == 123UL && Convert.ToBoolean(1) && !Convert.ToBoolean(0)
            && Convert.ToInt32(2.5) == 2 && Convert.ToInt32(3.5) == 4
            && Convert.ToInt32(2147483647.0) == 2147483647
            && Convert.ToUInt32(4294967295.0) == 4294967295U
            && Convert.ToDouble(123) == 123.0 && Convert.ToSingle(12) == 12.0f
            && Convert.ToString(-321) == "-321" && Convert.ToString(12.5) == "12.5"
            && Convert.ToString(true) == "True"
            && intOverflow && uintOverflow && longOverflow && ulongOverflow;
    }

    private static bool TestComparables()
    {
        IComparable nonGeneric = 7;
        IComparable<int> generic = 7;
        IComparable<long> longGeneric = 9L;
        IEquatable<uint> uintEquatable = 77U;
        return nonGeneric.CompareTo(6) > 0 && nonGeneric.CompareTo(7) == 0
            && generic.CompareTo(8) < 0 && ((IComparable<int>)8).CompareTo(7) > 0
            && longGeneric.CompareTo(10L) < 0 && uintEquatable.Equals(77U)
            && ((IComparable<char>)'b').CompareTo('a') > 0
            && ((IEquatable<bool>)true).Equals(true);
    }

    private static bool TestDelegates()
    {
        int observed = 0;
        Action<int> action = value => observed = value;
        Action<int, int> add = (a, b) => observed = a + b;
        Func<int, int> twice = value => value * 2;
        Func<int, int, int> sum = (a, b) => a + b;
        Func<int> constant = () => 11;
        Predicate<int> positive = value => value > 0;
        Comparison<int> compare = (a, b) => a.CompareTo(b);
        Converter<int, long> widen = value => value;
        action(9);
        bool first = observed == 9 && twice(6) == 12 && constant() == 11;
        add(7, 8);
        return first && observed == 15 && sum(4, 5) == 9
            && positive(1) && !positive(-1) && compare(3, 7) < 0 && widen(44) == 44L;
    }

    private static bool TestSpan()
    {
        int[] values = { 1, 2, 3, 4, 5 };
        Span<int> span = values;
        span[1] = 20;
        Span<int> middle = span.Slice(1, 3);
        middle.Fill(7);
        ReadOnlySpan<int> readOnly = span;
        int[] copy = readOnly.Slice(1, 3).ToArray();

        int[] overlapping = { 1, 2, 3, 4, 5 };
        overlapping.AsSpan(0, 4).CopyTo(overlapping.AsSpan(1, 4));
        Span<int> tooSmall = new int[2];
        int[]? nullArrayForSpan = null;
        Span<int> nullSpan = nullArrayForSpan;
        return span.Length == 5 && span[0] == 1 && span[1] == 7 && span[3] == 7
            && readOnly.Length == 5 && copy.Length == 3 && copy[0] == 7 && copy[2] == 7
            && overlapping[0] == 1 && overlapping[1] == 1 && overlapping[4] == 4
            && !span.TryCopyTo(tooSmall) && Span<int>.Empty.IsEmpty && ReadOnlySpan<int>.Empty.IsEmpty
            && nullSpan.IsEmpty;
    }

    private static bool TestMemory()
    {
        int[] values = { 10, 20, 30, 40 };
        Memory<int> memory = values;
        Memory<int> middle = memory.Slice(1, 2);
        middle.Span[0] = 25;
        ReadOnlyMemory<int> readOnly = memory;
        int[] copy = readOnly.Slice(1, 2).ToArray();
        int[]? nullArrayForMemory = null;
        Memory<int> nullMemory = nullArrayForMemory;
        return memory.Length == 4 && values[1] == 25 && middle.Span[1] == 30
            && readOnly.Span[1] == 25 && copy.Length == 2 && copy[0] == 25 && copy[1] == 30
            && Memory<int>.Empty.IsEmpty && ReadOnlyMemory<int>.Empty.IsEmpty && nullMemory.IsEmpty;
    }

    private static bool TestGenericComparisonEquality()
    {
        Comparer<int> ints = Comparer<int>.Default;
        Comparer<long> longs = Comparer<long>.Default;
        EqualityComparer<uint> uints = EqualityComparer<uint>.Default;
        EqualityComparer<long> longEquality = EqualityComparer<long>.Default;
        return ints.Compare(2, 7) < 0 && ints.Compare(7, 2) > 0 && ints.Compare(4, 4) == 0
            && longs.Compare(9L, 10L) < 0
            && uints.Equals(99U, 99U) && !uints.Equals(99U, 100U)
            && longEquality.Equals(-5L, -5L) && !longEquality.Equals(-5L, 5L);
    }

}
