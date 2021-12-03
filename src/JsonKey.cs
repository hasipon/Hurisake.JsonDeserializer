using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#nullable enable

namespace Hurisake
{
    public readonly struct JsonKey : IEquatable<JsonKey>
    {
        public static implicit operator JsonKey(byte[] value)
        {
            return new JsonKey(value);
        }

        public static implicit operator JsonKey(string value)
        {
            return new JsonKey(Encoding.UTF8.GetBytes(value));
        }

        public override string ToString()
        {
            if (_s != null) return "\"" + Encoding.UTF8.GetString(_s) + "\"";
            var h = _hashCode;
            var a = new List<byte>();
            while (h != 0)
            {
                a.Insert(0, (byte)(h & 0xFF));
                h >>= 8;
            }

            return "\"" + Encoding.UTF8.GetString(a.ToArray()) + "\"";
        }

        private readonly ulong _hashCode;
        private readonly byte[]? _s;

        public JsonKey(byte[] s)
        {
            if (s.Length <= 8)
            {
                _hashCode = 0;
                foreach (var c in s)
                {
                    _hashCode = (_hashCode << 8) | c;
                }
                _s = null;
            }
            else
            {
                _hashCode = 5381;
                foreach (var c in s)
                {
                    _hashCode = unchecked(_hashCode * 33 + c);
                }
                _s = s;
            }
        }

        public JsonKey(byte[] s, int p, int q)
        {
            if (q - p <= 8)
            {
                _hashCode = 0;
                for (var i = p; i < q; i++)
                {
                    _hashCode = (_hashCode << 8) | s[i];
                }
                _s = null;
            }
            else
            {
                _hashCode = 5381;
                for (var i = p; i < q; i++)
                {
                    _hashCode = unchecked(_hashCode * 33 + s[i]);
                }
                _s = new byte[q - p];
                Buffer.BlockCopy(s, p, _s, 0, q - p);
            }
        }

        public bool Equals(JsonKey other)
        {
            if (_hashCode != other._hashCode) return false;
            if (_s == null) return other._s == null;
            return other._s != null && _s.Length == other._s.Length && _s.SequenceEqual(other._s);
        }

        public override bool Equals(object? obj)
        {
            if (obj is JsonKey other)
            {
                return Equals(other);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return unchecked((int) (_hashCode >> 32) * 37 + (int) _hashCode);
        }

        public static bool operator ==(JsonKey left, JsonKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(JsonKey left, JsonKey right)
        {
            return !(left == right);
        }
    }
}
