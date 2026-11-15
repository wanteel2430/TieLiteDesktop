using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace TieLiteDesktop.Core.Proto
{
    /// <summary>
    /// Generic decoded protobuf message: field name -> scalar / nested message / repeated list.
    /// Values are materialised lazily from the wire format at decode time.
    /// </summary>
    public sealed class ProtoMessage
    {
        internal readonly Dictionary<string, object> Values = new(StringComparer.Ordinal);

        public ProtoMessageType Type { get; }

        public string Dump(string prefix = "")
        {
            var builder = new System.Text.StringBuilder();
            foreach (var pair in Values)
            {
                if (pair.Value is ProtoMessage message)
                {
                    builder.AppendLine(prefix + pair.Key + ":");
                    builder.Append(message.Dump(prefix + "  "));
                }
                else if (pair.Value is System.Collections.IEnumerable list && !(pair.Value is string))
                {
                    var index = 0;
                    foreach (var item in list)
                    {
                        if (item is ProtoMessage child)
                        {
                            builder.AppendLine(prefix + pair.Key + "[" + index + "]:");
                            builder.Append(child.Dump(prefix + "  "));
                        }
                        else
                        {
                            builder.AppendLine(prefix + pair.Key + "[" + index + "]=" + item);
                        }
                        index++;
                    }
                }
                else
                {
                    builder.AppendLine(prefix + pair.Key + "=" + pair.Value);
                }
            }
            return builder.ToString();
        }

        public ProtoMessage(ProtoMessageType type) => Type = type;

        public bool Has(string name) => Values.ContainsKey(name);

        public object GetRaw(string name) => Values.TryGetValue(name, out var v) ? v : null;

        public string GetString(string name)
        {
            var v = GetRaw(name);
            return v switch
            {
                null => null,
                string s => s,
                byte[] b => System.Text.Encoding.UTF8.GetString(b),
                _ => Convert.ToString(v, CultureInfo.InvariantCulture),
            };
        }

        public long GetLong(string name, long fallback = 0)
        {
            var v = GetRaw(name);
            return v switch
            {
                null => fallback,
                long l => l,
                ulong ul => unchecked((long)ul),
                int i => i,
                uint u => u,
                double d => (long)d,
                string s => long.TryParse(s, out var parsed) ? parsed : fallback,
                bool b => b ? 1 : 0,
                _ => fallback,
            };
        }

        public int GetInt(string name, int fallback = 0) => (int)Math.Clamp(GetLong(name, fallback), int.MinValue, int.MaxValue);

        public bool GetBool(string name) => GetLong(name) != 0;

        public double GetDouble(string name, double fallback = 0)
        {
            var v = GetRaw(name);
            return v switch
            {
                null => fallback,
                double d => d,
                long l => l,
                _ => fallback,
            };
        }

        public ProtoMessage GetMessage(string name)
        {
            var v = GetRaw(name);
            if (v is ProtoMessage m) return m;
            if (v is IReadOnlyList<object> list && list.Count > 0 && list[0] is ProtoMessage first) return first;
            return null;
        }

        public IReadOnlyList<ProtoMessage> GetMessages(string name)
        {
            var result = new List<ProtoMessage>();
            if (!Values.TryGetValue(name, out var v) || v == null) return result;
            if (v is ProtoMessage single)
            {
                result.Add(single);
                return result;
            }
            if (v is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is ProtoMessage m) result.Add(m);
                }
            }
            return result;
        }

        public IReadOnlyList<long> GetLongs(string name)
        {
            var result = new List<long>();
            if (!Values.TryGetValue(name, out var v) || v == null) return result;
            if (v is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is long l) result.Add(l);
                    else if (item is int i) result.Add(i);
                }
            }
            else if (v is long l) result.Add(l);
            return result;
        }

        public IReadOnlyList<string> GetStrings(string name)
        {
            var result = new List<string>();
            if (!Values.TryGetValue(name, out var v) || v == null) return result;
            if (v is IEnumerable enumerable && v is not string)
            {
                foreach (var item in enumerable)
                {
                    if (item is string s) result.Add(s);
                }
            }
            else if (v is string s)
            {
                result.Add(s);
            }
            return result;
        }
    }
}
