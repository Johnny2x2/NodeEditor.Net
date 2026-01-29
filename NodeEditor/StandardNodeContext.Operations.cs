namespace NodeEditor
{
    public partial class StandardNodeContext
    {
        [Node("Num ==", "Conditions/Numbers", "Basic", "Compare 2 Values", false)]
        public void IsEqual(nNum a, nNum b, out bool result)
        {
            result = a.Value == b.Value;
        }

        [Node("Num !=", "Conditions/Numbers", "Basic", "Compare 2 Values", false)]
        public void IsNotEqual(nNum a, nNum b, out bool result)
        {
            result = a.Value != b.Value;
        }

        [Node("Num <=", "Conditions/Numbers", "Basic", "Compare 2 Values", false)]
        public void IslessThanEqual(nNum a, nNum b, out bool result)
        {
            result = a.Value <= b.Value;
        }

        [Node("Num <=", "Conditions/Numbers", "Basic", "Compare 2 Values", false)]
        public void IsGreaterThanEqual(nNum a, nNum b, out bool result)
        {
            result = a.Value >= b.Value;
        }

        [Node("Num <", "Conditions/Numbers", "Basic", "Compare 2 Values", false)]
        public void IslessThan(nNum a, nNum b, out bool result)
        {
            result = a.Value < b.Value;
        }

        [Node("Num >", "Conditions/Numbers", "Basic", "Compare 2 Values", false)]
        public void IsGreaterThan(nNum a, nNum b, out bool result)
        {
            result = a.Value > b.Value;
        }

        [Node("!Value", "Conditions", "Basic", "Compare 2 Values", false)]
        public void IsNot(bool input, out bool result)
        {
            result = !input;
        }

        [Node("Input.contains(value)", "Conditions/Strings", "Basic", "See if Input string contains string value", true)]
        public void DoesContain(string input, string value, out bool result)
        {
            result = input.Contains(value);
        }

        [Node("string == string", "Conditions/Strings", "Basic", "See if Input string contains string value", true)]
        public void StringsEqual(string input, string value, out bool result)
        {
            result = input == value;
        }
    }
}
