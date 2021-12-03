using System;
using System.Globalization;
using System.IO;
using System.Text;

#nullable enable

namespace Hurisake
{
    public ref struct JsonDeserializer
    {
        public static JsonValue? Deserialize(string value)
        {
            var sc = new JsonDeserializer(Encoding.UTF8.GetBytes(value));
            return JsonValue.ReadValue(ref sc);
        }

        public static JsonValue? Deserialize(byte[] value)
        {
            var sc = new JsonDeserializer(value);
            return JsonValue.ReadValue(ref sc);
        }

        private readonly byte[] _s;
        private readonly int _l;
        private int _p;

        private JsonDeserializer(byte[] s)
        {
            _s = s;
            _l = s.Length;
            _p = 0;
        }

        internal int GetValue()
        {
            ReadWhitespace();
            switch (_s[_p])
            {
                case 0x22: // "
                    _p += 1;
                    return 2;
                case 0x7B: // {
                    _p += 1;
                    return 3;
                case 0x5B: // [
                    _p += 1;
                    return 4;
                case 0x74: // t
                    _p += 4;
                    return 5;
                case 0x66: // f
                    _p += 5;
                    return 6;
                case 0x6e: // n
                    _p += 4;
                    return 7;
                default:
                    return 1;
            }
        }

        private byte[] GetBlock(int p, int q)
        {
            var buf = new byte[q - p];
            Buffer.BlockCopy(_s, p, buf, 0, q - p);
            return buf;
        }

        internal (long, double) ReadNumber()
        {
            var p0 = _p;
            if (_s[_p] == 0x2D) _p += 1; // -

            var p1 = _p;
            while (_p < _l && 0x30 <= _s[_p] && _s[_p] <= 0x39) _p += 1; // 0-9
            if (_p == p1) throw new Exception();

            var p2 = _p;

            if (_p < _l && _s[_p] == 0x2E) // .
            {
                _p += 1;
                while (_p < _l && 0x30 <= _s[_p] && _s[_p] <= 0x39) _p += 1; // 0-9
            }

            if (_p < _l && (_s[_p] == 0x65 || _s[_p] == 0x45)) // e E
            {
                _p += 1;
                if (_s[_p] == 0x2D || _s[_p] == 0x2B) _p += 1; // - +
                while (_p < _l && 0x30 <= _s[_p] && _s[_p] <= 0x39) _p += 1; // 0-9
            }

            if (_p == p2 && p2 - p1 <= 19)
            {
                ulong x = 0;
                unchecked
                {
                    for (var i = p1; i < p2; i++)
                    {
                        x = x * 10 + (ulong) (_s[i] - 0x30);
                    }
                }

                if (x <= long.MaxValue)
                {
                    return (p0 == p1 ? (long) x : - (long) x, double.NaN);
                }
            }

            var f = double.Parse(Encoding.UTF8.GetString(GetBlock(p0, _p)), NumberStyles.Float, CultureInfo.InvariantCulture);
            if (!JsonValue.IsFiniteDouble(f)) throw new Exception();
            return (0, f);
        }

        internal byte[] ReadString()
        {
            var p0 = _p;
            return SeekString() ? Unescape(p0, _p-1) : GetBlock(p0, _p-1);
        }

        internal JsonKey ReadKey()
        {
            ReadWhitespace();
            if (_s[_p++] != 0x22) throw new Exception(); // "
            var p0 = _p;
            var r = SeekString() ? new JsonKey(Unescape(p0, _p-1)) : new JsonKey(_s, p0, _p-1);
            ReadWhitespace();
            if (_s[_p++] != 0x3A) throw new Exception(); // :
            return r;
        }

        internal bool IsEmptyObject()
        {
            ReadWhitespace();
            if (_s[_p] != 0x7D) return false; // }
            _p += 1;
            return true;
        }

        internal bool IsEmptyArray()
        {
            ReadWhitespace();
            if (_s[_p] != 0x5D) return false; // ]
            _p += 1;
            return true;
        }

        internal bool IsObjectEnd()
        {
            ReadWhitespace();
            return _s[_p++] switch
            {
                0x7D => true, // }
                0x2C => false, // ,
                _ => throw new Exception()
            };
        }

        internal bool IsArrayEnd()
        {
            ReadWhitespace();
            return _s[_p++] switch
            {
                0x5D => true, // ]
                0x2C => false, // ,
                _ => throw new Exception($"IsArrayEnd error {_s[_p-1]}")
            };
        }

        private void ReadWhitespace()
        {
            for (;;)
            {
                switch (_s[_p])
                {
                    case 0x20: // SP
                    case 0x09: // HT
                    case 0x0A: // LF
                    case 0x0D: // CR
                        _p += 1;
                        break;
                    default:
                        return;
                }
            }
        }

        private bool SeekString()
        {
            var escaped = false;
            for (;;)
            {
                switch (_s[_p++])
                {
                    case 0x22: // "
                        return escaped;
                    case 0x5C: // \
                        escaped = true;
                        _p += _s[_p] == 0x75 ? 5 : 1; // u
                        break;
                }
            }
        }

        private byte[] Unescape(int p0, int q0)
        {
            using var stream = new MemoryStream(q0 - p0);
            var i = p0;
            var p = p0;
            for (;;)
            {
                if (i == q0)
                {
                    stream.Write(_s, p, q0-p);
                    return stream.ToArray();
                }

                if (_s[i++] != 0x5C) continue;

                stream.Write(_s, p, i-1-p);
                switch (_s[i++])
                {
                    case 0x22: // "
                        stream.WriteByte(0x22);
                        break;
                    case 0x5C: // \
                        stream.WriteByte(0x5C);
                        break;
                    case 0x2F: // /
                        stream.WriteByte(0x2F);
                        break;
                    case 0x62: // b
                        stream.WriteByte(0x08);
                        break;
                    case 0x66: // f
                        stream.WriteByte(0x0C);
                        break;
                    case 0x6E: // n
                        stream.WriteByte(0x0A);
                        break;
                    case 0x72: // r
                        stream.WriteByte(0x0D);
                        break;
                    case 0x74: // t
                        stream.WriteByte(0x09);
                        break;
                    case 0x75: // u
                        var c = (Hex(_s[i]) << 12) | (Hex(_s[i+1]) << 8) | (Hex(_s[i+2]) << 4) | Hex(_s[i+3]);
                        i += 4;
                        if (c <= 0x7F)
                        {
                            stream.WriteByte(unchecked((byte) c));
                        }
                        else if (c <= 0x7FF)
                        {
                            stream.WriteByte(unchecked((byte) ((c >> 6) | 0xC0)));
                            stream.WriteByte(unchecked((byte) ((c & 0x3F) | 0x80)));
                        }
                        else
                        {
                            stream.WriteByte(unchecked((byte) ((c >> 12) | 0xE0)));
                            stream.WriteByte(unchecked((byte) (((c >> 6) & 0x3F) | 0x80)));
                            stream.WriteByte(unchecked((byte) ((c & 0x3F) | 0x80)));
                        }
                        break;
                    default:
                        throw new Exception();
                }

                p = i;
            }
        }

        private static int Hex(byte x)
        {
            if (0x30 <= x && x <= 0x39) return x - 0x30;
            if (0x41 <= x && x <= 0x46) return x - 0x41 + 10;
            if (0x61 <= x && x <= 0x66) return x - 0x61 + 10;
            throw new Exception();
        }
    }
}
