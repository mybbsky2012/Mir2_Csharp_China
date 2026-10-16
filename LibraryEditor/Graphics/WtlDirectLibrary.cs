using ManagedSquish;
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
    /// WEMADE .WTL（ILIB）"直接打开（不转换）"资源库：原地读写 .wtl，不产出 .Lib。
    ///
    /// 架构（照抄 Mir3Library）：打开时只解析 32 字节头 + 索引表 + 帧记录头，
    /// 位图按需解码（EnsureTexture）；未改动帧的负载与容器解析出的原始字节是同一引用，
    /// Save() 时原样回写 —— "打开 → 未改动 → 保存" 与原始文件字节级一致
    /// （SHA-256 相同，含版本串填充、@0x18 未知名、v1.3 表尾垫字节、负载对齐填充）。
    ///
    /// 关键格式事实（全语料实测）：
    ///   v2.0 图像负载 = 裸 DEFLATE；网格 w = NextPow2(RoundUp4(W)), h = NextPow2(RoundUp4(H))。
    ///     ImageTextureType 0/1=DXT1(w*h/2)、3=DXT3(w*h)、5=DXT5(w*h)。
    ///   v2.0 且 (MaskTextureType & 0x80) != 0 → 负载实为 raw 32bpp BGRA（w*h*4，自顶向下），
    ///     取左上 W×H；CBIcons/Inventory/Storeitem 等图标库属于这一类。
    ///   v1.3 图像负载 = 不压缩的 "8 字节计数表 + 8 字节 DXT1 块" 流，位图尺寸 = RoundUp4(W/H)。
    ///   独立掩码段（v2.0 Unknown2!=0 / v1.3 ShadowByte bit7）按同规则解码；改帧时原样保留原始掩码字节。
    /// </summary>
    public sealed class WtlDirectLibrary : ILibrary
    {
        private string _wtlPath;
        private readonly WtlContainer _container;
        private readonly bool _isNewVersion;
        private FrameSet _frames = new FrameSet();

        /// <summary>与 Images 一一对应的"上次保存时"状态，用于变更检测与字节级回写。</summary>
        private sealed class EntryState
        {
            public int Record = -1;         // 对应的容器记录下标；-1 = 新帧/空槽
            public byte[] PayloadRef;       // 上次保存时的图像负载引用
            public byte[] MaskRef;          // 上次保存时的独立掩码负载引用
            public short W, H, X, Y;

            // 编解码参数（惰性解码与保存共用）
            public byte Unknown1;           // v2.0 保留字节
            public byte ImageTextureType;   // v2.0 DXT 类型
            public byte MaskTextureType;    // v2.0（bit0=独立掩码段，bit7=raw32）
            public byte ShadowByte;         // v1.3（bit7=独立掩码段）
        }

        private List<EntryState> _state = new List<EntryState>();

        public List<MLibraryV2.MImage> Images { get; private set; } = new List<MLibraryV2.MImage>();

        public int Count { get { return Images == null ? 0 : Images.Count; } }

        /// <summary>WTL 文件路径（.wtl）。</summary>
        public string FileName
        {
            get { return _wtlPath; }
            set { _wtlPath = string.IsNullOrEmpty(value) ? value : Path.ChangeExtension(value, ".wtl"); }
        }

        /// <summary>WTL 没有动作帧段，返回可写空 FrameSet（写盘时忽略）。</summary>
        public FrameSet Frames
        {
            get { return _frames; }
            set { _frames = value ?? new FrameSet(); }
        }

        // ---------------- 识别 / 构造 ----------------

        /// <summary>头部满足 WTL 魔数（ILIB v1.3/v2.0 前缀）且文件存在时可打开。</summary>
        public static bool CanOpen(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return false;
            try
            {
                if (!File.Exists(filename)) return false;
                using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] head = new byte[22];
                    int n = 0;
                    while (n < head.Length)
                    {
                        int r = fs.Read(head, n, head.Length - n);
                        if (r <= 0) break;
                        n += r;
                    }
                    if (n < 22) return false;
                    return WtlContainer.IsWtl(head);
                }
            }
            catch
            {
                return false;
            }
        }

        public WtlDirectLibrary(string filename)
        {
            if (string.IsNullOrEmpty(filename))
                throw new InvalidDataException("WTL 打开失败：文件名为空。");

            string resolved = ResolvePath(filename);
            if (resolved == null)
                throw new InvalidDataException(
                    "WTL 打开失败：找不到文件 " + filename + Environment.NewLine +
                    "（已按大小写不敏感在所在目录查找同名 .wtl）");
            _wtlPath = resolved;

            try
            {
                _container = WtlContainer.Parse(File.ReadAllBytes(_wtlPath));
            }
            catch (EliteFormatException ex)
            {
                throw new InvalidDataException(
                    "WTL 打开失败：" + Path.GetFileName(_wtlPath) + Environment.NewLine +
                    "原因：" + ex.Message + Environment.NewLine +
                    "本编辑器支持版本串为 \"ILIB v1.3-WEMADE\" 或 \"ILIB v2.0-WEMADE\" 的 WTL。", ex);
            }

            _isNewVersion = _container.IsNewVersion;
            BuildImages();
        }

        /// <summary>按大小写不敏感解析 .wtl 扩展名；找不到返回 null。</summary>
        private static string ResolvePath(string filename)
        {
            string full;
            try { full = Path.GetFullPath(filename); }
            catch { return null; }

            if (File.Exists(full)) return full;

            string dir = Path.GetDirectoryName(full);
            string stem = Path.GetFileNameWithoutExtension(full);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                foreach (string f in Directory.EnumerateFiles(dir))
                {
                    if (!string.Equals(Path.GetFileNameWithoutExtension(f), stem, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (Path.GetExtension(f).Equals(".wtl", StringComparison.OrdinalIgnoreCase))
                        return f;
                }
            }

            // 尝试补 .wtl 扩展名
            string guess = Path.ChangeExtension(full, ".wtl");
            return File.Exists(guess) ? guess : null;
        }

        private void BuildImages()
        {
            int n = _container.EntryRecord.Length;
            Images = new List<MLibraryV2.MImage>(n);
            _state = new List<EntryState>(n);

            for (int i = 0; i < n; i++)
            {
                int ri = _container.EntryRecord[i];
                EntryState st = new EntryState { Record = ri };

                if (ri < 0 || ri >= _container.Records.Count)
                {
                    // 空帧槽（索引 0）：空 MImage；保存时索引仍写 0
                    Images.Add(new MLibraryV2.MImage(Array.Empty<byte>(), 0, 0));
                    st.PayloadRef = Array.Empty<byte>();
                    st.Record = -1;
                    _state.Add(st);
                    continue;
                }

                WtlFrameRecord fr = _container.Records[ri];
                MLibraryV2.MImage mi = new MLibraryV2.MImage(fr.Payload, fr.Width, fr.Height)
                {
                    X = fr.X,
                    Y = fr.Y,
                    ShadowX = fr.ShadowX,
                    ShadowY = fr.ShadowY,
                    Length = fr.StoredLength,
                    Shadow = _isNewVersion ? (byte)0 : fr.ShadowByte,
                    HasMask = fr.HasSeparateMask,
                    MaskFBytes = fr.MaskPayload,
                    MaskLength = fr.MaskStoredLength,
                };

                if (!_isNewVersion && fr.HasSeparateMask)
                {
                    mi.MaskWidth = fr.MaskWidth;
                    mi.MaskHeight = fr.MaskHeight;
                    mi.MaskX = fr.MaskX;
                    mi.MaskY = fr.MaskY;
                }

                st.PayloadRef = fr.Payload;
                st.MaskRef = fr.MaskPayload;
                st.W = fr.Width; st.H = fr.Height; st.X = fr.X; st.Y = fr.Y;
                st.Unknown1 = fr.Unknown1;
                st.ImageTextureType = fr.ImageTextureType;
                st.MaskTextureType = fr.MaskTextureType;
                st.ShadowByte = fr.ShadowByte;

                Images.Add(mi);
                _state.Add(st);
            }
            // 位图全部按需解码（EnsureTexture）
        }

        // ---------------- 生命周期 ----------------

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

        // ---------------- 解码（按需） ----------------

        /// <summary>
        /// 按需解码。单帧损坏只让该帧 Image=null，绝不影响其它帧或整库可用性。
        /// </summary>
        public void EnsureTexture(int index)
        {
            if (Images == null || index < 0 || index >= Images.Count) return;

            MLibraryV2.MImage mi = Images[index];
            if (mi == null || mi.TextureValid || mi.Image != null) return;
            if (!MLibraryV2.Load) return;
            if (mi.Width < 1 || mi.Height < 1) return;
            if (mi.FBytes == null || mi.FBytes.Length == 0) return;

            EntryState st = (index < _state.Count) ? _state[index] : null;

            try
            {
                if (_isNewVersion)
                {
                    byte maskType = st != null ? st.MaskTextureType : (byte)0;
                    byte imgType = st != null ? st.ImageTextureType : (byte)1;
                    if ((maskType & 0x80) != 0)
                        mi.Image = DecodeV2Raw32(mi.FBytes, mi.Width, mi.Height);
                    else
                        mi.Image = DecodeV2Dxt(mi.FBytes, mi.Width, mi.Height, imgType);
                }
                else
                {
                    mi.Image = DecodeV1(mi.FBytes, mi.Width, mi.Height);
                }
                mi.TextureValid = mi.Image != null;
            }
            catch
            {
                mi.Image = null;
                mi.Preview = null;
            }

            // 独立掩码段：解不出来就优雅降级（MaskImage=null, HasMask=false），不影响图像
            if (mi.Image != null && mi.MaskFBytes != null && mi.MaskFBytes.Length > 0)
            {
                try
                {
                    byte maskType = st != null ? st.MaskTextureType : (byte)1;
                    byte imgType = st != null ? st.ImageTextureType : (byte)1;
                    if (_isNewVersion)
                    {
                        if ((maskType & 0x80) != 0)
                            mi.MaskImage = DecodeV2Raw32(mi.MaskFBytes, mi.Width, mi.Height);
                        else
                        {
                            byte t = (byte)(maskType & 0x7F);
                            if (t != 1 && t != 3 && t != 5) t = imgType;
                            mi.MaskImage = DecodeV2Dxt(mi.MaskFBytes, mi.Width, mi.Height, t);
                        }
                    }
                    else
                    {
                        short mw = mi.MaskWidth > 0 ? mi.MaskWidth : mi.Width;
                        short mh = mi.MaskHeight > 0 ? mi.MaskHeight : mi.Height;
                        mi.MaskImage = DecodeV1(mi.MaskFBytes, mw, mh);
                    }
                }
                catch
                {
                    mi.MaskImage = null;
                }

                if (mi.MaskImage == null) mi.HasMask = false;
            }
        }

        // ---- v2.0：DXT（裸 DEFLATE → Squish 解压 → R/B 交换 → 裁剪 W×H）----

        private static unsafe Bitmap DecodeV2Dxt(byte[] payload, int w0, int h0, byte textureType)
        {
            int w = NextPow2(RoundUp4(w0));
            int h = NextPow2(RoundUp4(h0));
            SquishFlags type = DxtFlags(textureType);

            byte[] blocks = Ionic.Zlib.DeflateStream.UncompressBuffer(payload);
            int need = (type == SquishFlags.Dxt1) ? w * h / 2 : w * h;
            if (blocks.Length < need)
                throw new InvalidDataException("WTL DXT 负载长度不足: " + blocks.Length + " < " + need);

            Bitmap full = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData data = full.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                fixed (byte* source = blocks)
                    Squish.DecompressImage(data.Scan0, w, h, (IntPtr)source, type);

                // Squish 输出 RGBA，GDI 需要 BGRA（与 Graphics/WTLLibrary.cs 的 DecompressV2Texture 一致）
                byte* dest = (byte*)data.Scan0;
                int cap = w * h * 4;
                for (int i = 0; i + 3 < cap; i += 4)
                {
                    byte b = dest[i];
                    dest[i] = dest[i + 2];
                    dest[i + 2] = b;
                }
            }
            finally
            {
                full.UnlockBits(data);
            }

            Bitmap crop = full.Clone(new Rectangle(0, 0, w0, h0), PixelFormat.Format32bppArgb);
            full.Dispose();
            return crop;
        }

        // ---- v2.0 bit7：raw 32bpp BGRA（裸 DEFLATE → 左上 W×H 直接拷贝，自顶向下）----

        private static Bitmap DecodeV2Raw32(byte[] payload, int w0, int h0)
        {
            int w = NextPow2(RoundUp4(w0));
            int h = NextPow2(RoundUp4(h0));

            byte[] raw = Ionic.Zlib.DeflateStream.UncompressBuffer(payload);
            if (raw.Length < w * h * 4)
                throw new InvalidDataException("WTL raw32 负载长度不足: " + raw.Length + " < " + (w * h * 4));

            Bitmap bmp = new Bitmap(w0, h0, PixelFormat.Format32bppArgb);
            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w0, h0), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] dst = new byte[bd.Stride * h0];
                for (int y = 0; y < h0; y++)
                    Buffer.BlockCopy(raw, y * w * 4, dst, y * bd.Stride, w0 * 4);
                Marshal.Copy(dst, 0, bd.Scan0, dst.Length);
            }
            finally
            {
                bmp.UnlockBits(bd);
            }
            return bmp;
        }

        // ---- v1.3：计数表 + DXT1 块流（逐行移植 Graphics/WTLLibrary.cs 的 DecompressV1Texture）----

        private static unsafe Bitmap DecodeV1(byte[] payload, int w0, int h0)
        {
            const int blockSize = 8;
            int offset = 0, blockOffset = 0;
            int tWidth = 2;
            while (tWidth < w0) tWidth *= 2;

            int outputWidth = RoundUp4(w0);
            int outputHeight = RoundUp4(h0);
            if (outputWidth < 4 || outputHeight < 4)
                throw new InvalidDataException("WTL v1.3 帧尺寸过小: " + w0 + "×" + h0);

            byte[] bytes = payload;
            if (bytes == null || bytes.Length == 0)
                throw new InvalidDataException("WTL v1.3 负载为空");

            Bitmap output = new Bitmap(outputWidth, outputHeight, PixelFormat.Format32bppArgb);
            bool truncated = false;
            BitmapData data = output.LockBits(new Rectangle(0, 0, outputWidth, outputHeight),
                                              ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte* pixels = (byte*)data.Scan0;
                int cap = outputWidth * outputHeight * 4;
                int currentx = 0;
                byte[] countList = new byte[8];

                while (blockOffset < bytes.Length)
                {
                    // 读 8 字节计数表：4 组 (跳过量, 块数) 交替
                    for (int i = 0; i < 8; i++)
                    {
                        if (blockOffset >= bytes.Length) { truncated = true; break; }
                        countList[i] = bytes[blockOffset++];
                    }
                    if (truncated) break;

                    for (int i = 0; i < countList.Length; i++)
                    {
                        int count = countList[i];

                        if (i % 2 == 0)
                        {
                            if (currentx >= tWidth) currentx -= tWidth;
                            for (int off = 0; off < count; off++)
                            {
                                if (currentx < outputWidth) offset++;
                                currentx += 4;
                                if (currentx >= tWidth) currentx -= tWidth;
                            }
                            continue;
                        }

                        for (int c = 0; c < count; c++)
                        {
                            if (blockOffset >= bytes.Length) break;

                            byte[] newPixels = new byte[64];
                            byte[] block = new byte[blockSize];
                            Array.Copy(bytes, blockOffset, block, 0, blockSize);
                            blockOffset += blockSize;
                            DecompressBlock(newPixels, block);

                            int pixelOffset = 0;
                            for (int py = 0; py < 4; py++)
                            {
                                for (int px = 0; px < 4; px++)
                                {
                                    int blockx = offset % (outputWidth / 4);
                                    int blocky = offset / (outputWidth / 4);
                                    int x = blockx * 4;
                                    int y = blocky * 4;
                                    int destPixel = ((y + py) * outputWidth) * 4 + (x + px) * 4;
                                    if (destPixel < 0 || destPixel + 4 > cap) { pixelOffset += 4; continue; }
                                    pixels[destPixel] = newPixels[pixelOffset];
                                    pixels[destPixel + 1] = newPixels[pixelOffset + 1];
                                    pixels[destPixel + 2] = newPixels[pixelOffset + 2];
                                    pixels[destPixel + 3] = newPixels[pixelOffset + 3];
                                    pixelOffset += 4;
                                }
                            }
                            offset++;
                            if (currentx >= outputWidth) currentx -= outputWidth;
                            currentx += 4;
                        }
                    }
                }
            }
            finally
            {
                output.UnlockBits(data);
            }
            if (truncated)
            {
                output.Dispose();
                throw new InvalidDataException("WTL v1.3 计数表被截断");
            }
            return output;
        }

        /// <summary>DXT1 块解码（与 WTLLibrary.DecompressBlock 完全一致，输出 BGRA，纯黑改 (1,1,1) 防透明误判）。</summary>
        private static void DecompressBlock(byte[] newPixels, byte[] block)
        {
            byte[] codes = new byte[16];
            int a = Unpack(block, 0, codes, 0);
            int b = Unpack(block, 2, codes, 4);

            for (int i = 0; i < 3; i++)
            {
                int c = codes[i];
                int d = codes[4 + i];
                if (a <= b)
                {
                    codes[8 + i] = (byte)((c + d) / 2);
                    codes[12 + i] = 0;
                }
                else
                {
                    codes[8 + i] = (byte)((2 * c + d) / 3);
                    codes[12 + i] = (byte)((c + 2 * d) / 3);
                }
            }

            codes[8 + 3] = 255;
            codes[12 + 3] = (a <= b) ? (byte)0 : (byte)255;
            for (int i = 0; i < 4; i++)
            {
                if (codes[i * 4] == 0 && codes[i * 4 + 1] == 0 && codes[i * 4 + 2] == 0 && codes[i * 4 + 3] == 255)
                {
                    codes[i * 4] = 1;
                    codes[i * 4 + 1] = 1;
                    codes[i * 4 + 2] = 1;
                }
            }

            byte[] indices = new byte[16];
            for (int i = 0; i < 4; i++)
            {
                byte packed = block[4 + i];
                indices[0 + i * 4] = (byte)(packed & 0x3);
                indices[1 + i * 4] = (byte)((packed >> 2) & 0x3);
                indices[2 + i * 4] = (byte)((packed >> 4) & 0x3);
                indices[3 + i * 4] = (byte)((packed >> 6) & 0x3);
            }

            for (int i = 0; i < 16; i++)
            {
                int off = 4 * indices[i];
                newPixels[4 * i] = codes[off];
                newPixels[4 * i + 1] = codes[off + 1];
                newPixels[4 * i + 2] = codes[off + 2];
                newPixels[4 * i + 3] = codes[off + 3];
            }
        }

        private static int Unpack(byte[] packed, int srcOffset, byte[] colour, int dstOffset)
        {
            int value = packed[srcOffset] | (packed[srcOffset + 1] << 8);
            byte red = (byte)((value >> 11) & 0x1F);
            byte green = (byte)((value >> 5) & 0x3F);
            byte blue = (byte)(value & 0x1F);

            colour[2 + dstOffset] = (byte)((red << 3) | (red >> 2));
            colour[1 + dstOffset] = (byte)((green << 2) | (green >> 4));
            colour[0 + dstOffset] = (byte)((blue << 3) | (blue >> 2));
            colour[3 + dstOffset] = 255;
            return value;
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

        /// <summary>
        /// 编码一张位图为一帧负载。模板为空则用默认类型（v2.0=DXT1，v1.3=计数表 DXT1）。
        /// </summary>
        private MLibraryV2.MImage CreateFromBitmap(Bitmap image, Bitmap maskImage, short x, short y,
                                                   bool removeBlack, EntryState template, out EntryState created)
        {
            if (image == null)
            {
                created = new EntryState { Record = -1, PayloadRef = Array.Empty<byte>() };
                return new MLibraryV2.MImage(Array.Empty<byte>(), 0, 0) { X = x, Y = y };
            }

            short w = (short)image.Width;
            short h = (short)image.Height;

            byte imgType = template != null ? template.ImageTextureType : (byte)1;
            byte maskType = template != null ? template.MaskTextureType : (byte)0;
            byte unknown1 = template != null ? template.Unknown1 : (byte)0;
            byte shadowByte = template != null ? template.ShadowByte : (byte)0;

            byte[] payload;
            byte[] maskPayload = null;

            if (_isNewVersion)
            {
                bool raw = (maskType & 0x80) != 0;
                payload = EncodeV2(image, imgType, raw, removeBlack);

                if (maskImage != null)
                {
                    if (raw)
                    {
                        maskType = (byte)(0x80 | 0x01);   // raw 图像 + 独立 raw 掩码段
                        maskPayload = EncodeV2(maskImage, 1, true, removeBlack);
                    }
                    else
                    {
                        maskType = (byte)0x01;
                        maskPayload = EncodeV2(maskImage, 1, false, removeBlack);
                    }
                }
                else
                {
                    maskType = (byte)(maskType & 0xFE);   // 无独立掩码段（清 bit0，保留 bit7=raw32 标志）
                }
            }
            else
            {
                payload = EncodeV1(image, removeBlack);
                if (maskImage != null)
                {
                    maskPayload = EncodeV1(maskImage, removeBlack);
                    shadowByte |= 0x80;
                }
                else
                {
                    shadowByte &= 0x7F;
                }
            }

            MLibraryV2.MImage mi = new MLibraryV2.MImage(payload, w, h)
            {
                X = x,
                Y = y,
                ShadowX = 0,
                ShadowY = 0,
                Length = payload.Length,
                Shadow = _isNewVersion ? (byte)0 : shadowByte,
                HasMask = maskPayload != null,
                MaskFBytes = maskPayload,
                MaskLength = maskPayload != null ? maskPayload.Length : 0,
            };

            if (!_isNewVersion && maskPayload != null)
            {
                mi.MaskWidth = w;
                mi.MaskHeight = h;
                mi.MaskX = x;
                mi.MaskY = y;
            }

            created = new EntryState
            {
                Record = template != null ? template.Record : -1,
                PayloadRef = payload,
                MaskRef = maskPayload,
                W = w, H = h, X = x, Y = y,
                Unknown1 = unknown1,
                ImageTextureType = imgType,
                MaskTextureType = maskType,
                ShadowByte = shadowByte,
            };
            return mi;
        }

        public void AddImage(Bitmap image, short x, short y, bool removeBlack = true)
        {
            AddImage(image, null, x, y, removeBlack);
        }

        public void AddImage(Bitmap image, Bitmap MaskImage, short x, short y, bool removeBlack = true)
        {
            EntryState st;
            MLibraryV2.MImage mi = CreateFromBitmap(image, MaskImage, x, y, removeBlack, null, out st);
            Images.Add(mi);
            _state.Add(st);
        }

        public void InsertImage(int index, Bitmap image, short x, short y, bool removeBlack = true)
        {
            if (index < 0) index = 0;
            if (index > Images.Count) index = Images.Count;
            EntryState st;
            MLibraryV2.MImage mi = CreateFromBitmap(image, null, x, y, removeBlack, null, out st);
            Images.Insert(index, mi);
            _state.Insert(index, st);
        }

        public void ReplaceImage(int index, Bitmap image, short xOffset, short yOffset, bool removeBlack = true)
        {
            if (Images == null || index < 0 || index >= Images.Count) return;

            EntryState template = (index < _state.Count) ? _state[index] : null;
            MLibraryV2.MImage old = Images[index];

            EntryState created;
            MLibraryV2.MImage mi = CreateFromBitmap(image, null, xOffset, yOffset, removeBlack, template, out created);

            // 保留原帧阴影偏移
            if (old != null)
            {
                mi.ShadowX = old.ShadowX;
                mi.ShadowY = old.ShadowY;
            }

            // 掩码未编辑：沿用基线引用，保存时原样回写原始掩码字节
            bool maskPreserved = template != null && template.MaskRef != null && image != null && template.Record >= 0;
            if (maskPreserved)
            {
                mi.MaskFBytes = template.MaskRef;
                mi.MaskLength = template.MaskRef.Length;
                mi.HasMask = true;
                if (!_isNewVersion && old != null)
                {
                    mi.MaskWidth = old.MaskWidth;
                    mi.MaskHeight = old.MaskHeight;
                    mi.MaskX = old.MaskX;
                    mi.MaskY = old.MaskY;
                }
            }

            // 变更检测基线必须保持"上次保存时"的旧值（新负载只挂在 MImage 上），
            // 否则未改动的引用比较会把本次编辑误判为无变化而在 Save 时丢弃。
            EntryState st = new EntryState
            {
                Record = created.Record,
                PayloadRef = template != null ? template.PayloadRef : created.PayloadRef,
                MaskRef = maskPreserved ? template.MaskRef : created.MaskRef,
                W = template != null ? template.W : created.W,
                H = template != null ? template.H : created.H,
                X = template != null ? template.X : created.X,
                Y = template != null ? template.Y : created.Y,
                Unknown1 = created.Unknown1,
                ImageTextureType = created.ImageTextureType,
                MaskTextureType = maskPreserved ? template.MaskTextureType : created.MaskTextureType,
                ShadowByte = maskPreserved ? template.ShadowByte : created.ShadowByte,
            };

            DisposeImage(old);
            Images[index] = mi;
            if (index < _state.Count) _state[index] = st;
            else _state.Insert(index, st);
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

        // ---------------- 编码 ----------------

        private static SquishFlags DxtFlags(byte textureType)
        {
            switch (textureType)
            {
                case 0:
                case 1: return SquishFlags.Dxt1;
                case 3: return SquishFlags.Dxt3;
                case 5: return SquishFlags.Dxt5;
                default:
                    throw new NotSupportedException("不支持的 WTL 纹理类型: " + textureType + "（仅支持 0/1/3/5）。");
            }
        }

        /// <summary>读位图为网格上的像素缓冲；raw=true 时写 BGRA，否则写 RGBA（Squish 输入）。</summary>
        private static byte[] BuildGridPixels(Bitmap bmp, int gw, int gh, bool raw, bool removeBlack)
        {
            byte[] px = new byte[gw * gh * 4];
            int w = bmp.Width, h = bmp.Height;

            BitmapData bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] src = new byte[bd.Stride * h];
                Marshal.Copy(bd.Scan0, src, 0, src.Length);
                for (int y = 0; y < h; y++)
                {
                    int sb = y * bd.Stride;
                    int db = y * gw * 4;
                    for (int x = 0; x < w; x++)
                    {
                        int si = sb + x * 4;
                        byte b = src[si], g = src[si + 1], r = src[si + 2], a = src[si + 3];
                        if (a == 0) { b = g = r = 0; }
                        else if (removeBlack && b == 0 && g == 0 && r == 0) a = 0;

                        int di = db + x * 4;
                        if (raw) { px[di] = b; px[di + 1] = g; px[di + 2] = r; }
                        else { px[di] = r; px[di + 1] = g; px[di + 2] = b; }
                        px[di + 3] = a;
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(bd);
            }
            return px;
        }

        private static byte[] Deflate(byte[] raw)
        {
            return Ionic.Zlib.DeflateStream.CompressBuffer(raw);
        }

        /// <summary>v2.0 编码：Squish 压缩（或 raw 32bpp）→ 裸 DEFLATE。</summary>
        private static unsafe byte[] EncodeV2(Bitmap bmp, byte textureType, bool raw, bool removeBlack)
        {
            int gw = NextPow2(RoundUp4(bmp.Width));
            int gh = NextPow2(RoundUp4(bmp.Height));
            if (gw < 1 || gh < 1) throw new NotSupportedException("WTL 编码失败：位图尺寸为 0。");

            byte[] px = BuildGridPixels(bmp, gw, gh, raw, removeBlack);
            if (raw) return Deflate(px);

            SquishFlags flags = DxtFlags(textureType);
            int storage = Squish.GetStorageRequirements(gw, gh, flags);
            if (storage <= 0) throw new NotSupportedException("WTL 编码失败：DXT 存储尺寸非法。");

            byte[] blocks = new byte[storage];
            fixed (byte* ps = px)
            fixed (byte* pd = blocks)
            {
                Squish.CompressImage((IntPtr)ps, gw, gh, (IntPtr)pd, flags);
            }
            return Deflate(blocks);
        }

        /// <summary>
        /// v1.3 编码：产出合法的 "8 字节计数表 + 8 字节 DXT1 块" 流。
        /// 计数表每 8 字节 = 4 组 (跳过量, 块数) 交替；这里跳过量恒为 0，块按光栅顺序排布。
        /// </summary>
        private static unsafe byte[] EncodeV1(Bitmap bmp, bool removeBlack)
        {
            int gw = RoundUp4(bmp.Width);
            int gh = RoundUp4(bmp.Height);
            int bw = gw / 4, bh = gh / 4;
            int total = bw * bh;
            if (total <= 0) throw new NotSupportedException("WTL v1.3 编码失败：位图尺寸为 0。");

            byte[] px = BuildGridPixels(bmp, gw, gh, false, removeBlack);

            // 逐 4×4 块压缩（raster 顺序）
            byte[][] blockData = new byte[total][];
            byte[] src4 = new byte[16 * 4];
            for (int idx = 0; idx < total; idx++)
            {
                int bx = (idx % bw) * 4;
                int by = (idx / bw) * 4;
                for (int py = 0; py < 4; py++)
                    Buffer.BlockCopy(px, ((by + py) * gw + bx) * 4, src4, py * 16, 16);

                byte[] blk = new byte[8];
                fixed (byte* ps = src4)
                fixed (byte* pd = blk)
                {
                    Squish.CompressImage((IntPtr)ps, 4, 4, (IntPtr)pd, SquishFlags.Dxt1);
                }
                blockData[idx] = blk;
            }

            // 组装：计数表 + 其块数据，循环直至写完
            using (MemoryStream ms = new MemoryStream(total * 9 + 16))
            {
                int remaining = total, idx = 0;
                while (remaining > 0)
                {
                    int[] pairs = new int[4];
                    int sum = 0;
                    for (int k = 0; k < 4 && sum < remaining; k++)
                    {
                        int c = Math.Min(255, remaining - sum);
                        pairs[k] = c;
                        sum += c;
                    }
                    for (int k = 0; k < 4; k++)
                    {
                        ms.WriteByte(0);
                        ms.WriteByte((byte)pairs[k]);
                    }
                    for (int k = 0; k < 4; k++)
                    {
                        for (int c = 0; c < pairs[k]; c++)
                        {
                            ms.Write(blockData[idx], 0, 8);
                            idx++;
                        }
                    }
                    remaining -= sum;
                }

                if (ms.Length > 0xFFFFFF)
                    throw new NotSupportedException("WTL v1.3 编码失败：负载超过 3 字节长度上限（" + ms.Length + " 字节）。");
                return ms.ToArray();
            }
        }

        // ---------------- 保存 ----------------

        public void Save()
        {
            if (string.IsNullOrEmpty(_wtlPath)) return;
            SyncToContainer();
            File.WriteAllBytes(_wtlPath, _container.Rebuild());
        }

        /// <summary>
        /// 把帧模型回写到容器：未改动帧复用原记录对象（保存时按 RawBytes 原样回写），
        /// 只对修改/新增/删除做最小改动。
        /// </summary>
        private void SyncToContainer()
        {
            int n = Images.Count;
            List<WtlFrameRecord> oldRecords = _container.Records;

            // 1) 标记"未改动、且原记录可原样保留"的记录
            bool[] kept = new bool[oldRecords.Count];
            bool[] unchanged = new bool[n];

            for (int i = 0; i < n; i++)
            {
                MLibraryV2.MImage mi = Images[i];
                if (i >= _state.Count) continue;
                EntryState st = _state[i];

                bool blank = mi == null || (mi.Width == 0 && mi.Height == 0 &&
                                            (mi.FBytes == null || mi.FBytes.Length == 0));
                bool payloadSame = st.Record >= 0 && st.Record < oldRecords.Count && mi != null
                                   && ReferenceEquals(mi.FBytes, st.PayloadRef);
                bool maskSame = mi != null && ReferenceEquals(mi.MaskFBytes, st.MaskRef);
                bool geomSame = mi != null && mi.Width == st.W && mi.Height == st.H &&
                                mi.X == st.X && mi.Y == st.Y;

                if (!blank && payloadSame && maskSame && geomSame)
                {
                    unchanged[i] = true;
                    kept[st.Record] = true;
                }
            }

            // 2) 新记录列表：先放原样保留的记录（保持数据区顺序），再放改动/新增记录
            List<WtlFrameRecord> newRecords = new List<WtlFrameRecord>(oldRecords.Count + 8);
            int[] oldToNew = new int[oldRecords.Count];
            for (int r = 0; r < oldRecords.Count; r++)
            {
                if (!kept[r]) { oldToNew[r] = -1; continue; }
                oldToNew[r] = newRecords.Count;
                newRecords.Add(oldRecords[r]);              // 同一对象：RawBytes 原样回写
            }

            int[] entryRecord = new int[n];
            for (int i = 0; i < n; i++)
            {
                MLibraryV2.MImage mi = Images[i];
                EntryState st = (i < _state.Count) ? _state[i] : new EntryState();

                bool blank = mi == null || (mi.Width == 0 && mi.Height == 0 &&
                                            (mi.FBytes == null || mi.FBytes.Length == 0));
                if (blank)
                {
                    entryRecord[i] = -1;
                    continue;
                }

                if (unchanged[i])
                {
                    entryRecord[i] = oldToNew[st.Record];
                    continue;
                }

                WtlFrameRecord rec = BuildRecord(i, mi, st);
                entryRecord[i] = newRecords.Count;
                newRecords.Add(rec);
            }

            _container.Records = newRecords;
            _container.EntryRecord = entryRecord;

            // 3) 重新基线化，供多次保存使用
            List<EntryState> newState = new List<EntryState>(n);
            for (int i = 0; i < n; i++)
            {
                MLibraryV2.MImage mi = Images[i];
                EntryState st = new EntryState { Record = entryRecord[i] };
                if (entryRecord[i] >= 0 && entryRecord[i] < newRecords.Count && mi != null)
                {
                    WtlFrameRecord fr = newRecords[entryRecord[i]];
                    st.PayloadRef = fr.Payload;
                    st.MaskRef = fr.MaskPayload;
                    st.W = mi.Width; st.H = mi.Height; st.X = mi.X; st.Y = mi.Y;
                    st.Unknown1 = fr.Unknown1;
                    st.ImageTextureType = fr.ImageTextureType;
                    st.MaskTextureType = fr.MaskTextureType;
                    st.ShadowByte = fr.ShadowByte;
                }
                else
                {
                    // 空帧槽：无记录，索引写 0
                    st.Record = -1;
                    st.PayloadRef = Array.Empty<byte>();
                    st.MaskRef = null;
                }
                newState.Add(st);
            }
            _state = newState;
        }

        /// <summary>按当前 MImage + 基线状态生成一条待写入记录（克隆原记录以保留未知名与掩码头）。</summary>
        private WtlFrameRecord BuildRecord(int i, MLibraryV2.MImage mi, EntryState st)
        {
            WtlFrameRecord rec;
            if (st.Record >= 0 && st.Record < _container.Records.Count)
                rec = _container.Records[st.Record].CloneShallow();
            else
                rec = new WtlFrameRecord();

            rec.Width = mi.Width;
            rec.Height = mi.Height;
            rec.X = mi.X;
            rec.Y = mi.Y;
            rec.ShadowX = mi.ShadowX;
            rec.ShadowY = mi.ShadowY;
            rec.RawBytes = null;
            rec.RawRecordOffset = -1;
            rec.Modified = true;
            rec.Payload = mi.FBytes ?? Array.Empty<byte>();

            if (_isNewVersion)
            {
                rec.Unknown1 = st.Unknown1;
                rec.ImageTextureType = st.ImageTextureType;
                rec.StoredLength = rec.Payload.Length;
                rec.Payload = PadTo4(rec.Payload);
                rec.MaskTextureType = st.MaskTextureType;
            }
            else
            {
                rec.ShadowByte = st.ShadowByte;
                rec.StoredLength = rec.Payload.Length;
            }

            // ---- 掩码段 ----
            byte[] mask = mi.MaskFBytes;
            if (mi.HasMask && mask != null && mask.Length > 0)
            {
                bool preserved = ReferenceEquals(mask, st.MaskRef) && st.Record >= 0;
                rec.MaskPayload = _isNewVersion ? PadTo4(mask) : mask;
                if (preserved)
                {
                    // 原掩码逐字节保留：长度字段与原掩码头沿用克隆里的原始值（可能未按 4 对齐）
                    rec.MaskStoredLength = rec.MaskStoredLength > 0 ? rec.MaskStoredLength : mask.Length;
                }
                else
                {
                    rec.MaskStoredLength = mask.Length;
                    if (_isNewVersion)
                    {
                        rec.MaskTextureType = (byte)((rec.MaskTextureType & 0x80) | 0x01);
                    }
                    else
                    {
                        rec.ShadowByte |= 0x80;
                        if (rec.MaskWidth == 0) { rec.MaskWidth = mi.Width; rec.MaskHeight = mi.Height; }
                        rec.MaskX = mi.X; rec.MaskY = mi.Y;
                    }
                }
            }
            else
            {
                rec.MaskPayload = null;
                rec.MaskStoredLength = 0;
                if (_isNewVersion) rec.MaskTextureType = (byte)(rec.MaskTextureType & 0xFE);   // 清 bit0，保留 bit7
                else rec.ShadowByte &= 0x7F;
            }

            return rec;
        }

        private static byte[] PadTo4(byte[] b)
        {
            if (b == null) return Array.Empty<byte>();
            int padded = WtlContainer.Pad4(b.Length);
            if (padded == b.Length) return b;
            byte[] r = new byte[padded];
            Buffer.BlockCopy(b, 0, r, 0, b.Length);
            return r;
        }

        // ---------------- 工具 ----------------

        private static int RoundUp4(int x)
        {
            return x + ((4 - (x % 4)) % 4);
        }

        private static int NextPow2(int x)
        {
            int p = 1;
            while (p < x) p *= 2;
            return p;
        }
    }
}
