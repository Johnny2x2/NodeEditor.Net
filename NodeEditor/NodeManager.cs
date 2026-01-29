using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace NodeEditor
{

    public class NodeManager
    {
        public NodeControl control;

        internal List<Node> Nodes = new List<Node>();
        internal List<NodeConnection> Connections = new List<NodeConnection>();

        internal class NodeToken
        {
#pragma warning disable CS0649 // Field 'NodeManager.NodeToken.Method' is never assigned to, and will always have its default value null
            public MethodInfo Method;
#pragma warning restore CS0649 // Field 'NodeManager.NodeToken.Method' is never assigned to, and will always have its default value null
#pragma warning disable CS0649 // Field 'NodeManager.NodeToken.Attribute' is never assigned to, and will always have its default value null
            public NodeAttribute Attribute;
#pragma warning restore CS0649 // Field 'NodeManager.NodeToken.Attribute' is never assigned to, and will always have its default value null
        }

        public event EventHandler OnExecutionFinished;
        public event EventHandler OnExecutionCanceled;

        public Stack<Node> executionStack = new Stack<Node>();
        public bool rebuildConnectionDictionary = true;
        public Dictionary<string, NodeConnection> connectionDictionary = new Dictionary<string, NodeConnection>();

        private INodesContext context;

        private bool breakExecution = false;

        public static object OutputObject = null;

        public NodeManager()
        {

        }

        /// <summary>
        /// Context of the editor. You should set here an instance that implements INodesContext interface.
        /// In context you should define your nodes (methods decorated by Node attribute).
        /// </summary>
        public INodesContext Context
        {
            get 
            { 
                return context; 
            }
            set
            {
                if (context != null)
                {
                    context.FeedbackInfo -= ContextOnFeedbackInfo;
                }

                context = value;

                if (context != null)
                {
                    context.FeedbackInfo += ContextOnFeedbackInfo;
                }
            }
        }

        private void ContextOnFeedbackInfo(string message, Node node, FeedbackType type, object tag, bool breakExecution)
        {
            this.breakExecution = breakExecution;
            if (breakExecution)
            {
                node.Feedback = type;
                Debug.WriteLine(message);
                //OnNodeHint(message);
            }
        }

        public static void Execute(CancellationToken token, NodeManager manager)
        {
            manager.StartExecute(token);
        }

        /// <summary>
        /// Executes whole node graph (when called parameterless) or given node when specified.
        /// </summary>
        /// <param name="node"></param>
        public void StartExecute(CancellationToken token, Node node = null)
        {           
            NodeManager.OutputObject = null;
            var nodeQueue = new Queue<Node>();
            nodeQueue.Enqueue(node);

            while (nodeQueue.Count > 0)
            {
                //Refresh();
                if (token.IsCancellationRequested)
                {
                    breakExecution = false;
                    executionStack.Clear();
                    OnExecutionCanceled?.Invoke(this, new EventArgs());
                    return;
                }

                var init = nodeQueue.Dequeue() ?? Nodes.FirstOrDefault(x => x.ExecInit);
                if (init != null)
                {
                    init.Feedback = FeedbackType.Debug;

                    Resolve(init);
                    init.Execute(Context);

                    var connection =
                        Connections.FirstOrDefault(
                            x => x.OutputNode == init && x.IsExecution && x.OutputSocket.Value != null && (x.OutputSocket.Value as ExecutionPath).IsSignaled);

                    if (connection == null)
                    {
                        connection = Connections.FirstOrDefault(x => x.OutputNode == init && x.IsExecution && x.OutputSocket.IsMainExecution);
                    }
                    else
                    {
                        //executionStack.Push(init); //Causes infi loop when using exection path as inputs and outputs
                    }

                    if (connection != null)
                    {
                        connection.InputNode.IsBackExecuted = false;
                        //Execute(connection.InputNode);
                        nodeQueue.Enqueue(connection.InputNode);
                    }
                    else
                    {
                        if (executionStack.Count > 0)
                        {
                            var back = executionStack.Pop();
                            back.IsBackExecuted = true;
                            StartExecute(token, back);
                        }
                    }
                }
            }

            OnExecutionFinished?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// Resolves given node, resolving it all dependencies recursively.
        /// </summary>
        /// <param name="node"></param>
        private void Resolve(Node node)
        {
            var icontext = (node.GetNodeContext() as DynamicNodeContext);
            foreach (var input in node.GetInputs())
            {
                var connection = GetConnection(node.GUID + input.Name);
                //graph.Connections.FirstOrDefault(x => x.InputNode == node && x.InputSocketName == input.Name);
                if (connection != null)
                {
                    Resolve(connection.OutputNode);
                    if (!connection.OutputNode.Callable)
                    {
                        connection.OutputNode.Execute(Context);
                    }
                    var ocontext = (connection.OutputNode.GetNodeContext() as DynamicNodeContext);
                    icontext[connection.InputSocketName] = ocontext[connection.OutputSocketName];
                }
            }
        }

        public void ExecuteResolving(params string[] nodeNames)
        {
            var nodes = Nodes.Where(x => nodeNames.Contains(x.Name));

            foreach (var node in nodes)
            {
                ExecuteResolvingInternal(node);
            }
        }

        private void ExecuteResolvingInternal(Node node)
        {
            var icontext = (node.GetNodeContext() as DynamicNodeContext);
            foreach (var input in node.GetInputs())
            {
                var connection =
                    Connections.FirstOrDefault(x => x.InputNode == node && x.InputSocketName == input.Name);
                if (connection != null)
                {
                    Resolve(connection.OutputNode);

                    connection.OutputNode.Execute(Context);

                    ExecuteResolvingInternal(connection.OutputNode);

                    var ocontext = (connection.OutputNode.GetNodeContext() as DynamicNodeContext);
                    icontext[connection.InputSocketName] = ocontext[connection.OutputSocketName];
                }
            }
        }


        public void RebuildConnectionDictionary()
        {
            rebuildConnectionDictionary = true;
        }

        private NodeConnection GetConnection(string v)
        {
            if (rebuildConnectionDictionary)
            {
                rebuildConnectionDictionary = false;
                connectionDictionary.Clear();
                foreach (var conn in Connections)
                {
                    connectionDictionary.Add(conn.InputNode.GUID + conn.InputSocketName, conn);
                }
            }
            NodeConnection nc = null;
            if (connectionDictionary.TryGetValue(v, out nc))
            {
                return nc;
            }
            return null;
        }

        /// <summary>
        /// Serializes current node graph to binary data.
        /// </summary>        
        public byte[] Serialize()
        {
            using (var bw = new BinaryWriter(new MemoryStream()))
            {
                bw.Write("NodeSystemP"); //recognization string
                bw.Write(1000); //version
                bw.Write(Nodes.Count);
                foreach (var node in Nodes)
                {
                    SerializeNode(bw, node);
                }
                bw.Write(Connections.Count);
                foreach (var connection in Connections)
                {
                    bw.Write(connection.OutputNode.GUID);
                    bw.Write(connection.OutputSocketName);

                    bw.Write(connection.InputNode.GUID);
                    bw.Write(connection.InputSocketName);
                    bw.Write(0); //additional data size per connection
                }
                bw.Write(0); //additional data size per graph
                return (bw.BaseStream as MemoryStream).ToArray();
            }
        }

        public static void SerializeNode(BinaryWriter bw, Node node)
        {
            bw.Write(node.GUID);
            bw.Write(node.visual.X);
            bw.Write(node.visual.Y);
            bw.Write(node.Callable);
            bw.Write(node.ExecInit);
            bw.Write(node.Name);
            bw.Write(node.Order);
            if (node.visual.CustomEditor == null)
            {
                bw.Write("");
                bw.Write("");
            }
            else
            {
                bw.Write(node.visual.CustomEditor.GetType().Assembly.GetName().Name);
                bw.Write(node.visual.CustomEditor.GetType().FullName);
            }
            bw.Write(node.Type.Name);
            var context = (node.GetNodeContext() as DynamicNodeContext).Serialize();
            bw.Write(context.Length);
            bw.Write(context);
            bw.Write(8); //additional data size per node
            bw.Write(node.Int32Tag);
            bw.Write(node.visual.NodeColor.ToArgb());
        }

        /// <summary>
        /// Restores node graph state from previously serialized binary data.
        /// </summary>
        /// <param name="data"></param>
        public void Deserialize(byte[] data)
        {
            using (var br = new BinaryReader(new MemoryStream(data)))
            {
                var ident = br.ReadString();
                if (ident != "NodeSystemP") return;
                rebuildConnectionDictionary = true;
                Connections.Clear();
                Nodes.Clear();

                var version = br.ReadInt32();
                int nodeCount = br.ReadInt32();
                for (int i = 0; i < nodeCount; i++)
                {
                    var nv = DeserializeNode(br);

                    Nodes.Add(nv);
                }
                var connectionsCount = br.ReadInt32();
                for (int i = 0; i < connectionsCount; i++)
                {
                    var con = new NodeConnection();
                    var og = br.ReadString();
                    con.OutputNode = Nodes.FirstOrDefault(x => x.GUID == og);
                    con.OutputSocketName = br.ReadString();
                    var ig = br.ReadString();
                    con.InputNode = Nodes.FirstOrDefault(x => x.GUID == ig);
                    con.InputSocketName = br.ReadString();
                    br.ReadBytes(br.ReadInt32()); //read additional data

                    Connections.Add(con);
                    rebuildConnectionDictionary = true;
                }
                br.ReadBytes(br.ReadInt32()); //read additional data
            }
            //Refresh(); //control feature
        }

        public Node DeserializeNode(BinaryReader br)
        {
            var nv = new Node();
            nv.GUID = br.ReadString();
            nv.visual.X = br.ReadSingle();
            nv.visual.Y = br.ReadSingle();
            nv.Callable = br.ReadBoolean();
            nv.ExecInit = br.ReadBoolean();
            nv.Name = br.ReadString();
            nv.Order = br.ReadInt32();
            var customEditorAssembly = br.ReadString();
            var customEditor = br.ReadString();
            nv.Type = Context.GetType().GetMethod(br.ReadString());
            var attribute = nv.Type.GetCustomAttributes(typeof(NodeAttribute), false)
                                        .Cast<NodeAttribute>()
                                        .FirstOrDefault();
            if (attribute != null)
            {
                nv.visual.CustomWidth = attribute.Width;
                nv.visual.CustomHeight = attribute.Height;
            }
            (nv.GetNodeContext() as DynamicNodeContext).Deserialize(br.ReadBytes(br.ReadInt32()));
            var additional = br.ReadInt32(); //read additional data
            if (additional >= 4)
            {
                nv.Int32Tag = br.ReadInt32();
                if (additional >= 8)
                {
                    nv.visual.NodeColor = Color.FromArgb(br.ReadInt32());
                }
            }
            if (additional > 8)
            {
                br.ReadBytes(additional - 8);
            }

            if (customEditor != "")
            {
                nv.visual.CustomEditor =
                    Activator.CreateInstance(AppDomain.CurrentDomain, customEditorAssembly, customEditor).Unwrap() as Control;

                Control ctrl = nv.visual.CustomEditor;
                if (ctrl != null)
                {
                    ctrl.Tag = nv;
                    if(control != null)
                    {
                        control.Controls.Add(ctrl);
                    }                    
                }
            }
            return nv;
        }

    }
}
