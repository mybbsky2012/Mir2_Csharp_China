using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using LibraryEditor.Formats;
using ManagedSquish;
using ZstdSharp;

namespace LibraryEditor
{
    /// <summary>
    /// MLX（Artificial Mir 引擎 "MLX" 容器）资源库，通常以 .Lib / .MLX 扩展名出现。
    /// 像素两种编码：Ver=4 DXT1(BC1)（98% 以上帧）、Ver=1 16bpp RGB565（黑=透明，盛大 WIL 转制帧）；
    /// 负载可再经 zstd 压缩（带字典库见 DICT 块，帧 Reserved 高 16 位 = 字典序号）。
    /// 位图按需解码（浏览/选中才解），编辑产生的新帧统一重编码为 DXT1 + zstd。
    /// 「导出为标准 Lib」把全部帧解码转成 Mir2 Library V3（BGRA + gzip）。
    /// </summary>
    public class MlxLibrary : ILibrary
    {
        private string _fileName;
        protected MlxContainer _container;
        private List<MLibraryV2.MImage> _images = new List<MLibraryV2.MImage>();
        private FrameSet _frames = new FrameSet();

        public List<MLibraryV2.MImage> Images { get { return _images; } }

        /// <summary>底层容器（只读访问，供外部诊断/统计帧编码分布）。</summary>
        public MlxContainer Container { get { return _container; } }

        public FrameSet Frames
        {
            get { return _frames; }
            set { _frames = value ?? new FrameSet(); }
        }

        public int Count { get { return _images == null ? 0 : _images.Count; } }

        public string FileName
        {
            get { return _fileName; }
            set { _fileName = value; }
        }

        public MlxLibrary(string filename)
        {
            _fileName = filename;
            Initialize();
        }

        /// <summary>工厂已解析好容器时用这个构造，避免重复解析整库。</summary>
        public MlxLibrary(MlxContainer container, string filename)
        {
            _fileName = filename;
            _container = container;
            BuildImages();
        }

        private void Initialize()
        {
            if (!string.IsNullOrEmpty(_fileName) && File.Exists(_fileName))
                _container = MlxContainer.Parse(File.ReadAllBytes(_fileName));
            else
            {
                string name = string.IsNullOrEmpty(_fileName) ? "mlx" : Path.GetFileNameWithoutExtension(_fileName);
                _container = MlxContainer.Parse(MlxContainer.BuildNewFile(name));
            }

            BuildImages();
        }

        private void BuildImages()
        {
            _images = new List<MLibraryV2.MImage>();
            for (int i = 0; i < _container.Frames.Count; i++)
                _images.Add(FromFrame(_container.Frames[i]));
        }

        private static MLibraryV2.MImage FromFrame(MlxFrame f)
        {
            byte[] stored = f.Stored ?? Array.Empty<byte>();
            // FBytes 持有原始存储字节引用：未改动帧保存时原样回写
            return new MLibraryV2.MImage(stored, f.Width, f.Height)
            {
                X = f.OffsetX,
                Y = f.OffsetY,
                ShadowX = f.ShadowX,
                ShadowY = f.ShadowY,
                Shadow = f.Shadow,
                Length = stored.Length,
                HasMask = false,
            };
        }

        // ---------------- 按需解码 ----------------

        public void EnsureTexture(int index)
        {
            if (_images == null || index < 0 || index >= _images.Count) return;

            MLibraryV2.MImage mi = _images[index];
            if (mi == null || mi.TextureValid || mi.Image != null) return;
            if (!MLibraryV2.Load) return;

            try
            {
                MlxFrame f = _container.Frames[index];
                DecodeInto(mi, f);
                mi.TextureValid = true;
            }
            catch
            {
                // 单帧损坏不应导致整库无法浏览，预览显示占位图
                mi.Image = null;
                mi.Preview = null;
            }
        }

