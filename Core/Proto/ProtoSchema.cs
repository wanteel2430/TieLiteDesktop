using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TieLiteDesktop.Core.Proto
{
    public sealed class ProtoField
    {
        public int Number { get; init; }
        public string Name { get; init; }
        public string Type { get; init; }
        public bool Repeated { get; init; }
    }

    public sealed class ProtoMessageType
    {
        public string FullName { get; init; }
        public string Package { get; init; }
        public string ShortName { get; init; }
        public Dictionary<int, ProtoField> ByNumber { get; } = new();
        public Dictionary<string, ProtoField> ByName { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Schema generated from the Android project's .proto files (Assets/schema.json).
    /// </summary>
    public sealed class ProtoSchema
    {
        private readonly Dictionary<string, ProtoMessageType> _byFull = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ProtoMessageType>> _byShort = new(StringComparer.Ordinal);

        public IEnumerable<ProtoMessageType> All => _byFull.Values;

        public static ProtoSchema Load(string path)
        {
            var schema = new ProtoSchema();
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var m in doc.RootElement.GetProperty("messages").EnumerateArray())
            {
                var full = m.GetProperty("name").GetString();
                var type = new ProtoMessageType
                {
                    FullName = full,
                    Package = full.Contains('.') ? full[..full.LastIndexOf('.')] : "",
                    ShortName = full.Contains('.') ? full[(full.LastIndexOf('.') + 1)..] : full,
                };
                foreach (var f in m.GetProperty("fields").EnumerateArray())
                {
                    var field = new ProtoField
                    {
                        Number = f.GetProperty("n").GetInt32(),
                        Name = f.GetProperty("name").GetString(),
                        Type = f.GetProperty("type").GetString(),
                        Repeated = f.GetProperty("repeated").GetBoolean(),
                    };
                    type.ByNumber[field.Number] = field;
                    type.ByName[field.Name] = field;
                }
                schema._byFull[full] = type;
                if (!schema._byShort.TryGetValue(type.ShortName, out var list))
                {
                    list = new List<ProtoMessageType>();
                    schema._byShort[type.ShortName] = list;
                }
                list.Add(type);
            }
            return schema;
        }

        public ProtoMessageType Get(string fullName) =>
            _byFull.TryGetValue(fullName, out var t) ? t : null;

        /// <summary>
        /// Resolve a field type name ("ThreadInfo", "tieba.ThreadInfo", ...) against the schema.
        /// Prefers a match inside the enclosing message's package.
        /// </summary>
        public ProtoMessageType Resolve(string typeName, string contextPackage)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            if (_byFull.TryGetValue(typeName, out var exact)) return exact;
            var shortName = typeName.Contains('.') ? typeName[(typeName.LastIndexOf('.') + 1)..] : typeName;
            if (_byShort.TryGetValue(shortName, out var candidates) && candidates.Count > 0)
            {
                if (!string.IsNullOrEmpty(contextPackage))
                {
                    foreach (var c in candidates)
                    {
                        if (c.Package == contextPackage) return c;
                    }
                }
                return candidates[0];
            }
            return null;
        }
    }
}
