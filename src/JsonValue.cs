using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

#nullable enable

namespace Hurisake
{
    public readonly struct JsonValue : IEnumerable<JsonValue?>
    {
        private static readonly IReadOnlyDictionary<JsonKey, JsonValue?> EmptyDictionary = new Dictionary<JsonKey, JsonValue?>();
        private static readonly JsonValue EmptyObject = new JsonValue(EmptyDictionary);

        private static readonly IReadOnlyList<JsonValue?> EmptyList = new List<JsonValue?>();
        private static readonly JsonValue EmptyArray = new JsonValue(EmptyList);

        private static readonly JsonValue True = new JsonValue(1, double.NegativeInfinity);

        private static readonly JsonValue False = new JsonValue(0, double.NegativeInfinity);

        public static explicit operator bool(JsonValue value)
        {
            if (double.IsNegativeInfinity(value._f)) return value._i != 0;
            throw new InvalidCastException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsFiniteDouble(double d)
        {
            long bits = BitConverter.DoubleToInt64Bits(d);
            return (bits & 0x7FFFFFFFFFFFFFFF) < 0x7FF0000000000000;
        }

        public static explicit operator int(JsonValue value)
        {
            if (double.IsNaN(value._f)) return unchecked((int)value._i);
            if (IsFiniteDouble(value._f)) return unchecked((int)value._f);
            throw new InvalidCastException();
        }

        public static explicit operator long(JsonValue value)
        {
            if (double.IsNaN(value._f)) return value._i;
            if (IsFiniteDouble(value._f)) return unchecked((long)value._f);
            throw new InvalidCastException();
        }

        public static explicit operator float(JsonValue value)
        {
            if (IsFiniteDouble(value._f)) return (float)value._f;
            if (double.IsNaN(value._f)) return value._i;
            throw new InvalidCastException();
        }

        public static explicit operator double(JsonValue value)
        {
            if (IsFiniteDouble(value._f)) return value._f;
            if (double.IsNaN(value._f)) return value._i;
            throw new InvalidCastException();
        }

        public static explicit operator bool[](JsonValue value)
        {
            var num = value.Count;
            var table = new bool[num];
            for (var i = 0; i < num; ++i)
            {
                table[i] = (bool)value[i];
            }

            return table;
        }

        public static explicit operator int[](JsonValue value)
        {
            var num = value.Count;
            var table = new int[num];
            for (var i = 0; i < num; ++i)
            {
                table[i] = (int)value[i];
            }

            return table;
        }

        public static explicit operator long[](JsonValue value)
        {
            var num = value.Count;
            var table = new long[num];
            for (var i = 0; i < num; ++i)
            {
                table[i] = (long)value[i];
            }

            return table;
        }

        public static explicit operator double[](JsonValue value)
        {
            var num = value.Count;
            var table = new double[num];
            for (var i = 0; i < num; ++i)
            {
                table[i] = (double)value[i];
            }

            return table;
        }

        public static explicit operator float[](JsonValue value)
        {
            var num = value.Count;
            var table = new float[num];
            for (var i = 0; i < num; ++i)
            {
                table[i] = (float)value[i];
            }

            return table;
        }

        public static explicit operator string[](JsonValue value)
        {
            var num = value.Count;
            var table = new string[num];
            for (var i = 0; i < num; ++i)
            {
                table[i] = (string)value[i];
            }

            return table;
        }

        public static explicit operator byte[](JsonValue value)
        {
            if (value._s == null) throw new InvalidCastException();
            return value._s;
        }

        public static explicit operator string(JsonValue value)
        {
            if (value._s == null) throw new InvalidCastException();
            return Encoding.UTF8.GetString(value._s);
        }

        public JsonValue this[JsonKey k] => Get(k)!.Value;

        public JsonValue this[int k] => Get(k)!.Value;

        public JsonValue? Get(JsonKey k)
        {
            if (_o == null) return null;
            if (_o.TryGetValue(k, out var r)) return r;
            return null;
        }

        public JsonValue? Get(int k)
        {
            if (_a == null) return null;
            if (k < 0 || k >= _a.Count) return null;
            return _a[k];
        }

        public bool ContainsKey(JsonKey k)
        {
            if(_o == null) return false;
            return _o.ContainsKey(k);
        }

        public int Count => _a?.Count ?? (_o?.Count ?? 0);

        public override string ToString()
        {
            if (double.IsNegativeInfinity(_f)) return _i != 0 ? "true" : "false";
            if (double.IsNaN(_f)) return _i.ToString();
            if (!double.IsPositiveInfinity(_f)) return _f.ToString(CultureInfo.InvariantCulture);
            if (_s != null) return "\"" + Regex.Replace(Encoding.UTF8.GetString(_s), @"[\u0000-\u001F\u0022\u005C]", m => $"\\u{(int)m.Value[0]:X4}") + "\"";
            if (_a != null) return "[" + string.Join(",", _a.Select(x => x != null ? x.ToString() : "null")) + "]";
            if (_o != null) return "{" + string.Join(",", _o.Select(
                x => x.Key + ":" + (x.Value != null ? x.Value.ToString() : "null")
            )) + "}";
            throw new Exception();
        }

        internal static JsonValue? ReadValue(ref JsonDeserializer sc)
        {
            switch (sc.GetValue())
            {
                case 1:
                    var (i, f) = sc.ReadNumber();
                    return new JsonValue(i, f);
                case 2:
                    return new JsonValue(sc.ReadString());
                case 3:
                    return ReadObject(ref sc);
                case 4:
                    return ReadArray(ref sc);
                case 5:
                    return True;
                case 6:
                    return False;
                case 7:
                    return null;
                default:
                    throw new Exception();
            }
        }

        private static JsonValue? ReadObject(ref JsonDeserializer s)
        {
            if (s.IsEmptyObject()) return EmptyObject;
            var res = new Dictionary<JsonKey, JsonValue?>();
            do
            {
                var k = s.ReadKey();
                var v = ReadValue(ref s);
                res.Add(k, v);
            } while (!s.IsObjectEnd());
            return new JsonValue(res);
        }

        private static JsonValue? ReadArray(ref JsonDeserializer s)
        {
            if (s.IsEmptyArray()) return EmptyArray;
            var res = new List<JsonValue?>();
            do
            {
                res.Add(ReadValue(ref s));
            } while (!s.IsArrayEnd());
            return new JsonValue(res);
        }

        public IEnumerator<JsonValue?> GetEnumerator()
        {
            return _a!.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        private readonly byte[]? _s;
        private readonly long _i;
        private readonly double _f;
        private readonly IReadOnlyDictionary<JsonKey, JsonValue?>? _o;
        private readonly IReadOnlyList<JsonValue?>? _a;

        private JsonValue(byte[] s)
        {
            _s = s;
            _i = 0;
            _f = double.PositiveInfinity;
            _o = null;
            _a = null;
        }

        private JsonValue(long i, double f)
        {
            _s = null;
            _i = i;
            _f = f;
            _o = null;
            _a = null;
        }

        private JsonValue(IReadOnlyDictionary<JsonKey, JsonValue?> o)
        {
            _s = null;
            _i = 0;
            _f = double.PositiveInfinity;
            _o = o;
            _a = null;
        }

        private JsonValue(IReadOnlyList<JsonValue?> a)
        {
            _s = null;
            _i = 0;
            _f = double.PositiveInfinity;
            _o = null;
            _a = a;
        }
    }
}