        /// <summary>负载解包：zstd（按帧字典）/ 原始。</summary>
        protected byte[] DecodeRaw(MlxFrame f)
        {
            byte[] stored = f.Stored ?? Array.Empty<byte>();
            if (stored.Length == 0) return Array.Empty<byte>();
            if ((f.Flags & 0x0100) == 0) return stored;

            Decompressor d = new Decompressor();
            if (f.DictIndex > 0)
            {
                byte[] dict;
                if (_container.Dicts.TryGetValue(f.DictIndex, out dict) && dict != null)
                    d.LoadDictionary(dict);
            }
            int maxSize = f.RawSize > 0 ? (int)Math.Min(f.RawSize, int.MaxValue) : stored.Length * 40;
            return d.Unwrap(stored, maxSize).ToArray();
        }

        /// <summary>
        /// DXT1 解压。必须走 IntPtr 重载：ManagedSquish 的 byte[] 重载在本环境会写坏托管堆
        /// （实测 CompressImage(byte[]) 之后枚举任意集合即 AccessViolation），
        /// 本仓库其它库（MLibraryV0/V1、WtlDirectLibrary）也都只用 IntPtr 版本。
        /// </summary>
        private static unsafe byte[] SquishDecompress(byte[] blocks, int w, int h)
        {
            byte[] rgba = new byte[w * h * 4];
            fixed (byte* ps = blocks)
            fixed (byte* pd = rgba)
            {
                Squish.DecompressImage((IntPtr)pd, w, h, (IntPtr)ps, SquishFlags.Dxt1);
            }
            return rgba;
        }

        private static unsafe byte[] SquishCompress(byte[] rgba, int w, int h)
        {
            int count = Squish.GetStorageRequirements(w, h, SquishFlags.Dxt1);
            byte[] dxt = new byte[count];
            fixed (byte* ps = rgba)
            fixed (byte* pd = dxt)
            {
                Squish.CompressImage((IntPtr)ps, w, h, (IntPtr)pd, SquishFlags.Dxt1);
            }
            return dxt;
        }

        private void DecodeInto(MLibraryV2.MImage mi, MlxFrame f)
        {
            int w = f.Width, h = f.Height;
            byte[] bgra = DecodeToBgra(f);
            if (bgra == null) return;

            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(bgra, 0, bd.Scan0, Math.Min(bgra.Length, bd.Stride * h));
            bmp.UnlockBits(bd);
            mi.Image = bmp;
        }

        /// <summary>把一帧解成 BGRA 字节流（DXT1 / RGB565 / BGRA32 三种编码自动判定）；无法解码返回 null。
        /// 子类（加密lib）可重写以支持扩展帧编码（如 ver=8 调色板帧）。</summary>
        protected virtual byte[] DecodeToBgra(MlxFrame f)
        {
            int w = f.Width, h = f.Height;
            if (w <= 0 || h <= 0) return null;

            byte[] raw = DecodeRaw(f);
            byte[] bgra = new byte[w * h * 4];

            int ver = f.Ver;
            if (ver != 1 && ver != 2 && ver != 4)
            {
                if (raw.Length == w * h * 4) ver = 2;
                else if (raw.Length == w * h * 2) ver = 1;
                else ver = 4;
            }

            if (ver == 2)
            {
                Buffer.BlockCopy(raw, 0, bgra, 0, Math.Min(raw.Length, bgra.Length));
            }
            else if (ver == 1)
            {
                int pixels = w * h;
                if (raw.Length < pixels * 2)
                {
                    byte[] pad = new byte[pixels * 2];
                    Buffer.BlockCopy(raw, 0, pad, 0, raw.Length);
                    raw = pad;
                }
                for (int i = 0, p = 0; i < pixels; i++, p += 2)
                {
                    ushort v = (ushort)(raw[p] | (raw[p + 1] << 8));
                    bgra[i * 4 + 0] = (byte)(((v & 0x1F) * 255 + 15) / 31);
                    bgra[i * 4 + 1] = (byte)((((v >> 5) & 0x3F) * 255 + 31) / 63);
                    bgra[i * 4 + 2] = (byte)((((v >> 11) & 0x1F) * 255 + 15) / 31);
                    bgra[i * 4 + 3] = v == 0 ? (byte)0 : (byte)255;
                }
            }
            else
            {
                int needLen = ((w + 3) / 4) * ((h + 3) / 4) * 8;
                if (raw.Length < needLen)
                {
                    byte[] pad = new byte[needLen];
                    Buffer.BlockCopy(raw, 0, pad, 0, raw.Length);
                    raw = pad;
                }
                byte[] rgba = SquishDecompress(raw, w, h);
                int n = Math.Min(rgba.Length, bgra.Length);
                for (int i = 0; i + 3 < n; i += 4)
                {
                    bgra[i] = rgba[i + 2];
                    bgra[i + 1] = rgba[i + 1];
                    bgra[i + 2] = rgba[i];
                    bgra[i + 3] = rgba[i + 3];
                }
            }

            return bgra;
        }

