using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace NodeEditor.Net.Models;

/// <summary>
/// A thread-safe, serializable dictionary for use in node graphs.
/// Keys are always strings; values can be any object.
/// </summary>
[Serializable]
[JsonConverter(typeof(SerializableDictJsonConverter))]
public sealed class SerializableDict : ISerializable
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, object> _values = new();

    public int Count
    {
        get { lock (_syncRoot) return _values.Count; }
    }

    public SerializableDict() { }

    private SerializableDict(SerializationInfo info, StreamingContext context)
    {
        var count = info.GetInt32("C");
        for (var i = 0; i < count; i++)
        {
            var key = info.GetString($"K{i}")!;
            var value = info.GetValue($"V{i}", typeof(object))!;
            _values[key] = value;
        }
    }

    public void Set(string key, object value)
    {
        lock (_syncRoot) _values[key] = value;
    }

    public bool TryGet(string key, out object value)
    {
        lock (_syncRoot) return _values.TryGetValue(key, out value!);
    }

    public bool ContainsKey(string key)
    {
        lock (_syncRoot) return _values.ContainsKey(key);
    }

    public bool Remove(string key)
    {
        lock (_syncRoot) return _values.Remove(key);
    }

    public void Clear()
    {
        lock (_syncRoot) _values.Clear();
    }

    public string[] Keys()
    {
        lock (_syncRoot) return _values.Keys.ToArray();
    }

    public object[] Values()
    {
        lock (_syncRoot) return _values.Values.ToArray();
    }

    public KeyValuePair<string, object>[] Snapshot()
    {
        lock (_syncRoot) return _values.ToArray();
    }

    public SerializableDict Clone()
    {
        lock (_syncRoot)
        {
            var clone = new SerializableDict();
            foreach (var kvp in _values)
                clone._values[kvp.Key] = kvp.Value;
            return clone;
        }
    }

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        lock (_syncRoot)
        {
            info.AddValue("C", _values.Count);
            int i = 0;
            foreach (var kvp in _values)
            {
                info.AddValue($"K{i}", kvp.Key);
                info.AddValue($"V{i}", kvp.Value);
                i++;
            }
        }
    }
}
