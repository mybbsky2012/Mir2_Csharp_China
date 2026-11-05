using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ManagedSquish;

namespace LibraryEditor
{
    /// <summary>
    /// V2 Library
    /// Uses DXT5 Images
    /// Supports Frames (When at LibVersion 3)
    /// </summary>
    public sealed class MLibraryV2 : ILibrary
    {
        public const int LibVersion = 3;

        public static bool Load = true;
        public string FileName { get; set; }

        public List<MImage> Images { get; set; } = new List<MImage>();
        public FrameSet Frames { get; set; } = new FrameSet();

        public List<int> IndexList = new List<int>();
        public int Count { get; set; }
        private bool _initialized;

        private BinaryReader _reader;
        private FileStream _stream;
        /// <summary>
        /// 1MB 顺序缓冲。帧在库里是按索引顺序连续存放的，加上缓冲后
        /// 逐帧读取不再落到真实的 read/seek 系统调用上——17 万帧的库
        /// 原本要发 30 多万次系统调用，现在只剩每个缓冲块一次。
        /// </summary>
        private BufferedStream _buffered;

        public MLibraryV2(string filename)
        {
            FileName = filename;
            Initialize();
            Close();
        }

        public void Initialize()
        {
            int CurrentVersion;
            _initialized = true;

            if (!File.Exists(FileName))
                return;

            _stream = new FileStream(FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            _buffered = new BufferedStream(_stream, 1 << 20);
            _reader = new BinaryReader(_buffered);
            CurrentVersion = _reader.ReadInt32();

            // 版本 1（Artificial Mir Monster 等，帧负载为 DXT1）与 2/3 同构，直接支持；
            // 仅低于 1 的才拒绝。探测 DecodePayload 会自动识别 DXT1/gzip/BGRA/565。
            if (CurrentVersion < 1)
            {
                MessageBox.Show("版本错误, 要求LIB版本为: " + LibVersion.ToString() + " 基于版本: " + CurrentVersion.ToString() + ".", "打开失败", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                return;
            }

            Count = _reader.ReadInt32();

            // 预分配容量：上万帧的库靠 List 自动扩容会反复搬移大数组
            Images = new List<MImage>(Count > 0 ? Count : 0);
            IndexList = new List<int>(Count > 0 ? Count : 0);

            int frameSeek = 0;
            if (CurrentVersion >= 3)
            {
                // 8 字节空库（ver=3、count=0）没有 frameSeek 字段，按无动作段处理
                if (_stream.Length >= 12)
                    frameSeek = _reader.ReadInt32();
            }

            for (int i = 0; i < Count; i++)
                IndexList.Add(_reader.ReadInt32());

            for (int i = 0; i < Count; i++)
                Images.Add(null);

            for (int i = 0; i < Count; i++)
                CheckImage(i, false);   // 只读帧头；位图改为按需解码（大库打开不再卡住）

            if (CurrentVersion >= 3)
            {
                // 动作帧段：部分引擎（Artificial Mir 原版库）写的 frameSeek 是脏值或根本没有
                // 动作段。先整段校验（计数合理、每条记录的动作枚举合法），校验不过就当没有，
                // 不能因为动作段损坏而打不开整个库。
                try
                {
                    _buffered.Position = frameSeek;

                    var frameCount = _reader.ReadInt32();
                    long sectEnd = frameSeek + 4L + (long)frameCount * 35L;

                    if (frameCount > 0 && frameCount <= 256 && sectEnd <= _stream.Length)
                    {
                        var entries = new List<KeyValuePair<MirAction, Frame>>(frameCount);
                        bool valid = true;

                        for (int i = 0; i < frameCount; i++)
                        {
                            int act = _reader.ReadByte();
                            Frame fr = new Frame(_reader);

                            if (act > 43 ||
                                fr.Start < 0 || fr.Count < 0 || fr.Skip < 0 || fr.Interval < 0 ||
                                fr.EffectStart < 0 || fr.EffectCount < 0 || fr.EffectSkip < 0 || fr.EffectInterval < 0)
                            {
                                valid = false;
                                break;
                            }
                            entries.Add(new KeyValuePair<MirAction, Frame>((MirAction)act, fr));
                        }

                        if (valid)
                            foreach (var kv in entries)
                                Frames[kv.Key] = kv.Value;   // 重复动作覆盖，不抛异常
                    }
                }
                catch
                {
                    Frames.Clear();
                }
            }
        }

        public void Close()
        {
            // BinaryReader.Dispose 会连带释放 BufferedStream 与底层 FileStream
            if (_reader != null)
            {
                try { _reader.Dispose(); }
                catch { }
                _reader = null;
            }

            _buffered = null;

            if (_stream != null)
            {
                try { _stream.Dispose(); }
                catch { }
                _stream = null;
            }
        }

        public void Save()
        {
            Close();

            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);

            Count = Images.Count;
            IndexList.Clear();

            int offSet = (4 + 4 + 4) + (Count * 4);
            for (int i = 0; i < Count; i++)
            {
                IndexList.Add((int)stream.Length + offSet);
                Images[i].Save(writer);
            }

            var frameSeek = (int)stream.Length + offSet;

            writer.Flush();
            byte[] fBytes = stream.ToArray();

            _stream = File.Create(FileName);
            writer = new BinaryWriter(_stream);

            writer.Write(LibVersion);

            writer.Write(Count);

            writer.Write(frameSeek);

            for (int i = 0; i < Count; i++)
                writer.Write(IndexList[i]);

            writer.Write(fBytes);

            writer.Write(Frames.Keys.Count);
            foreach (var action in Frames.Keys)
            {
                writer.Write((byte)action);
                Frames[action].Save(writer);
            }

            writer.Flush();
            writer.Close();
            writer.Dispose();
            Close();
        }

        private void CheckImage(int index)
        {
            CheckImage(index, true);
        }

        /// <summary>
        /// ensureTexture=false 时只读帧头，不解码位图。
        /// 打开大库（上万帧）时若一次性解码会耗尽 GDI 句柄与内存，故改为按需解码。
        /// </summary>
        private void CheckImage(int index, bool ensureTexture)
        {
            if (!_initialized)
                Initialize();

            if (Images == null || index < 0 || index >= Images.Count)
                return;

            if (Images[index] == null)
            {
                // 帧头尚未读入。注意：MLibraryV2 构造函数末尾会 Close() 关流，
                // 此时只能靠已缓存的数据，不能再从文件读。
                if (_reader == null) return;

                long off = IndexList[index];

                // 部分引擎（Artificial Mir 原版库）索引表最后一格是垃圾值/越界：按空帧处理
                if (off < 0 || off + 17 > _stream.Length)
                {
                    Images[index] = new MLibraryV2.MImage(new byte[0], 0, 0);
                    return;
                }

                try
                {
                    // 顺序推进：帧在文件中按索引顺序连续存放，只有位置对不上时才真的 seek
                    if (_buffered.Position != off)
                        _buffered.Position = off;

                    Images[index] = new MImage(_reader);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (EndOfStreamException)
                {
                    // 帧头/负载超出文件尾：按空帧处理，不影响其它帧
                    Images[index] = new MLibraryV2.MImage(new byte[0], 0, 0);
                }
            }

            if (!ensureTexture) return;
            if (!Load) return;

            MImage mi = Images[index];

            if (mi.TextureValid || mi.Image != null) return;
            if (mi.FBytes == null || mi.FBytes.Length == 0) return;

            // 负载（FBytes）在读取帧头时已进内存，解码不再需要访问文件流
            mi.CreateTexture(_reader);
            mi.TextureValid = true;
        }

        /// <summary>最近一次按需解码失败的原因（诊断用）。</summary>
        public string LastEnsureError { get; private set; }

        /// <summary>按需解码指定帧的位图（浏览/选中时才解码，用完即驻留缓存）。</summary>
        public void EnsureTexture(int index)
        {
            if (Images == null || index < 0 || index >= Images.Count) return;

            MImage mi = Images[index];
            if (mi != null && (mi.TextureValid || mi.Image != null)) return;

            try
            {
                CheckImage(index, true);
            }
            catch (Exception ex)
            {
                // 单帧损坏不应影响其它帧浏览
                LastEnsureError = ex.GetType().Name + ": " + ex.Message + " (帧 " + index + ")";
                if (Images[index] != null)
                {
                    Images[index].Image = null;
                    Images[index].Preview = null;
                }
            }

            if (Images[index] != null && Images[index].Image == null && LastEnsureError == null)
                LastEnsureError = "解码后仍未生成位图 (帧 " + index + ", FBytes=" +
                                  (Images[index].FBytes == null ? -1 : Images[index].FBytes.Length) +
                                  ", Load=" + Load + ")";
        }

        public Point GetOffSet(int index)
        {
            if (!_initialized)
                Initialize();

            if (Images == null || index < 0 || index >= Images.Count)
                return Point.Empty;

            if (Images[index] == null)
            {
                CheckImage(index, false);
                if (Images[index] == null) return Point.Empty;
            }

            return new Point(Images[index].X, Images[index].Y);
        }

        public Size GetSize(int index)
        {
            if (!_initialized)
                Initialize();
            if (Images == null || index < 0 || index >= Images.Count)
                return Size.Empty;

            if (Images[index] == null)
            {
                CheckImage(index, false);
                if (Images[index] == null) return Size.Empty;
            }

            return new Size(Images[index].Width, Images[index].Height);
        }

        public MImage GetMImage(int index)
        {
            if (Images == null || index < 0 || index >= Images.Count)
                return null;

            EnsureTexture(index);
            return Images[index];
        }

        public Bitmap GetPreview(int index)
        {
            if (Images == null || index < 0 || index >= Images.Count)
                return new Bitmap(1, 1);

            EnsureTexture(index);

            MImage image = Images[index];

            if (image == null || image.Image == null)
                return new Bitmap(1, 1);

            if (image.Preview == null)
                image.CreatePreview();

            return image.Preview;
        }

        public void AddImage(Bitmap image, short x, short y, bool removeBlack = true)
        {
            MImage mImage = new MImage(image, removeBlack) { X = x, Y = y };

            Count++;
            Images.Add(mImage);
        }

        public void AddImage(Bitmap image, Bitmap maskImage, short x, short y, bool removeBlack = true)
        {
            MImage mImage = new MImage(image, maskImage, removeBlack) { X = x, Y = y };

            Count++;
            Images.Add(mImage);
        }

        public void ReplaceImage(int Index, Bitmap image, short x, short y, bool removeBlack = true)
        {
            MImage mImage = new MImage(image, removeBlack) { X = x, Y = y };

            Images[Index] = mImage;
        }

        public void InsertImage(int index, Bitmap image, short x, short y, bool removeBlack = true)
        {
            MImage mImage = new MImage(image, removeBlack) { X = x, Y = y };

            Count++;
            Images.Insert(index, mImage);
        }

        public void RemoveImage(int index)
        {
            if (Images == null || Count <= 1)
            {
                Count = 0;
                Images = new List<MImage>();
                return;
            }
            Count--;

            Images.RemoveAt(index);
        }

        public static bool CompareBytes(byte[] a, byte[] b)
        {
            if (a == b) return true;

            if (a == null || b == null || a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;

            return true;
        }

        public void RemoveBlanks(bool safe = false)
        {
            for (int i = Count - 1; i >= 0; i--)
            {
                if (Images[i].FBytes == null || Images[i].FBytes.Length <= 24)
                {
                    if (!safe)
                        RemoveImage(i);
                    else if (Images[i].X == 0 && Images[i].Y == 0)
                        RemoveImage(i);
                }
            }
        }

        public sealed class MImage
        {
            public short Width, Height, X, Y, ShadowX, ShadowY;
            public byte Shadow;
            public int Length;
            public byte[] FBytes;
            public bool TextureValid;
            public Bitmap Image, Preview;

            //layer 2:
            public short MaskWidth, MaskHeight, MaskX, MaskY;

            public int MaskLength;
            public byte[] MaskFBytes;
            public Bitmap MaskImage;
            public Boolean HasMask;

            /// <summary>解码时跳过的字节数：ALib 的解压负载带 10 字节常量前缀，Elite/.Lib 为 0。</summary>
            public int PayloadSkip;

            /// <summary>编码时前置到像素数据前的常量前缀（ALib 用 ALibContainer.PayloadPrefix，其它为 null）。</summary>
            public byte[] PayloadPrefix;

            public MImage(BinaryReader reader) : this(reader, true) { }

            /// <summary>
            /// allowMask = false 时忽略 Shadow 的 bit7（Elite 系格式该字节是普通保留数据，不是 mask 标记）。
            /// </summary>
            public MImage(BinaryReader reader, bool allowMask)
            {
                //read layer 1
                Width = reader.ReadInt16();
                Height = reader.ReadInt16();
                X = reader.ReadInt16();
                Y = reader.ReadInt16();
                ShadowX = reader.ReadInt16();
                ShadowY = reader.ReadInt16();
                Shadow = reader.ReadByte();

                // 哨兵空帧（如 Prguse.Lib 末帧 w=h=-1，长度字段为垃圾值）：直接按空帧处理，
                // 每帧都按 IndexList 独立寻址，流位置不必对齐
                if (Width < 0 || Height < 0)
                {
                    Length = 0;
                    FBytes = new byte[0];
                    return;
                }

                Length = reader.ReadInt32();
                if (Length < 0)
                {
                    Length = 0;
                    FBytes = new byte[0];
                    return;
                }
                FBytes = reader.ReadBytes(Length);
                //check if there's a second layer and read it
                HasMask = allowMask && ((Shadow >> 7) == 1) ? true : false;
                if (HasMask)
                {
                    MaskWidth = reader.ReadInt16();
                    MaskHeight = reader.ReadInt16();
                    MaskX = reader.ReadInt16();
                    MaskY = reader.ReadInt16();
                    MaskLength = reader.ReadInt32();
                    MaskFBytes = reader.ReadBytes(MaskLength);
                }
            }

            public MImage(byte[] image, short Width, short Height)//only use this when converting from old to new type!
            {
                FBytes = image;
                this.Width = Width;
                this.Height = Height;
            }

            public MImage(Bitmap image, bool removeBlack = true, int payloadSkip = 0, byte[] payloadPrefix = null)
            {
                PayloadSkip = payloadSkip;
                PayloadPrefix = payloadPrefix;

                if (image == null)
                {
                    FBytes = new byte[0];
                    return;
                }

                Width = (short)image.Width;
                Height = (short)image.Height;

                Image = image;// FixImageSize(image);
                FBytes = ConvertBitmapToArray(Image, removeBlack);
            }

            public MImage(Bitmap image, Bitmap Maskimage, bool removeBlack = true, int payloadSkip = 0, byte[] payloadPrefix = null)
            {
                PayloadSkip = payloadSkip;
                PayloadPrefix = payloadPrefix;

                if (image == null)
                {
                    FBytes = new byte[0];
                    return;
                }

                Width = (short)image.Width;
                Height = (short)image.Height;
                Image = image;// FixImageSize(image);
                FBytes = ConvertBitmapToArray(Image, removeBlack);
                if (Maskimage == null)
                {
                    MaskFBytes = new byte[0];
                    return;
                }
                HasMask = true;
                MaskWidth = (short)Maskimage.Width;
                MaskHeight = (short)Maskimage.Height;
                MaskImage = Maskimage;// FixImageSize(Maskimage);
                MaskFBytes = ConvertBitmapToArray(MaskImage, removeBlack);
            }

            private Bitmap FixImageSize(Bitmap input)
            {
                int w = input.Width + (4 - input.Width % 4) % 4;
                int h = input.Height + (4 - input.Height % 4) % 4;

                if (input.Width != w || input.Height != h)
                {
                    Bitmap temp = new Bitmap(w, h);
                    using (Graphics g = Graphics.FromImage(temp))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.DrawImage(input, 0, 0);
                        g.Save();
                    }
                    input.Dispose();
                    input = temp;
                }

                return input;
            }

            private unsafe byte[] ConvertBitmapToArray(Bitmap input, bool removeBlack = true)
            {
                BitmapData data = input.LockBits(new Rectangle(0, 0, input.Width, input.Height), ImageLockMode.ReadOnly,
                                                 PixelFormat.Format32bppArgb);

                byte[] pixels = new byte[input.Width * input.Height * 4];

                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

                input.UnlockBits(data);

                if (removeBlack)
                {
                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        if (pixels[i] == 0 && pixels[i + 1] == 0 && pixels[i + 2] == 0)
                            pixels[i + 3] = 0; //Make Transparent
                    }
                }

                //ALib 等格式需要在像素数据前前置常量前缀（Elite/.Lib 为 null，行为与改前完全一致）
                if (PayloadPrefix != null && PayloadPrefix.Length > 0)
                {
                    byte[] prefixed = new byte[PayloadPrefix.Length + pixels.Length];
                    Buffer.BlockCopy(PayloadPrefix, 0, prefixed, 0, PayloadPrefix.Length);
                    Buffer.BlockCopy(pixels, 0, prefixed, PayloadPrefix.Length, pixels.Length);
                    return Compress(prefixed);
                }

                byte[] compressedBytes;
                compressedBytes = Compress(pixels);

                return compressedBytes;
            }

            public unsafe void CreateTexture(BinaryReader reader)
            {
                int w = Width;// +(4 - Width % 4) % 4;
                int h = Height;// +(4 - Height % 4) % 4;

                if (w < 2 || h < 2) return;

                Image = new Bitmap(w, h);

                BitmapData data = Image.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite,
                                                 PixelFormat.Format32bppArgb);

                byte[] dest = DecodePayload(FBytes, w, h);

                //跳过格式前缀（ALib=10，Elite/.Lib=0），并防止 dest 短于位图缓冲时越界
                int copyLength = dest.Length - PayloadSkip;
                if (copyLength > w * h * 4) copyLength = w * h * 4;
                if (copyLength > 0)
                    Marshal.Copy(dest, PayloadSkip, data.Scan0, copyLength);

                Image.UnlockBits(data);

                dest = null;

                if (HasMask)
                {
                    w = MaskWidth;// +(4 - MaskWidth % 4) % 4;
                    h = MaskHeight;// +(4 - MaskHeight % 4) % 4;

                    if (w == 0 || h == 0)
                    {
                        return;
                    }

                    try
                    {
                        MaskImage = new Bitmap(w, h);

                        data = MaskImage.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite,
                                                         PixelFormat.Format32bppArgb);

                        dest = DecodePayload(MaskFBytes, w, h);

                        Marshal.Copy(dest, 0, data.Scan0, dest.Length);

                        MaskImage.UnlockBits(data);
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText(@".\Error.txt",
                                       string.Format("[{0}] {1}{2}", DateTime.Now, ex, Environment.NewLine));
                    }
                }

                dest = null;
            }


