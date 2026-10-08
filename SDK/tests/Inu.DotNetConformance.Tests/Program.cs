using System;
using System.Collections.Generic;
using System.Text;

namespace Inu.DotNetConformance.Tests;

internal static class Program
{
    private const string TargetName = "Inu.BCL.Core.v1";
    private const int TargetItemCount = 26;
    private static int _passed;
    private static int _failed;

    private sealed class Probe { }

    private static int Main()
    {
        Console.WriteLine($"[INFO] Reference BCL target: {TargetName} ({TargetItemCount} items)");

        Check("System.Object", TestObject());
        Check("System.Boolean", TestBoolean());
        Check("System.Char", TestChar());
        Check("System.Int32", TestInt32());
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
        return ReferenceEquals(value, alias) && !ReferenceEquals(value, other)
            && value.Equals(alias) && value.ToString() == "System.Object";
    }

    private static bool TestBoolean()
        => true.ToString() == "True" && false.ToString() == "False";

    private static bool TestChar()
        => char.IsWhiteSpace(' ') && char.IsWhiteSpace('\n') && !char.IsWhiteSpace('X')
            && char.MinValue == (char)0 && char.MaxValue == (char)0xFFFF;

    private static bool TestInt32()
    {
        int value = 12345;
        return value.Equals(12345) && !value.Equals(12346)
            && value.GetHashCode() == 12345 && int.MinValue < 0 && int.MaxValue > 0;
    }

    private static bool TestIntPtr()
    {
        IntPtr value = new IntPtr(100);
        IntPtr advanced = IntPtr.Add(value, 23);
        return (IntPtr.Size == 4 || IntPtr.Size == 8) && advanced.ToInt64() == 123
            && IntPtr.Subtract(advanced, 23) == value;
    }

    private static bool TestUIntPtr()
    {
        UIntPtr value = new UIntPtr(200UL);
        UIntPtr advanced = UIntPtr.Add(value, 17);
        return (UIntPtr.Size == 4 || UIntPtr.Size == 8) && advanced.ToUInt64() == 217UL
            && UIntPtr.Subtract(advanced, 17) == value;
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
            && 12345.ToString("G", null) == "12345" && ((uint)99).ToString() == "99"
            && ((long)-9000000000L).ToString() == "-9000000000";

    private static bool TestMath()
        => Math.Abs(-17) == 17 && Math.Min(5, 9) == 5 && Math.Max(5, 9) == 9
            && Math.Sign(-8) == -1 && Math.Sign(0) == 0 && Math.Sign(8) == 1
            && Math.Clamp(15, 0, 10) == 10 && Math.Clamp(-4, 0, 10) == 0;

    private static bool TestConvert()
        => Convert.ToInt32(true) == 1 && Convert.ToInt32(false) == 0
            && Convert.ToInt64(-123) == -123L && Convert.ToBoolean(1) && !Convert.ToBoolean(0)
            && Convert.ToString(-321) == "-321" && Convert.ToString(true) == "True";

    private static bool TestComparables()
    {
        IComparable nonGeneric = 7;
        IComparable<int> generic = 7;
        return nonGeneric.CompareTo(6) > 0 && nonGeneric.CompareTo(7) == 0
            && generic.CompareTo(8) < 0 && ((IComparable<int>)8).CompareTo(7) > 0;
    }

    private static bool TestDelegates()
    {
        int observed = 0;
        Action<int> action = value => observed = value;
        Func<int, int> twice = value => value * 2;
        Func<int> constant = () => 11;
        action(9);
        return observed == 9 && twice(6) == 12 && constant() == 11;
    }

    private static bool TestSpan()
    {
        int[] values = { 1, 2, 3, 4 };
        Span<int> span = values;
        span[1] = 20;
        Span<int> middle = span.Slice(1, 2);
        ReadOnlySpan<int> readOnly = span;
        int[] copy = readOnly.Slice(1, 2).ToArray();
        middle[1] = 30;
        return span.Length == 4 && span[1] == 20 && span[2] == 30
            && readOnly.Length == 4 && copy.Length == 2 && copy[0] == 20 && copy[1] == 3;
    }

}
