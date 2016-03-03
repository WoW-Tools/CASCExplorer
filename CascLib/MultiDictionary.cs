using System.Collections.Generic;

namespace CASCExplorer
{
    public class MultiDictionary<K, V> : Dictionary<K, List<V>>
    {
        public void Add(K key, V value)
        {
            List<V> hset;
            if (TryGetValue(key, out hset))
            {
                hset.Add(value);
            }
            else
            {
                hset = new List<V>();
                hset.Add(value);
                base[key] = hset;
            }
        }

        public new void Clear()
        {
            foreach (var kv in this)
            {
                kv.Value.Clear();
            }

            base.Clear();
        }
    }
}
