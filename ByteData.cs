using System;
using System.Collections.Generic;

namespace CachingServer
{
    class ByteData
    {
        private readonly List<List<byte>> keys;
        private readonly List<List<byte>> values;
        private readonly object sync;
        private readonly long limiter;

        public long ValueBytes { get; private set; }

        public ByteData(long limiter)
        {
            keys = new List<List<byte>>();
            values = new List<List<byte>>();
            sync = new object();
            this.limiter = limiter;
            ValueBytes = 0;
        }

        public List<byte> GetValue(List<byte> key)
        {
            int keyPosition = getKeyPosition(key);
            if(keyPosition >= 0)
            {
                lock(sync)
                {
                    return values[keyPosition];
                }
            }
            throw new Exception("Key not found!");
        }

        public void SetValue(List<byte> key, List<byte> value)
        {
            if (values.Count == 0 && value.Count + ValueBytes > limiter) return;

            while (values.Count > 0 && value.Count + ValueBytes > limiter)
            {
                lock(sync)
                {
                    ValueBytes -= values[0].Count;
                    keys.RemoveAt(0);
                    values.RemoveAt(0);
                }
            }

            int keyPosition = getKeyPosition(key);
            if (keyPosition >= 0)
            {
                lock(sync)
                {
                    ValueBytes -= values[keyPosition].Count;
                    values[keyPosition] = value;
                    ValueBytes += values[keyPosition].Count;
                }
            }
            else
            {
                lock(sync)
                {
                    keys.Add(key);
                    values.Add(value);
                    ValueBytes += values[values.Count - 1].Count;
                }
            }
        }

        private int getKeyPosition(List<byte> key)
        {
            lock (sync)
            {
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i].Count == key.Count)
                    {
                        for (int j = 0; j < keys[i].Count; j++)
                        {
                            if (keys[i][j] != key[j])
                            {
                                break;
                            }
                            if (j + 1 == keys[i].Count) return i;
                        }
                    }
                }
                return -1;
            }
        }
    }
}
