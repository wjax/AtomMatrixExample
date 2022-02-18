using System;
using System.Collections;
using System.Text;

namespace Nano.Base
{
    public class CircularBuffer<T>
    {
        int _size;
        T[] _values = null;
        int _valuesIndex = 0;
        int _valueCount = 0;

        public CircularBuffer(int size)
        {
            _size = Math.Max(size, 1);
            _values = new T[_size];
        }

        public int Length => _size;
        public T this[int key]
        {
            get => _values[key];
        }

        public void Add(T newValue)
        {
            _values[_valuesIndex] = newValue;
            _valuesIndex++;
            _valuesIndex %= _size;

            if (_valueCount < _size)
                _valueCount++;
        }

        public T Newest { get => _values[ (_valuesIndex - 1) < 0 ? _size-1 : (_valuesIndex - 1)]; }

        public T Oldest { get => _values[_valuesIndex]; }
    }
}
