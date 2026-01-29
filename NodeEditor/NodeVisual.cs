using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace NodeEditor
{
    public class NodeVisual
    {
        public Node node;

        private const float nodeWidth = 140;
        private const float headerHeight = 20;
        private const float componentPadding = 2;

        /// <summary>
        /// Current node position X coordinate.
        /// </summary>
        public float X { get; set; }

        /// <summary>
        /// Current node position Y coordinate.
        /// </summary>
        public float Y { get; set; }

        internal bool IsSelected { get; set; }

        public Control CustomEditor { get; internal set; }

        public float HeaderHeight { get => headerHeight; } 

        public float ComponentPadding { get => componentPadding; }

        public float NodeWidth { get => nodeWidth; }

        internal Color NodeColor = Color.LightCyan;

        internal int CustomWidth = -1;
        internal int CustomHeight = -1;

        public int inputs = 0;
        public int outputs = 0;

        internal NodeVisual()
        {

        }

        /// <summary>
        /// Returns current size of the node.
        /// </summary>        
        public SizeF GetNodeBounds()
        {
            var csize = new SizeF();
            if (CustomEditor != null)
            {
                //csize = new SizeF(CustomEditor.ClientSize.Width + 2 + 80 + SocketVisual.SocketHeight * 2,
                //    CustomEditor.ClientSize.Height + HeaderHeight + 8);

                var zoomable = CustomEditor as IZoomable;
                float zoom = zoomable == null ? 1f : (float)Math.Sqrt(zoomable.Zoom);

                csize = new SizeF(CustomEditor.ClientSize.Width / zoom + 2 + 80 + SocketVisual.SocketHeight * 2,
                    CustomEditor.ClientSize.Height / zoom + HeaderHeight + 8);
            }

            
            var h = HeaderHeight + Math.Max(inputs * (SocketVisual.SocketHeight + ComponentPadding),
                outputs * (SocketVisual.SocketHeight + ComponentPadding)) + ComponentPadding * 2f;

            csize.Width = Math.Max(csize.Width, NodeWidth);
            csize.Height = Math.Max(csize.Height, h);
            if (CustomWidth >= 0)
            {
                csize.Width = CustomWidth;
            }
            if (CustomHeight >= 0)
            {
                csize.Height = CustomHeight;
            }

            return new SizeF(csize.Width, csize.Height);
        }

        /// <summary>
        /// Returns current size of node caption (header belt).
        /// </summary>
        /// <returns></returns>
        public SizeF GetHeaderSize()
        {
            return new SizeF(GetNodeBounds().Width, HeaderHeight);
        }

        /// <summary>
        /// Allows node to be drawn on given Graphics context.       
        /// </summary>
        /// <param name="g">Graphics context.</param>
        /// <param name="mouseLocation">Location of the mouse relative to NodesControl instance.</param>
        /// <param name="mouseButtons">Mouse buttons that are pressed while drawing node.</param>
        public void Draw(Graphics g, Point mouseLocation, MouseButtons mouseButtons)
        {
            var rect = new RectangleF(new PointF(X, Y), GetNodeBounds());

            var feedrect = rect;
            feedrect.Inflate(10, 10);

            if (node.Feedback == FeedbackType.Warning)
            {
                g.DrawRectangle(new Pen(Color.Yellow, 4), Rectangle.Round(feedrect));
            }
            else if (node.Feedback == FeedbackType.Error)
            {
                g.DrawRectangle(new Pen(Color.Red, 5), Rectangle.Round(feedrect));
            }

            var caption = new RectangleF(new PointF(X, Y), GetHeaderSize());
            bool mouseHoverCaption = caption.Contains(mouseLocation);

            g.FillRectangle(new SolidBrush(NodeColor), rect);

            if (IsSelected)
            {
                g.FillRectangle(new SolidBrush(Color.FromArgb(180, Color.WhiteSmoke)), rect);
                g.FillRectangle(mouseHoverCaption ? Brushes.Gold : Brushes.Goldenrod, caption);
            }
            else
            {
                g.FillRectangle(mouseHoverCaption ? Brushes.Cyan : Brushes.Aquamarine, caption);
            }

            g.DrawRectangle(Pens.Gray, Rectangle.Round(caption));
            g.DrawRectangle(Pens.Black, Rectangle.Round(rect));

            g.DrawString(node.Name, SystemFonts.DefaultFont, Brushes.Black, new PointF(X + 3, Y + 3));

            var sockets = node.GetSockets();
            foreach (var socet in sockets)
            {
                socet.visual.Draw(g, mouseLocation, mouseButtons);
            }
        }

     
        internal void LayoutEditor(float zoom)
        {
            if (CustomEditor != null)
            {
                //CustomEditor.Location = new Point((int)(X + 1 + 40 + SocketVisual.SocketHeight), (int)(Y + HeaderHeight + 4));

                CustomEditor.Location = new Point((int)(zoom * (X + 1 + 40 + SocketVisual.SocketHeight)), (int)(zoom * (Y + HeaderHeight + 4)));
            }
        }
    }
}
