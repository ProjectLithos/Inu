// 0.1.47 companion source: kept in ChangedFiles whenever the host conformance project references this shared contract.
using System;
using System.Collections;
using System.Collections.Generic;

namespace Inu.Runtime.Conformance.Shared
{
    internal static class ListConformance
    {
        internal static Boolean TestList()
        {
            List<Int32> list = new List<Int32>(1);
            Boolean initialCapacity = list.Capacity == 1 && list.Count == 0;

            list.Add(1);
            list.Add(2);
            list.AddRange(new Int32[] { 3, 4 });
            Boolean add = list.Count == 4 && list[0] == 1 && list[3] == 4;

            list.AddRange(list);
            Boolean addRangeSelf = list.Count == 8
                && list[0] == 1 && list[1] == 2 && list[2] == 3 && list[3] == 4
                && list[4] == 1 && list[5] == 2 && list[6] == 3 && list[7] == 4;

            list.Insert(2, 99);
            Boolean insert = list.Count == 9 && list[2] == 99;
            list[2] = 5;
            Boolean indexer = list[2] == 5;
            Boolean search = list.Contains(5) && list.IndexOf(5) == 2 && !list.Contains(99) && list.IndexOf(99) == -1;

            Boolean removed = list.Remove(5);
            list.RemoveAt(0);
            Boolean remove = removed && !list.Remove(12345) && list.Count == 7
                && list[0] == 2 && list[1] == 3 && list[6] == 4;

            Int32[] snapshot = list.ToArray();
            Boolean toArray = snapshot.Length == 7 && snapshot[0] == 2 && snapshot[6] == 4;
            snapshot[0] = -100;
            toArray = toArray && list[0] == 2;

            Int32 genericSum = 0;
            foreach (Int32 value in list) genericSum += value;
            IEnumerable nongeneric = list;
            IEnumerator nongenericIterator = nongeneric.GetEnumerator();
            Boolean currentBeforeThrows = false;
            Boolean currentAfterThrows = false;
            try { Object ignored = nongenericIterator.Current; } catch (InvalidOperationException) { currentBeforeThrows = true; }
            Int32 nongenericSum = 0;
            while (nongenericIterator.MoveNext()) nongenericSum += (Int32)nongenericIterator.Current;
            try { Object ignored = nongenericIterator.Current; } catch (InvalidOperationException) { currentAfterThrows = true; }
            Boolean enumeration = genericSum == 19 && nongenericSum == 19 && currentBeforeThrows && currentAfterThrows;

            IEnumerator<Int32> versioned = ((IEnumerable<Int32>)list).GetEnumerator();
            Boolean started = versioned.MoveNext() && versioned.Current == 2;
            list.Add(10);
            Boolean modifiedThrows = false;
            try { versioned.MoveNext(); } catch (InvalidOperationException) { modifiedThrows = true; }
            versioned.Dispose();
            list.RemoveAt(list.Count - 1);
            enumeration = enumeration && started && modifiedThrows;

            list.Capacity = 20;
            Boolean capacity = list.Capacity == 20 && list.Count == 7;
            Boolean smallCapacityThrows = false;
            try { list.Capacity = list.Count - 1; } catch (ArgumentOutOfRangeException) { smallCapacityThrows = true; }

            Boolean lowIndexThrows = false;
            Boolean highIndexThrows = false;
            Int32 lowIndex = -1;
            try { Int32 ignored = list[lowIndex]; } catch (ArgumentOutOfRangeException) { lowIndexThrows = true; }
            try { Int32 ignored = list[list.Count]; } catch (ArgumentOutOfRangeException) { highIndexThrows = true; }

            Boolean addRangeNullThrows = false;
            System.Collections.Generic.IEnumerable<Int32> nullSource = null!;
            try { list.AddRange(nullSource); } catch (ArgumentNullException) { addRangeNullThrows = true; }

            list.Clear();
            Boolean clear = list.Count == 0 && list.Capacity == 20 && list.ToArray().Length == 0;

            return initialCapacity && add && addRangeSelf && insert && indexer && search && remove
                && toArray && enumeration && capacity && smallCapacityThrows
                && lowIndexThrows && highIndexThrows && addRangeNullThrows && clear;
        }
    }
}
