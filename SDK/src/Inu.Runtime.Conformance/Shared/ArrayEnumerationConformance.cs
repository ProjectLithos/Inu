using System;
using System.Collections;
using System.Collections.Generic;

namespace Inu.Runtime.Conformance.Shared
{
    internal static class ArrayEnumerationConformance
    {
        internal static Boolean TestArray()
        {
            Int32[] values = new Int32[] { 1, 2, 3, 4 };
            Boolean lowBounds = false;
            Boolean highBounds = false;
            Int32 lowIndex = -1;
            try { Int32 ignored = values[lowIndex]; } catch (IndexOutOfRangeException) { lowBounds = true; }
            try { Int32 ignored = values[values.Length]; } catch (IndexOutOfRangeException) { highBounds = true; }

            Int32[] clone = (Int32[])values.Clone();
            Array.Clear(clone, 1, 2);
            Int32[] copied = new Int32[4];
            Array.Copy(values, copied, values.Length);
            Array.Copy(copied, 0, copied, 1, 3);

            String[] strings = new String[] { "a", "b" };
            Object[] covariant = strings;
            Boolean covarianceGuard = false;
            try { covariant[0] = new Object(); } catch (ArrayTypeMismatchException) { covarianceGuard = true; }

            Int32 sum = 0;
            foreach (Int32 value in values) sum += value;
            Int32[] empty = Array.Empty<Int32>();
            return values.Length == 4 && values.LongLength == 4L && values.Rank == 1
                && lowBounds && highBounds && covarianceGuard
                && clone[0] == 1 && clone[1] == 0 && clone[2] == 0 && clone[3] == 4
                && copied[0] == 1 && copied[1] == 1 && copied[2] == 2 && copied[3] == 3
                && sum == 10 && empty != null && empty.Length == 0;
        }

        internal static Boolean TestIEnumerable()
        {
            IEnumerable enumerable = new Int32[] { 3, 4, 5 };
            IEnumerator iterator = enumerable.GetEnumerator();
            Int32 sum = 0;
            while (iterator.MoveNext()) sum += (Int32)iterator.Current;
            iterator.Reset();
            return sum == 12 && iterator.MoveNext() && (Int32)iterator.Current == 3;
        }

        internal static Boolean TestIEnumerator()
        {
            IEnumerator iterator = ((IEnumerable)new String[] { "a", "b" }).GetEnumerator();
            Boolean beforeThrows = false;
            Boolean afterThrows = false;
            try { Object ignored = iterator.Current; } catch (InvalidOperationException) { beforeThrows = true; }
            Boolean moved = iterator.MoveNext() && String.Equals((String)iterator.Current, "a")
                && iterator.MoveNext() && String.Equals((String)iterator.Current, "b") && !iterator.MoveNext();
            try { Object ignored = iterator.Current; } catch (InvalidOperationException) { afterThrows = true; }
            return beforeThrows && moved && afterThrows;
        }

        internal static Boolean TestGenericIEnumerable()
        {
            IEnumerable<Int32> enumerable = new Int32[] { 7, 8 };
            Int32 sum = 0;
            foreach (Int32 value in enumerable) sum += value;
            IEnumerable<Object> covariant = new String[] { "x", "y" };
            Int32 count = 0;
            foreach (Object value in covariant) if (value is String) count++;
            return sum == 15 && count == 2;
        }

        internal static Boolean TestGenericIEnumerator()
        {
            IEnumerator<Int32> iterator = ((IEnumerable<Int32>)new Int32[] { 9, 10 }).GetEnumerator();
            Boolean beforeThrows = false;
            try { Int32 ignored = iterator.Current; } catch (InvalidOperationException) { beforeThrows = true; }
            Boolean ok = iterator.MoveNext() && iterator.Current == 9
                && iterator.MoveNext() && iterator.Current == 10 && !iterator.MoveNext();
            iterator.Reset();
            Boolean reset = iterator.MoveNext() && iterator.Current == 9;
            iterator.Dispose();
            return beforeThrows && ok && reset;
        }
    }
}
