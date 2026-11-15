using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Net.Http;
using TieLiteDesktop.Core;

namespace TieLiteDesktop.Core
{
    /// <summary>Exports a cached thread to PDF (text + embedded images) through Microsoft Print to PDF.</summary>
    public static class PdfExporter
    {
        private sealed class Block
        {
            public string Text { get; init; }
            public Image Image { get; init; }
            public bool Heading { get; init; }
            public bool Indent { get; init; }
        }

        public static bool Export(CachedThread thread, string path, out string error,
            IProgress<double> progress = null)
        {
            error = "";
            var images = new List<Image>();
            try
            {
                progress?.Report(0.05);
                var blocks = BuildBlocks(thread, images, progress);
                if (blocks.Count == 0)
                {
                    error = "没有可导出的内容";
                    return false;
                }

                var document = new PrintDocument
                {
                    DocumentName = thread.Title + ".pdf",
                    PrinterSettings =
                    {
                        PrinterName = "Microsoft Print to PDF",
                        PrintToFile = true,
                        PrintFileName = path,
                    },
                    DefaultPageSettings =
                    {
                        Margins = new Margins(60, 60, 60, 60),
                        Landscape = false,
                    },
                };
                var index = 0;
                document.PrintPage += (_, e) =>
                {
                    var graphics = e.Graphics;
                    var area = e.MarginBounds;
                    var width = (float)area.Width;
                    var y = (float)area.Top;
                    using var titleFont = new Font("Microsoft YaHei", 16, FontStyle.Bold);
                    using var bodyFont = new Font("Microsoft YaHei", 10.5f);
                    using var smallFont = new Font("Microsoft YaHei", 9);
                    if (index == 0)
                    {
                        graphics.DrawString(thread.Title, titleFont, Brushes.Black, area.Left, y);
                        y += titleFont.GetHeight(graphics) + 2;
                        graphics.DrawString(thread.ForumName + " · 缓存于 " +
                                            DateTimeOffset.FromUnixTimeSeconds(thread.SavedAt).LocalDateTime
                                                .ToString("yyyy-MM-dd HH:mm"),
                            smallFont, Brushes.Gray, area.Left, y);
                        y += smallFont.GetHeight(graphics) + 10;
                    }
                    while (index < blocks.Count)
                    {
                        var block = blocks[index];
                        if (block.Image != null)
                        {
                            var scale = Math.Min(1.0, width / block.Image.Width);
                            var drawWidth = (float)(block.Image.Width * scale);
                            var drawHeight = (float)(block.Image.Height * scale);
                            if (drawHeight > 520)
                            {
                                scale *= 520 / drawHeight;
                                drawWidth = (float)(block.Image.Width * scale);
                                drawHeight = (float)(block.Image.Height * scale);
                            }
                            if (y + drawHeight > area.Bottom)
                            {
                                e.HasMorePages = true;
                                return;
                            }
                            graphics.DrawImage(block.Image,
                                new RectangleF(area.Left, y, (float)drawWidth, (float)drawHeight));
                            y += drawHeight + 8;
                            index++;
                            continue;
                        }

                        var font = block.Heading ? bodyFont : bodyFont;
                        var brush = block.Heading ? Brushes.Black : Brushes.DimGray;
                        var text = block.Text ?? "";
                        var size = graphics.MeasureString(text, font, (int)Math.Max(1, width - (block.Indent ? 14 : 0)));
                        if (y + size.Height > area.Bottom)
                        {
                            e.HasMorePages = true;
                            return;
                        }
                        graphics.DrawString(text, font, brush,
                            new RectangleF(area.Left + (block.Indent ? 14 : 0), y,
                                width - (block.Indent ? 14 : 0), size.Height));
                        y += Math.Max(font.GetHeight(graphics) + 2, size.Height);
                        index++;
                    }
                };
                progress?.Report(0.9);
                document.Print();
                progress?.Report(1);
                return File.Exists(path);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                foreach (var image in images)
                {
                    try
                    {
                        image.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static List<Block> BuildBlocks(CachedThread thread, List<Image> images,
            IProgress<double> progress)
        {
            var blocks = new List<Block>();
            if (!string.IsNullOrWhiteSpace(thread.Title))
            {
                blocks.Add(new Block { Text = thread.Title, Heading = true });
            }
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/135 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://tieba.baidu.com/");
            var postIndex = 0;
            foreach (var post in thread.Posts)
            {
                postIndex++;
                progress?.Report(0.05 + 0.8 * postIndex / Math.Max(1, thread.Posts.Count));
                blocks.Add(new Block
                {
                    Text = post.Author + " · " + post.Floor + "楼" +
                           (post.Time > 0
                               ? " · " + DateTimeOffset.FromUnixTimeSeconds(post.Time).LocalDateTime.ToString("yyyy-MM-dd HH:mm")
                               : ""),
                    Heading = true,
                });
                foreach (var segment in post.Segments)
                {
                    if (segment.Kind == "Image")
                    {
                        var url = string.IsNullOrWhiteSpace(segment.Url) ? segment.ThumbUrl : segment.Url;
                        if (string.IsNullOrWhiteSpace(url)) continue;
                        try
                        {
                            Image image;
                            if (!string.IsNullOrWhiteSpace(segment.LocalImage) && File.Exists(segment.LocalImage))
                            {
                                image = Image.FromFile(segment.LocalImage);
                            }
                            else
                            {
                                var bytes = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
                                using var stream = new MemoryStream(bytes);
                                image = Image.FromStream(stream);
                            }
                            images.Add(image);
                            blocks.Add(new Block { Image = image });
                        }
                        catch
                        {
                            blocks.Add(new Block { Text = "[图片加载失败] " + url });
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(segment.Text))
                    {
                        foreach (var raw in segment.Text.Replace("\r", "").Split('\n'))
                        {
                            blocks.Add(new Block { Text = raw, Indent = true });
                        }
                    }
                }
                blocks.Add(new Block { Text = "" });
            }
            return blocks;
        }
    }
}
