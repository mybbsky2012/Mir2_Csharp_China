using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using LibraryEditor.Formats;

namespace LibraryEditor
{
    /// <summary>
    /// WeMade（WEMADE Entertainment）资源库 .WIX(索引) + .WIL(数据) 的“直接打开（不转换）”实现。
    ///
    /// 与 Mir3Library 同构：自包含的 ILibrary 实现，构造时只解析索引与 16 字节帧头，
    /// 位图按需解码（EnsureTexture），铁律是“未改动帧的负载与容器解析出的原始字节是同一引用”，
    /// 保存时原样回写；“打开 → 未改动 → 保存”对 .WIX 与 .WIL 均字节级一致。
    ///
    /// 解码复用 WeMadeLibrary.WeMadeImage（本文件不修改 WeMadeLibrary.cs）；
    /// 编码只针对被修改 / 新增帧，按 WeMadeImage.CreateTexture 的逆过程实现（RGB565 16bit / BGRA 32bit + 裸 DEFLATE）。
    /// </summary>
    public sealed class WeMadeDirectLibrary : ILibrary
    {
        // 与 WeMadeLibrary.ImageStructureSize 一致：负载相对记录起点的偏移。
        private static readonly int[] ImageStructureSize = { 8, 16, 16, 17, 16, 16 };

        private string _wixPath;
        private string _wilPath;
        private WeMadeContainer _container;
        private byte _subtype;
        private FrameSet _frames = new FrameSet();

        /// <summary>调色板。子类型 2/5 不用；仅子类型 0/1（8bit）解码时才会被读到，兜底给一张全透明表。</summary>
        private int[] _palette = new int[256];

        /// <summary>与 Images 一一对应的“上次保存时”状态，用于变更检测与字节级回写。</summary>
        private sealed class EntryState
        {
            public int Frame = -1;          // 对应的容器帧索引；-1 = 无
            public byte[] PayloadRef;       // 上次保存时的负载引用
            public short W, H, X, Y;
        }

        private List<EntryState> _state = new List<EntryState>();

        /// <summary>每帧原始位深（16 / 32），0 = 未知；修改子类型 5 的帧时用于决定编码位深。</summary>
        private List<int> _frameBits = new List<int>();

        public List<MLibraryV2.MImage> Images { get; private set; } = new List<MLibraryV2.MImage>();

        public int Count { get { return Images == null ? 0 : Images.Count; } }

        /// <summary>数据文件（.WIL）路径。</summary>
        public string FileName
        {
            get { return _wilPath; }
            set
            {
                _wilPath = value;
                if (!string.IsNullOrEmpty(value))
                    _wixPath = Path.ChangeExtension(value, IndexExtForData(Path.GetExtension(value)));
            }
        }

        /// <summary>WeMade 没有动作帧段，返回可写空 FrameSet（写盘时忽略）。</summary>
        public FrameSet Frames
        {
            get { return _frames; }
            set { _frames = value ?? new FrameSet(); }
        }

        public WeMadeDirectLibrary(string filename)
        {
            ResolvePaths(filename);
            Load();
        }

        // ---------------- 成对文件定位 ----------------

