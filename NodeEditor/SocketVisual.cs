using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;
namespace NodeEditor
{
    public class SocketVisual
    {
        public nSocket socket;

        public const float SocketHeight = 16;

        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        public SocketVisual(nSocket _socket)
        {
            socket = _socket;
        }

        public void Draw(Graphics g, Point mouseLocation, MouseButtons mouseButtons)
        {
            var socketRect = new RectangleF(X, Y, Width, Height);
            var hover = socketRect.Contains(mouseLocation);
            var fontBrush = Brushes.Black;

            if (hover)
            {
                socketRect.Inflate(4, 4);
                fontBrush = Brushes.Blue;
            }

            g.SmoothingMode = SmoothingMode.HighSpeed;
            g.InterpolationMode = InterpolationMode.Low;

            if (socket.Input)
            {
                var sf = new StringFormat();
                sf.Alignment = StringAlignment.Near;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(socket.Name, SystemFonts.SmallCaptionFont, fontBrush, new RectangleF(X + Width + 2, Y, 1000, Height), sf);
            }
            else
            {
                var sf = new StringFormat();
                sf.Alignment = StringAlignment.Far;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(socket.Name, SystemFonts.SmallCaptionFont, fontBrush, new RectangleF(X - 1000, Y, 1000, Height), sf);
            }

            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.SmoothingMode = SmoothingMode.HighQuality;

            if (socket.IsExecution)
            {
                g.DrawImage(Resources.exec, socketRect);
            }
            else
            {
                g.DrawImage(Resources.socket, socketRect);
            }
        }

        public RectangleF GetBounds()
        {
            return new RectangleF(X, Y, Width, Height);
        }
    }
}
