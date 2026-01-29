using System;
using System.Windows.Forms;

namespace NodeEditor
{
    public partial class StandardNodeContext
    {
        [Node("Show Number Value", "Math", "Basic", "Adds two input values.", true)]
        public void ShowNum(nNum a)
        {
            MessageBox.Show(a.AsString);
        }

        [Node("Add", "Math", "Basic", "Adds two input values.", false)]
        public void Add(nNum a, nNum b, out nNum result)
        {
            result = new nNum(a.Value + b.Value);
        }

        [Node("Subtract", "Math", "Basic", "Subtract two input values.", false)]
        public void Sub(nNum a, nNum b, out nNum result)
        {
            result = new nNum(a.Value - b.Value);
        }

        [Node("Multiply", "Math", "Basic", "multiply two input values.", false)]
        public void Multi(nNum a, nNum b, out nNum result)
        {
            result = new nNum(a.Value * b.Value);
        }

        [Node("Divide", "Math", "Basic", "Divide two input values.", false)]
        public void divi(nNum a, nNum b, out nNum result)
        {
            result = new nNum(a.Value / b.Value);
        }

        [Node("Power", "Math", "Basic", "a ^ b", false)]
        public void Powe(nNum a, nNum b, out nNum result)
        {
            result = new nNum(Math.Pow(a.Value, b.Value));
        }

        [Node("Sqrt", "Math", "Basic", "sqrt(a)", false)]
        public void SquRt(nNum a, out nNum result)
        {
            result = new nNum(Math.Sqrt(a.Value));
        }

        [Node("Sin", "Math", "Basic", "Sin(a)", false)]
        public void Sine(nNum a, out nNum result)
        {
            result = new nNum(Math.Sin(a.Value));
        }

        [Node("Cos", "Math", "Basic", "Cos(a)", false)]
        public void Cose(nNum a, out nNum result)
        {
            result = new nNum(Math.Cos(a.Value));
        }

        [Node("PI", "Math", "Basic", "Cos(a)", false)]
        public void PIE(out nNum result)
        {
            result = new nNum(Math.PI);
        }

        [Node("Number Value", "Math", "Basic", "Allows to output a simple value.", false)]
        public void InputValue(nNum inValue, out nNum outValue)
        {
            outValue = inValue;
        }
    }
}
