// 0.1.47 companion source: kept in ChangedFiles whenever the host conformance project references this shared contract.
using System;
using System.Runtime.CompilerServices;

namespace Inu.Runtime.Conformance.Shared
{
    /// <summary>
    /// Shared host/in-kernel tuple contract. The source intentionally uses ordinary C# tuple syntax
    /// so Roslyn must resolve the same ValueTuple and TupleElementNamesAttribute contracts that OS
    /// developer code uses.
    /// </summary>
    public static class TupleConformance
    {
        public static Boolean TestTuple()
        {
            Tuple<Int32, String> pair = Tuple.Create(7, "Inu");
            Tuple<Int32, String> same = Tuple.Create(7, "Inu");
            Tuple<Int32, String> different = Tuple.Create(8, "Inu");

            ITuple runtimePair = (ITuple)(Object)pair;
            Tuple<Int32, Int32, Int32, Int32, Int32, Int32, Int32, Tuple<Int32>> eight =
                Tuple.Create(1, 2, 3, 4, 5, 6, 7, 8);
            ITuple runtimeEight = (ITuple)(Object)eight;

            return pair.Item1 == 7
                && String.Equals(pair.Item2, "Inu")
                && pair.Equals(same)
                && !pair.Equals(different)
                && pair.GetHashCode() == same.GetHashCode()
                && String.Equals(pair.ToString(), "(7, Inu)")
                && runtimePair.Length == 2
                && (Int32)runtimePair[0] == 7
                && String.Equals((String)runtimePair[1], "Inu")
                && runtimeEight.Length == 8
                && (Int32)runtimeEight[7] == 8;
        }

        public static Boolean TestValueTuple()
        {
            ValueTuple<Int32, String> pair = ValueTuple.Create(11, "value");
            ValueTuple<Int32, String> same = ValueTuple.Create(11, "value");
            ValueTuple<Int32, String> different = ValueTuple.Create(12, "value");
            pair.Deconstruct(out Int32 number, out String text);

            ITuple runtimePair = (ITuple)(Object)pair;
            ValueTuple empty = ValueTuple.Create();

            return pair.Item1 == 11
                && String.Equals(pair.Item2, "value")
                && number == 11
                && String.Equals(text, "value")
                && pair.Equals(same)
                && !pair.Equals(different)
                && pair.GetHashCode() == same.GetHashCode()
                && String.Equals(pair.ToString(), "(11, value)")
                && empty.Equals(default(ValueTuple))
                && String.Equals(empty.ToString(), "()")
                && runtimePair.Length == 2
                && (Int32)runtimePair[0] == 11
                && String.Equals((String)runtimePair[1], "value");
        }

        public static Boolean TestLanguageTuples()
        {
            (Int32 code, String name) named = MakeNamedTuple();
            (Int32 left, Int32 right) pair = (3, 4);
            (Int32 left, Int32 right) copied = pair;
            (Int32 deconstructedLeft, Int32 deconstructedRight) = pair;

            var nine = (1, 2, 3, 4, 5, 6, 7, 8, 9);
            ITuple runtimeNine = (ITuple)(Object)nine;

            Boolean equality = (1, "x") == (1, "x") && (1, "x") != (2, "x");

            return named.code == 42
                && String.Equals(named.name, "Inu")
                && pair.left == 3
                && pair.right == 4
                && copied.left == 3
                && deconstructedLeft == 3
                && deconstructedRight == 4
                && nine.Item8 == 8
                && nine.Item9 == 9
                && runtimeNine.Length == 9
                && (Int32)runtimeNine[8] == 9
                && equality;
        }

        public static (Int32 code, String name) MakeNamedTuple() => (42, "Inu");
    }
}
