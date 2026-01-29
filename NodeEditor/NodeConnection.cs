using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NodeEditor
{
    public class NodeConnection
    {
        public Node OutputNode { get; set; }
        public string OutputSocketName { get; set; }
        public Node InputNode { get; set; }
        public string InputSocketName { get; set; }

        public nSocket OutputSocket
        {
            get { return OutputNode.GetSockets().FirstOrDefault(x => x.Name == OutputSocketName); }
        }

        public nSocket InputSocket
        {
            get { return InputNode.GetSockets().FirstOrDefault(x => x.Name == InputSocketName); }
        }

        public bool IsExecution
        {
            get { return OutputSocket.IsExecution; }
        }
    }
}
