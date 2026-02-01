using System;

namespace NodeEditor
{
    public partial class StandardNodeContext
    {
        [Node("List Add", "Lists", "Basic", "Add item to list.", false)]
        public void ListAdd(SerializableList list, object value, out SerializableList result)
        {
            ReportRunning();
            list?.Add(value);
            result = list;
        }

        [Node("List Insert", "Lists", "Basic", "Insert item at index.", false)]
        public void ListInsert(SerializableList list, nNum index, object value, out bool success)
        {
            ReportRunning();
            success = list != null && list.TryInsert(index.ToInt, value);
        }

        [Node("List Remove At", "Lists", "Basic", "Remove item at index.", false)]
        public void ListRemoveAt(SerializableList list, nNum index, out bool success, out object removed)
        {
            ReportRunning();
            if (list is null)
            {
                success = false;
                removed = new object();
                return;
            }

            success = list.TryRemoveAt(index.ToInt, out removed);
            if (!success)
            {
                removed = new object();
            }
        }

        [Node("List Remove Value", "Lists", "Basic", "Remove first matching value.", false)]
        public void ListRemoveValue(SerializableList list, object value, out bool removed)
        {
            ReportRunning();
            removed = list != null && list.TryRemoveValue(value);
        }

        [Node("List Clear", "Lists", "Basic", "Clear list items.", false)]
        public void ListClear(SerializableList list, out SerializableList result)
        {
            ReportRunning();
            list?.Clear();
            result = list;
        }

        [Node("List Contains", "Lists", "Basic", "Check if list contains value.", false)]
        public void ListContains(SerializableList list, object value, out bool contains)
        {
            ReportRunning();
            contains = list != null && list.Contains(value);
        }

        [Node("List Index Of", "Lists", "Basic", "Get index of first match.", false)]
        public void ListIndexOf(SerializableList list, object value, out nNum index)
        {
            ReportRunning();
            index = new nNum(list?.IndexOf(value) ?? -1);
        }

        [Node("List Get", "Lists", "Basic", "Get item at index.", false)]
        public void ListGet(SerializableList list, nNum index, out object value, out bool found)
        {
            ReportRunning();
            if (list is null)
            {
                value = new object();
                found = false;
                return;
            }

            found = list.TryGetAt(index.ToInt, out value);
            if (!found)
            {
                value = new object();
            }
        }

        [Node("List Set", "Lists", "Basic", "Set item at index.", false)]
        public void ListSet(SerializableList list, nNum index, object value, out bool success)
        {
            ReportRunning();
            success = list != null && list.TrySetAt(index.ToInt, value);
        }

        [Node("List Slice", "Lists", "Basic", "Slice list by start and count.", false)]
        public void ListSlice(SerializableList list, nNum start, nNum count, out SerializableList result)
        {
            ReportRunning();
            result = new SerializableList();

            if (list is null)
            {
                return;
            }

            var snapshot = list.Snapshot();
            var startIndex = Math.Max(0, start.ToInt);
            var length = Math.Max(0, count.ToInt);

            if (startIndex >= snapshot.Length)
            {
                return;
            }

            var end = Math.Min(snapshot.Length, startIndex + length);
            for (var i = startIndex; i < end; i++)
            {
                result.Add(snapshot[i]);
            }
        }
    }
}
