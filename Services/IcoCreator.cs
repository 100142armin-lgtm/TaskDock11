using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace TaskDock11.Services
{
    public static class IcoCreator
    {
        public static void GenerateAppIco(string targetIcoPath)
        {
            int[] sizes = new int[] { 16, 32, 48, 64, 128, 256 };
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // ICO Header
            bw.Write((short)0); // Reserved
            bw.Write((short)1); // Type: 1 for Icon
            bw.Write((short)sizes.Length); // Image count

            var imageBuffers = new byte[sizes.Length][];

            for (int i = 0; i < sizes.Length; i++)
            {
                int sz = sizes[i];
                using var bmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    // 1. 发光外环 (Cyan to Purple)
                    using var path = new GraphicsPath();
                    path.AddEllipse(sz * 0.08f, sz * 0.08f, sz * 0.84f, sz * 0.84f);
                    using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 0, 229, 255), Math.Max(2f, sz * 0.07f));
                    g.DrawPath(pen, path);

                    // 2. 悬浮暗蓝底座
                    float dockY = sz * 0.55f;
                    float dockH = sz * 0.26f;
                    using var brushDock = new SolidBrush(System.Drawing.Color.FromArgb(240, 15, 23, 42));
                    using var penDock = new System.Drawing.Pen(System.Drawing.Color.FromArgb(200, 56, 189, 248), Math.Max(1f, sz * 0.03f));
                    g.FillRectangle(brushDock, sz * 0.2f, dockY, sz * 0.6f, dockH);
                    g.DrawRectangle(penDock, sz * 0.2f, dockY, sz * 0.6f, dockH);

                    // 3. 科技晶体 (冰蓝、霓虹紫、亮青)
                    using var b1 = new SolidBrush(System.Drawing.Color.FromArgb(255, 6, 182, 212));
                    using var b2 = new SolidBrush(System.Drawing.Color.FromArgb(255, 139, 92, 246));
                    using var b3 = new SolidBrush(System.Drawing.Color.FromArgb(255, 56, 189, 248));

                    g.FillRectangle(b1, sz * 0.28f, sz * 0.38f, sz * 0.12f, sz * 0.17f);
                    g.FillRectangle(b2, sz * 0.44f, sz * 0.32f, sz * 0.12f, sz * 0.23f);
                    g.FillRectangle(b3, sz * 0.60f, sz * 0.38f, sz * 0.12f, sz * 0.17f);
                }

                using var pngStream = new MemoryStream();
                bmp.Save(pngStream, ImageFormat.Png);
                imageBuffers[i] = pngStream.ToArray();
            }

            int offset = 6 + (16 * sizes.Length);

            // Write Directory Entries
            for (int i = 0; i < sizes.Length; i++)
            {
                int sz = sizes[i];
                bw.Write((byte)(sz >= 256 ? 0 : sz)); // Width
                bw.Write((byte)(sz >= 256 ? 0 : sz)); // Height
                bw.Write((byte)0); // Color count
                bw.Write((byte)0); // Reserved
                bw.Write((short)1); // Color planes
                bw.Write((short)32); // Bits per pixel
                bw.Write((int)imageBuffers[i].Length); // Size of image data
                bw.Write((int)offset); // Offset of image data
                offset += imageBuffers[i].Length;
            }

            // Write Image Data
            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write(imageBuffers[i]);
            }

            File.WriteAllBytes(targetIcoPath, ms.ToArray());
        }
    }
}
