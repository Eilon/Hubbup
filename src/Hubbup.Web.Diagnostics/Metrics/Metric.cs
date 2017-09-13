using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Hubbup.Web.Diagnostics.Metrics
{
    public struct Metric
    {
        public double Value { get; }
        public string Measurement { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }
        public DateTimeOffset Timestamp { get; }

        public Metric(string measurement, TimeSpan value) : this(measurement, value.TotalMilliseconds) { }
        public Metric(string measurement, TimeSpan value, DateTimeOffset timestamp) : this(measurement, value.TotalMilliseconds, timestamp) { }
        public Metric(string measurement, TimeSpan value, IReadOnlyDictionary<string, string> tags) : this(measurement, value.TotalMilliseconds, tags) { }
        public Metric(string measurement, TimeSpan value, DateTimeOffset timestamp, IReadOnlyDictionary<string, string> tags) : this(measurement, value.TotalMilliseconds, timestamp, tags) { }

        public Metric(string measurement, double value) : this(measurement, value, DateTimeOffset.Now, EmptyDictionary<string, string>.Instance) { }
        public Metric(string measurement, double value, DateTimeOffset timestamp) : this(measurement, value, timestamp, EmptyDictionary<string, string>.Instance) { }
        public Metric(string measurement, double value, IReadOnlyDictionary<string, string> tags) : this(measurement, value, DateTimeOffset.Now, tags) { }
        public Metric(string measurement, double value, DateTimeOffset timestamp, IReadOnlyDictionary<string, string> tags)
        {
            Measurement = measurement;
            Timestamp = timestamp;
            Value = value;
            Tags = tags;
        }

        private class EmptyDictionary<K, V> : IReadOnlyDictionary<K, V>
        {
            public static readonly EmptyDictionary<K, V> Instance = new EmptyDictionary<K, V>();

            public V this[K key] => throw new KeyNotFoundException();
            public IEnumerable<K> Keys => Enumerable.Empty<K>();
            public IEnumerable<V> Values => Enumerable.Empty<V>();
            public int Count => 0;
            public bool ContainsKey(K key) => false;

            private EmptyDictionary() { }

            public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => Enumerable.Empty<KeyValuePair<K, V>>().GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public bool TryGetValue(K key, out V value)
            {
                value = default;
                return false;
            }
        }
    }
}