        // ---------------- 编辑 ----------------

        private sealed class EncodedFrame
        {
            public MLibraryV2.MImage Image;
            public MlxFrame Frame;
        }

        /// <summary>把位图编码为 MLX 帧（DXT1 + zstd；DXT 后尺寸不小于压缩前的则不压缩）。</summary>
        private EncodedFrame EncodeBitmap(Bitmap image, short x, short y, bool removeBlack,
                                          short shadowX, short shadowY, byte shadow, uint reservedTemplate)
        {
            if (image == null)
                throw new ArgumentNullException("image");

            // DXT 按 4x4 块存储：补齐到 4 的倍数（透明填充），与原库所有帧一致
            int pw = image.Width + ((4 - image.Width % 4) % 4);
            int ph = image.Height + ((4 - image.Height % 4) % 4);

            Bitmap src = image;
            Bitmap padded = null;
            if (pw != image.Width || ph != image.Height)
            {
                padded = new Bitmap(pw, ph, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(padded))
                {
                    g.Clear(Color.Transparent);
                    g.DrawImage(image, 0, 0);
                    g.Save();
                }
                src = padded;
            }

            byte[] rgba;
            BitmapData data = src.LockBits(new Rectangle(0, 0, pw, ph), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] bgra = new byte[pw * ph * 4];
                Marshal.Copy(data.Scan0, bgra, 0, bgra.Length);
                rgba = new byte[bgra.Length];
                for (int i = 0; i < bgra.Length; i += 4)
                {
                    rgba[i] = bgra[i + 2];       // R
                    rgba[i + 1] = bgra[i + 1];   // G
                    rgba[i + 2] = bgra[i];       // B
                    rgba[i + 3] = bgra[i + 3];   // A
                    if (removeBlack && bgra[i] == 0 && bgra[i + 1] == 0 && bgra[i + 2] == 0)
                        rgba[i + 3] = 0;
                }
            }
            finally
            {
                src.UnlockBits(data);
                if (padded != null) padded.Dispose();
            }

            byte[] dxt = SquishCompress(rgba, pw, ph);

            // 字典库沿用首个字典
            byte[] dict = null;
            int dictIdx = 0;
            foreach (var kv in _container.Dicts)
            {
                dictIdx = kv.Key;
                dict = kv.Value;
                break;
            }

            byte[] stored = dxt;
            ushort flags = 0;
            if (dxt.Length > 32)
            {
                Compressor c = new Compressor(3);
                if (dict != null) c.LoadDictionary(dict);
                byte[] z = c.Wrap(dxt).ToArray();
                if (z.Length < dxt.Length)
                {
                    stored = z;
                    flags |= 0x0100;
                }
            }

            MlxFrame frame = new MlxFrame
            {
                Width = (short)pw,
                Height = (short)ph,
                OffsetX = x,
                OffsetY = y,
                ShadowX = shadowX,
                ShadowY = shadowY,
                Shadow = shadow,
                Ver = 4,
                Flags = flags,
                Reserved = dictIdx > 0 ? ((uint)dictIdx << 16) : reservedTemplate,
                RawSize = (uint)dxt.Length,
                Stored = stored,
                Modified = true,
            };

            MLibraryV2.MImage mi = new MLibraryV2.MImage(stored, (short)pw, (short)ph)
            {
                X = x,
                Y = y,
                ShadowX = shadowX,
                ShadowY = shadowY,
                Shadow = shadow,
                Length = stored.Length,
                HasMask = false,
            };

            return new EncodedFrame { Image = mi, Frame = frame };
        }

