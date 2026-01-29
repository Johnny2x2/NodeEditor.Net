using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace NodeEditor
{
    internal class NodeGraph
    {
        internal NodeManager nodeManager;

        static Pen executionPen;
        static Pen executionPen2;


        public void CheckAndUpdateDrawSize()
        {
            foreach (var node in nodeManager.Nodes)
            {
                if(!nodeManager.control.Bounds.Contains(new Rectangle((int)node.visual.X, (int)node.visual.Y, (int)node.visual.GetNodeBounds().Width, (int)node.visual.GetNodeBounds().Height)))
                {
                    //Check to see if value is negative (I Don't want to shift all node values yets.. so only allow width and height of control to grow)
                    if(node.visual.X < 0 || node.visual.Y < 0)
                    {
                        continue;
                    }

                    //Check Width adjustment
                    int wPoint = (int)node.visual.X + (int)node.visual.GetNodeBounds().Width;
                    if(nodeManager.control.Width <= wPoint)
                    {
                        nodeManager.control.Width = wPoint + 20; //Update width with a 20px buffer
                    }

                    //Check Height adjustment
                    int hPoint = (int)node.visual.Y + (int)node.visual.GetNodeBounds().Height;

                    if (nodeManager.control.Height <= hPoint)
                    {
                        nodeManager.control.Height = hPoint + 20; //Update height with a 20px buffer
                    }
                }
            }
        }

        public void Draw(Graphics g, Point mouseLocation, MouseButtons mouseButtons, bool preferFastRendering, DrawInfo info)
        {
            g.InterpolationMode = InterpolationMode.Low;
            g.SmoothingMode = SmoothingMode.HighSpeed;

            CheckAndUpdateDrawSize();

            foreach (var node in nodeManager.Nodes)
            {
                g.FillRectangle(Brushes.Black, new RectangleF(new PointF(node.visual.X + 6, node.visual.Y + 6), node.visual.GetNodeBounds()));
            }

            g.FillRectangle(new SolidBrush(Color.FromArgb(200, Color.White)), g.ClipBounds);

            executionPen = (executionPen ?? new Pen(Color.Gold, 3));
            executionPen2 = (executionPen2 ?? new Pen(Color.Black, 5));

            //var cpen = Pens.Black;
            //var epen = new Pen(Color.Gold, 3);
            //var epen2 = new Pen(Color.Black, 5);
            foreach (var connection in nodeManager.Connections.Where(x => x.IsExecution))
            {
                var osoc = connection.OutputNode.GetSockets().FirstOrDefault(x => x.Name == connection.OutputSocketName);
                var beginSocket = osoc.visual.GetBounds();
                var isoc = connection.InputNode.GetSockets().FirstOrDefault(x => x.Name == connection.InputSocketName);
                var endSocket = isoc.visual.GetBounds();
                var begin = beginSocket.Location + new SizeF(beginSocket.Width / 2f, beginSocket.Height / 2f);
                var end = endSocket.Location += new SizeF(endSocket.Width / 2f, endSocket.Height / 2f);

                //DrawConnection(g, epen2, begin, end);
                //DrawConnection(g, epen, begin, end);

                DrawConnection(g, executionPen2, begin, end, preferFastRendering);
                DrawConnection(g, executionPen, begin, end, preferFastRendering);
            }
            foreach (var connection in nodeManager.Connections.Where(x => !x.IsExecution))
            {
                var osoc = connection.OutputNode.GetSockets().FirstOrDefault(x => x.Name == connection.OutputSocketName);
                var beginSocket = osoc.visual.GetBounds();
                var isoc = connection.InputNode.GetSockets().FirstOrDefault(x => x.Name == connection.InputSocketName);
                var endSocket = isoc.visual.GetBounds();
                var begin = beginSocket.Location + new SizeF(beginSocket.Width / 2f, beginSocket.Height / 2f);
                var end = endSocket.Location += new SizeF(endSocket.Width / 2f, endSocket.Height / 2f);

                var cpen = info.GetConnectionStyle(connection.InputSocket.Type, false);
                DrawConnection(g, cpen, begin, end, preferFastRendering);

            }

            var orderedNodes = nodeManager.Nodes.OrderByDescending(x => x.Order);
            foreach (var node in orderedNodes)
            {
                node.visual.Draw(g, mouseLocation, mouseButtons);
            }
        }

        public static void DrawConnection(Graphics g, Pen pen, PointF output, PointF input, bool preferFastRendering = false)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.SmoothingMode = SmoothingMode.HighQuality;

            if (input == output) return;
            int interpolation = preferFastRendering ? 16 : 48;

            PointF[] points = new PointF[interpolation];
            for (int i = 0; i < interpolation; i++)
            {
                float amount = i / (float)(interpolation - 1);

                var lx = Lerp(output.X, input.X, amount);
                var d = Math.Min(Math.Abs(input.X - output.X), 100);
                var a = new PointF((float)Scale(amount, 0, 1, output.X, output.X + d),
                    output.Y);
                var b = new PointF((float)Scale(amount, 0, 1, input.X - d, input.X), input.Y);

                var bas = Sat(Scale(amount, 0.1, 0.9, 0, 1));
                var cos = Math.Cos(bas * Math.PI);
                if (cos < 0)
                {
                    cos = -Math.Pow(-cos, 0.2);
                }
                else
                {
                    cos = Math.Pow(cos, 0.2);
                }
                amount = (float)cos * -0.5f + 0.5f;

                var f = Lerp(a, b, amount);
                points[i] = f;
            }

            g.DrawLines(pen, points);
        }

        public static double Sat(double x)
        {
            if (x < 0) return 0;
            if (x > 1) return 1;
            return x;
        }


        public static double Scale(double x, double a, double b, double c, double d)
        {
            double s = (x - a) / (b - a);
            return s * (d - c) + c;
        }

        public static float Lerp(float a, float b, float amount)
        {
            return a * (1f - amount) + b * amount;
        }

        public static PointF Lerp(PointF a, PointF b, float amount)
        {
            PointF result = new PointF();

            result.X = a.X * (1f - amount) + b.X * amount;
            result.Y = a.Y * (1f - amount) + b.Y * amount;

            return result;
        }
    }
}
