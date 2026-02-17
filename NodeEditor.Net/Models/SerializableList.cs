using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace NodeEditor.Net.Models;

[Serializable]
[JsonConverter(typeof(SerializableListJsonConverter))]
public sealed class SerializableList : ISerializable
{
    private readonly object _syncRoot = new();
    private readonly List<object> _values = new();

    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _values.Count;
            }
        }
    }

    public SerializableList()
    {
    }

    private SerializableList(SerializationInfo info, StreamingContext context)
    {
        var count = info.GetInt32("C");
        for (var i = 0; i < count; i++)
        {
            _values.Add(info.GetValue($"L{i}", typeof(object))!);
        }
    }

    public void Add(object value)
    {
        lock (_syncRoot)
        {
            _values.Add(value);
        }
    }

    public bool TryGetAt(int index, out object value)
    {
        lock (_syncRoot)
        {
            if (index < 0 || index >= _values.Count)
            {
                value = default!;
                return false;
            }

            value = _values[index];
            return true;
        }
    }

    public bool TrySetAt(int index, object value)
    {
        lock (_syncRoot)
        {
            if (index < 0 || index >= _values.Count)
            {
                return false;
            }

            _values[index] = value;
            return true;
        }
    }

    public bool TryInsert(int index, object value)
    {
        lock (_syncRoot)
        {
            if (index < 0 || index > _values.Count)
            {
                return false;
            }

            _values.Insert(index, value);
            return true;
        }
    }

    public bool TryRemoveAt(int index, out object value)
    {
        lock (_syncRoot)
        {
            if (index < 0 || index >= _values.Count)
            {
                value = default!;
                return false;
            }

            value = _values[index];
            _values.RemoveAt(index);
            return true;
        }
    }

    public bool TryRemoveValue(object value)
    {
        lock (_syncRoot)
        {
            return _values.Remove(value);
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _values.Clear();
        }
    }

    public bool Contains(object value)
    {
        lock (_syncRoot)
        {
            return _values.Contains(value);
        }
    }

    public int IndexOf(object value)
    {
        lock (_syncRoot)
        {
            return _values.IndexOf(value);
        }
    }

    public object[] Snapshot()
    {
        lock (_syncRoot)
        {
            return _values.ToArray();
        }
    }

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        lock (_syncRoot)
        {
            info.AddValue("C", _values.Count);
            for (var i = 0; i < _values.Count; i++)
            {
                info.AddValue($"L{i}", _values[i]);
            }
        }
    }
}