        private static void DisposeImage(MLibraryV2.MImage mi)
        {
            if (mi == null) return;
            if (mi.Preview != null) { mi.Preview.Dispose(); mi.Preview = null; }
            if (mi.Image != null) { mi.Image.Dispose(); mi.Image = null; }
            if (mi.MaskImage != null) { mi.MaskImage.Dispose(); mi.MaskImage = null; }
            mi.TextureValid = false;
        }

        public void AddImage(Bitmap image, short x, short y, bool removeBlack = true)
        {
            EncodedFrame ef = EncodeBitmap(image, x, y, removeBlack, 0, 0, 0, 8);
            _container.Frames.Add(ef.Frame);
            _images.Add(ef.Image);
        }

        public void AddImage(Bitmap image, Bitmap MaskImage, short x, short y, bool removeBlack = true)
        {
            AddImage(image, x, y, removeBlack);   // MLX 无遮罩层，忽略 MaskImage
        }

        public void InsertImage(int index, Bitmap image, short x, short y, bool removeBlack = true)
        {
            if (index < 0) index = 0;
            if (index > _images.Count) index = _images.Count;
            EncodedFrame ef = EncodeBitmap(image, x, y, removeBlack, 0, 0, 0, 8);
            _container.Frames.Insert(index, ef.Frame);
            _images.Insert(index, ef.Image);
        }

        public void ReplaceImage(int index, Bitmap image, short xOffset, short yOffset, bool removeBlack = true)
        {
            if (_images == null || index < 0 || index >= _images.Count) return;

            MLibraryV2.MImage old = _images[index];
            MlxFrame oldFrame = _container.Frames[index];
            EncodedFrame ef = EncodeBitmap(image, xOffset, yOffset, removeBlack,
                old != null ? old.ShadowX : (short)0,
                old != null ? old.ShadowY : (short)0,
                old != null ? old.Shadow : (byte)0,
                oldFrame != null ? oldFrame.Reserved : 8u);

            DisposeImage(old);
            _images[index] = ef.Image;
            _container.Frames[index] = ef.Frame;
        }

        public void RemoveImage(int index)
        {
            if (_images == null) return;

            if (_images.Count <= 1)
            {
                for (int i = 0; i < _images.Count; i++) DisposeImage(_images[i]);
                _images.Clear();
                _container.Frames.Clear();
                return;
            }

            if (index < 0 || index >= _images.Count) return;
            DisposeImage(_images[index]);
            _images.RemoveAt(index);
            _container.Frames.RemoveAt(index);
        }

        public void RemoveBlanks(bool safe = false)
        {
            if (_images == null) return;

            for (int i = _images.Count - 1; i >= 0; i--)
            {
                MLibraryV2.MImage mi = _images[i];
                if (mi == null) continue;

                if (mi.FBytes == null || mi.FBytes.Length <= 24)
                {
                    if (!safe)
                        RemoveImage(i);
                    else if (mi.X == 0 && mi.Y == 0)
                        RemoveImage(i);
                }
            }
        }

        // ---------------- 取帧 ----------------

        public MLibraryV2.MImage GetMImage(int index)
        {
            if (_images == null || index < 0 || index >= _images.Count) return null;
            EnsureTexture(index);
            return _images[index];
        }

        public Bitmap GetPreview(int index)
        {
            if (_images == null || index < 0 || index >= _images.Count) return new Bitmap(1, 1);

            EnsureTexture(index);

            MLibraryV2.MImage image = _images[index];
            if (image == null || image.Image == null) return new Bitmap(1, 1);

            if (image.Preview == null) image.CreatePreview();
            return image.Preview;
        }

        public Point GetOffSet(int index)
        {
            if (_images == null || index < 0 || index >= _images.Count) return Point.Empty;
            return new Point(_images[index].X, _images[index].Y);
        }

        public Size GetSize(int index)
        {
            if (_images == null || index < 0 || index >= _images.Count) return Size.Empty;
            return new Size(_images[index].Width, _images[index].Height);
        }

        // ---------------- 保存 ----------------

        public void Save()
        {
            if (string.IsNullOrEmpty(_fileName)) return;

            SyncToContainer();
            byte[] outb = _container.Rebuild();
            File.WriteAllBytes(_fileName, outb);
            _container.MarkSaved(outb);   // 之后未再修改时保存输出字节级稳定
        }

