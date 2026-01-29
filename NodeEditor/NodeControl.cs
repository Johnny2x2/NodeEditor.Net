using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace NodeEditor
{
    [ToolboxBitmap(typeof(NodeControl), "nodeed")]
    public partial class NodeControl : UserControl, IZoomable
    {
        public NodeManager nodeManager = new NodeManager();

        public event EventHandler<PaintEventArgs> OnPaintNodesBackground = delegate { };

        internal class NodeToken
        {
            public MethodInfo Method;
            public NodeAttribute Attribute;
        }

        private NodeGraph graph = new NodeGraph();

        private bool needRepaint = true;
        private Timer timer = new Timer();
        private bool mdown;
        private Point lastmpos;
        private nSocket dragSocket;
        private Node dragSocketNode;
        private PointF dragConnectionBegin;
        private PointF dragConnectionEnd;

        //private Stack<NodeVisual> executionStack = new Stack<NodeVisual>();
        //private bool rebuildConnectionDictionary = true;
        //private Dictionary<string, NodeConnection> connectionDictionary = new Dictionary<string, NodeConnection>();

        /// <summary>
        /// Occurs when user selects a node. In the object will be passed node settings for unplugged inputs/outputs.
        /// </summary>
        public event Action<object> OnNodeContextSelected = delegate { };

        /// <summary>
        /// Occurs when node would to share its description.
        /// </summary>
        public event Action<string> OnNodeHint = delegate { };

        /// <summary>
        /// Indicates which part of control should be actually visible. It is useful when dragging nodes out of autoscroll parent control,
        /// to guarantee that moving node/connection is visible to user.
        /// </summary>
        public event Action<RectangleF> OnShowLocation = delegate { };

        private readonly Dictionary<ToolStripMenuItem, int> allContextItems = new Dictionary<ToolStripMenuItem, int>();

        private Point lastMouseLocation;

#pragma warning disable CS0169 // The field 'NodeControl.autoScroll' is never used
        private Point autoScroll;
#pragma warning restore CS0169 // The field 'NodeControl.autoScroll' is never used

        private PointF selectionStart;

        private PointF selectionEnd;

        private DrawInfo customDrawInfo = new DrawInfo();

        public DrawInfo CustomDrawInfo
        {
            get { return customDrawInfo; }
            set { customDrawInfo = value; }
        }
        /// <summary>
        /// If true, drawing events will use fast painting modes instead of high quality ones
        /// </summary>
        public bool PreferFastRendering { get; set; }

        private float zoom = 1f;

        public float Zoom
        {
            get { return zoom; }
            set
            {
                zoom = value;
                PassZoomToNodes();
                Invalidate();
            }
        }


        /// <summary>
        /// Default constructor
        /// </summary>
        public NodeControl()
        {
            graph.nodeManager = nodeManager;
            nodeManager.control = this;
            InitializeComponent();
            timer.Interval = 30;
            timer.Tick += TimerOnTick;
            timer.Start();
            KeyDown += OnKeyDown;
            SetStyle(ControlStyles.Selectable, true);
        }


        private void PassZoomToNodeCustomEditor(Control control)
        {
            var zoomable = control as IZoomable;
            if (zoomable != null)
            {
                zoomable.Zoom = zoom * zoom;
            }
        }

        public void Execute()
        {
            //nodeManager.Execute();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 7)
            {
                return;
            }
            base.WndProc(ref m);
        }

        private void OnKeyDown(object sender, KeyEventArgs keyEventArgs)
        {
            if (keyEventArgs.KeyCode == Keys.Delete)
            {
                DeleteSelectedNodes();
            }
        }

        private void TimerOnTick(object sender, EventArgs eventArgs)
        {
            if (DesignMode) return;
            if (needRepaint)
            {
                Invalidate();
            }
        }

        private void NodesControl_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = PreferFastRendering ? SmoothingMode.HighSpeed : SmoothingMode.HighQuality;
            e.Graphics.InterpolationMode = PreferFastRendering ? InterpolationMode.Low : InterpolationMode.HighQualityBilinear;
            e.Graphics.ScaleTransform(zoom, zoom);

            OnPaintNodesBackground(sender, e);


            //graph.Draw(e.Graphics, PointToClient(MousePosition), MouseButtons);

            graph.Draw(e.Graphics, GetLocationWithZoom(PointToClient(MousePosition)), MouseButtons, PreferFastRendering, customDrawInfo);

            if (dragSocket != null)
            {
                var pen = customDrawInfo.GetConnectionStyle(dragSocket.Type, true);
                NodeGraph.DrawConnection(e.Graphics, pen, dragConnectionBegin, dragConnectionEnd);
            }

            if (selectionStart != PointF.Empty)
            {
                var rect = Rectangle.Round(MakeRect(selectionStart, selectionEnd));
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(50, Color.CornflowerBlue)), rect);
                e.Graphics.DrawRectangle(new Pen(Color.DodgerBlue), rect);
            }

            needRepaint = false;
        }

        private static RectangleF MakeRect(PointF a, PointF b)
        {
            var x1 = a.X;
            var x2 = b.X;
            var y1 = a.Y;
            var y2 = b.Y;
            return new RectangleF(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        }

        private void NodesControl_MouseMove(object sender, MouseEventArgs e)
        {
            var loc = GetLocationWithZoom(e.Location);

            //var em = PointToScreen(e.Location);

            var em = PointToScreen(loc);


            if (selectionStart != PointF.Empty)
            {
                //selectionEnd = e.Location;
                selectionEnd = loc;
            }
            if (mdown)
            {
                foreach (var node in nodeManager.Nodes.Where(x => x.visual.IsSelected))
                {
                    node.visual.X += em.X - lastmpos.X;
                    node.visual.Y += em.Y - lastmpos.Y;
                    node.DiscardCache();
                    node.visual.LayoutEditor(zoom);
                }
                if (nodeManager.Nodes.Exists(x => x.visual.IsSelected))
                {
                    var n = nodeManager.Nodes.FirstOrDefault(x => x.visual.IsSelected);
                    var bound = new RectangleF(new PointF(n.visual.X, n.visual.Y), n.visual.GetNodeBounds());
                    foreach (var node in nodeManager.Nodes.Where(x => x.visual.IsSelected))
                    {
                        bound = RectangleF.Union(bound, new RectangleF(new PointF(node.visual.X, node.visual.Y), node.visual.GetNodeBounds()));
                    }
                    OnShowLocation(bound);
                }
                Invalidate();

                if (dragSocket != null)
                {
                    var center = new PointF(dragSocket.visual.X + dragSocket.visual.Width / 2f, dragSocket.visual.Y + dragSocket.visual.Height / 2f);
                    if (dragSocket.Input)
                    {
                        dragConnectionBegin.X += em.X - lastmpos.X;
                        dragConnectionBegin.Y += em.Y - lastmpos.Y;
                        dragConnectionEnd = center;
                        OnShowLocation(new RectangleF(dragConnectionBegin, new SizeF(10, 10)));
                    }
                    else
                    {
                        dragConnectionBegin = center;
                        dragConnectionEnd.X += em.X - lastmpos.X;
                        dragConnectionEnd.Y += em.Y - lastmpos.Y;
                        OnShowLocation(new RectangleF(dragConnectionEnd, new SizeF(10, 10)));
                    }

                }
                lastmpos = em;
            }

            needRepaint = true;
        }

        private void NodesControl_MouseDown(object sender, MouseEventArgs e)
        {
            
            var loc = GetLocationWithZoom(e.Location); //New zoom feature

            if (e.Button == MouseButtons.Left)
            {
                selectionStart = PointF.Empty;

                Focus();

                if ((ModifierKeys & Keys.Shift) != Keys.Shift)
                {
                    nodeManager.Nodes.ForEach(x => x.visual.IsSelected = false);
                }

                //var node =
                //    nodeManager.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                //        x => new RectangleF(new PointF(x.visual.X, x.visual.Y), x.visual.GetHeaderSize()).Contains(e.Location));

                var node =
                    nodeManager.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                        x => new RectangleF(new PointF(x.visual.X, x.visual.Y), x.visual.GetHeaderSize()).Contains(loc)); //New zoom feature

                if (node != null && !mdown)
                {

                    node.visual.IsSelected = true;

                    node.Order = nodeManager.Nodes.Min(x => x.Order) - 1;
                    if (node.visual.CustomEditor != null)
                    {
                        node.visual.CustomEditor.BringToFront();

                        PassZoomToNodeCustomEditor(node.visual.CustomEditor); //New zoom feature
                    }
                    mdown = true;
                    //lastmpos = PointToScreen(e.Location);
                    lastmpos = PointToScreen(loc); //New zoom feature
                    Refresh();
                }
                if (node == null && !mdown)
                {
                    var nodeWhole =
                    nodeManager.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                       // x => new RectangleF(new PointF(x.visual.X, x.visual.Y), x.visual.GetNodeBounds()).Contains(e.Location));
                       x => new RectangleF(new PointF(x.visual.X, x.visual.Y), x.visual.GetNodeBounds()).Contains(loc));//New zoom feature
                    if (nodeWhole != null)
                    {
                        node = nodeWhole;
                        //var socket = nodeWhole.GetSockets().FirstOrDefault(x => x.visual.GetBounds().Contains(e.Location));
                        var socket = nodeWhole.GetSockets().FirstOrDefault(x => x.visual.GetBounds().Contains(loc)); //New zoom feature
                        if (socket != null)
                        {
                            if ((ModifierKeys & Keys.Control) == Keys.Control)
                            {
                                var connection =
                                    nodeManager.Connections.FirstOrDefault(
                                        x => x.InputNode == nodeWhole && x.InputSocketName == socket.Name);

                                if (connection != null)
                                {
                                    dragSocket =
                                        connection.OutputNode.GetSockets()
                                            .FirstOrDefault(x => x.Name == connection.OutputSocketName);
                                    dragSocketNode = connection.OutputNode;
                                }
                                else
                                {
                                    connection =
                                        nodeManager.Connections.FirstOrDefault(
                                            x => x.OutputNode == nodeWhole && x.OutputSocketName == socket.Name);

                                    if (connection != null)
                                    {
                                        dragSocket =
                                            connection.InputNode.GetSockets()
                                                .FirstOrDefault(x => x.Name == connection.InputSocketName);
                                        dragSocketNode = connection.InputNode;
                                    }
                                }

                                nodeManager.Connections.Remove(connection);
                                nodeManager.rebuildConnectionDictionary = true;
                            }
                            else
                            {
                                dragSocket = socket;
                                dragSocketNode = nodeWhole;
                            }
                            dragConnectionBegin = loc; // e.Location; //New zoom feature
                            dragConnectionEnd = loc;//e.Location; //New zoom feature
                            mdown = true;
                            //lastmpos = PointToScreen(e.Location); 
                            lastmpos = PointToScreen(loc); //New zoom feature
                        }
                    }
                    else
                    {
                        selectionStart = selectionEnd = loc; // e.Location; //New zoom feature
                    }
                }
                if (node != null)
                {
                    OnNodeContextSelected(node.GetNodeContext());
                }
            }

            needRepaint = true;
        }

        private Point GetLocationWithZoom(Point location)
        {
            var zx = location.X / zoom;
            var zy = location.Y / zoom;
            var zl = new Point((int)zx, (int)zy);
            return zl;
        }

        private void PassZoomToNodes()
        {
            foreach (var node in nodeManager.Nodes)
            {
                if (node.visual.CustomEditor != null)
                {
                    PassZoomToNodeCustomEditor(node.visual.CustomEditor);
                    node.DiscardCache();
                    node.visual.LayoutEditor(zoom);
                }
            }
        }

        private bool IsConnectable(nSocket a, nSocket b)
        {
            var input = a.Input ? a : b;
            var output = a.Input ? b : a;
            var otype = Type.GetType(output.Type.FullName.Replace("&", ""), AssemblyResolver, TypeResolver);
            var itype = Type.GetType(input.Type.FullName.Replace("&", ""), AssemblyResolver, TypeResolver);
            if (otype == null || itype == null) return false;
            var allow = otype == itype || otype.IsSubclassOf(itype);
            return allow;
        }

        private Type TypeResolver(Assembly assembly, string name, bool inh)
        {
            if (assembly == null) assembly = ResolveAssembly(name);
            if (assembly == null) return null;
            return assembly.GetType(name);
        }

        private Assembly ResolveAssembly(string fullTypeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(x => x.GetTypes().Any(o => o.FullName == fullTypeName));
        }

        private Assembly AssemblyResolver(AssemblyName assemblyName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.FullName == assemblyName.FullName);
        }

        private void NodesControl_MouseUp(object sender, MouseEventArgs e)
        {
            var loc = GetLocationWithZoom(e.Location);

            if (selectionStart != PointF.Empty)
            {
                var rect = MakeRect(selectionStart, selectionEnd);
                nodeManager.Nodes.ForEach(
                    x => x.visual.IsSelected = rect.Contains(new RectangleF(new PointF(x.visual.X, x.visual.Y), x.visual.GetNodeBounds())));
                selectionStart = PointF.Empty;
            }

            if (dragSocket != null)
            {
                var nodeWhole =
                    nodeManager.Nodes.OrderBy(x => x.Order).FirstOrDefault(
                        x => new RectangleF(new PointF(x.visual.X, x.visual.Y), x.visual.GetNodeBounds()).Contains(loc)); //.Contains(e.Location));
                if (nodeWhole != null)
                {
                    var socket = nodeWhole.GetSockets().FirstOrDefault(x => x.visual.GetBounds().Contains(loc)); //.Contains(e.Location));
                    if (socket != null)
                    {
                        if (IsConnectable(dragSocket, socket) && dragSocket.Input != socket.Input)
                        {
                            var nc = new NodeConnection();
                            if (!dragSocket.Input)
                            {
                                nc.OutputNode = dragSocketNode;
                                nc.OutputSocketName = dragSocket.Name;
                                nc.InputNode = nodeWhole;
                                nc.InputSocketName = socket.Name;
                            }
                            else
                            {
                                nc.InputNode = dragSocketNode;
                                nc.InputSocketName = dragSocket.Name;
                                nc.OutputNode = nodeWhole;
                                nc.OutputSocketName = socket.Name;
                            }

                            nodeManager.Connections.RemoveAll(
                                x => x.InputNode == nc.InputNode && x.InputSocketName == nc.InputSocketName);

                            nodeManager.Connections.Add(nc);
                            nodeManager.rebuildConnectionDictionary = true;
                        }
                    }
                }
            }

            dragSocket = null;
            mdown = false;
            needRepaint = true;
        }

        private void AddToMenu(ToolStripItemCollection items, NodeToken token, string path, EventHandler click)
        {
            var pathParts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var first = pathParts.FirstOrDefault();
            ToolStripMenuItem item = null;
            if (!items.ContainsKey(first))
            {
                item = new ToolStripMenuItem(first);
                item.Name = first;
                item.Tag = token;
                items.Add(item);
            }
            else
            {
                item = items[first] as ToolStripMenuItem;
            }
            var next = string.Join("/", pathParts.Skip(1));
            if (!string.IsNullOrEmpty(next))
            {
                item.MouseEnter += (sender, args) => OnNodeHint("");
                AddToMenu(item.DropDownItems, token, next, click);
            }
            else
            {
                item.Click += click;
                item.Click += (sender, args) =>
                {
                    var i = allContextItems.Keys.FirstOrDefault(x => x.Name == item.Name);
                    allContextItems[i]++;
                };
                item.MouseEnter += (sender, args) => OnNodeHint(token.Attribute.Description ?? "");
                if (!allContextItems.Keys.Any(x => x.Name == item.Name))
                {
                    allContextItems.Add(item, 0);
                }
            }
        }

        private void NodesControl_MouseClick(object sender, MouseEventArgs e)
        {
            //lastMouseLocation = e.Location;

            var loc = GetLocationWithZoom(e.Location);
            lastMouseLocation = loc;

            if (nodeManager.Context == null) return;

            if (e.Button == MouseButtons.Right)
            {
                var methods = nodeManager.Context.GetType().GetMethods();
                var nodes =
                    methods.Select(
                        x =>
                            new
                                NodeToken()
                            {
                                Method = x,
                                Attribute =
                                    x.GetCustomAttributes(typeof(NodeAttribute), false)
                                        .Cast<NodeAttribute>()
                                        .FirstOrDefault()
                            }).Where(x => x.Attribute != null);

                var context = new ContextMenuStrip();
                if (nodeManager.Nodes.Exists(x => x.visual.IsSelected))
                {
                    context.Items.Add("Delete Node(s)", null, ((o, args) =>
                    {
                        DeleteSelectedNodes();
                    }));
                    context.Items.Add("Duplicate Node(s)", null, ((o, args) =>
                    {
                        DuplicateSelectedNodes();
                    }));
                    context.Items.Add("Change Color ...", null, ((o, args) =>
                    {
                        ChangeSelectedNodesColor();
                    }));
                    if (nodeManager.Nodes.Count(x => x.visual.IsSelected) == 2)
                    {
                        var sel = nodeManager.Nodes.Where(x => x.visual.IsSelected).ToArray();
                        context.Items.Add("Check Impact", null, ((o, args) =>
                        {
                            if (HasImpact(sel[0], sel[1]) || HasImpact(sel[1], sel[0]))
                            {
                                MessageBox.Show("One node has impact on other.", "Impact detected.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                MessageBox.Show("These nodes not impacts themselves.", "No impact.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }));
                    }
                    context.Items.Add(new ToolStripSeparator());
                }
                if (allContextItems.Values.Any(x => x > 0))
                {
                    var handy = allContextItems.Where(x => x.Value > 0 && !string.IsNullOrEmpty(((x.Key.Tag) as NodeToken).Attribute.Menu)).OrderByDescending(x => x.Value).Take(8);
                    foreach (var kv in handy)
                    {
                        context.Items.Add(kv.Key);
                    }
                    context.Items.Add(new ToolStripSeparator());
                }
                foreach (var node in nodes.OrderBy(x => x.Attribute.Path))
                {
                    AddToMenu(context.Items, node, node.Attribute.Path, (s, ev) =>
                    {
                        var tag = (s as ToolStripMenuItem).Tag as NodeToken;

                        var nv = new Node();
                        nv.visual.X = lastMouseLocation.X;
                        nv.visual.Y = lastMouseLocation.Y;
                        nv.Type = node.Method;
                        nv.Callable = node.Attribute.IsCallable;
                        nv.Name = node.Attribute.Name;
                        nv.Order = nodeManager.Nodes.Count;
                        nv.ExecInit = node.Attribute.IsExecutionInitiator;
                        nv.XmlExportName = node.Attribute.XmlExportName;
                        nv.visual.CustomWidth = node.Attribute.Width;
                        nv.visual.CustomHeight = node.Attribute.Height;

                        if (node.Attribute.CustomEditor != null)
                        {
                            Control ctrl = null;
                            nv.visual.CustomEditor = ctrl = Activator.CreateInstance(node.Attribute.CustomEditor) as Control;
                            if (ctrl != null)
                            {
                                ctrl.Tag = nv;
                                Controls.Add(ctrl);
                                PassZoomToNodeCustomEditor(ctrl);
                            }
                            nv.visual.LayoutEditor(zoom);
                        }

                        nodeManager.Nodes.Add(nv);
                        Refresh();
                        needRepaint = true;
                    });
                }
                context.Show(MousePosition);
            }
        }

        private void ChangeSelectedNodesColor()
        {
            ColorDialog cd = new ColorDialog();
            cd.FullOpen = true;
            if (cd.ShowDialog() == DialogResult.OK)
            {
                foreach (var n in nodeManager.Nodes.Where(x => x.visual.IsSelected))
                {
                    n.visual.NodeColor = cd.Color;
                }
            }
            Refresh();
            needRepaint = true;
        }

        private void DuplicateSelectedNodes()
        {
            var cloned = new List<Node>();
            foreach (var n in nodeManager.Nodes.Where(x => x.visual.IsSelected))
            {
                int count = nodeManager.Nodes.Count(x => x.visual.IsSelected);
                var ms = new MemoryStream();
                var bw = new BinaryWriter(ms);
                NodeManager.SerializeNode(bw, n);
                ms.Seek(0, SeekOrigin.Begin);
                var br = new BinaryReader(ms);
                var clone = nodeManager.DeserializeNode(br);
                clone.visual.X += 40;
                clone.visual.Y += 40;
                clone.GUID = Guid.NewGuid().ToString();
                cloned.Add(clone);
                br.Dispose();
                bw.Dispose();
                ms.Dispose();
            }
            nodeManager.Nodes.ForEach(x => x.visual.IsSelected = false);
            cloned.ForEach(x => x.visual.IsSelected = true);
            cloned.Where(x => x.visual.CustomEditor != null).ToList().ForEach(x => 
            {
                x.visual.CustomEditor.BringToFront();
                PassZoomToNodeCustomEditor(x.visual.CustomEditor);
            });
            nodeManager.Nodes.AddRange(cloned);
            Invalidate();
        }

        private void DeleteSelectedNodes()
        {
            if (nodeManager.Nodes.Exists(x => x.visual.IsSelected))
            {
                foreach (var n in nodeManager.Nodes.Where(x => x.visual.IsSelected))
                {
                    Controls.Remove(n.visual.CustomEditor);
                    nodeManager.Connections.RemoveAll(
                        x => x.OutputNode == n || x.InputNode == n);
                }
                nodeManager.Nodes.RemoveAll(x => nodeManager.Nodes.Where(n => n.visual.IsSelected).Contains(x));
            }
            Invalidate();
        }

        public List<Node> GetNodes(params string[] nodeNames)
        {
            var nodes = nodeManager.Nodes.Where(x => nodeNames.Contains(x.Name));
            return nodes.ToList();
        }

        public bool HasImpact(Node startNode, Node endNode)
        {
            var connections = nodeManager.Connections.Where(x => x.OutputNode == startNode && !x.IsExecution);
            foreach (var connection in connections)
            {
                if (connection.InputNode == endNode)
                {
                    return true;
                }
                bool nextImpact = HasImpact(connection.InputNode, endNode);
                if (nextImpact)
                {
                    return true;
                }
            }

            return false;
        }    

        public string ExportToXml()
        {
            var xml = new XmlDocument();

            XmlElement el = (XmlElement)xml.AppendChild(xml.CreateElement("NodeGrap"));
            el.SetAttribute("Created", DateTime.Now.ToString());
            var nodes = el.AppendChild(xml.CreateElement("Nodes"));
            foreach (var node in nodeManager.Nodes)
            {
                var xmlNode = (XmlElement)nodes.AppendChild(xml.CreateElement("Node"));
                xmlNode.SetAttribute("Name", node.XmlExportName);
                xmlNode.SetAttribute("Id", node.GetGuid());
                var xmlContext = (XmlElement)xmlNode.AppendChild(xml.CreateElement("Context"));
                var context = node.GetNodeContext() as DynamicNodeContext;
                foreach (var kv in context)
                {
                    var ce = (XmlElement)xmlContext.AppendChild(xml.CreateElement("ContextMember"));
                    ce.SetAttribute("Name", kv);
                    ce.SetAttribute("Value", Convert.ToString(context[kv] ?? ""));
                    ce.SetAttribute("Type", context[kv] == null ? "" : context[kv].GetType().FullName);
                }
            }

            var connections = el.AppendChild(xml.CreateElement("Connections"));

            foreach (var conn in nodeManager.Connections)
            {
                var xmlConn = (XmlElement)nodes.AppendChild(xml.CreateElement("Connection"));
                xmlConn.SetAttribute("OutputNodeId", conn.OutputNode.GetGuid());
                xmlConn.SetAttribute("OutputNodeSocket", conn.OutputSocketName);
                xmlConn.SetAttribute("InputNodeId", conn.InputNode.GetGuid());
                xmlConn.SetAttribute("InputNodeSocket", conn.InputSocketName);
            }
            StringBuilder sb = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace
            };
            using (XmlWriter writer = XmlWriter.Create(sb, settings))
            {
                xml.Save(writer);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Clears node graph state.
        /// </summary>
        public void Clear()
        {
            nodeManager.Nodes.Clear();
            nodeManager.Connections.Clear();
            Controls.Clear();
            Refresh();
            nodeManager.rebuildConnectionDictionary = true;
        }
    }
}
