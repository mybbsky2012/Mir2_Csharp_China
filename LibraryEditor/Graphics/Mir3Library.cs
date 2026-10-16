using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using LibraryEditor.Formats;

namespace LibraryEditor
{
    /// <summary>
    /// Mir3（传奇3）资源库：.MIX(索引) + .MIZ(数据) 成对读写。
    /// 帧模型复用 MLibraryV2.MImage，但位图编解码为 Mir3 专用（裸 zlib + RGB565），
    /// 因此不继承 FrameLibrary（那套是 17 字节头 + gzip）。
    ///
    /// 架构：打开时只读索引与 16 字节帧头，位图按需解码（单库可达几十万帧）。
    /// 铁律：未改动帧的 Payload 与容器解析出的原始 zlib 字节是同一引用，保存时原样回写；
    /// "打开 → 未改动 → 保存" 对 .MIX 与 .MIZ 均应字节级一致。
    /// </summary>
    public sealed class Mir3Library : ILibrary
    {
        private string _mixPath;
        private string _mizPath;
        private Mir3Container _container;
        private FrameSet _frames = new FrameSet();

        /// <summary>与 Images 一一对应的"上次保存时"状态，用于变更检测与字节级回写。</summary>
        private sealed class EntryState
        {
            public int Frame = -1;          // 对应的帧索引；-1 = 无
            public byte[] PayloadRef;       // 上次保存时的负载引用
            public short W, H, X, Y;
        }

        private List<EntryState> _state = new List<EntryState>();

        public List<MLibraryV2.MImage> Images { get; private set; } = new List<MLibraryV2.MImage>();

        public int Count { get { return Images == null ? 0 : Images.Count; } }

        /// <summary>数据文件（.MIZ）路径。</summary>
        public string FileName
        {
            get { return _mizPath; }
            set { _mizPath = value; }
        }

        /// <summary>Mir3 没有动作帧段，返回可写空 FrameSet（写盘时忽略）。</summary>
        public FrameSet Frames
        {
            get { return _frames; }
            set { _frames = value ?? new FrameSet(); }
        }

        public Mir3Library(string filename)
        {
            ResolvePaths(filename);
            Load();
        }

