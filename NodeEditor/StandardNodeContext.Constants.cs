using System;

namespace NodeEditor
{
    public partial class StandardNodeContext
    {
        [Node("Create Constant Number", "Constants/Numbers", "Basic", "Signal Node", false)]
        public void CreateNumConst(out string guid, out nNum value)
        {
            nNum num = new nNum();

            guid = CurrentProcessingNode.GetGuid();
            value = num;

            if (!dynamicDict.TryGetValue(guid, out var temp))
            {
                dynamicDict.TryAdd(guid, num);
            }
            else
            {
                value = temp as nNum;
            }
        }

        [Node("Update Number", "Constants/Numbers", "Basic", "Update List", true)]
        public void SetnNum(string guid, nNum iValue, out nNum oValue)
        {
            dynamicDict[guid] = iValue;
            oValue = iValue;
        }

        [Node("Create List", "Constants/List", "Basic", "Signal Node", false)]
        public void CreateListConst(out string guid, out SerializableList value)
        {
            SerializableList list = new SerializableList();

            guid = CurrentProcessingNode.GetGuid();
            value = list;

            if (!dynamicDict.TryGetValue(guid, out var temp))
            {
                dynamicDict.TryAdd(guid, list);
            }
            else
            {
                value = temp as SerializableList;
            }
        }

        [Node("Set List", "Constants/List", "Basic", "Update List", true)]
        public void SetConstantList(string guid, SerializableList iValue, out SerializableList oValue)
        {
            dynamicDict[guid] = iValue;
            oValue = iValue;
        }

        [Node("List Add", "Constants/List", "Basic", "Signal Node", true)]
        public void Add2ConstantList(string guid, object iValue, out object oValue)
        {
            if (dynamicDict.TryGetValue(guid, out var temp) && temp is SerializableList dList)
            {
                dList.Add(iValue);
            }
            oValue = iValue;
        }

        [Node("Get List Element", "Constants/List", "Basic", "", false)]
        public void GetListElement(SerializableList list, nNum index, out object obj)
        {
            if (!list.TryGetAt(index.ToInt, out obj))
            {
                obj = new object();
            }
        }

        [Node("List Count", "Constants/List", "Basic", "Signal Node", false)]
        public void ListSize(SerializableList list, out nNum length)
        {
            length = new nNum(list.Count);
        }
    }
}
