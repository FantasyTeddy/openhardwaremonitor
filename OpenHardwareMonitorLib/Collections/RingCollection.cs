﻿/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2011 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenHardwareMonitor.Collections
{
    public class RingCollection<T> : IEnumerable<T>
    {

        private T[] array;

        // first item of collection
        private int head;

        // index after the last item of the collection
        private int tail;

        public RingCollection() : this(0) { }

        public RingCollection(int capacity)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException("capacity");
            this.array = new T[capacity];
            this.head = 0;
            this.tail = 0;
            Count = 0;
        }

        public int Capacity
        {
            get => array.Length;
            set
            {
                T[] newArray = new T[value];
                if (Count > 0)
                {
                    if (head < tail)
                    {
                        Array.Copy(array, head, newArray, 0, Count);
                    }
                    else
                    {
                        Array.Copy(array, head, newArray, 0, array.Length - head);
                        Array.Copy(array, 0, newArray, array.Length - head, tail);
                    }
                }
                this.array = newArray;
                this.head = 0;
                this.tail = Count == value ? 0 : Count;
            }
        }

        public void Clear()
        {

            // remove potential references 
            if (head < tail)
            {
                Array.Clear(array, head, Count);
            }
            else
            {
                Array.Clear(array, 0, tail);
                Array.Clear(array, head, array.Length - head);
            }

            this.head = 0;
            this.tail = 0;
            Count = 0;
        }

        public void Append(T item)
        {
            if (Count == array.Length)
            {
                int newCapacity = array.Length * 3 / 2;
                if (newCapacity < array.Length + 8)
                    newCapacity = array.Length + 8;
                Capacity = newCapacity;
            }

            array[tail] = item;
            tail = tail + 1 == array.Length ? 0 : tail + 1;
            Count++;
        }

        public T Remove()
        {
            if (Count == 0)
                throw new InvalidOperationException();

            T result = array[head];
            array[head] = default;
            head = head + 1 == array.Length ? 0 : head + 1;
            Count--;

            return result;
        }

        public int Count { get; private set; }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                    throw new IndexOutOfRangeException();
                int i = head + index;
                if (i >= array.Length)
                    i -= array.Length;
                return array[i];
            }
            set
            {
                if (index < 0 || index >= Count)
                    throw new IndexOutOfRangeException();
                int i = head + index;
                if (i >= array.Length)
                    i -= array.Length;
                array[i] = value;
            }
        }

        public T First
        {
            get
            {
                if (Count == 0)
                    throw new InvalidOperationException();
                return array[head];
            }
            set
            {
                if (Count == 0)
                    throw new InvalidOperationException();
                array[head] = value;
            }
        }

        public T Last
        {
            get
            {
                if (Count == 0)
                    throw new InvalidOperationException();
                return array[tail == 0 ? array.Length - 1 : tail - 1];
            }
            set
            {
                if (Count == 0)
                    throw new InvalidOperationException();
                array[tail == 0 ? array.Length - 1 : tail - 1] = value;
            }
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new RingCollection<T>.Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new RingCollection<T>.Enumerator(this);
        }

        private struct Enumerator : IEnumerator<T>, IEnumerator
        {

            private readonly RingCollection<T> collection;
            private int index;

            public Enumerator(RingCollection<T> collection)
            {
                this.collection = collection;
                this.index = -1;
            }

            public void Dispose()
            {
                this.index = -2;
            }

            public void Reset()
            {
                this.index = -1;
            }

            public T Current
            {
                get
                {
                    if (index < 0)
                        throw new InvalidOperationException();
                    return collection[index];
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    if (index < 0)
                        throw new InvalidOperationException();
                    return collection[index];
                }
            }

            public bool MoveNext()
            {
                if (index == -2)
                    return false;

                index++;

                if (index == collection.Count)
                {
                    index = -2;
                    return false;
                }

                return true;
            }
        }
    }
}
