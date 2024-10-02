using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapshotNet.Utils
{

    internal class SnapshotDoubleIndexHeap
    {
        private int size = 0;

        // An array of values which are the snapshot ids
        private int[] values = new int[INITIAL_CAPACITY];

        // An array of where the value's handle is in the handles array.
        private int[] index = new int[INITIAL_CAPACITY];

        // An array of handles which tracks where the value is in the values array. Free handles are stored
        // as a single linked list using the array value as the link to the next free handle location.
        // It is initialized with 1, 2, 3, ... which produces a linked list of all handles free starting
        // at 0.
        private int[] handles = new int[INITIAL_CAPACITY];

        // The first free handle.
        private int firstFreeHandle = 0;

        private const int INITIAL_CAPACITY = 16;

        public SnapshotDoubleIndexHeap()
        {
            for (int i = 0; i < INITIAL_CAPACITY; i++)
            {
                handles[i] = i + 1;
            }
        }

        public int LowestOrDefault(int defaultValue = 0)
        {
            return size > 0 ? values[0] : defaultValue;
        }

        public int Add(int value)
        {
            Ensure(size + 1);
            int i = size++;
            int handle = AllocateHandle();
            values[i] = value;
            index[i] = handle;
            handles[handle] = i;
            ShiftUp(i);
            return handle;
        }

        public void Remove(int handle)
        {
            int i = handles[handle];
            Swap(i, size - 1);
            size--;
            ShiftUp(i);
            ShiftDown(i);
            FreeHandle(handle);
        }

        // Validate that the heap invariants hold.
        public void Validate()
        {
            for (int idx = 1; idx < size; idx++)
            {
                int parent = ((idx + 1) >> 1) - 1;
                if (values[parent] > values[idx])
                {
                    throw new InvalidOperationException($"Index {idx} is out of place");
                }
            }
        }

        // Validate that the handle refers to the expected value.
        public void ValidateHandle(int handle, int value)
        {
            int i = handles[handle];
            if (index[i] != handle)
            {
                throw new InvalidOperationException($"Index for handle {handle} is corrupted");
            }

            if (values[i] != value)
            {
                throw new InvalidOperationException($"Value for handle {handle} was {values[i]} but was supposed to be {value}");
            }
        }

        private void ShiftUp(int idx)
        {
            int value = values[idx];
            int current = idx;

            while (current > 0)
            {
                int parent = ((current + 1) >> 1) - 1;
                if (values[parent] > value)
                {
                    Swap(parent, current);
                    current = parent;
                    continue;
                }
                break;
            }
        }

        private void ShiftDown(int idx)
        {
            int half = size >> 1;
            int current = idx;

            while (current < half)
            {
                int right = (current + 1) << 1;
                int left = right - 1;

                if (right < size && values[right] < values[left])
                {
                    if (values[right] < values[current])
                    {
                        Swap(right, current);
                        current = right;
                    }
                    else
                    {
                        return;
                    }
                }
                else if (values[left] < values[current])
                {
                    Swap(left, current);
                    current = left;
                }
                else
                {
                    return;
                }
            }
        }

        private void Swap(int a, int b)
        {
            int tempValue = values[a];
            values[a] = values[b];
            values[b] = tempValue;

            int tempIndex = index[a];
            index[a] = index[b];
            index[b] = tempIndex;

            handles[index[a]] = a;
            handles[index[b]] = b;
        }

        private void Ensure(int atLeast)
        {
            int capacity = values.Length;
            if (atLeast <= capacity) return;

            int newCapacity = capacity * 2;
            Array.Resize(ref values, newCapacity);
            Array.Resize(ref index, newCapacity);
        }

        private int AllocateHandle()
        {
            int capacity = handles.Length;
            if (firstFreeHandle >= capacity)
            {
                int newCapacity = capacity * 2;
                int[] newHandles = new int[newCapacity];
                Array.Copy(handles, newHandles, capacity);
                for (int i = capacity; i < newCapacity; i++)
                {
                    newHandles[i] = i + 1;
                }
                handles = newHandles;
            }

            int handle = firstFreeHandle;
            firstFreeHandle = handles[firstFreeHandle];
            return handle;
        }

        private void FreeHandle(int handle)
        {
            handles[handle] = firstFreeHandle;
            firstFreeHandle = handle;
        }
    }

}
