using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NodeEditor
{
    public class nSocket
    {
        public string Name { get; set; }
        public Type Type { get; set; }
        public bool Input { get; set; }
        public object Value { get; set; }
        public bool IsMainExecution { get; set; }

        public SocketVisual visual;

        public nSocket()
        {
            visual = new SocketVisual(this);
        }

        public bool IsExecution
        {
            get { return Type.Name.Replace("&", "") == typeof(ExecutionPath).Name; }
        }
    }
}