        /// <summary>把 MImage 的头字段同步回容器帧；字段变化时置 Modified。</summary>
        private void SyncToContainer()
        {
            List<MlxFrame> frames = _container.Frames;
            while (frames.Count > _images.Count)
                frames.RemoveAt(frames.Count - 1);
            while (frames.Count < _images.Count)
                frames.Add(new MlxFrame());

            for (int i = 0; i < _images.Count; i++)
            {
                MLibraryV2.MImage mi = _images[i];
                if (mi == null) continue;

                MlxFrame f = frames[i];
                if (f.Width != mi.Width || f.Height != mi.Height ||
                    f.OffsetX != mi.X || f.OffsetY != mi.Y ||
                    f.ShadowX != mi.ShadowX || f.ShadowY != mi.ShadowY || f.Shadow != mi.Shadow)
                {
                    f.Width = mi.Width;
                    f.Height = mi.Height;
                    f.OffsetX = mi.X;
                    f.OffsetY = mi.Y;
                    f.ShadowX = mi.ShadowX;
                    f.ShadowY = mi.ShadowY;
                    f.Shadow = mi.Shadow;
                    f.Modified = true;
                }
            }
        }

        public void Close()
        {
            if (_images == null) return;
            for (int i = 0; i < _images.Count; i++)
                DisposeImage(_images[i]);
        }

        // ---------------- 导出标准 .Lib（Mir2 Library V3） ----------------

        /// <summary>把全部帧解码为 BGRA + gzip，按 MLibraryV2 的格式写出独立 .Lib 文件。</summary>
        public void ExportToStandardLib(string path, Action<int, int> onProgress = null)
        {
            int count = _images.Count;
            string tmp = path + ".tmp";

            long[] blobOffsets = new long[count];

            using (FileStream blobFs = File.Create(tmp))
            using (BinaryWriter bw = new BinaryWriter(blobFs))
            {
                for (int i = 0; i < count; i++)
                {
                    MlxFrame f = _container.Frames[i];
                    int w = f.Width, h = f.Height;

                    blobOffsets[i] = blobFs.Position;

                    // 帧头：6 个 int16 + 1 字节标志 + int32 长度（与 MImage.Save 完全一致）
                    bw.Write((short)w);
                    bw.Write((short)h);
                    bw.Write(f.OffsetX);
                    bw.Write(f.OffsetY);
                    bw.Write(f.ShadowX);
                    bw.Write(f.ShadowY);
                    bw.Write(f.Shadow);           // HasMask 恒为 false，不置 bit7
                    bw.Write(0);                  // 长度占位

                    if (w > 0 && h > 0)
                    {
                        byte[] bgra = DecodeToBgra(f);
                        if (bgra == null) continue;

                        byte[] gz = GzipCompress(bgra);
                        bw.Seek(-4, SeekOrigin.Current);
                        bw.Write(gz.Length);
                        bw.Write(gz);
                    }

                    if (onProgress != null && (i & 0x3F) == 0)
                        onProgress(i, count);
                }
            }

            // blob 总长 = 临时文件长度
            long totalBlob = new FileInfo(tmp).Length;

            using (FileStream fs = File.Create(path))
            using (BinaryWriter w = new BinaryWriter(fs))
            using (FileStream blobFs = File.OpenRead(tmp))
            {
                long frameSeek = 12 + 4L * count + totalBlob;

                w.Write(MLibraryV2.LibVersion);
                w.Write(count);
                w.Write((int)frameSeek);
                for (int i = 0; i < count; i++)
                    w.Write((int)(12 + 4L * count + blobOffsets[i]));
                // blobs
                byte[] buffer = new byte[1 << 20];
                int read;
                while ((read = blobFs.Read(buffer, 0, buffer.Length)) > 0)
                    w.Write(buffer, 0, read);
                // 动作帧段：空
                w.Write(0);
                w.Flush();
            }

            File.Delete(tmp);

            if (onProgress != null) onProgress(count, count);
        }

        private static byte[] GzipCompress(byte[] raw)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream gz = new GZipStream(ms, CompressionLevel.Optimal, true))
                {
                    gz.Write(raw, 0, raw.Length);
                }
                return ms.ToArray();
            }
        }
    }
}
