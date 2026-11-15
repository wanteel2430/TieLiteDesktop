using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TieLiteDesktop.Core.Proto
{
    /// <summary>
    /// Wire-format reader/writer driven by <see cref="ProtoSchema"/>.
    /// The Android app uses Wire-generated classes; desktop keeps the same field numbers
    /// by decoding against the shared schema instead of 300 generated classes.
    /// </summary>
    public static class ProtoCodec
    {
        public static ProtoMessage Decode(ProtoSchema schema, string fullTypeName, byte[] data)
        {
            var type = schema.Get(fullTypeName) ?? schema.Resolve(fullTypeName, null)
                ?? throw new InvalidOperationException("Unknown proto type: " + fullTypeName);
            return Decode(schema, type, data, 0);
        }

        private static ProtoMessage Decode(ProtoSchema schema, ProtoMessageType type, byte[] data, int depth)
        {
            var message = new ProtoMessage(type);
            if (depth > 16 || data == null || data.Length == 0) return message;

            var pos = 0;
            while (pos < data.Length)
            {
                var key = ReadVarint(data, ref pos);
                var number = (int)(key >> 3);
                var wire = (int)(key & 7);
                if (number <= 0) break;

                if (!type.ByNumber.TryGetValue(number, out var field))
                {
                    SkipField(data, ref pos, wire);
                    continue;
                }

                object value;
                switch (wire)
                {
                    case 0:
                    {
                        var raw = ReadVarint(data, ref pos);
                        value = field.Type == "bool" ? (raw != 0) : unchecked((long)raw);
                        break;
                    }
                    case 1:
                    {
                        if (pos + 8 > data.Length) return message;
                        if (field.Type == "double")
                        {
                            value = BitConverter.ToDouble(data, pos);
                        }
                        else
                        {
                            value = BitConverter.ToInt64(data, pos);
                        }
                        pos += 8;
                        break;
                    }
                    case 2:
                    {
                        var length = checked((int)ReadVarint(data, ref pos));
                        if (length < 0 || pos + length > data.Length) return message;
                        var raw = new byte[length];
                        Buffer.BlockCopy(data, pos, raw, 0, length);
                        pos += length;
                        if (field.Type == "string")
                        {
                            value = Encoding.UTF8.GetString(raw);
                        }
                        else if (field.Type == "bytes")
                        {
                            value = raw;
                        }
                        else
                        {
                            var nestedType = schema.Resolve(field.Type, type.Package);
                            value = nestedType != null ? Decode(schema, nestedType, raw, depth + 1) : raw;
                        }
                        break;
                    }
                    case 5:
                    {
                        if (pos + 4 > data.Length) return message;
                        value = field.Type == "float" ? BitConverter.ToSingle(data, pos) : (object)BitConverter.ToInt32(data, pos);
                        pos += 4;
                        break;
                    }
                    default:
                        return message;
                }

                if (field.Repeated)
                {
                    if (!message.Values.TryGetValue(field.Name, out var existing) || existing is not List<object> list)
                    {
                        list = new List<object>();
                        message.Values[field.Name] = list;
                    }
                    list.Add(value);
                }
                else
                {
                    message.Values[field.Name] = value;
                }
            }
            return message;
        }

        private static void SkipField(byte[] data, ref int pos, int wire)
        {
            switch (wire)
            {
                case 0:
                    ReadVarint(data, ref pos);
                    break;
                case 1:
                    pos += 8;
                    break;
                case 2:
                {
                    var len = checked((int)ReadVarint(data, ref pos));
                    pos += len;
                    break;
                }
                case 5:
                    pos += 4;
                    break;
                default:
                    pos = data.Length;
                    break;
            }
        }

        private static ulong ReadVarint(byte[] data, ref int pos)
        {
            ulong result = 0;
            var shift = 0;
            while (pos < data.Length)
            {
                var b = data[pos++];
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return result;
                shift += 7;
                if (shift >= 64) break;
            }
            return result;
        }

        public static byte[] Encode(ProtoSchema schema, string fullTypeName, IDictionary<string, object> values)
        {
            var type = schema.Get(fullTypeName) ?? schema.Resolve(fullTypeName, null)
                ?? throw new InvalidOperationException("Unknown proto type: " + fullTypeName);
            using var ms = new MemoryStream();
            EncodeMessage(schema, type, values, ms);
            return ms.ToArray();
        }

        private static void EncodeMessage(ProtoSchema schema, ProtoMessageType type, IDictionary<string, object> values, Stream output)
        {
            foreach (var pair in values)
            {
                if (pair.Value == null) continue;
                if (!type.ByName.TryGetValue(pair.Key, out var field)) continue;

                if (field.Repeated)
                {
                    foreach (var item in (IEnumerable)pair.Value)
                    {
                        if (item == null) continue;
                        EncodeField(schema, type, field, item, output);
                    }
                }
                else
                {
                    EncodeField(schema, type, field, pair.Value, output);
                }
            }
        }

        private static void EncodeField(ProtoSchema schema, ProtoMessageType parent, ProtoField field, object value, Stream output)
        {
            var nestedType = schema.Resolve(field.Type, parent.Package);
            switch (field.Type)
            {
                case "string":
                {
                    var bytes = Encoding.UTF8.GetBytes(Convert.ToString(value) ?? "");
                    WriteTag(output, field.Number, 2);
                    WriteVarint(output, (ulong)bytes.Length);
                    output.Write(bytes, 0, bytes.Length);
                    break;
                }
                case "bytes":
                {
                    var bytes = (byte[])value;
                    WriteTag(output, field.Number, 2);
                    WriteVarint(output, (ulong)bytes.Length);
                    output.Write(bytes, 0, bytes.Length);
                    break;
                }
                case "double":
                    WriteTag(output, field.Number, 1);
                    WriteFixed64(output, BitConverter.DoubleToInt64Bits(Convert.ToDouble(value)));
                    break;
                case "float":
                    WriteTag(output, field.Number, 5);
                    WriteFixed32(output, BitConverter.SingleToInt32Bits(Convert.ToSingle(value)));
                    break;
                case "bool":
                    WriteTag(output, field.Number, 0);
                    WriteVarint(output, (bool)value ? 1UL : 0UL);
                    break;
                default:
                    if (nestedType != null)
                    {
                        using var ms = new MemoryStream();
                        if (value is IDictionary<string, object> dict)
                        {
                            EncodeMessage(schema, nestedType, dict, ms);
                        }
                        else if (value is ProtoMessage pm)
                        {
                            EncodeMessage(schema, nestedType, pm.Values, ms);
                        }
                        var bytes = ms.ToArray();
                        WriteTag(output, field.Number, 2);
                        WriteVarint(output, (ulong)bytes.Length);
                        output.Write(bytes, 0, bytes.Length);
                    }
                    else
                    {
                        WriteTag(output, field.Number, 0);
                        WriteVarint(output, unchecked((ulong)Convert.ToInt64(value)));
                    }
                    break;
            }
        }

        private static void WriteTag(Stream output, int number, int wire)
        {
            WriteVarint(output, (ulong)((number << 3) | wire));
        }

        private static void WriteVarint(Stream output, ulong value)
        {
            while (value >= 0x80)
            {
                output.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            output.WriteByte((byte)value);
        }

        private static void WriteFixed32(Stream output, int value)
        {
            output.Write(BitConverter.GetBytes(value), 0, 4);
        }

        private static void WriteFixed64(Stream output, long value)
        {
            output.Write(BitConverter.GetBytes(value), 0, 8);
        }
    }
}
