using System.Buffers;
using System.Collections;

namespace SnapshotNet.Utils
{
    internal class WeakHashSet<T> : ICollection<T> where T : class
    {

        public void Add(T item)
        {
            if (!_hashTable.ContainsKey(item))
            {
                _hashTable.SetWeak(item, null);
            }
        }

        public void Clear()
        {
            _hashTable.Clear();
        }

        public bool Contains(T item)
        {
            return _hashTable.ContainsKey(item);
        }

        public void RemoveIf(Predicate<T> predicate)
        {
            var arr = ArrayPool<T>.Shared.Rent(Count);
            var index = 0;
            try
            {
                foreach (var item in _hashTable.Values)
                {
                    var tItem = item as T;
                    if(predicate(tItem))
                    {
                        arr[index++] = tItem;
                    }
                }
                for(int i = 0; i < index; i++)
                {
                    Remove(arr[i]);
                }
            }
            catch (Exception ex)
            {
                ArrayPool<T>.Shared.Return(arr);
            }
        }
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (arrayIndex < 0)
            {
                throw new ArgumentOutOfRangeException("arrayIndex");
            }
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }

            int count = 0;
            foreach (T item in this)
            {
                count++;
            }

            if (count + arrayIndex > array.Length)
            {
                throw new ArgumentOutOfRangeException("arrayIndex");
            }

            foreach (T item in this)
            {
                array[arrayIndex++] = item;
            }
        }

        public int Count
        {
            get
            {
                return _hashTable.Count;
            }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            if (_hashTable.ContainsKey(item))
            {
                _hashTable.Remove(item);
                return true;
            }
            return false;
        }


        public IEnumerator<T> GetEnumerator()
        {
            foreach (object key in _hashTable.Keys)
            {
                WeakHashtable.EqualityWeakReference objRef = key as WeakHashtable.EqualityWeakReference;
                if (objRef != null)
                {
                    T obj = objRef.Target as T;
                    if (obj != null)
                    {
                        yield return obj;
                    }
                }
            }
        }



        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }


        WeakHashtable _hashTable = new WeakHashtable();
    }
}
