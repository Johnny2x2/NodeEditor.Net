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
            Values.Add(obj);
            base.values.Add(obj);
            itemCount++;
        }

        public List<T> ReturnList()
        {
            List<T> vals = new List<T>();

            foreach (object item in base.values)
            {
                vals.Add((T)item);
            }

            return vals;
        }

        public void UpdateList(List<T> vals)
        {
            base.values.Clear();

            foreach (T item in vals)
            {
                base.values.Add(item);
            }

            itemCount = values.Count;
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

        private List<object> _values = new List<object>();

        public List<object> values { get => _values; set => _values = value; }

        public SerializableList()
        {
        }

        public void Add(object item)
        {
            _values.Add(item);
            itemCount++;
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
            info.AddValue("C", values.Count);

            for (int i = 0; i < values.Count; i++)
            {
                info.AddValue($"L{i}", values[i]);
            }
        }
    }
}
