using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TieLiteDesktop.Core.Models;
using TieLiteDesktop.Core.Proto;

namespace TieLiteDesktop.Core
{
    /// <summary>Turns PbContent protobuf segments into renderable content segments.</summary>
    public static class ContentParser
    {
        private static readonly Regex EmoticonToken = new(@"#\(([^\)]{1,16})\)", RegexOptions.Compiled);
        private static readonly Regex ImageEmoticonId = new(@"^image_emoticon\d*$", RegexOptions.Compiled);
        private static Dictionary<string, string> _nameToId = new(StringComparer.Ordinal);

        public static string AssetsDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Assets");

        public static IReadOnlyList<KeyValuePair<string, string>> AllEmoticons
        {
            get
            {
                var list = _nameToId.Where(p => EmoticonFileExists(p.Value)).ToList();
                list.Sort((a, b) =>
                {
                    int Parse(string id) => int.TryParse(id.Replace("image_emoticon", ""), out var n) ? n : 9999;
                    return Parse(a.Value).CompareTo(Parse(b.Value));
                });
                return list;
            }
        }

        public static IReadOnlyList<KeyValuePair<string, string>> KnownEmoticons
        {
            get
            {
                var hidden = Svc.Settings?.HiddenEmoticons ?? Array.Empty<string>();
                var list = _nameToId.Where(p => EmoticonFileExists(p.Value) && !hidden.Contains(p.Key)).ToList();
                list.Sort((a, b) =>
                {
                    int Parse(string id) => int.TryParse(id.Replace("image_emoticon", ""), out var n) ? n : 9999;
                    return Parse(a.Value).CompareTo(Parse(b.Value));
                });
                return list;
            }
        }

        public static void Initialize(string assetsDirectory)
        {
            AssetsDirectory = assetsDirectory;
            try
            {
                var path = Path.Combine(assetsDirectory, "emoticons.json");
                if (File.Exists(path))
                {
                    _nameToId = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                                ?? new Dictionary<string, string>();
                }
            }
            catch
            {
                _nameToId = new Dictionary<string, string>();
            }
        }

        public static bool TryGetEmoticonId(string name, out string id)
        {
            id = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (ImageEmoticonId.IsMatch(name))
            {
                id = name == "image_emoticon" ? "image_emoticon1" : name;
                return EmoticonFileExists(id);
            }
            if (_nameToId.TryGetValue(name, out var mapped) && EmoticonFileExists(mapped))
            {
                id = mapped;
                return true;
            }
            return false;
        }

        public static string EmoticonPath(string id) => Path.Combine(AssetsDirectory, "Emoticons", id + ".png");

        private static readonly HashSet<string> ExistingEmoticons = new(StringComparer.Ordinal);

        private static bool EmoticonFileExists(string id)
        {
            if (ExistingEmoticons.Contains(id)) return true;
            if (File.Exists(EmoticonPath(id)))
            {
                ExistingEmoticons.Add(id);
                return true;
            }
            return false;
        }

        public static List<ContentSegment> ParseSegments(ProtoMessage post)
        {
            var result = new List<ContentSegment>();
            foreach (var c in post.GetMessages("content"))
            {
                var type = c.GetInt("type");
                var text = c.GetString("text") ?? "";
                switch (type)
                {
                    case 0:
                    case 9:
                    case 27:
                    case 35:
                    case 40:
                        AppendText(result, text);
                        break;
                    case 1:
                        result.Add(new ContentSegment
                        {
                            Kind = ContentKind.Link,
                            Text = text,
                            Url = c.GetString("link") ?? c.GetString("src") ?? "",
                        });
                        break;
                    case 2:
                    {
                        var name = c.GetString("c") ?? "";
                        var idText = c.GetString("text") ?? "";
                        var id = (string)null;
                        if (!TryGetEmoticonId(idText, out id)) TryGetEmoticonId(name, out id);
                        result.Add(new ContentSegment
                        {
                            Kind = ContentKind.Emoticon,
                            Text = name,
                            EmoticonId = id ?? "",
                        });
                        break;
                    }
                    case 3:
                    {
                        var url = FirstNonEmpty(c.GetString("bigCdnSrc"), c.GetString("originSrc"), c.GetString("cdnSrc"), c.GetString("src"));
                        var thumb = FirstNonEmpty(c.GetString("cdnSrc"), c.GetString("bigCdnSrc"), c.GetString("src"), url);
                        var (w, h) = ParseSize(c.GetString("bsize"));
                        result.Add(new ContentSegment
                        {
                            Kind = ContentKind.Image,
                            Url = url,
                            ThumbUrl = thumb,
                            Width = w,
                            Height = h,
                        });
                        break;
                    }
                    case 4:
                        result.Add(new ContentSegment
                        {
                            Kind = ContentKind.At,
                            Text = text,
                            Uid = c.GetLong("uid"),
                        });
                        break;
                    case 5:
                        result.Add(new ContentSegment
                        {
                            Kind = ContentKind.Video,
                            Text = text,
                            Url = FirstNonEmpty(c.GetString("link"), c.GetString("src")),
                            ThumbUrl = c.GetString("src") ?? "",
                        });
                        break;
                    case 10:
                        result.Add(new ContentSegment
                        {
                            Kind = ContentKind.Voice,
                            Text = text,
                            Url = c.GetString("voiceMD5") ?? "",
                        });
                        break;
                    default:
                        if (!string.IsNullOrEmpty(text)) AppendText(result, text);
                        break;
                }
            }
            return result;
        }

        private static void AppendText(List<ContentSegment> result, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var last = 0;
            foreach (Match match in EmoticonToken.Matches(text))
            {
                if (match.Index > last)
                {
                    result.Add(new ContentSegment { Kind = ContentKind.Text, Text = text[last..match.Index] });
                }
                var name = match.Groups[1].Value;
                if (TryGetEmoticonId(name, out var id))
                {
                    result.Add(new ContentSegment { Kind = ContentKind.Emoticon, Text = name, EmoticonId = id });
                }
                else
                {
                    result.Add(new ContentSegment { Kind = ContentKind.Text, Text = match.Value });
                }
                last = match.Index + match.Length;
            }
            if (last < text.Length)
            {
                result.Add(new ContentSegment { Kind = ContentKind.Text, Text = text[last..] });
            }
        }

        private static (int, int) ParseSize(string bsize)
        {
            if (string.IsNullOrEmpty(bsize)) return (0, 0);
            var parts = bsize.Split(',');
            if (parts.Length != 2) return (0, 0);
            int.TryParse(parts[0], out var w);
            int.TryParse(parts[1], out var h);
            return (w, h);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
            return "";
        }

        public static string PlainText(IEnumerable<ContentSegment> segments)
        {
            var sb = new StringBuilder();
            foreach (var segment in segments)
            {
                switch (segment.Kind)
                {
                    case ContentKind.Text:
                    case ContentKind.At:
                    case ContentKind.Link:
                        sb.Append(segment.Text);
                        break;
                    case ContentKind.Emoticon:
                        sb.Append('#').Append('(').Append(segment.Text).Append(')');
                        break;
                }
            }
            return sb.ToString().Trim();
        }
    }
}
