using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapshotNet.Extensions
{
    public static class HashSetExtensions
    {
        public static HashSet<T> CloneAndAdd<T>(this HashSet<T> source, T value)
        {
            var cloned = new HashSet<T>(source)
            {
                value
            };
            return cloned;
        }
        public static HashSet<T> CloneAndRemove<T>(this HashSet<T> source, T value)
        {
            var cloned = new HashSet<T>(source);
            cloned.Remove(value);
            return cloned;
        }

        public static HashSet<int> CloneAndAddRange(this HashSet<int> source, int from, int until)
        {
            var cloned = new HashSet<int>(source);
            for (int i = from; i < until; i++)
                cloned.Add(i);
            return cloned;
        }
        public static HashSet<T> Clone<T>(this HashSet<T> source)
        {
            var cloned = new HashSet<T>(source);
            return cloned;
        }
    }
}
