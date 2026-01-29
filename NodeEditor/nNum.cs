using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.Serialization;

namespace NodeEditor
{
    [Serializable]
    [DefaultPropertyAttribute("Value")]
    [TypeConverter(typeof(nNumConverter))]
    public class nNum : ISerializable
    {
        private double _value = 0;

        public double Value { get => _value; set => _value = value; }

        [Browsable(false)]
        public double ToDouble { get => _value; }
        [Browsable(false)]
        public int ToInt { get => (int)_value; }
        [Browsable(false)]
        public float ToFloat { get => (float)_value; }
        [Browsable(false)]
        public string AsString { get => _value.ToString(); }

        public static nNum operator +(nNum a, nNum b) => new nNum(a.ToDouble + b.ToDouble);
        public static nNum operator -(nNum a, nNum b) => new nNum(a.ToDouble - b.ToDouble);
        public static nNum operator *(nNum a, nNum b) => new nNum(a.ToDouble * b.ToDouble);
        public static nNum operator /(nNum a, nNum b)
        {
            if (b.ToDouble == 0)
            {
                throw new DivideByZeroException();
            }
            return new nNum(a.ToDouble / b.ToDouble);
        }

        public static implicit operator int(nNum d) => d.ToInt;
        public static implicit operator nNum(int d) => new nNum(d);
        public static implicit operator double(nNum d) => d.ToDouble;
        public static implicit operator nNum(double b) => new nNum(b);
        public static implicit operator float(nNum d) => d.ToFloat;
        public static implicit operator nNum(float b) => new nNum(b);

        public static nNum operator +(nNum a, double b) => new nNum(a.ToDouble + b);
        public static nNum operator -(nNum a, double b) => new nNum(a.ToDouble - b);
        public static nNum operator *(nNum a, double b) => new nNum(a.ToDouble * b);
        public static nNum operator /(nNum a, double b)
        {
            if (b == 0)
            {
                throw new DivideByZeroException();
            }
            return new nNum(a.ToDouble / b);
        }

        public static nNum operator +(nNum a, int b) => new nNum(a.ToDouble + b);
        public static nNum operator -(nNum a, int b) => new nNum(a.ToDouble - b);
        public static nNum operator *(nNum a, int b) => new nNum(a.ToDouble * b);
        public static nNum operator /(nNum a, int b)
        {
            if (b == 0)
            {
                throw new DivideByZeroException();
            }
            return new nNum(a.ToDouble / b);
        }

        public nNum()
        {
        }

        public nNum(double val)
        {
            _value = val;
        }

        public nNum(int val)
        {
            _value = (double)val;
        }

        public nNum(float val)
        {
            _value = (double)val;
        }

        public nNum(nNum num)
        {
            _value = num.ToDouble;
        }

        public nNum(SerializationInfo info, StreamingContext ctx)
        {
            _value = info.GetDouble("V");
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("V", _value);
        }

        public override string ToString()
        {
            return _value.ToString();
        }
    }

    class nNumConverter : ExpandableObjectConverter
    {
        public override bool GetCreateInstanceSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override object CreateInstance(ITypeDescriptorContext context, System.Collections.IDictionary propertyValues)
        {
            if (propertyValues == null)
                throw new ArgumentNullException("propertyValues");

            object boxed = Activator.CreateInstance(context.PropertyDescriptor.PropertyType);
            foreach (System.Collections.DictionaryEntry entry in propertyValues)
            {
                System.Reflection.PropertyInfo pi = context.PropertyDescriptor.PropertyType.GetProperty(entry.Key.ToString());
                if ((pi != null) && (pi.CanWrite))
                {
                    pi.SetValue(boxed, Convert.ChangeType(entry.Value, pi.PropertyType), null);
                }
            }
            return boxed;
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            return new nNum((double)value);
        }
    }
}