            public void Save(BinaryWriter writer)
            {
                writer.Write(Width);
                writer.Write(Height);
                writer.Write(X);
                writer.Write(Y);
                writer.Write(ShadowX);
                writer.Write(ShadowY);
                writer.Write(HasMask ? (byte)(Shadow | 0x80) : (byte)Shadow);
                writer.Write(FBytes.Length);
                writer.Write(FBytes);
                if (HasMask)
                {
                    writer.Write(MaskWidth);
                    writer.Write(MaskHeight);
                    writer.Write(MaskX);
                    writer.Write(MaskY);
                    writer.Write(MaskFBytes.Length);
                    writer.Write(MaskFBytes);
                }
            }

            public static byte[] Compress(byte[] raw)
            {
                using (MemoryStream memory = new MemoryStream())
                {
                    using (GZipStream gzip = new GZipStream(memory,
                    CompressionMode.Compress, true))
                    {
                        gzip.Write(raw, 0, raw.Length);
                    }
                    return memory.ToArray();
                }
            }

            static byte[] Decompress(byte[] gzip)
            {
                // Create a GZIP stream with decompression mode.
                // ... Then create a buffer and write into while reading from the GZIP stream.
                using (GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress))
                {
                    const int size = 4096;
                    byte[] buffer = new byte[size];
                    using (MemoryStream memory = new MemoryStream())
                    {
                        int count = 0;
                        do
                        {
                            count = stream.Read(buffer, 0, size);
                            if (count > 0)
                            {
                                memory.Write(buffer, 0, count);
                            }
                        }
                        while (count > 0);
                        return memory.ToArray();
                    }
                }
            }