        private static bool IsIndexExt(string e)
        {
            return e.Equals(".WIX", StringComparison.OrdinalIgnoreCase) ||
                   e.Equals(".WZX", StringComparison.OrdinalIgnoreCase) ||
                   e.Equals(".MIX", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDataExt(string e)
        {
            return e.Equals(".WIL", StringComparison.OrdinalIgnoreCase) ||
                   e.Equals(".WZL", StringComparison.OrdinalIgnoreCase) ||
                   e.Equals(".MIZ", StringComparison.OrdinalIgnoreCase);
        }

        private static string IndexExtForData(string dataExt)
        {
            if (dataExt == null) return ".wix";
            if (dataExt.Equals(".WZL", StringComparison.OrdinalIgnoreCase)) return ".wzx";
            if (dataExt.Equals(".MIZ", StringComparison.OrdinalIgnoreCase)) return ".mix";
            return ".wix";
        }

        private static string DataExtForIndex(string idxExt)
        {
            if (idxExt == null) return ".wil";
            if (idxExt.Equals(".WZX", StringComparison.OrdinalIgnoreCase)) return ".wzl";
            if (idxExt.Equals(".MIX", StringComparison.OrdinalIgnoreCase)) return ".miz";
            return ".wil";
        }

        /// <summary>按扩展名/大小写不敏感地在同目录内定位 .WIX/.WIL 成对文件（含 .WZX/.WZL、.MIX/.MIZ）。</summary>
        private void ResolvePaths(string filename)
        {
            string full = Path.GetFullPath(filename);
            string dir = Path.GetDirectoryName(full);
            string stem = Path.GetFileNameWithoutExtension(full);

            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                foreach (string f in Directory.EnumerateFiles(dir))
                {
                    string name = Path.GetFileName(f);
                    if (!string.Equals(Path.GetFileNameWithoutExtension(name), stem, StringComparison.OrdinalIgnoreCase))
                        continue;
                    string e = Path.GetExtension(name);
                    if (IsIndexExt(e)) _wixPath = f;
                    else if (IsDataExt(e)) _wilPath = f;
                }
            }

            if (_wilPath == null)
            {
                string fe = Path.GetExtension(full);
                if (IsDataExt(fe)) _wilPath = full;
                else if (IsIndexExt(fe)) _wilPath = Path.ChangeExtension(full, DataExtForIndex(fe));
                else _wilPath = Path.ChangeExtension(full, ".wil");
            }

            if (_wixPath == null)
                _wixPath = Path.ChangeExtension(_wilPath, IndexExtForData(Path.GetExtension(_wilPath)));
        }

        // ---------------- 子类型识别 ----------------

        /// <summary>完全复刻 WeMadeLibrary.LoadImageInfo 的子类型判定（输入为 .WIL 起始若干字节，至少 51 字节）。</summary>
        private static byte DetectSubtype(byte[] h)
        {
            byte t = 0;
            if (h.Length > 40 && (h[40] == 1 || h[40] == 6)) t = 2;
            else if (h.Length > 2 && (h[2] == 73 || h[2] == 72)) t = 3;
            else if (h.Length > 48 && h[48] == 32) t = 5;

            if ((t == 2 || t == 0) && h.Length >= 51)
            {
                short v = (short)(h[49] | (h[50] << 8));
                if (v == 32) t = 5;
            }
            return t;
        }

        private static bool IsDirectOpenableSubtype(byte t)
        {
            return t == 0 || t == 1 || t == 2 || t == 4 || t == 5;
        }

        /// <summary>是否可直接打开（不转换）：成对文件齐备、子类型受支持、且不是 Mir3（.MIX/.MIZ 走 Mir3Library）。</summary>
        public static bool CanOpen(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return false;

            string wixPath = null, wilPath = null;
            try
            {
                string full = Path.GetFullPath(filename);
                string dir = Path.GetDirectoryName(full);
                string stem = Path.GetFileNameWithoutExtension(full);

                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    foreach (string f in Directory.EnumerateFiles(dir))
                    {
                        string name = Path.GetFileName(f);
                        if (!string.Equals(Path.GetFileNameWithoutExtension(name), stem, StringComparison.OrdinalIgnoreCase))
                            continue;
                        string e = Path.GetExtension(name);
                        if (IsIndexExt(e)) wixPath = f;
                        else if (IsDataExt(e)) wilPath = f;
                    }
                }

                if (wilPath == null)
                {
                    string fe = Path.GetExtension(full);
                    if (IsDataExt(fe)) wilPath = full;
                    else if (IsIndexExt(fe)) wilPath = Path.ChangeExtension(full, DataExtForIndex(fe));
                }
                if (wixPath == null && wilPath != null)
                    wixPath = Path.ChangeExtension(wilPath, IndexExtForData(Path.GetExtension(wilPath)));

                if (wilPath == null || wixPath == null) return false;
                if (!File.Exists(wilPath) || !File.Exists(wixPath)) return false;

                byte[] head = new byte[64];
                int n;
                using (FileStream fs = new FileStream(wilPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    n = fs.Read(head, 0, head.Length);
                if (n < 51) return false;
                if (n < head.Length) Array.Resize(ref head, n);

                // Mir3 的 .MIX/.MIZ 与 WeMade 缩写撞名：按魔数排除。
                using (FileStream fs = new FileStream(wixPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] hx = new byte[16];
                    int nx = fs.Read(hx, 0, hx.Length);
                    if (nx == hx.Length && Mir3Container.IsMir3(hx)) return false;
                }
                if (Mir3Container.IsMir3(head)) return false;

                return IsDirectOpenableSubtype(DetectSubtype(head));
            }
            catch
            {
                return false;
            }
        }

        // ---------------- 生命周期 ----------------

        private void Load()
        {
            Images = new List<MLibraryV2.MImage>();
            _state = new List<EntryState>();
            _frameBits = new List<int>();

            if (string.IsNullOrEmpty(_wilPath) || string.IsNullOrEmpty(_wixPath) ||
                !File.Exists(_wilPath) || !File.Exists(_wixPath))
            {
                throw new InvalidDataException(
                    "WeMade 资源库必须成对存在同名 .WIX 与 .WIL 文件（.WZX/.WZL、.MIX/.MIZ 同理）。" + Environment.NewLine +
                    "数据文件：" + (_wilPath ?? "(未找到)") + Environment.NewLine +
                    "索引文件：" + (_wixPath ?? "(未找到)"));
            }

            byte[] wil = File.ReadAllBytes(_wilPath);
            byte[] wix = File.ReadAllBytes(_wixPath);

            // Mir3 的 .MIX/.MIZ 与 WeMade 缩写撞名：按魔数排除（应走 Mir3Library）。
            if (Mir3Container.IsMir3(wix) || Mir3Container.IsMir3(wil))
                throw new InvalidDataException(
                    "该资源库是 Mir3（传奇3）格式，请使用「传奇3（.MIX/.MIZ）」方式打开。" + Environment.NewLine +
                    "文件：" + _wilPath);

            _subtype = DetectSubtype(wil);
            if (_subtype == 3)
                throw new InvalidDataException(
                    "该 .WIL 是 WeMade Mir3（子类型 3）格式，请使用「传奇3（.MIX/.MIZ）」方式打开。" + Environment.NewLine +
                    "文件：" + _wilPath);
            if (!IsDirectOpenableSubtype(_subtype))
                throw new InvalidDataException(
                    "暂不支持的 WeMade 子类型：" + _subtype + "（文件：" + _wilPath + "）");

            try
            {
                _container = WeMadeContainer.Parse(wix, wil, _subtype);
            }
            catch (EliteFormatException ex)
            {
                throw new InvalidDataException("解析 WeMade 资源库失败：" + ex.Message + Environment.NewLine + "文件：" + _wilPath, ex);
            }

            BuildImages();
        }

        private void BuildImages()
        {
            int n = _container.Frames.Count;
            Images = new List<MLibraryV2.MImage>(n);
            _state = new List<EntryState>(n);
            _frameBits = new List<int>(n);

            for (int i = 0; i < n; i++)
            {
                WeMadeFrameRecord fr = _container.Frames[i];
                byte[] payload = fr.Payload ?? Array.Empty<byte>();

                // 空帧（Size==0）Width/Height 记为 0，避免 UI 试图解码零负载帧。
                bool blank = payload.Length == 0;
                MLibraryV2.MImage mi = new MLibraryV2.MImage(payload, blank ? (short)0 : fr.Width, blank ? (short)0 : fr.Height)
                {
                    X = fr.OffsetX,
                    Y = fr.OffsetY,
                    Length = payload.Length,
                    HasMask = false,
                };
                Images.Add(mi);

                EntryState st = new EntryState
                {
                    Frame = i,
                    PayloadRef = payload,
                    W = blank ? (short)0 : fr.Width,
                    H = blank ? (short)0 : fr.Height,
                    X = fr.OffsetX,
                    Y = fr.OffsetY,
                };
                _state.Add(st);
                _frameBits.Add(0);
            }
        }

        public void Close()
        {
            if (Images == null) return;
            foreach (MLibraryV2.MImage mi in Images) DisposeImage(mi);
        }

        private static void DisposeImage(MLibraryV2.MImage mi)
        {
            if (mi == null) return;
            if (mi.Preview != null) { mi.Preview.Dispose(); mi.Preview = null; }
            if (mi.Image != null) { mi.Image.Dispose(); mi.Image = null; }
            if (mi.MaskImage != null) { mi.MaskImage.Dispose(); mi.MaskImage = null; }
            mi.TextureValid = false;
        }

        // ---------------- 解码（按需，复用 WeMadeLibrary.WeMadeImage） ----------------

        public void EnsureTexture(int index)
        {
            if (Images == null || index < 0 || index >= Images.Count) return;

            MLibraryV2.MImage mi = Images[index];
            if (mi == null || mi.TextureValid || mi.Image != null) return;
            if (!MLibraryV2.Load) return;
            if (mi.Width < 1 || mi.Height < 1) return;
            if (mi.FBytes == null || mi.FBytes.Length == 0) return;

            int payloadOffset = _subtype > 0 ? ImageStructureSize[_subtype] : 8;

            try
            {
                // 合成一条完整的“记录”（16 字节头 + 负载），前置 1 字节哑元使 MemoryStream.Position != 0，
                // 以便复用 WeMadeImage 的构造守卫（它在 Position==0 时直接返回）。
                byte[] payload = mi.FBytes;
                byte[] buf = new byte[1 + WeMadeContainer.FrameHeaderLength + payload.Length];
                int p = 1;
                BitConverter.GetBytes(mi.Width).CopyTo(buf, p + 0x00);
                BitConverter.GetBytes(mi.Height).CopyTo(buf, p + 0x02);
                BitConverter.GetBytes(mi.X).CopyTo(buf, p + 0x04);
                BitConverter.GetBytes(mi.Y).CopyTo(buf, p + 0x06);
                BitConverter.GetBytes((short)0).CopyTo(buf, p + 0x08);
                BitConverter.GetBytes((short)0).CopyTo(buf, p + 0x0A);
                BitConverter.GetBytes((uint)payload.Length).CopyTo(buf, p + 0x0C);
                Buffer.BlockCopy(payload, 0, buf, p + WeMadeContainer.FrameHeaderLength, payload.Length);

                using (MemoryStream ms = new MemoryStream(buf, false))
                using (BinaryReader br = new BinaryReader(ms))
                {
                    ms.Position = 1;
                    WeMadeLibrary.WeMadeImage wi = new WeMadeLibrary.WeMadeImage(br, _subtype);
                    ms.Position = 1 + payloadOffset;
                    wi.CreateTexture(br, _palette);

                    mi.Image = wi.Image;
                    mi.TextureValid = mi.Image != null;

                    if (_subtype == 5)
                        _frameBits[index] = wi.bo16bit ? 16 : 32;
                    else
                        _frameBits[index] = wi.bo16bit ? 16 : 8;
                }
            }
            catch
            {
                // 单帧损坏不应导致整库无法浏览
                mi.Image = null;
                mi.Preview = null;
                mi.TextureValid = false;
            }
        }

        // ---------------- 编码（仅修改/新增帧） ----------------

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

        /// <summary>zlib 兼容 CRC-32（反射多项式 0xEDB88320），与 Python zlib.crc32 一致。</summary>
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

        /// <summary>
        /// 位图 → WeMade 负载：像素缓冲（16bit RGB565 或 32bit BGRA，行序自底向上）→ 裸 DEFLATE，
        /// 前面加 { 0x08, 0x00 } + CRC32(解压后字节) 小端。
        /// </summary>
        private static byte[] EncodePayload(Bitmap image, int bits, bool removeBlack)
        {
            int w = image.Width, h = image.Height;
            int bpp = bits == 32 ? 4 : 2;
            int rowBytes = w * bpp;
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
                        int di = dstBase + x * bpp;

                        if (bits == 32)
                        {
                            raw[di] = b; raw[di + 1] = g; raw[di + 2] = r; raw[di + 3] = a;
                        }
                        else
                        {
                            ushort v;
                            if (a == 0) v = 0;
                            else if (removeBlack && b == 0 && g == 0 && r == 0) v = 0;
                            else v = (ushort)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));
                            raw[di] = (byte)(v & 0xFF);
                            raw[di + 1] = (byte)((v >> 8) & 0xFF);
                        }
                    }
                }
            }
            finally
            {
                image.UnlockBits(bd);
            }

            byte[] deflate = DeflateRaw(raw);
            byte[] payload = new byte[6 + deflate.Length];
            payload[0] = 0x08;
            payload[1] = 0x00;
            BitConverter.GetBytes(Crc32(raw)).CopyTo(payload, 2);
            Buffer.BlockCopy(deflate, 0, payload, 6, deflate.Length);
            return payload;
        }

        /// <summary>修改子类型 5 的帧时沿用该帧原始位深；其余（含新增帧）一律 16bit。</summary>
        private int DetermineBits(int index)
        {
            if (_subtype != 5) return 16;
            int b = (index >= 0 && index < _frameBits.Count) ? _frameBits[index] : 0;
            if (b == 0)
            {
                EnsureTexture(index);
                b = (index >= 0 && index < _frameBits.Count) ? _frameBits[index] : 0;
            }
            return b == 32 ? 32 : 16;
        }

        private MLibraryV2.MImage CreateFromBitmap(Bitmap image, short x, short y, bool removeBlack, int bits)
        {
            if (image == null) return new MLibraryV2.MImage(Array.Empty<byte>(), 0, 0) { X = x, Y = y };
            byte[] payload = EncodePayload(image, bits, removeBlack);
            return new MLibraryV2.MImage(payload, (short)image.Width, (short)image.Height)
            {
                X = x,
                Y = y,
                Length = payload.Length,
                HasMask = false,
            };
        }

        // ---------------- 读取接口 ----------------

        public MLibraryV2.MImage GetMImage(int index)
        {
            if (Images == null || index < 0 || index >= Images.Count) return null;
            EnsureTexture(index);
            return Images[index];
        }

        public Bitmap GetPreview(int index)
        {
            if (Images == null || index < 0 || index >= Images.Count) return new Bitmap(1, 1);
            EnsureTexture(index);
            MLibraryV2.MImage image = Images[index];
            if (image == null || image.Image == null) return new Bitmap(1, 1);
            if (image.Preview == null) image.CreatePreview();
            return image.Preview;
        }

        // ---------------- 编辑接口 ----------------

        public void AddImage(Bitmap image, short x, short y, bool removeBlack = true)
        {
            Images.Add(CreateFromBitmap(image, x, y, removeBlack, 16));
            _state.Add(new EntryState { Frame = -1 });
            _frameBits.Add(0);
        }

        public void AddImage(Bitmap image, Bitmap MaskImage, short x, short y, bool removeBlack = true)
        {
            // WeMade 无遮罩层，忽略 MaskImage
            AddImage(image, x, y, removeBlack);
        }

        public void InsertImage(int index, Bitmap image, short x, short y, bool removeBlack = true)
        {
            if (index < 0) index = 0;
            if (index > Images.Count) index = Images.Count;
            Images.Insert(index, CreateFromBitmap(image, x, y, removeBlack, 16));
            _state.Insert(index, new EntryState { Frame = -1 });
            _frameBits.Insert(index, 0);
        }

        public void ReplaceImage(int index, Bitmap image, short xOffset, short yOffset, bool removeBlack = true)
        {
            if (Images == null || index < 0 || index >= Images.Count) return;

            int bits = DetermineBits(index);    // 需在替换前判定位深（会触发一次原始帧解码）
            MLibraryV2.MImage mImage = CreateFromBitmap(image, xOffset, yOffset, removeBlack, bits);

            DisposeImage(Images[index]);
            Images[index] = mImage;
            // _state 保持原值（Frame 与几何），Save 时按引用/几何差异判定为变更
        }

        public void RemoveImage(int index)
        {
            if (Images == null) return;

            if (Images.Count <= 1)
            {
                for (int i = 0; i < Images.Count; i++) DisposeImage(Images[i]);
                Images.Clear();
                _state.Clear();
                _frameBits.Clear();
                return;
            }

            if (index < 0 || index >= Images.Count) return;
            DisposeImage(Images[index]);
            Images.RemoveAt(index);
            if (index < _state.Count) _state.RemoveAt(index);
            if (index < _frameBits.Count) _frameBits.RemoveAt(index);
        }

        public void RemoveBlanks(bool safe = false)
        {
            if (Images == null) return;
            for (int i = Images.Count - 1; i >= 0; i--)
            {
                MLibraryV2.MImage mi = Images[i];
                if (mi == null) continue;
                if (mi.FBytes == null || mi.FBytes.Length == 0)
                {
                    if (!safe) RemoveImage(i);
                    else if (mi.X == 0 && mi.Y == 0) RemoveImage(i);
                }
            }
        }

        // ---------------- 保存 ----------------

        public void Save()
        {
            if (string.IsNullOrEmpty(_wilPath) && string.IsNullOrEmpty(_wixPath)) return;
            if (_container == null) return;

            SyncToContainer();

            if (!string.IsNullOrEmpty(_wilPath)) File.WriteAllBytes(_wilPath, _container.RebuildWil());
            if (!string.IsNullOrEmpty(_wixPath)) File.WriteAllBytes(_wixPath, _container.RebuildWix());
        }

        /// <summary>
        /// 把帧模型回写到容器：未改动帧复用原始负载引用与原帧头（含 Unknown1/2），
        /// 只对修改/新增/删除做最小改动（仅坐标变化时恰好只改帧头 +4/+6）。
        /// </summary>
        private void SyncToContainer()
        {
            List<WeMadeFrameRecord> srcFrames = _container.Frames;
            int origCount = srcFrames.Count;
            int n = Images.Count;

            List<WeMadeFrameRecord> work = new List<WeMadeFrameRecord>(origCount + 8);
            for (int k = 0; k < origCount; k++) work.Add(srcFrames[k].CloneShallow());

            int[] frameOfEntry = new int[n];
            for (int i = 0; i < n; i++)
            {
                MLibraryV2.MImage mi = Images[i];
                EntryState st = (i < _state.Count) ? _state[i] : new EntryState();
                int fi = st.Frame;

                bool blank = mi == null || (mi.Width == 0 && mi.Height == 0 && (mi.FBytes == null || mi.FBytes.Length == 0));
                bool payloadSame = fi >= 0 && fi < origCount && mi != null && ReferenceEquals(mi.FBytes, st.PayloadRef);
                bool geomSame = fi >= 0 && fi < origCount && mi != null
                                && mi.Width == st.W && mi.Height == st.H && mi.X == st.X && mi.Y == st.Y;

                if (fi >= 0 && fi < origCount && payloadSame && geomSame)
                {
                    frameOfEntry[i] = fi;   // 完全未改：原样保留
                    continue;
                }

                if (blank)
                {
                    frameOfEntry[i] = -1;   // 槽位改空
                    continue;
                }

                if (fi >= 0 && fi < origCount)
                {
                    // 独占帧（WeMade 索引与帧一一对应）：原位更新
                    work[fi].Width = mi.Width;
                    work[fi].Height = mi.Height;
                    work[fi].OffsetX = mi.X;
                    work[fi].OffsetY = mi.Y;
                    if (!payloadSame)
                    {
                        work[fi].Payload = mi.FBytes ?? Array.Empty<byte>();
                        work[fi].Modified = true;
                    }
                    frameOfEntry[i] = fi;
                }
                else
                {
                    // 新增帧：追加一条新记录
                    work.Add(NewFrame(mi));
                    frameOfEntry[i] = work.Count - 1;
                }
            }

            bool[] used = new bool[work.Count];
            for (int i = 0; i < n; i++)
                if (frameOfEntry[i] >= 0) used[frameOfEntry[i]] = true;

            int[] keepMap = new int[work.Count];
            List<WeMadeFrameRecord> finalFrames = new List<WeMadeFrameRecord>(work.Count);
            for (int k = 0; k < work.Count; k++)
            {
                if (used[k]) { keepMap[k] = finalFrames.Count; finalFrames.Add(work[k]); }
                else keepMap[k] = -1;
            }

            int[] newEntryFrame = new int[n];
            for (int i = 0; i < n; i++)
                newEntryFrame[i] = frameOfEntry[i] >= 0 ? keepMap[frameOfEntry[i]] : -1;

            _container.Frames = finalFrames;

            // 重新基线化，供多次保存使用
            _state = new List<EntryState>(n);
            for (int i = 0; i < n; i++)
            {
                EntryState st = new EntryState { Frame = newEntryFrame[i] };
                int fi = newEntryFrame[i];
                if (fi >= 0 && fi < finalFrames.Count)
                {
                    WeMadeFrameRecord fr = finalFrames[fi];
                    st.PayloadRef = fr.Payload;
                    st.W = fr.Width; st.H = fr.Height; st.X = fr.OffsetX; st.Y = fr.OffsetY;
                }
                else
                {
                    st.PayloadRef = Array.Empty<byte>();
                }
                _state.Add(st);
            }
        }

        private static WeMadeFrameRecord NewFrame(MLibraryV2.MImage mi)
        {
            return new WeMadeFrameRecord
            {
                Width = mi.Width,
                Height = mi.Height,
                OffsetX = mi.X,
                OffsetY = mi.Y,
                Unknown1 = 0,
                Unknown2 = 0,
                Payload = mi.FBytes ?? Array.Empty<byte>(),
                Modified = true,
            };
        }
    }
}
