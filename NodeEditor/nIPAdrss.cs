using System;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace NodeEditor
{
    [Serializable]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class nIPAdrss : ISerializable
    {
        private byte digi1 = 127;
        public byte Digi1 { get => digi1; set => digi1 = value; }

        private byte digi2 = 0;
        public byte Digi2 { get => digi2; set => digi2 = value; }

        private byte digi3 = 0;
        public byte Digi3 { get => digi3; set => digi3 = value; }

        private byte digi4 = 1;
        public byte Digi4 { get => digi4; set => digi4 = value; }

        public int Port { get => port; set => port = value; }

        private int port = 9999;

        public nIPAdrss()
        {
        }

        public override string ToString()
        {
            return $@"{Digi1}.{Digi2}.{Digi3}.{Digi4}:{Port}";
        }

        public string ToIPString()
        {
            return $@"{Digi1}.{Digi2}.{Digi3}.{Digi4}";
        }

        private nIPAdrss(SerializationInfo info, StreamingContext ctx)
        {
            Digi1 = info.GetByte("D1");
            Digi2 = info.GetByte("D2");
            Digi3 = info.GetByte("D3");
            Digi4 = info.GetByte("D4");
            try
            {
                Port = info.GetInt32("P");
            }
            catch
            {
                Port = 9999;
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("D1", digi1);
            info.AddValue("D2", digi2);
            info.AddValue("D3", digi3);
            info.AddValue("D4", digi4);
            info.AddValue("P", port);
        }
    }
}
