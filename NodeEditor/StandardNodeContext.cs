using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Windows.Forms;

namespace NodeEditor
{
    public class NodeOutputEventArgs
    {
        public NodeOutputEventArgs(object obj)
        {
            Output = obj;
        }

        public object Output { get; }
    }

    public delegate void NodeOutputEventHandler(object sender, NodeOutputEventArgs e);

    public partial class StandardNodeContext : INodesContext
    {
        public Node CurrentProcessingNode { get; set; }

        public event Action<string, Node, FeedbackType, object, bool> FeedbackInfo;

        public Dictionary<string, object> dynamicDict = new Dictionary<string, object>();

        public event NodeOutputEventHandler NodeOutputEvent;

        [Node("Output Event", "Events", "Basic", "Output Objects Event", true)]
        public void OutputEventNode(object obj)
        {
            NodeOutputEvent?.Invoke(this, new NodeOutputEventArgs(obj));
        }

        [Node("Output MessageBox", "Events", "Basic", "Output Message box Event", true)]
        public void OutputMessageBoxNode(string msg)
        {
            MessageBox.Show(msg);
        }

        [Node("Starter", "Basic", "Basic", "Starts execution", true, true)]
        public virtual void Starter()
        {
            dynamicDict.Clear();
        }

        [Node("SerialMsg", "Helper", "Basic", "Sends Serial Message", true)]
        public void SendSerialMessage(string port, nNum baud, string msg)
        {
            try
            {
                using (SerialPort _serialPort = new SerialPort(port, baud.ToInt))
                {
                    _serialPort.Open();

                    if (_serialPort.IsOpen)
                    {
                        _serialPort.WriteLine(msg);
                        _serialPort.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error opening/writing to serial port :: " + ex.Message, "Error!");
            }
        }

        [Node("Send TCP msg", "TCP", "Basic", "Get TCP Response", true)]
        public void SendTcpMessage(nIPAdrss ip, string message, out string response)
        {
            using (TcpClient myClient = new TcpClient(ip.ToString(), ip.Port))
            {
                NetworkStream ns = myClient.GetStream();
                StreamWriter sw = new StreamWriter(ns);
                StreamReader rd = new StreamReader(ns);

                sw.WriteLine(message);
                sw.Flush();

                response = rd.ReadLine();

                myClient.Close();
            }
        }
    }
}
