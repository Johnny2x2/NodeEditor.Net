using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace NodeEditor
{
    public class Node
    {
        /// <summary>
        /// Current node name.
        /// </summary>
        public string Name { get; set; }

        internal MethodInfo Type { get; set; }
        internal int Order { get; set; }
        internal bool Callable { get; set; }
        internal bool ExecInit { get; set; }

        internal FeedbackType Feedback { get; set; }
        private object nodeContext { get; set; }

        internal string GUID = Guid.NewGuid().ToString();
        public bool IsBackExecuted { get; internal set; }

        private nSocket[] socketCache;

        public NodeVisual visual = new NodeVisual();

        /// <summary>
        /// Tag for various puposes - may be used freely.
        /// </summary>
        public int Int32Tag = 0;
        public string XmlExportName { get; internal set; }


        internal Node()
        {
            Feedback = FeedbackType.Debug;

            visual.node = this;
        }

        public string GetGuid()
        {
            return GUID;
        }

        internal nSocket[] GetSockets()
        {
            if (socketCache != null)
            {
                return socketCache;
            }

            visual.inputs = 0;
            visual.outputs = 0;

            var socketList = new List<nSocket>();

            float curInputH = visual.HeaderHeight + visual.ComponentPadding;
            float curOutputH = visual.HeaderHeight + visual.ComponentPadding;

            if (Callable)
            {
                if (!ExecInit)
                {
                    nSocket socket = new nSocket();
                    socket.visual.Height = SocketVisual.SocketHeight;
                    socket.Name = "Enter";
                    socket.Type = typeof(ExecutionPath);
                    socket.IsMainExecution = true;
                    socket.visual.Width = SocketVisual.SocketHeight;
                    socket.visual.X = visual.X;
                    socket.visual.Y = visual.Y + curInputH;
                    socket.Input = true;

                    socketList.Add(socket);

                    visual.inputs++;
                }
                nSocket socketExit = new nSocket();

                socketExit.visual.Height = SocketVisual.SocketHeight;
                socketExit.Name = "Exit";
                socketExit.Type = typeof(ExecutionPath);
                socketExit.IsMainExecution = true;
                socketExit.visual.Width = SocketVisual.SocketHeight;
                socketExit.visual.X = visual.X + visual.NodeWidth - SocketVisual.SocketHeight;
                socketExit.visual.Y = visual.Y + curOutputH;

                socketList.Add(socketExit);

                curOutputH += SocketVisual.SocketHeight + visual.ComponentPadding;
                curInputH += SocketVisual.SocketHeight + visual.ComponentPadding;

                visual.outputs++;
            }

            foreach (var input in GetInputs())
            {
                nSocket socketExit = new nSocket();

                socketExit.visual.Height = SocketVisual.SocketHeight;
                socketExit.Name = input.Name;
                socketExit.Type = input.ParameterType;
                socketExit.visual.Width = SocketVisual.SocketHeight;
                socketExit.visual.X = visual.X;
                socketExit.visual.Y = visual.Y + curInputH;
                socketExit.Input = true;

                curInputH += SocketVisual.SocketHeight + visual.ComponentPadding;

                socketList.Add(socketExit);

                visual.inputs++;
            }

            var ctx = GetNodeContext() as DynamicNodeContext;
            foreach (var output in GetOutputs())
            {
                nSocket socketExit = new nSocket();

                socketExit.visual.Height = SocketVisual.SocketHeight;
                socketExit.Name = output.Name;
                socketExit.Type = output.ParameterType;
                socketExit.visual.Width = SocketVisual.SocketHeight;
                socketExit.visual.X = visual.X + visual.NodeWidth - SocketVisual.SocketHeight;
                socketExit.visual.Y = visual.Y + curOutputH;
                socketExit.Value = ctx[socketExit.Name];

                curOutputH += SocketVisual.SocketHeight + visual.ComponentPadding;

                socketList.Add(socketExit);

                visual.outputs++;
            }

            socketCache = socketList.ToArray();
            return socketCache;
        }

        internal void DiscardCache()
        {
            socketCache = null;
        }

        /// <summary>
        /// Returns node context which is dynamic type. It will contain all node default input/output properties.
        /// </summary>
        public object GetNodeContext()
        {
            const string stringTypeName = "System.String";

            if (nodeContext == null)
            {
                dynamic context = new DynamicNodeContext();

                foreach (var input in GetInputs())
                {
                    var contextName = input.Name.Replace(" ", "");
                    if (input.ParameterType.FullName.Replace("&", "") == stringTypeName)
                    {
                        context[contextName] = string.Empty;
                    }
                    else
                    {
                        try
                        {
                            context[contextName] = Activator.CreateInstance(AppDomain.CurrentDomain, input.ParameterType.Assembly.GetName().Name,
                            input.ParameterType.FullName.Replace("&", "").Replace(" ", "")).Unwrap();
                        }
                        catch (MissingMethodException ex) //For case when type does not have default constructor
                        {
                            context[contextName] = null;
                        }
                    }
                }
                foreach (var output in GetOutputs())
                {
                    var contextName = output.Name.Replace(" ", "");
                    if (output.ParameterType.FullName.Replace("&", "") == stringTypeName)
                    {
                        context[contextName] = string.Empty;
                    }
                    else
                    {
                        try
                        {
                            context[contextName] = Activator.CreateInstance(AppDomain.CurrentDomain, output.ParameterType.Assembly.GetName().Name,
                            output.ParameterType.FullName.Replace("&", "").Replace(" ", "")).Unwrap();
                        }
#pragma warning disable CS0168 // The variable 'ex' is declared but never used
                        catch (MissingMethodException ex) //For case when type does not have default constructor
#pragma warning restore CS0168 // The variable 'ex' is declared but never used
                        {
                            context[contextName] = null;
                        }
                    }
                }

                nodeContext = context;
            }
            return nodeContext;
        }

        internal ParameterInfo[] GetInputs()
        {
            return Type.GetParameters().Where(x => !x.IsOut).ToArray();
        }

        internal ParameterInfo[] GetOutputs()
        {
            return Type.GetParameters().Where(x => x.IsOut).ToArray();
        }

        internal void Execute(INodesContext context)
        {
            context.CurrentProcessingNode = this; 

            var dc = (GetNodeContext() as DynamicNodeContext);
            var parametersDict = Type.GetParameters().OrderBy(x => x.Position).ToDictionary(x => x.Name, x => dc[x.Name]);
            var parameters = parametersDict.Values.ToArray();

            int ndx = 0;
            Type.Invoke(context, parameters);
            foreach (var kv in parametersDict.ToArray())
            {
                parametersDict[kv.Key] = parameters[ndx];
                ndx++;
            }

            var outs = GetSockets();


            foreach (var parameter in dc.ToArray())
            {
                dc[parameter] = parametersDict[parameter];
                var o = outs.FirstOrDefault(x => x.Name == parameter);
                //if (o != null)
                Debug.Assert(o != null, "Output not found");
                {
                    o.Value = dc[parameter];
                }
            }
        }
    }
}
