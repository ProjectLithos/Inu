#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;

namespace Inu.Runtime.Conformance.Shared
{
    internal static class CoreCollectionsConformance
    {
        private sealed class ConstantHashStringComparer : EqualityComparer<String>
        {
            public override Boolean Equals(String x, String y)
            {
                if (Object.ReferenceEquals(x, y)) return true;
                if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null) || x.Length != y.Length) return false;
                for (Int32 i = 0; i < x.Length; i++)
                {
                    Char a = x[i], b = y[i];
                    if (a >= 'A' && a <= 'Z') a = (Char)(a + 32);
                    if (b >= 'A' && b <= 'Z') b = (Char)(b + 32);
                    if (a != b) return false;
                }
                return true;
            }

            public override Int32 GetHashCode(String value) => 1;
        }

        internal static Boolean TestKeyValuePair()
        {
            KeyValuePair<String, Int32> pair = new KeyValuePair<String, Int32>("answer", 42);
            String key;
            Int32 value;
            pair.Deconstruct(out key, out value);
            KeyValuePair<String, Int32> created = KeyValuePair.Create("inu", 7);
            return String.Equals(pair.Key, "answer") && pair.Value == 42
                && String.Equals(key, "answer") && value == 42
                && String.Equals(created.Key, "inu") && created.Value == 7
                && String.Equals(pair.ToString(), "[answer, 42]");
        }

        internal static Boolean TestDictionary()
        {
            ConstantHashStringComparer comparer = new ConstantHashStringComparer();
            Dictionary<String, Int32> dictionary = new Dictionary<String, Int32>(1, comparer);
            for (Int32 i = 0; i < 24; i++) dictionary.Add(String.Concat("k", i.ToString()), i);
            if (dictionary.Count != 24 || !dictionary.ContainsKey("K5") || dictionary["K5"] != 5) return false;
            if (dictionary.Comparer != comparer) return false;
            if (dictionary.TryAdd("K5", 500)) return false;
            Boolean duplicate = false;
            try { dictionary.Add("K5", 500); } catch (ArgumentException) { duplicate = true; }
            if (!duplicate) return false;
            dictionary["K5"] = 55;
            Int32 found;
            if (!dictionary.TryGetValue("k5", out found) || found != 55) return false;
            if (!dictionary.Keys.Contains("K6") || !dictionary.Values.Contains(7) || dictionary.Keys.Count != dictionary.Count || dictionary.Values.Count != dictionary.Count) return false;
            Int32 keyCount = 0; foreach (String ignored in dictionary.Keys) keyCount++;
            Int32 valueCount = 0; foreach (Int32 ignored in dictionary.Values) valueCount++;
            Int32 pairCount = 0; Int32 sum = 0; foreach (KeyValuePair<String, Int32> item in dictionary) { pairCount++; sum += item.Value; }
            if (keyCount != 24 || valueCount != 24 || pairCount != 24 || sum <= 0) return false;
            if (!dictionary.Remove("K5") || dictionary.ContainsKey("k5") || dictionary.Count != 23) return false;

            IEnumerator<KeyValuePair<String, Int32>> enumerator = ((IEnumerable<KeyValuePair<String, Int32>>)dictionary).GetEnumerator();
            if (!enumerator.MoveNext()) return false;
            dictionary.Add("extra", 99);
            Boolean invalidated = false;
            try { enumerator.MoveNext(); } catch (InvalidOperationException) { invalidated = true; }
            if (!invalidated) return false;

            dictionary.Clear();
            return dictionary.Count == 0 && dictionary.Keys.Count == 0 && dictionary.Values.Count == 0;
        }

        internal static Boolean TestHashSet()
        {
            ConstantHashStringComparer comparer = new ConstantHashStringComparer();
            HashSet<String> set = new HashSet<String>(comparer);
            for (Int32 i = 0; i < 24; i++) if (!set.Add(String.Concat("v", i.ToString()))) return false;
            if (set.Count != 24 || set.Add("V5") || !set.Contains("V5") || set.Comparer != comparer) return false;
            String actual;
            if (!set.TryGetValue("V5", out actual) || !String.Equals(actual, "v5")) return false;
            if (!set.Remove("V5") || set.Contains("v5") || set.Count != 23) return false;
            String[] copy = new String[set.Count];
            set.CopyTo(copy, 0);
            Int32 enumerated = 0; foreach (String ignored in set) enumerated++;
            if (enumerated != set.Count || copy.Length != set.Count) return false;
            IEnumerator<String> iterator = ((IEnumerable<String>)set).GetEnumerator();
            if (!iterator.MoveNext()) return false;
            set.Add("new-value");
            Boolean invalidated = false;
            try { iterator.MoveNext(); } catch (InvalidOperationException) { invalidated = true; }
            if (!invalidated) return false;
            set.Clear();
            return set.Count == 0;
        }

        internal static Boolean TestQueue()
        {
            Queue<Int32> queue = new Queue<Int32>(1);
            for (Int32 i = 0; i < 12; i++) queue.Enqueue(i);
            if (queue.Count != 12 || queue.Peek() != 0 || !queue.Contains(8)) return false;
            Int32 peeked; if (!queue.TryPeek(out peeked) || peeked != 0) return false;
            for (Int32 i = 0; i < 5; i++) if (queue.Dequeue() != i) return false;
            for (Int32 i = 12; i < 20; i++) queue.Enqueue(i);
            Int32[] array = queue.ToArray();
            Int32[] copied = new Int32[17]; queue.CopyTo(copied, 1);
            if (copied[1] != 5 || copied[15] != 19) return false;
            if (array.Length != 15 || array[0] != 5 || array[array.Length - 1] != 19) return false;
            Int32 expected = 5; foreach (Int32 item in queue) { if (item != expected++) return false; }
            IEnumerator<Int32> iterator = ((IEnumerable<Int32>)queue).GetEnumerator();
            if (!iterator.MoveNext()) return false;
            queue.Enqueue(20);
            Boolean invalidated = false; try { iterator.MoveNext(); } catch (InvalidOperationException) { invalidated = true; }
            if (!invalidated) return false;
            Int32 removed; if (!queue.TryDequeue(out removed) || removed != 5) return false;
            queue.EnsureCapacity(40); queue.TrimExcess(); queue.Clear();
            if (queue.Count != 0 || queue.TryPeek(out peeked) || queue.TryDequeue(out removed)) return false;
            Boolean empty = false; try { queue.Dequeue(); } catch (InvalidOperationException) { empty = true; }
            Queue<Int32> fromEnumerable = new Queue<Int32>(new Int32[] { 3, 4, 5 });
            return empty && fromEnumerable.Count == 3 && fromEnumerable.Dequeue() == 3;
        }

        internal static Boolean TestStack()
        {
            Stack<Int32> stack = new Stack<Int32>(1);
            for (Int32 i = 0; i < 12; i++) stack.Push(i);
            if (stack.Count != 12 || stack.Peek() != 11 || !stack.Contains(8)) return false;
            Int32 peeked; if (!stack.TryPeek(out peeked) || peeked != 11) return false;
            Int32[] array = stack.ToArray();
            Int32[] copied = new Int32[14]; stack.CopyTo(copied, 1);
            if (copied[1] != 11 || copied[12] != 0) return false;
            if (array.Length != 12 || array[0] != 11 || array[array.Length - 1] != 0) return false;
            Int32 expected = 11; foreach (Int32 item in stack) { if (item != expected--) return false; }
            IEnumerator<Int32> iterator = ((IEnumerable<Int32>)stack).GetEnumerator();
            if (!iterator.MoveNext()) return false;
            stack.Push(12);
            Boolean invalidated = false; try { iterator.MoveNext(); } catch (InvalidOperationException) { invalidated = true; }
            if (!invalidated) return false;
            Int32 popped; if (!stack.TryPop(out popped) || popped != 12) return false;
            stack.EnsureCapacity(40); stack.TrimExcess(); stack.Clear();
            if (stack.Count != 0 || stack.TryPeek(out peeked) || stack.TryPop(out popped)) return false;
            Boolean empty = false; try { stack.Pop(); } catch (InvalidOperationException) { empty = true; }
            Stack<Int32> fromEnumerable = new Stack<Int32>(new Int32[] { 3, 4, 5 });
            return empty && fromEnumerable.Count == 3 && fromEnumerable.Pop() == 5;
        }
    }
}
