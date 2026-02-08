using System;
using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution;

public sealed partial class StandardNodeContext
{
    [Node("List Create", "Lists", "Basic", "Create an empty list.", false)]
    public void ListCreate(out SerializableList List)
    {
        ReportRunning();
        List = new SerializableList();
    }

    [Node("List Add", "Lists", "Basic", "Add item to list.", false)]
    public void ListAdd(SerializableList List, object Value, out SerializableList Result)
    {
        ReportRunning();
        List?.Add(Value);
        Result = List!;
    }

    [Node("List Insert", "Lists", "Basic", "Insert item at index.", false)]
    public void ListInsert(SerializableList List, int Index, object Value, out bool Success)
    {
        ReportRunning();
        Success = List != null && List.TryInsert(Index, Value);
    }

    [Node("List Remove At", "Lists", "Basic", "Remove item at index.", false)]
    public void ListRemoveAt(SerializableList List, int Index, out bool Success, out object Removed)
    {
        ReportRunning();
        if (List is null)
        {
            Success = false;
            Removed = new object();
            return;
        }

        Success = List.TryRemoveAt(Index, out Removed);
        if (!Success)
        {
            Removed = new object();
        }
    }

    [Node("List Remove Value", "Lists", "Basic", "Remove first matching value.", false)]
    public void ListRemoveValue(SerializableList List, object Value, out bool Removed)
    {
        ReportRunning();
        Removed = List != null && List.TryRemoveValue(Value);
    }

    [Node("List Clear", "Lists", "Basic", "Clear list items.", false)]
    public void ListClear(SerializableList List, out SerializableList Result)
    {
        ReportRunning();
        List?.Clear();
        Result = List!;
    }

    [Node("List Contains", "Lists", "Basic", "Check if list contains value.", false)]
    public void ListContains(SerializableList List, object Value, out bool Contains)
    {
        ReportRunning();
        Contains = List != null && List.Contains(Value);
    }

    [Node("List Index Of", "Lists", "Basic", "Get index of first match.", false)]
    public void ListIndexOf(SerializableList List, object Value, out int Index)
    {
        ReportRunning();
        Index = List?.IndexOf(Value) ?? -1;
    }

    [Node("List Count", "Lists", "Basic", "Get list item count.", false)]
    public void ListCount(SerializableList List, out int Count)
    {
        ReportRunning();
        Count = List?.Count ?? 0;
    }

    [Node("List Get", "Lists", "Basic", "Get item at index.", false)]
    public void ListGet(SerializableList List, int Index, out object Value, out bool Found)
    {
        ReportRunning();
        if (List is null)
        {
            Value = new object();
            Found = false;
            return;
        }

        Found = List.TryGetAt(Index, out Value);
        if (!Found)
        {
            Value = new object();
        }
    }

    [Node("List Set", "Lists", "Basic", "Set item at index.", false)]
    public void ListSet(SerializableList List, int Index, object Value, out bool Success)
    {
        ReportRunning();
        Success = List != null && List.TrySetAt(Index, Value);
    }

    [Node("List Slice", "Lists", "Basic", "Slice list by start and count.", false)]
    public void ListSlice(SerializableList List, int Start, int Count, out SerializableList Result)
    {
        ReportRunning();
        Result = new SerializableList();

        if (List is null)
        {
            return;
        }

        var snapshot = List.Snapshot();
        var startIndex = Math.Max(0, Start);
        var length = Math.Max(0, Count);

        if (startIndex >= snapshot.Length)
        {
            return;
        }

        var end = Math.Min(snapshot.Length, startIndex + length);
        for (var i = startIndex; i < end; i++)
        {
            Result.Add(snapshot[i]);
        }
    }
}
