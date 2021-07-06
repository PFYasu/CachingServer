using System;
using System.Collections.Generic;
using System.Linq;

namespace CachingServer
{
    class ByteData
    {
        public int ValueBytes { get; private set; }
        public readonly int Limit;

        private readonly Dictionary<List<byte>, List<byte>> dict;
        private readonly object dictLock;

        public ByteData(int limit)
        {
            ValueBytes = 0;
            Limit = limit;

            dict = new Dictionary<List<byte>, List<byte>>(new ListByteComparer());
            dictLock = new object();
        }

        public List<byte> GetValue(List<byte> key)
        {
            lock(dictLock)
            {
                try
                {
                    return dict[key];
                }
                catch (Exception)
                {
                    throw new Exception("Unable to get value");
                }
            }
        }

        public void SetValue(List<byte> key, List<byte> value)
        {
            lock(dictLock)
            {
                if(value.Count > Limit)
                {
                    throw new Exception("Value length is bigger than limit");
                }

                if (dict.ContainsKey(key))
                {
                    ValueBytes -= dict[key].Count;
                }

                while(ValueBytes + value.Count > Limit)
                {
                    dict.Remove(dict.Keys.FirstOrDefault());
                }

                ValueBytes += value.Count;
                dict[key] = value;
            }
        }
    }

    class ListByteComparer : IEqualityComparer<List<byte>>
    {
        public int GetHashCode(List<byte> list)
        {
            if(list.Count > 0)
            {
                int first = list[0];
                first *= list.Count;

                int second = list[list.Count - 1];

                return second * 17 + first;
            }
            return 0;
        }

        public bool Equals(List<byte> first, List<byte> second)
        {
            if (first == null || second == null)
            {
                return first == second;
            }
            return first.SequenceEqual(second);
        }
    }
}
