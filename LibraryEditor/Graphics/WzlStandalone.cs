using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace LibraryEditor
{
    /// <summary>
    /// 盛大《热血传奇》官方客户端“独立 .WZL”（无 .WZX 索引）直读实现。
    ///
    /// 容器：64 字节头（偏移 0x2C 处 int32 = 帧数），帧记录从 0x40 起顺序排列，无索引表。
    /// 帧记录 16 字节：[0]=位深标记（3=8bit 调色板索引 / 5=16bit RGB565），[1..3] 保留，
    /// [4]=宽 [6]=高 [8]=X [10]=Y（int16，有符号），[12]=压缩负载长（int32）。
    /// 负载 = { 0x08, 0x00 } + CRC32 + 裸 DEFLATE（即标准 zlib 流）；
    /// 解压后行序自底向上，行宽按 4 字节对齐（WidthBytes）。
    /// 8bit 帧使用 WeMade 固定 256 色调色板（与 WeMadeLibrary 内置调色板一致）。
    ///
    /// 与 WeMadeDirectLibrary 的区别：不要求 .WZX 成对存在；帧负载惰性读取，
    /// 未修改帧保存时按原始字节原样回写（打开→未改动→保存 字节级一致）。
    /// </summary>
    public sealed class WzlStandalone : ILibrary
    {
        /// <summary>WeMade 固定 256 色调色板（ARGB，取自 WeMadeLibrary 内置表）。</summary>
        private static readonly int[] Palette = new int[256]
        {
            -16777216, -8388608, -16744448, -8355840, -16777088, -8388480, -16744320, -4144960,
            -11173737, -6440504, -8686733, -13817559, -10857902, -10266022, -12437191, -14870504,
            -15200240, -14084072, -15726584, -886415, -2005153, -42406, -52943, -2729390,
            -7073792, -7067368, -13039616, -9236480, -4909056, -4365486, -12445680, -21863,
            -10874880, -9225943, -5944783, -7046285, -4369871, -11394800, -8703720, -13821936,
            -7583183, -7067392, -4378368, -3771566, -9752296, -3773630, -3257856, -5938375,
            -10866408, -14020608, -15398912, -12969984, -16252928, -14090240, -11927552, -6488064,
            -2359296, -2228224, -327680, -6524078, -7050422, -9221591, -11390696, -7583208,
            -7846895, -11919104, -14608368, -2714534, -3773663, -1086720, -35072, -5925756,
            -12439263, -15200248, -14084088, -14610432, -13031144, -7576775, -12441328, -9747944,
            -8697320, -7058944, -7568261, -9739430, -11910599, -14081768, -12175063, -4872812,
            -8688806, -3231340, -5927821, -7572646, -4877197, -2710157, -1071798, -1063284,
            -8690878, -9742791, -4352934, -10274560, -2701651, -11386327, -7052520, -1059155,
            -5927837, -10266038, -4348549, -10862056, -4355023, -13291223, -7043997, -8688822,
            -5927846, -10859991, -6522055, -12439280, -1069791, -15200256, -14081792, -6526208,
            -7044006, -11386344, -9741783, -8690911, -6522079, -2185984, -10857927, -13555440,
            -3228293, -10266055, -7044022, -3758807, -15688680, -12415926, -13530046, -15690711,
            -16246768, -16246760, -16242416, -15187415, -5917267, -9735309, -15193815, -15187382,
            -13548982, -10238242, -12263937, -7547153, -9213127, -532935, -528500, -530688,
            -9737382, -10842971, -12995089, -11887410, -13531979, -13544853, -2171178, -4342347,
            -7566204, -526370, -16775144, -16246727, -16248791, -16246784, -16242432, -16756059,
            -16745506, -15718070, -15713941, -15707508, -14591323, -15716006, -15711612, -13544828,
            -15195855, -11904389, -11375707, -14075549, -15709474, -14079711, -11908551, -14079720,
            -11908567, -8684734, -6513590, -10855895, -12434924, -13027072, -10921728, -3525332,
            -9735391, -14077696, -13551344, -13551336, -12432896, -11377896, -10849495, -13546984,
            -15195904, -15191808, -15189744, -10255286, -9716406, -10242742, -10240694, -10838966,
            -11891655, -10238390, -10234294, -11369398, -13536471, -10238374, -11354806, -15663360,
            -15193832, -11892662, -11868342, -16754176, -16742400, -16739328, -16720384, -16716288,
            -16712960, -11904364, -10259531, -8680234, -9733162, -8943361, -3750194, -7039844,
            -6515514, -13553351, -14083964, -15204220, -11910574, -11386245, -10265997, -3230217,
            -7570532, -8969524, -2249985, -1002454, -2162529, -1894477, -1040, -6250332,
            -8355712, -65536, -16711936, -256, -16776961, -65281, -16711681, -1,
        };

        private sealed class Rec
        {
            public long Offset;         // 帧记录起点
            public byte B0;             // 3=8bit / 5=16bit
            public byte[] Keep3;        // 原始 [1..3] 三字节（保存未修改帧时原样回写）
            public short W, H, X, Y;
            public int CLen;            // 压缩负载长
        }

        private string _path;
        private readonly List<Rec> _recs = new List<Rec>();
        private readonly List<MLibraryV2.MImage> _images = new List<MLibraryV2.MImage>();
        private readonly List<byte[]> _origPayload = new List<byte[]>();   // null = 未读入
        private readonly List<bool> _modified = new List<bool>();
        private byte[] _header64;       // 原始 64 字节头
        private byte[] _defaultKeep3;   // 新增帧使用的 [1..3]
        private readonly FrameSet _frames = new FrameSet();

        public WzlStandalone(string filename)
        {
            _path = Path.GetFullPath(filename);
            Load();
        }

        public static bool CanOpen(string filename)
        {
            try
            {
                if (string.IsNullOrEmpty(filename)) return false;
                if (!Path.GetExtension(filename).Equals(".WZL", StringComparison.OrdinalIgnoreCase)) return false;
                if (!File.Exists(filename)) return false;
                using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] head = new byte[0x41];
                    if (fs.Read(head, 0, head.Length) < head.Length) return false;
                    int count = BitConverter.ToInt32(head, 0x2C);
                    if (count <= 0 || count > 50000000) return false;
                    byte b0 = head[0x40];
                    return b0 == 3 || b0 == 5;
                }
            }
            catch { return false; }
        }

        // ---------------- 加载 ----------------

        private void Load()
        {
            _images.Clear(); _recs.Clear(); _origPayload.Clear(); _modified.Clear();

            using (FileStream fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                _header64 = new byte[64];
                if (fs.Read(_header64, 0, 64) < 64)
                    throw new InvalidDataException("WZL 头不足 64 字节：" + _path);
                _defaultKeep3 = new byte[] { 0x01, 0x00, 0x00 };

                fs.Position = 0x40;
                var header = new byte[16];
                while (true)
                {
                    if (fs.Read(header, 0, 16) < 16) break;
                    byte b0 = header[0];
                    if (b0 != 3 && b0 != 5) break;
                    int clen = BitConverter.ToInt32(header, 12);
                    if (clen < 0 || clen > 128 * 1024 * 1024) break;

                    Rec r = new Rec
                    {
                        Offset = fs.Position - 16,
                        B0 = b0,
                        Keep3 = new[] { header[1], header[2], header[3] },
                        W = BitConverter.ToInt16(header, 4),
                        H = BitConverter.ToInt16(header, 6),
                        X = BitConverter.ToInt16(header, 8),
                        Y = BitConverter.ToInt16(header, 10),
                        CLen = clen,
                    };
                    if (_recs.Count == 0) _defaultKeep3 = (byte[])r.Keep3.Clone();
                    _recs.Add(r);
                    _origPayload.Add(null);
                    _modified.Add(false);
                    fs.Seek(clen, SeekOrigin.Current);
                }
            }

            for (int i = 0; i < _recs.Count; i++)
            {
                Rec r = _recs[i];
                bool blank = r.W <= 0 || r.H <= 0 || r.CLen <= 0;
                MLibraryV2.MImage mi = new MLibraryV2.MImage(null, blank ? (short)0 : r.W, blank ? (short)0 : r.H)
                {
                    X = r.X,
                    Y = r.Y,
                    Length = 0,
                    HasMask = false,
                };
                _images.Add(mi);
            }
        }

        // ---------------- ILibrary 基本属性 ----------------

        public string FileName
        {
            get { return _path; }
            set { _path = value; }
        }

        public List<MLibraryV2.MImage> Images { get { return _images; } }

        public FrameSet Frames { get { return _frames; } set { } }

        public int Count { get { return _images.Count; } }

        public void Close()
        {
            foreach (MLibraryV2.MImage mi in _images)
            {
                if (mi == null) continue;
                if (mi.Preview != null) { mi.Preview.Dispose(); mi.Preview = null; }
                if (mi.Image != null) { mi.Image.Dispose(); mi.Image = null; }
                mi.TextureValid = false;
            }
        }

        // ---------------- 惰性读取 + 解码 ----------------

        private byte[] ReadPayload(int index)
        {
            if (_origPayload[index] != null) return _origPayload[index];
            Rec r = _recs[index];
            using (FileStream fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fs.Seek(r.Offset + 16, SeekOrigin.Begin);
                byte[] payload = new byte[r.CLen];
                int got = 0;
                while (got < r.CLen)
                {
                    int n = fs.Read(payload, got, r.CLen - got);
                    if (n <= 0) break;
                    got += n;
                }
                Array.Resize(ref payload, got);
                _origPayload[index] = payload;
                return payload;
            }
        }

        public void EnsureTexture(int index)
        {
            if (index < 0 || index >= _images.Count) return;
            MLibraryV2.MImage mi = _images[index];
            if (mi == null || mi.TextureValid || mi.Image != null) return;
            if (!MLibraryV2.Load) return;
            if (mi.Width < 1 || mi.Height < 1) return;

            Rec r = _recs[index];
            if (mi.FBytes == null)
            {
                mi.FBytes = ReadPayload(index);
                mi.Length = mi.FBytes.Length;
            }

            try
            {
                // 修改/新增帧的 FBytes 一律是 16bit 重编码负载；未修改帧按记录标记取位深
                int bits = (r != null && !_modified[index]) ? (r.B0 == 5 ? 16 : 8) : 16;
                byte[] raw = ZlibDecompress(mi.FBytes);
                mi.Image = Decode(raw, bits, mi.Width, mi.Height);
                mi.TextureValid = mi.Image != null;
            }
            catch
            {
                mi.Image = null;
                mi.Preview = null;
                mi.TextureValid = false;
            }
        }

        /// <summary>标准 zlib 流（{08 00}+CRC32+DEFLATE）解压。</summary>
        private static byte[] ZlibDecompress(byte[] payload)
        {
            using (MemoryStream input = new MemoryStream(payload))
            using (ZLibStream zs = new ZLibStream(input, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream(payload.Length * 4))
            {
                zs.CopyTo(output, 81920);
                return output.ToArray();
            }
        }

        private static Bitmap Decode(byte[] raw, int bits, int width, int height)
        {
            int bpp = bits / 8;
            int stride = (width * bpp + 3) / 4 * 4;      // 4 字节对齐
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int[] dst = new int[width * height];
                int idx = 0;
                for (int fy = 0; fy < height; fy++)      // 文件行序 = 自底向上
                {
                    int y = height - 1 - fy;
                    int baseOut = y * width;
                    if (bits == 16)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int o = idx + x * 2;
                            if (o + 1 >= raw.Length) break;
                            int color = raw[o] | (raw[o + 1] << 8);
                            dst[baseOut + x] = Convert16(color);
                        }
                    }
                    else
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int o = idx + x;
                            if (o >= raw.Length) break;
                            dst[baseOut + x] = Palette[raw[o]];
                        }
                    }
                    idx += stride;
                }
                byte[] bytes = new byte[dst.Length * 4];
                Buffer.BlockCopy(dst, 0, bytes, 0, bytes.Length);
                Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
            }
            finally { bmp.UnlockBits(data); }
            return bmp;
        }

        private static int Convert16(int color)
        {
            int red = (color & 0xf800) >> 8;
            int green = (color & 0x07e0) >> 3;
            int blue = (color & 0x001f) << 3;
            return (red << 0x10) | (green << 0x8) | blue | (255 << 24);
        }

        // ---------------- 读取接口 ----------------

        public MLibraryV2.MImage GetMImage(int index)
        {
            if (index < 0 || index >= _images.Count) return null;
            EnsureTexture(index);
            return _images[index];
        }

        public Bitmap GetPreview(int index)
        {
            if (index < 0 || index >= _images.Count) return new Bitmap(1, 1);
            EnsureTexture(index);
            MLibraryV2.MImage image = _images[index];
            if (image == null || image.Image == null) return new Bitmap(1, 1);
            if (image.Preview == null) image.CreatePreview();
            return image.Preview;
        }

        // ---------------- 编辑接口 ----------------

        private static MLibraryV2.MImage CreateEncodedImage(Bitmap image, short x, short y, bool removeBlack)
        {
            // 修改/新增帧必须按 WZL 负载格式手工编码（16bit RGB565、行序自底向上、行宽 4 字节对齐），
            // 不能用 MImage(Bitmap) —— 那是 MLib V2 的 32bit 自上而下格式
            MLibraryV2.MImage mi = new MLibraryV2.MImage(null, (short)(image?.Width ?? 0), (short)(image?.Height ?? 0))
            {
                X = x,
                Y = y,
                HasMask = false,
            };
            if (image != null)
            {
                mi.Image = (Bitmap)image.Clone();
                mi.FBytes = EncodePayload(image, removeBlack);
                mi.Length = mi.FBytes.Length;
            }
            return mi;
        }

        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            uint[] t = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? (0xEDB88320u ^ (c >> 1)) : (c >> 1);
                t[i] = c;
            }
            return t;
        }

        private static uint Crc32(byte[] data)
        {
            uint c = 0xFFFFFFFFu;
            for (int i = 0; i < data.Length; i++)
                c = CrcTable[(c ^ data[i]) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }

        private static byte[] DeflateRaw(byte[] raw)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (DeflateStream ds = new DeflateStream(ms, CompressionLevel.Optimal, true))
                    ds.Write(raw, 0, raw.Length);
                return ms.ToArray();
            }
        }

        /// <summary>位图 → WZL 负载：{08 00}+CRC32+DEFLATE，像素 16bit RGB565，行序自底向上，行宽按 4 字节对齐。</summary>
        private static byte[] EncodePayload(Bitmap image, bool removeBlack)
        {
            int w = image.Width, h = image.Height;
            int rowBytes = (w * 2 + 3) / 4 * 4;
            byte[] raw = new byte[rowBytes * h];

            BitmapData bd = image.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] src = new byte[bd.Stride * h];
                Marshal.Copy(bd.Scan0, src, 0, src.Length);
                for (int y = 0; y < h; y++)
                {
                    int srcBase = y * bd.Stride;
                    int dstBase = (h - 1 - y) * rowBytes;   // 文件第 0 行 = 图像最下面一行
                    for (int x = 0; x < w; x++)
                    {
                        int si = srcBase + x * 4;
                        byte b = src[si], g = src[si + 1], r = src[si + 2], a = src[si + 3];
                        int di = dstBase + x * 2;
                        ushort v;
                        if (a == 0) v = 0;
                        else if (removeBlack && b == 0 && g == 0 && r == 0) v = 0;
                        else v = (ushort)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));
                        raw[di] = (byte)(v & 0xFF);
                        raw[di + 1] = (byte)((v >> 8) & 0xFF);
                    }
                }
            }
            finally { image.UnlockBits(bd); }

            byte[] deflate = DeflateRaw(raw);
            byte[] payload = new byte[6 + deflate.Length];
            payload[0] = 0x08;
            payload[1] = 0x00;
            BitConverter.GetBytes(Crc32(raw)).CopyTo(payload, 2);
            Buffer.BlockCopy(deflate, 0, payload, 6, deflate.Length);
            return payload;
        }

        public void AddImage(Bitmap image, short x, short y, bool removeBlack = true)
        {
            _images.Add(CreateEncodedImage(image, x, y, removeBlack));
            _recs.Add(null);
            _origPayload.Add(null);
            _modified.Add(true);
        }

        public void AddImage(Bitmap image, Bitmap MaskImage, short x, short y, bool removeBlack = true)
        {
            AddImage(image, x, y, removeBlack);   // WeMade 无遮罩层
        }

        public void InsertImage(int index, Bitmap image, short x, short y, bool removeBlack = true)
        {
            if (index < 0) index = 0;
            if (index > _images.Count) index = _images.Count;
            _images.Insert(index, CreateEncodedImage(image, x, y, removeBlack));
            _recs.Insert(index, null);
            _origPayload.Insert(index, null);
            _modified.Insert(index, true);
        }

        public void ReplaceImage(int index, Bitmap image, short xOffset, short yOffset, bool removeBlack = true)
        {
            if (index < 0 || index >= _images.Count) return;
            MLibraryV2.MImage mi = CreateEncodedImage(image, xOffset, yOffset, removeBlack);
            DisposeImage(_images[index]);
            _images[index] = mi;
            _modified[index] = true;
        }

        public void RemoveImage(int index)
        {
            if (index < 0 || index >= _images.Count) return;
            DisposeImage(_images[index]);
            _images.RemoveAt(index);
            _recs.RemoveAt(index);
            _origPayload.RemoveAt(index);
            _modified.RemoveAt(index);
        }

        public void RemoveBlanks(bool safe = false)
        {
            for (int i = _images.Count - 1; i >= 0; i--)
            {
                MLibraryV2.MImage mi = _images[i];
                if (mi == null) { RemoveImage(i); continue; }
                if (mi.Width == 0 && mi.Height == 0) { if (!safe) RemoveImage(i); continue; }
                if (mi.FBytes == null || mi.FBytes.Length == 0) { if (!safe) RemoveImage(i); }
            }
        }

        private static void DisposeImage(MLibraryV2.MImage mi)
        {
            if (mi == null) return;
            if (mi.Preview != null) { mi.Preview.Dispose(); mi.Preview = null; }
            if (mi.Image != null) { mi.Image.Dispose(); mi.Image = null; }
            mi.TextureValid = false;
        }

        // ---------------- 保存 ----------------

        public void Save()
        {
            // 帧记录重写：未修改帧原样回写（原 16 字节头 + 原负载），修改/新增帧按 16bit 重新编码
            using (MemoryStream ms = new MemoryStream())
            {
                // 头部 64 字节原样保留：实测头部 0x2C 的数值≠实际帧数（如 Deco 头=317 实=193），
                // 客户端按顺序读到文件尾，不依赖该字段；改动会破坏“未修改=字节级一致”
                byte[] header = (byte[])_header64.Clone();
                ms.Write(header, 0, 64);

                for (int i = 0; i < _images.Count; i++)
                {
                    MLibraryV2.MImage mi = _images[i];
                    Rec r = _recs[i];

                    bool untouched = r != null && !_modified[i] &&
                                     (mi.FBytes == null || ReferenceEquals(mi.FBytes, _origPayload[i]));

                    if (untouched)
                    {
                        WriteRecordHeader(ms, r.B0, r.Keep3, r.W, r.H, r.X, r.Y, r.CLen);
                        byte[] payload = ReadPayload(i);
                        ms.Write(payload, 0, payload.Length);
                    }
                    else
                    {
                        if (mi == null || mi.Width <= 0 || mi.Height <= 0 || mi.FBytes == null || mi.FBytes.Length == 0)
                            continue;   // 空帧不写
                        byte[] payload = mi.FBytes;
                        WriteRecordHeader(ms, 5, _defaultKeep3, mi.Width, mi.Height, mi.X, mi.Y, payload.Length);
                        ms.Write(payload, 0, payload.Length);
                    }
                }

                File.WriteAllBytes(_path, ms.ToArray());
            }

            // 保存后重新基线化
            for (int i = 0; i < _images.Count; i++)
            {
                _recs[i] = null;
                _origPayload[i] = null;
                _modified[i] = false;
            }
            // 重建记录表（偏移需要重算）
            ReloadAfterSave();
        }

        private void ReloadAfterSave()
        {
            _recs.Clear(); _origPayload.Clear(); _modified.Clear();
            using (FileStream fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                _header64 = new byte[64];
                fs.Read(_header64, 0, 64);
                _defaultKeep3 = new byte[] { 0x01, 0x00, 0x00 };
                fs.Position = 0x40;
                var header = new byte[16];
                while (true)
                {
                    if (fs.Read(header, 0, 16) < 16) break;
                    byte b0 = header[0];
                    if (b0 != 3 && b0 != 5) break;
                    int clen = BitConverter.ToInt32(header, 12);
                    if (clen < 0 || clen > 128 * 1024 * 1024) break;
                    Rec r = new Rec
                    {
                        Offset = fs.Position - 16,
                        B0 = b0,
                        Keep3 = new[] { header[1], header[2], header[3] },
                        W = BitConverter.ToInt16(header, 4),
                        H = BitConverter.ToInt16(header, 6),
                        X = BitConverter.ToInt16(header, 8),
                        Y = BitConverter.ToInt16(header, 10),
                        CLen = clen,
                    };
                    if (_recs.Count == 0) _defaultKeep3 = (byte[])r.Keep3.Clone();
                    _recs.Add(r);
                    _origPayload.Add(null);
                    _modified.Add(false);
                    fs.Seek(clen, SeekOrigin.Current);
                }
            }
            // 帧数应与 _images 一致（空帧被跳过时可能少，属正常；保持 Images 为主）
        }

        private static void WriteRecordHeader(Stream s, byte b0, byte[] keep3, short w, short h, short x, short y, int clen)
        {
            var buf = new byte[16];
            buf[0] = b0;
            if (keep3 != null && keep3.Length == 3) { buf[1] = keep3[0]; buf[2] = keep3[1]; buf[3] = keep3[2]; }
            BitConverter.GetBytes(w).CopyTo(buf, 4);
            BitConverter.GetBytes(h).CopyTo(buf, 6);
            BitConverter.GetBytes(x).CopyTo(buf, 8);
            BitConverter.GetBytes(y).CopyTo(buf, 10);
            BitConverter.GetBytes(clen).CopyTo(buf, 12);
            s.Write(buf, 0, 16);
        }
    }
}
