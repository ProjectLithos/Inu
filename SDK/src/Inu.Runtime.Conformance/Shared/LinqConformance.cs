#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Inu.Runtime.Conformance.Shared
{
    internal static class LinqConformance
    {
        private sealed class CaseInsensitiveComparer : IEqualityComparer<String>
        {
            public Boolean Equals(String x, String y)
            {
                if (Object.ReferenceEquals(x, y)) return true;
                if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null)) return false;
                return String.Compare(x, y, true) == 0;
            }

            public Int32 GetHashCode(String value)
            {
                if (Object.ReferenceEquals(value, null)) return 0;
                return value.ToUpper().GetHashCode();
            }
        }

        private readonly struct OrderedProbe
        {
            internal OrderedProbe(Int32 key, Int32 ordinal) { Key = key; Ordinal = ordinal; }
            internal Int32 Key { get; }
            internal Int32 Ordinal { get; }
        }

        internal static Boolean TestLinq()
        {
            List<Int32> source = new List<Int32>();
            source.Add(1); source.Add(2); source.Add(3); source.Add(4);

            IEnumerable<Int32> deferred = source.Where(x => x >= 2).Select(x => x * 10);
            source.Add(5);
            Int32[] deferredValues = deferred.ToArray();
            Boolean selectWhere = deferredValues.Length == 4 && deferredValues[0] == 20 && deferredValues[3] == 50;
            Int32[] indexedSelect = source.Select((x, i) => x + i).ToArray();
            Int32[] indexedWhere = source.Where((x, i) => (i % 2) == 0).ToArray();
            selectWhere = selectWhere && indexedSelect.Length == 5 && indexedSelect[0] == 1 && indexedSelect[4] == 9
                && indexedWhere.Length == 3 && indexedWhere[0] == 1 && indexedWhere[2] == 5;

            Boolean first = source.First() == 1 && source.First(x => x > 3) == 4
                && new Int32[0].FirstOrDefault() == 0 && source.FirstOrDefault(x => x > 99) == 0
                && new Int32[0].FirstOrDefault(42) == 42 && source.FirstOrDefault(x => x > 99, 43) == 43;
            Boolean firstThrows = false;
            try { new Int32[0].First(); } catch (InvalidOperationException) { firstThrows = true; }

            Boolean single = new Int32[] { 7 }.Single() == 7
                && source.Single(x => x == 3) == 3
                && new Int32[0].SingleOrDefault() == 0
                && source.SingleOrDefault(x => x == 99) == 0
                && new Int32[0].SingleOrDefault(42) == 42 && source.SingleOrDefault(x => x == 99, 43) == 43;
            Boolean singleEmptyThrows = false;
            Boolean singleManyThrows = false;
            try { new Int32[0].Single(); } catch (InvalidOperationException) { singleEmptyThrows = true; }
            try { new Int32[] { 1, 2 }.Single(); } catch (InvalidOperationException) { singleManyThrows = true; }

            Boolean quantifiers = source.Any() && source.Any(x => x == 5) && !new Int32[0].Any()
                && source.All(x => x > 0) && !source.All(x => x < 5);
            Boolean count = source.Count() == 5 && source.Count(x => (x % 2) == 1) == 3;

            IEqualityComparer<String> insensitive = new CaseInsensitiveComparer();
            String[] words = new String[] { "Inu", "Kath" };
            Boolean contains = words.Contains("Inu") && !words.Contains("inu") && words.Contains("inu", insensitive);

            Int32[] copied = source.Where(x => x <= 3).ToArray();
            List<Int32> listed = source.Where(x => x >= 4).ToList();
            Boolean materializers = copied.Length == 3 && copied[0] == 1 && copied[2] == 3
                && listed.Count == 2 && listed[0] == 4 && listed[1] == 5;

            OrderedProbe[] probes = new OrderedProbe[]
            {
                new OrderedProbe(2, 0), new OrderedProbe(1, 1), new OrderedProbe(2, 2), new OrderedProbe(1, 3)
            };
            OrderedProbe[] ordered = probes.OrderBy(x => x.Key).ToArray();
            Boolean orderBy = ordered.Length == 4
                && ordered[0].Key == 1 && ordered[0].Ordinal == 1
                && ordered[1].Key == 1 && ordered[1].Ordinal == 3
                && ordered[2].Key == 2 && ordered[2].Ordinal == 0
                && ordered[3].Key == 2 && ordered[3].Ordinal == 2;

            Boolean nullGuards = false;
            IEnumerable<Int32> nullSource = (IEnumerable<Int32>)null;
            try { nullSource.Any(); } catch (ArgumentNullException) { nullGuards = true; }

            return selectWhere && first && firstThrows && single && singleEmptyThrows && singleManyThrows
                && quantifiers && count && contains && materializers && orderBy && nullGuards;
        }
    }
}
