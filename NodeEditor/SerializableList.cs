using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace NodeEditor
{
    [Serializable]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class SerializedList<T> : SerializableList
    {
        public List<T> Values { get => ReturnList(); set => UpdateList(value); }

        public SerializedList()
        {
        }

        public T Vals(int index)
        {
            return (T)base.values[index];
        }

        public void Add(T obj)
        {
            lock (SyncRoot)
            {
                base.values.Add(obj);
                itemCount = base.values.Count;
            }
        }

        public List<T> ReturnList()
        {
            lock (SyncRoot)
            {
                List<T> vals = new List<T>();

                foreach (object item in base.values)
                {
                    vals.Add((T)item);
                }

                return vals;
            }
        }

        public void UpdateList(List<T> vals)
        {
            lock (SyncRoot)
            {
                base.values.Clear();

                foreach (T item in vals)
                {
                    base.values.Add(item);
                }

                itemCount = values.Count;
            }
        }

        private SerializedList(SerializationInfo info, StreamingContext ctx)
        {
            itemCount = info.GetInt16("C");

            for (int i = 0; i < itemCount; i++)
            {
                base.values.Add(info.GetValue($"L{i}", typeof(T)));
            }
        }
    }

    [Serializable]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class SerializableList : ISerializable
    {
        public int itemCount = 0;

        private readonly object _syncRoot = new object();
        private List<object> _values = new List<object>();

        public List<object> values { get => _values; set => _values = value; }

        public object SyncRoot => _syncRoot;

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

        public void Add(object item)
        {
            lock (_syncRoot)
            {
                _values.Add(item);
                itemCount = _values.Count;
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
                itemCount = _values.Count;
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
                itemCount = _values.Count;
                return true;
            }
        }

        public bool TryRemoveValue(object value)
        {
            lock (_syncRoot)
            {
                var removed = _values.Remove(value);
                if (removed)
                {
                    itemCount = _values.Count;
                }
                return removed;
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _values.Clear();
                itemCount = 0;
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

        private SerializableList(SerializationInfo info, StreamingContext ctx)
        {
            itemCount = info.GetInt16("C");

            for (int i = 0; i < itemCount; i++)
            {
                values.Add(info.GetValue($"L{i}", typeof(object)));
            }
        }

        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            lock (_syncRoot)
            {
                info.AddValue("C", values.Count);

                for (int i = 0; i < values.Count; i++)
                {
                    info.AddValue($"L{i}", values[i]);
                }
            }
        }
    }
}