            /// <summary>
            /// 负载类型自动判定（Artificial Mir 的 32bit Lib 变体帧内不是 gzip 而是其它编码）：
            ///   1F 8B 开头 → gzip BGRA（标准）；长度 = DXT1 块数×8 → DXT1(BC1)；
            ///   长度 = w*h*4 → 裸 BGRA32；长度 = w*h*2 → RGB565（黑=透明）。
            /// 其余按 gzip 尝试，保持旧行为。
            /// </summary>
            static byte[] DecodePayload(byte[] bytes, int w, int h)
            {
                if (bytes == null || bytes.Length == 0) return Array.Empty<byte>();
                if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B) return Decompress(bytes);

                int dxtLen = ((w + 3) / 4) * ((h + 3) / 4) * 8;
                if (bytes.Length == dxtLen) return Dxt1ToBgra(bytes, w, h);
                if (bytes.Length == w * h * 4) return bytes;
                if (w > 0 && h > 0 && bytes.Length == w * h * 2) return Rgb565ToBgra(bytes, w, h);

                return Decompress(bytes);
            }

            /// <summary>DXT1 → BGRA。必须走 IntPtr 重载（byte[] 重载在本环境会写坏托管堆）。</summary>
            static unsafe byte[] Dxt1ToBgra(byte[] blocks, int w, int h)
            {
                int needLen = ((w + 3) / 4) * ((h + 3) / 4) * 8;
                if (blocks.Length < needLen)
                {
                    byte[] pad = new byte[needLen];
                    Buffer.BlockCopy(blocks, 0, pad, 0, blocks.Length);
                    blocks = pad;
                }

                byte[] rgba = new byte[w * h * 4];
                fixed (byte* ps = blocks)
                fixed (byte* pd = rgba)
                {
                    Squish.DecompressImage((IntPtr)pd, w, h, (IntPtr)ps, SquishFlags.Dxt1);
                }

                for (int i = 0; i + 3 < rgba.Length; i += 4)
                {
                    byte r = rgba[i];
                    rgba[i] = rgba[i + 2];
                    rgba[i + 2] = r;
                }
                return rgba;
            }

            static byte[] Rgb565ToBgra(byte[] raw, int w, int h)
            {
                int pixels = w * h;
                byte[] bgra = new byte[pixels * 4];
                for (int i = 0, p = 0; i < pixels; i++, p += 2)
                {
                    ushort v = (ushort)(raw[p] | (raw[p + 1] << 8));
                    bgra[i * 4 + 0] = (byte)(((v & 0x1F) * 255 + 15) / 31);
                    bgra[i * 4 + 1] = (byte)((((v >> 5) & 0x3F) * 255 + 31) / 63);
                    bgra[i * 4 + 2] = (byte)((((v >> 11) & 0x1F) * 255 + 15) / 31);
                    bgra[i * 4 + 3] = v == 0 ? (byte)0 : (byte)255;
                }
                return bgra;
            }

            public void CreatePreview()
            {
                if (Image == null)
                {
                    Preview = new Bitmap(1, 1);
                    return;
                }

                Preview = new Bitmap(64, 64);

                using (Graphics g = Graphics.FromImage(Preview))
                {
                    g.InterpolationMode = InterpolationMode.Low;//HighQualityBicubic
                    g.Clear(Color.Transparent);
                    int w = Math.Min((int)Width, 64);
                    int h = Math.Min((int)Height, 64);
                    g.DrawImage(Image, new Rectangle((64 - w) / 2, (64 - h) / 2, w, h), new Rectangle(0, 0, Width, Height), GraphicsUnit.Pixel);

                    g.Save();
                }
            }
        }
    }
}