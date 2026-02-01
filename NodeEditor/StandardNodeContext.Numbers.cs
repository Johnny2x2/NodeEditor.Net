using System;

namespace NodeEditor
{
    public partial class StandardNodeContext
    {
        private static readonly object RandomLock = new object();
        private static readonly Random RandomGenerator = new Random();

        [Node("Abs", "Numbers", "Basic", "Absolute value.", false)]
        public void NumAbs(nNum value, out nNum result)
        {
            ReportRunning();
            result = new nNum(Math.Abs(value.ToDouble));
        }

        [Node("Min", "Numbers", "Basic", "Minimum of two numbers.", false)]
        public void NumMin(nNum a, nNum b, out nNum result)
        {
            ReportRunning();
            result = new nNum(Math.Min(a.ToDouble, b.ToDouble));
        }

        [Node("Max", "Numbers", "Basic", "Maximum of two numbers.", false)]
        public void NumMax(nNum a, nNum b, out nNum result)
        {
            ReportRunning();
            result = new nNum(Math.Max(a.ToDouble, b.ToDouble));
        }

        [Node("Mod", "Numbers", "Basic", "Remainder of division.", false)]
        public void NumMod(nNum a, nNum b, out nNum result)
        {
            ReportRunning();
            result = new nNum(a.ToDouble % b.ToDouble);
        }

        [Node("Round", "Numbers", "Basic", "Round to number of digits.", false)]
        public void NumRound(nNum value, nNum digits, out nNum result)
        {
            ReportRunning();
            result = new nNum(Math.Round(value.ToDouble, Math.Max(0, digits.ToInt)));
        }

        [Node("Floor", "Numbers", "Basic", "Floor value.", false)]
        public void NumFloor(nNum value, out nNum result)
        {
            ReportRunning();
            result = new nNum(Math.Floor(value.ToDouble));
        }

        [Node("Ceiling", "Numbers", "Basic", "Ceiling value.", false)]
        public void NumCeiling(nNum value, out nNum result)
        {
            ReportRunning();
            result = new nNum(Math.Ceiling(value.ToDouble));
        }

        [Node("Clamp", "Numbers", "Basic", "Clamp value between min and max.", false)]
        public void NumClamp(nNum value, nNum min, nNum max, out nNum result)
        {
            ReportRunning();
            var minVal = Math.Min(min.ToDouble, max.ToDouble);
            var maxVal = Math.Max(min.ToDouble, max.ToDouble);
            result = new nNum(Math.Min(Math.Max(value.ToDouble, minVal), maxVal));
        }

        [Node("Random Range", "Numbers", "Basic", "Random number between min and max.", false)]
        public void NumRandomRange(nNum min, nNum max, out nNum result)
        {
            ReportRunning();
            var minVal = Math.Min(min.ToDouble, max.ToDouble);
            var maxVal = Math.Max(min.ToDouble, max.ToDouble);

            double value;
            lock (RandomLock)
            {
                value = RandomGenerator.NextDouble() * (maxVal - minVal) + minVal;
            }

            result = new nNum(value);
        }

        [Node("Sign", "Numbers", "Basic", "Get sign of a value.", false)]
        public void NumSign(nNum value, out nNum result)
        {
            ReportRunning();
            result = new nNum(Math.Sign(value.ToDouble));
        }
    }
}
