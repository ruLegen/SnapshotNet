using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapshotNet.Extensions
{
    public static class ObjectExtensions
    {
        public static T Also<T>(this T o,Action<T> action)
        {
            action(o);
            return o;
        }
        public static R Let<T,R>(this T o, Func<T,R> block)
        {
            return block(o);
        }
    }
}