        // ---------------- 成对文件定位 ----------------

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
                    if (e.Equals(".MIX", StringComparison.OrdinalIgnoreCase)) _mixPath = f;
                    else if (e.Equals(".MIZ", StringComparison.OrdinalIgnoreCase)) _mizPath = f;
                }
            }

            if (_mizPath == null) _mizPath = Path.ChangeExtension(full, ".MIZ");
            if (_mixPath == null) _mixPath = Path.ChangeExtension(full, ".MIX");
        }

        // ---------------- 生命周期 ----------------

        private void Load()
        {
            Images = new List<MLibraryV2.MImage>();
            _state = new List<EntryState>();

            bool hasMix = File.Exists(_mixPath);
            bool hasMiz = File.Exists(_mizPath);

            if (!hasMix && !hasMiz)
                return;   // 全新空库（Save 时按空容器写出）

            if (!hasMix || !hasMiz)
                throw new InvalidDataException(
                    "Mir3 资源库必须成对存在同名 .MIX 与 .MIZ 文件。" + Environment.NewLine +
                    "已找到：" + (hasMix ? _mixPath : _mizPath) + Environment.NewLine +
                    "缺失：" + (hasMix ? _mizPath : _mixPath));

            EnsureContainer();
            BuildImages();
        }

        private void EnsureContainer()
        {
            if (_container != null) return;

            if (File.Exists(_mixPath) && File.Exists(_mizPath))
                _container = Mir3Container.Parse(File.ReadAllBytes(_mixPath), File.ReadAllBytes(_mizPath));
            else
                _container = BuildEmptyContainer();
        }

        private static Mir3Container BuildEmptyContainer()
        {
            Mir3Container c = new Mir3Container();
            Buffer.BlockCopy(Mir3Container.SignatureA, 0, c.MixSignature, 0, Mir3Container.SignatureLength);
            Buffer.BlockCopy(Mir3Container.SignatureA, 0, c.MizSignature, 0, Mir3Container.SignatureLength);
            c.MixDeclaredCount = 0;
            c.MizDeclaredCount = 0;
            c.MixUnknown = 0;
            c.MizUnknown = 0;
            c.Entries = Array.Empty<int>();
            c.EntryFrameIndex = Array.Empty<int>();
            c.Frames = new List<Mir3FrameRecord>();
            return c;
        }

        private void BuildImages()
        {
            int nt = _container.Entries.Length;
            Images = new List<MLibraryV2.MImage>(nt);
            _state = new List<EntryState>(nt);

            for (int i = 0; i < nt; i++)
            {
                int fi = _container.EntryFrameIndex[i];
                EntryState st = new EntryState { Frame = fi };

                if (fi < 0)
                {
                    Images.Add(new MLibraryV2.MImage(Array.Empty<byte>(), 0, 0));
                    st.PayloadRef = Array.Empty<byte>();
                }
                else
                {
                    Mir3FrameRecord fr = _container.Frames[fi];
                    MLibraryV2.MImage mi = new MLibraryV2.MImage(fr.Payload, fr.Width, fr.Height)
                    {
                        X = fr.OffsetX,
                        Y = fr.OffsetY,
                        Length = fr.Payload != null ? fr.Payload.Length : 0,
                        HasMask = false,
                    };
                    Images.Add(mi);
                    st.PayloadRef = fr.Payload;
                    st.W = fr.Width; st.H = fr.Height; st.X = fr.OffsetX; st.Y = fr.OffsetY;
                }
                _state.Add(st);
            }
            // 位图全部按需解码（EnsureTexture）：几十万帧的库一次性解码会耗尽 GDI 句柄
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
            mi.TextureValid = false;
        }

        // ---------------- 解码（按需） ----------------

        /// <summary>按需解码：裸 zlib → RGB565 → 32bpp BGRA（v==0 透明，行序自底向上，stride=W+(W&amp;1)）。</summary>
        public void EnsureTexture(int index)
        {
            if (Images == null || index < 0 || index >= Images.Count) return;

            MLibraryV2.MImage mi = Images[index];
            if (mi == null || mi.TextureValid || mi.Image != null) return;
            if (!MLibraryV2.Load) return;
            if (mi.Width < 1 || mi.Height < 1) return;
            if (mi.FBytes == null || mi.FBytes.Length == 0) return;

            try
            {
                mi.Image = DecodeFrames(mi.FBytes, mi.Width, mi.Height);
                mi.TextureValid = mi.Image != null;
            }
            catch
            {
                // 单帧损坏不应导致整库无法浏览
                mi.Image = null;
                mi.Preview = null;
            }
        }

        private static Bitmap DecodeFrames(byte[] zlibPayload, int w, int h)
        {
            byte[] raw = Mir3Container.ZlibDecompress(zlibPayload);
            int stride = w + (w & 1);
            int rowBytes = stride * 2;

            Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] dst = new byte[bd.Stride * h];
                for (int y = 0; y < h; y++)
                {
                    int srcRow = h - 1 - y;            // 文件第 0 行 = 图像最下面一行
                    int srcBase = srcRow * rowBytes;
                    int dstBase = y * bd.Stride;
                    for (int x = 0; x < w; x++)
                    {
                        byte b, g, r, a;
                        int si = srcBase + x * 2;
                        if (si + 1 >= raw.Length) { b = g = r = 0; a = 0; }
                        else
                        {
                            ushort v = (ushort)(raw[si] | (raw[si + 1] << 8));
                            if (v == 0) { b = g = r = 0; a = 0; }
                            else
                            {
                                int r5 = (v >> 11) & 0x1F;
                                int g6 = (v >> 5) & 0x3F;
                                int b5 = v & 0x1F;
                                r = (byte)((r5 << 3) | (r5 >> 2));
                                g = (byte)((g6 << 2) | (g6 >> 4));
                                b = (byte)((b5 << 3) | (b5 >> 2));
                                a = 255;
                            }
                        }
                        int di = dstBase + x * 4;
                        dst[di] = b; dst[di + 1] = g; dst[di + 2] = r; dst[di + 3] = a;
                    }
                }
                Marshal.Copy(dst, 0, bd.Scan0, dst.Length);
            }
            finally
            {
                bmp.UnlockBits(bd);
            }
            return bmp;
        }

        /// <summary>编码：32bpp BGRA → RGB565（底行起始、stride 按 4 字节对齐、A==0 或黑色 → 0x0000）→ 裸 zlib。</summary>
        private static byte[] EncodeBitmap(Bitmap image, bool removeBlack)
        {
            int w = image.Width, h = image.Height;
            int stride = w + (w & 1);
            int rowBytes = stride * 2;
            byte[] raw = new byte[rowBytes * h];

            BitmapData bd = image.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] src = new byte[bd.Stride * h];
                Marshal.Copy(bd.Scan0, src, 0, src.Length);
                for (int y = 0; y < h; y++)
                {
                    int srcBase = y * bd.Stride;
                    int dstBase = (h - 1 - y) * rowBytes;
                    for (int x = 0; x < w; x++)
                    {
                        int si = srcBase + x * 4;
                        byte b = src[si], g = src[si + 1], r = src[si + 2], a = src[si + 3];
                        ushort v;
                        if (a == 0) v = 0;
                        else if (removeBlack && b == 0 && g == 0 && r == 0) v = 0;
                        else v = (ushort)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));
                        int di = dstBase + x * 2;
                        raw[di] = (byte)(v & 0xFF);
                        raw[di + 1] = (byte)((v >> 8) & 0xFF);
                    }
                }
            }
            finally
            {
                image.UnlockBits(bd);
            }
            return Mir3Container.ZlibCompress(raw);
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

        private MLibraryV2.MImage CreateFromBitmap(Bitmap image, short x, short y, bool removeBlack)
        {
            if (image == null) return new MLibraryV2.MImage(Array.Empty<byte>(), 0, 0) { X = x, Y = y };
            byte[] z = EncodeBitmap(image, removeBlack);
            return new MLibraryV2.MImage(z, (short)image.Width, (short)image.Height) { X = x, Y = y };
        }

        public void AddImage(Bitmap image, short x, short y, bool removeBlack = true)
        {
            Images.Add(CreateFromBitmap(image, x, y, removeBlack));
            _state.Add(new EntryState { Frame = -1 });
        }

        public void AddImage(Bitmap image, Bitmap MaskImage, short x, short y, bool removeBlack = true)
        {
            // Mir3 无遮罩层，忽略 MaskImage
            AddImage(image, x, y, removeBlack);
        }

        public void InsertImage(int index, Bitmap image, short x, short y, bool removeBlack = true)
        {
            if (index < 0) index = 0;
            if (index > Images.Count) index = Images.Count;
            Images.Insert(index, CreateFromBitmap(image, x, y, removeBlack));
            _state.Insert(index, new EntryState { Frame = -1 });
        }

        public void ReplaceImage(int index, Bitmap image, short xOffset, short yOffset, bool removeBlack = true)
        {
            if (Images == null || index < 0 || index >= Images.Count) return;
            MLibraryV2.MImage mImage = CreateFromBitmap(image, xOffset, yOffset, removeBlack);
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
                return;
            }

            if (index < 0 || index >= Images.Count) return;
            DisposeImage(Images[index]);
            Images.RemoveAt(index);
            if (index < _state.Count) _state.RemoveAt(index);
        }

        public void RemoveBlanks(bool safe = false)
        {
            if (Images == null) return;
            for (int i = Images.Count - 1; i >= 0; i--)
            {
                MLibraryV2.MImage mi = Images[i];
                if (mi == null) continue;
                if (mi.FBytes == null || mi.FBytes.Length <= 24)
                {
                    if (!safe) RemoveImage(i);
                    else if (mi.X == 0 && mi.Y == 0) RemoveImage(i);
                }
            }
        }

        // ---------------- 保存 ----------------

        public void Save()
        {
            if (string.IsNullOrEmpty(_mizPath) && string.IsNullOrEmpty(_mixPath)) return;
            EnsureContainer();
            SyncToContainer();

            if (!string.IsNullOrEmpty(_mizPath)) File.WriteAllBytes(_mizPath, _container.RebuildMiz());
            if (!string.IsNullOrEmpty(_mixPath)) File.WriteAllBytes(_mixPath, _container.RebuildMix());
        }

        /// <summary>
        /// 把帧模型回写到容器：未改动帧复用原始 zlib 字节与原帧头（含 Resv），
        /// 只对修改/新增/删除做最小改动。
        /// </summary>
        private void SyncToContainer()
        {
            List<Mir3FrameRecord> srcFrames = _container.Frames;
            int origCount = srcFrames.Count;
            int n = Images.Count;

            // 原始引用计数（用于判断帧是否被多条索引共享 / 是否原本就是孤立帧）
            int[] origRef = new int[origCount];
            for (int i = 0; i < n && i < _state.Count; i++)
            {
                int fi = _state[i].Frame;
                if (fi >= 0 && fi < origCount) origRef[fi]++;
            }

            List<Mir3FrameRecord> work = new List<Mir3FrameRecord>(origCount + 8);
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
                    frameOfEntry[i] = fi;   // 完全未改：原样保留（含共享帧 / 孤立帧引用）
                    continue;
                }

                if (blank)
                {
                    frameOfEntry[i] = -1;   // 槽位改空
                    continue;
                }

                bool exclusive = fi >= 0 && fi < origCount && origRef[fi] == 1;
                if (exclusive)
                {
                    // 独占帧：原位更新（仅坐标变化时恰好只改帧头 +4/+6）
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
                    // 共享帧改动 / 新增帧：追加一条新帧记录
                    work.Add(NewFrame(mi, fi));
                    frameOfEntry[i] = work.Count - 1;
                }
            }

            // 哪些帧保留：被引用者、或原本就是孤立帧（必须保留）
            bool[] used = new bool[work.Count];
            for (int i = 0; i < n; i++)
                if (frameOfEntry[i] >= 0) used[frameOfEntry[i]] = true;

            int[] keepMap = new int[work.Count];
            List<Mir3FrameRecord> finalFrames = new List<Mir3FrameRecord>(work.Count);
            for (int k = 0; k < work.Count; k++)
            {
                bool keep = used[k] || (k < origCount && origRef[k] == 0);
                if (keep) { keepMap[k] = finalFrames.Count; finalFrames.Add(work[k]); }
                else keepMap[k] = -1;
            }

            int[] newEntryFrame = new int[n];
            for (int i = 0; i < n; i++)
                newEntryFrame[i] = frameOfEntry[i] >= 0 ? keepMap[frameOfEntry[i]] : -1;

            _container.Frames = finalFrames;
            _container.EntryFrameIndex = newEntryFrame;
            _container.Entries = new int[n];
            _container.RefreshEntriesFromFrameIndex();

            // 重新基线化，供多次保存使用
            _state = new List<EntryState>(n);
            for (int i = 0; i < n; i++)
            {
                EntryState st = new EntryState { Frame = newEntryFrame[i] };
                int fi = newEntryFrame[i];
                if (fi >= 0 && fi < finalFrames.Count)
                {
                    Mir3FrameRecord fr = finalFrames[fi];
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

        private Mir3FrameRecord NewFrame(MLibraryV2.MImage mi, int srcFrameIdx)
        {
            uint resv;
            List<Mir3FrameRecord> fr = _container.Frames;
            if (srcFrameIdx >= 0 && srcFrameIdx < fr.Count) resv = fr[srcFrameIdx].Reserved;
            else resv = (fr.Count > 0) ? fr[0].Reserved : 0u;

            return new Mir3FrameRecord
            {
                Width = mi.Width,
                Height = mi.Height,
                OffsetX = mi.X,
                OffsetY = mi.Y,
                Reserved = resv,
                Payload = mi.FBytes ?? Array.Empty<byte>(),
                Modified = true,
            };
        }
    }
}
