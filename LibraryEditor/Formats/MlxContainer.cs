using System;
using System.Collections.Generic;
using System.IO;
using ZstdSharp;

namespace LibraryEditor.Formats
{
    /// <summary>
    /// MLX（Artificial Mir 引擎）一帧的原始记录。
    /// INDX 条目 40 字节：
    ///   0x00 u16 Width | 0x02 u16 Height | 0x04 i16 OffsetX | 0x06 i16 OffsetY |
    ///   0x08 i16 ShadowX | 0x0A i16 ShadowY | 0x0C u8 Shadow | 0x0D u8 Ver |
    ///   0x0E u16 Flags | 0x10 u32 Reserved | 0x14 u32 StoredSize | 0x18 u64 绝对偏移 |
    ///   0x20 u32 RawSize | 0x24 u32 Hash
    /// Ver：4 = DXT1(BC1)，RawSize = (w/4)*(h/4)*8；1 = 16bpp RGB565（黑=透明）。
    /// Flags：bit8(0x0100) = Stored 经 zstd 压缩。
    /// Reserved：高 16 位 = 字典序号（1 基，0=无字典）；语料无字典库为 8/10。
    /// Hash = XXH3_64(Stored) 低 32 位（语料全部验证）。
    /// </summary>
    public sealed class MlxFrame
    {
        public short Width;
        public short Height;
        public short OffsetX;
        public short OffsetY;
        public short ShadowX;
        public short ShadowY;
        public byte Shadow;
        public byte Ver;
        public ushort Flags;
        public uint Reserved;
        public uint RawSize;
        public byte[] Stored;       // 文件中的原样存储字节（zstd 或原始像素）；空帧 = 占位字节
        public bool Modified;

        public bool Compressed
        {
            get { return (Flags & 0x0100) != 0; }
            set { if (value) Flags |= 0x0100; else Flags &= 0xFEFF; }
        }

        /// <summary>字典序号（1 基；0 = 无字典）。</summary>
        public int DictIndex
        {
            get { return (int)(Reserved >> 16); }
            set { Reserved = (uint)((Reserved & 0xFFFF) | ((uint)value << 16)); }
        }

        public MlxFrame()
        {
            Ver = 4;
            Flags = 0;
            Reserved = 8;
            Stored = Array.Empty<byte>();
        }
    }

    /// <summary>MLX 块表的一项（32 字节）。STRS/META/DICT 内容原样透传。</summary>
    public sealed class MlxBlock
    {
        public string Tag;
        public ushort Ver;
        public ushort Flags;
        public byte[] Stored;       // 原样压缩字节
        public byte[] OrigHash;     // 原 8 字节哈希（STRS/META/DICT 保留，INDX/PAGE 重建）
        public int RawLen;          // 块内容解压后长度（块表 +0x14 字段）

        public MlxBlock(string tag, ushort ver, ushort flags, byte[] stored, byte[] origHash)
        {
            Tag = tag;
            Ver = ver;
            Flags = flags;
            Stored = stored;
            OrigHash = origHash;
            RawLen = 0;
        }
    }

    /// <summary>
    /// .Lib（Artificial Mir "MLX" 容器）读写器。
    /// 布局：8 字节魔数 89 4D 4C 58 0D 0A 1A 0A | 0x40 字节头（版本/块表偏移/帧数/块数/
    ///       数据区起点 u64/文件大小 u32/保留 8 字节）| 块表（每项 32B：tag+ver+flags+偏移u64+
    ///       压缩长+原始长+哈希8B，哈希=XXH3_128 低64）| 块（INDX/STRS/PAGE/DICT/META，zstd）|
    ///       帧数据区（8 字节对齐，相同内容去重共享，空帧共享一个 8 字节透明 DXT 占位）。
    /// 铁律：未修改帧的 Stored 持有原始字节引用；任何帧未修改且帧数未变时 Rebuild 输出原字节。
    /// </summary>
    public sealed class MlxContainer
    {
        public const int HeaderLength = 0x40;
        public const int BlockEntrySize = 32;
        public const int FrameEntrySize = 40;
        public const int DataAlign = 8;

        public static readonly byte[] Magic = { 0x89, 0x4D, 0x4C, 0x58, 0x0D, 0x0A, 0x1A, 0x0A };

        public int Version;
        public uint Unknown10;      // 语料 = 0
        public uint Unknown14;      // 语料 17/19/33/49/51/81（含义未明，原样保留）
        public uint PageSizeHint;   // PAGE 块 +0x04（语料 0x80000）
        public byte[] Tail8;        // 头 0x38..0x40 的 8 字节（算法未明，原样保留）
        public List<MlxBlock> Blocks = new List<MlxBlock>();
        public List<MlxFrame> Frames = new List<MlxFrame>();
        public List<int> PageStarts = new List<int>();
        /// <summary>字典表：序号(1 基) → 字典字节。来自 DICT 块，可能为空。</summary>
        public SortedDictionary<int, byte[]> Dicts = new SortedDictionary<int, byte[]>();
        public byte[] DictBlockStored;   // DICT 块原样字节（无 DICT 块为 null）
        public MlxBlock StrsBlock;       // STRS 原样（新建库时合成）
        public MlxBlock MetaBlock;       // META 原样（新建库时合成）

        private byte[] _originalBytes;   // 打开时的完整文件字节；未改动时原样返回（保存后更新为新字节）

        private MlxContainer(byte[] originalBytes)
        {
            _originalBytes = originalBytes;
        }

        public static bool LooksLikeMlx(byte[] head)
        {
            if (head == null || head.Length < 8) return false;
            for (int i = 0; i < 8; i++)
                if (head[i] != Magic[i]) return false;
            return true;
        }

        private static byte[] DecompressBlock(byte[] stored, int rawLen)
        {
            var d = new Decompressor();
            return d.Unwrap(stored, rawLen).ToArray();
        }

        private static byte[] CompressBlock(byte[] raw)
        {
            var c = new Compressor(19);
            return c.Wrap(raw).ToArray();
        }

        public static MlxContainer Parse(byte[] data)
        {
            if (data == null) throw new EliteFormatException("data 为 null");
            if (data.Length < HeaderLength || !LooksLikeMlx(data))
                throw new EliteFormatException("魔数不符：不是 MLX（Artificial Mir）容器");

            MlxContainer c = new MlxContainer(data);
            c.Version = BitConverter.ToInt32(data, 0x08);
            c.Unknown10 = BitConverter.ToUInt32(data, 0x10);
            c.Unknown14 = BitConverter.ToUInt32(data, 0x14);
            c.Tail8 = FrameIo.Slice(data, 0x38, 0x40);

            int btOff = BitConverter.ToInt32(data, 0x0C);
            int count = BitConverter.ToInt32(data, 0x18);
            int bcnt = BitConverter.ToInt32(data, 0x1C);
            if (btOff < HeaderLength || btOff + bcnt * BlockEntrySize > data.Length)
                throw new EliteFormatException("块表越界: 块数=" + bcnt);
            if (count < 0) throw new EliteFormatException("帧数为负: " + count);

            for (int i = 0; i < bcnt; i++)
            {
                int p = btOff + i * BlockEntrySize;
                string tag = System.Text.Encoding.ASCII.GetString(data, p, 4).TrimEnd('\0');
                ushort ver = BitConverter.ToUInt16(data, p + 4);
                ushort flags = BitConverter.ToUInt16(data, p + 6);
                long off = BitConverter.ToInt64(data, p + 8);
                int comp = BitConverter.ToInt32(data, p + 16);
                int rawLen = BitConverter.ToInt32(data, p + 20);
                byte[] hash = FrameIo.Slice(data, p + 24, p + 32);

                if (off < 0 || off + comp > data.Length)
                    throw new EliteFormatException("块 " + tag + " 数据越界");

                byte[] stored = FrameIo.Slice(data, (int)off, (int)off + comp);
                MlxBlock blk = new MlxBlock(tag, ver, flags, stored, hash);
                blk.RawLen = rawLen;
                c.Blocks.Add(blk);

                byte[] content = (flags & 0x0100) != 0 ? DecompressBlock(stored, rawLen) : stored;

                if (tag == "INDX")
                {
                    int entrySize = BitConverter.ToInt32(content, 0);
                    int n = BitConverter.ToInt32(content, 4);
                    if (entrySize < FrameEntrySize || n < 0)
                        throw new EliteFormatException("INDX 非法: entrySize=" + entrySize + " count=" + n);
                    if (16 + (long)n * entrySize > content.Length)
                        throw new EliteFormatException("INDX 条目越界");

                    for (int k = 0; k < n; k++)
                    {
                        int e = 16 + k * entrySize;
                        MlxFrame f = new MlxFrame
                        {
                            Width = BitConverter.ToInt16(content, e),
                            Height = BitConverter.ToInt16(content, e + 2),
                            OffsetX = BitConverter.ToInt16(content, e + 4),
                            OffsetY = BitConverter.ToInt16(content, e + 6),
                            ShadowX = BitConverter.ToInt16(content, e + 8),
                            ShadowY = BitConverter.ToInt16(content, e + 10),
                            Shadow = content[e + 12],
                            Ver = content[e + 13],
                            Flags = BitConverter.ToUInt16(content, e + 14),
                            Reserved = BitConverter.ToUInt32(content, e + 16),
                            RawSize = BitConverter.ToUInt32(content, e + 32),
                        };
                        int size = BitConverter.ToInt32(content, e + 20);
                        long foff = BitConverter.ToInt64(content, e + 24);
                        if (size > 0)
                        {
                            if (foff < 0 || foff + size > data.Length)
                                throw new EliteFormatException("帧 " + k + " 数据越界");
                            f.Stored = FrameIo.Slice(data, (int)foff, (int)foff + size);
                        }
                        c.Frames.Add(f);
                    }
                }
                else if (tag == "PAGE")
                {
                    int pages = BitConverter.ToInt32(content, 0);
                    c.PageSizeHint = BitConverter.ToUInt32(content, 4);
                    for (int k = 0; k < pages; k++)
                    {
                        int q = 8 + k * 24;
                        c.PageStarts.Add(BitConverter.ToInt32(content, q + 12));
                    }
                }
                else if (tag == "DICT")
                {
                    c.DictBlockStored = stored;
                    int dcnt = BitConverter.ToInt32(content, 0);
                    for (int k = 0; k < dcnt; k++)
                    {
                        int q = 8 + k * 16;
                        int did = BitConverter.ToInt32(content, q);
                        int doff = BitConverter.ToInt32(content, q + 4);
                        int dsize = BitConverter.ToInt32(content, q + 8);
                        if (doff >= 0 && dsize > 0 && doff + dsize <= content.Length)
                        {
                            byte[] dictBytes = FrameIo.Slice(content, doff, doff + dsize);
                            c.Dicts[did] = dictBytes;
                            // 帧里的字典序号是完整编号的低字节：DICT 块写 0x101/0x102，
                            // 帧引用 1/2。两张编号都登记，取字典时不用再猜
                            int low = did & 0xFF;
                            if (low > 0 && low != did) c.Dicts[low] = dictBytes;
                        }
                    }
                }
                else if (tag == "STRS")
                {
                    c.StrsBlock = blk;
                }
                else if (tag == "META")
                {
                    c.MetaBlock = blk;
                }
            }

            if (c.PageStarts.Count == 0) c.PageStarts.Add(0);
            c.Seal();
            return c;
        }

        /// <summary>任何帧未修改且帧数未变时，输出与原文件字节一致。</summary>
        public bool IsClean()
        {
            if (_originalBytes == null || Frames.Count != OriginalFrameCount) return false;
            for (int i = 0; i < Frames.Count; i++)
                if (Frames[i].Modified) return false;
            return true;
        }

        private int OriginalFrameCount = -1;

        /// <summary>记录解析时的帧数（IsClean 判断用）。</summary>
        public void Seal()
        {
            OriginalFrameCount = Frames.Count;
        }

        /// <summary>保存成功后调用：把重建字节作为新的"原字节"基准并清除 Modified，保证重复保存字节稳定。</summary>
        public void MarkSaved(byte[] rebuilt)
        {
            _originalBytes = rebuilt;
            foreach (MlxFrame f in Frames)
                f.Modified = false;
            Seal();
        }

        /// <summary>帧数在解析后是否发生了增删。</summary>
        public bool CountChanged { get { return Frames.Count != OriginalFrameCount; } }

        // ---------------- 重建 ----------------

        private static void Put(byte[] dst, int pos, byte[] src)
        {
            Buffer.BlockCopy(src, 0, dst, pos, src.Length);
        }

        private static byte[] BuildStrsContent()
        {
            string[] keys = { "lib.name", "prov.compressor", "prov.discardedTransparentRgb", "prov.lossyOriginCount",
                              "prov.origins", "prov.restored565Count", "prov.sourceFormat", "prov.sources",
                              "prov.tool", "prov.toolVersion" };
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                w.Write(keys.Length);
                int total = 0;
                foreach (string k in keys) total += k.Length;
                w.Write(total);
                int off = 0;
                w.Write(off);
                foreach (string k in keys) { off += k.Length; w.Write(off); }
                foreach (string k in keys)
                {
                    byte[] b = System.Text.Encoding.ASCII.GetBytes(k);
                    w.Write(b);
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        private static byte[] BuildMetaContent(string libName)
        {
            // 结构对照 items.Lib：条目 = u32 键序号(1基) + u16 类型(1=串,3=i64) + u16 长度 + 值
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                Action<int, byte[]> str = (key, val) => { w.Write(key); w.Write((ushort)1); w.Write((ushort)val.Length); w.Write(val); };
                Action<int, long> i64 = (key, val) => { w.Write(key); w.Write((ushort)3); w.Write((ushort)8); w.Write(val); };

                w.Write(10);
                str(1, System.Text.Encoding.ASCII.GetBytes(libName ?? "mlx"));
                str(2, System.Text.Encoding.ASCII.GetBytes("zstd (ZstdSharp.Port) level 19"));
                i64(3, 0);   // prov.discardedTransparentRgb
                i64(4, 0);   // prov.lossyOriginCount
                i64(5, 0);   // prov.restored565Count
                str(6, System.Text.Encoding.ASCII.GetBytes("OursDxt"));
                str(7, System.Text.Encoding.ASCII.GetBytes(""));
                str(8, System.Text.Encoding.ASCII.GetBytes("mirassets"));
                str(9, System.Text.Encoding.ASCII.GetBytes("1.0.0.0"));
                i64(10, 0);  // prov.origins
                w.Flush();
                return ms.ToArray();
            }
        }

        private static MlxBlock MakeBlock(string tag, ushort flags, byte[] content, bool compress)
        {
            byte[] stored = compress ? CompressBlock(content) : content;
            ushort fl = (ushort)(flags & ~0x0100);   // 压缩位由 compress 决定，其余标志（如 INDX 的 0x0001）原样保留
            if (compress) fl |= 0x0100;
            byte[] hash = new byte[8];
            ulong h = MlxHash.Xxh3_128Low(stored, stored.Length);
            BitConverter.GetBytes(h).CopyTo(hash, 0);
            MlxBlock blk = new MlxBlock(tag, 1, fl, stored, hash);
            blk.RawLen = content.Length;
            return blk;
        }

        /// <summary>重建完整文件字节。未修改时（IsClean）直接返回原字节。</summary>
        public byte[] Rebuild()
        {
            if (IsClean()) return _originalBytes;

            int n = Frames.Count;

            // 1. 帧数据区布局：8 字节对齐 + 内容去重（空帧占位自然共享）
            long[] frameOffs = new long[n];
            var dedup = new Dictionary<MlxHashKey, long>();
            long cursor = 0;
            for (int i = 0; i < n; i++)
            {
                byte[] stored = Frames[i].Stored ?? Array.Empty<byte>();
                if (stored.Length == 0) { frameOffs[i] = 0; continue; }
                var key = new MlxHashKey(MlxHash.Xxh3_64(stored, stored.Length), stored.Length);
                long off;
                if (!dedup.TryGetValue(key, out off))
                {
                    cursor = Align8(cursor);
                    off = cursor;
                    cursor += stored.Length;
                    dedup[key] = off;
                }
                frameOffs[i] = off;
            }
            long dataLen = cursor;

            // 装配数据区字节流（页哈希 / 输出都要用）
            byte[] dataBytes = new byte[dataLen];
            var placed = new HashSet<long>();
            for (int i = 0; i < n; i++)
            {
                byte[] stored = Frames[i].Stored ?? Array.Empty<byte>();
                if (stored.Length == 0) continue;
                if (!placed.Add(frameOffs[i])) continue;
                Buffer.BlockCopy(stored, 0, dataBytes, (int)frameOffs[i], stored.Length);
            }

            // 2. 页表：先在"数据区相对坐标"下算好每页范围与哈希，绝对偏移在布局后回填
            List<int> starts = new List<int>();
            foreach (int s in PageStarts)
                if (s > 0 && s < n && (starts.Count == 0 || s > starts[starts.Count - 1]))
                    starts.Add(s);
            if (starts.Count == 0 || starts[0] != 0) starts.Insert(0, 0);

            int pageCount = starts.Count;
            long[] pageRels = new long[pageCount];
            int[] pageLens = new int[pageCount];
            for (int pi = 0; pi < pageCount; pi++)
            {
                int first = starts[pi];
                int next = pi + 1 < pageCount ? starts[pi + 1] : n;
                long poff = -1, pend = 0;
                for (int i = first; i < next; i++)
                {
                    byte[] stored = Frames[i].Stored ?? Array.Empty<byte>();
                    if (stored.Length == 0) continue;
                    long s = frameOffs[i], e = s + stored.Length;
                    if (poff < 0 || s < poff) poff = s;
                    if (e > pend) pend = e;
                }
                pageRels[pi] = poff < 0 ? -1 : poff;
                pageLens[pi] = poff < 0 ? 0 : (int)(pend - poff);
            }

            // 页哈希只依赖数据区内容，与绝对偏移无关，一次算定
            byte[][] pageHashes = new byte[pageCount][];
            for (int pi = 0; pi < pageCount; pi++)
            {
                pageHashes[pi] = new byte[8];
                if (pageLens[pi] > 0)
                {
                    byte[] pd = new byte[pageLens[pi]];
                    Buffer.BlockCopy(dataBytes, (int)pageRels[pi], pd, 0, pageLens[pi]);
                    BitConverter.GetBytes(MlxHash.Xxh3_128Low(pd, pd.Length)).CopyTo(pageHashes[pi], 0);
                }
            }

            byte[] pageContent = new byte[8 + pageCount * 24];
            BitConverter.GetBytes(pageCount).CopyTo(pageContent, 0);
            BitConverter.GetBytes(PageSizeHint).CopyTo(pageContent, 4);
            for (int pi = 0; pi < pageCount; pi++)
            {
                BitConverter.GetBytes(starts[pi]).CopyTo(pageContent, 20 + pi * 24);
                Put(pageContent, 24 + pi * 24, pageHashes[pi]);
            }

            // 3. INDX 内容（帧绝对偏移在布局后回填）
            byte[] indxContent = new byte[16 + n * FrameEntrySize];
            BitConverter.GetBytes(FrameEntrySize).CopyTo(indxContent, 0);
            BitConverter.GetBytes(n).CopyTo(indxContent, 4);
            for (int i = 0; i < n; i++)
            {
                MlxFrame f = Frames[i];
                int e = 16 + i * FrameEntrySize;
                byte[] stored = f.Stored ?? Array.Empty<byte>();
                BitConverter.GetBytes(f.Width).CopyTo(indxContent, e);
                BitConverter.GetBytes(f.Height).CopyTo(indxContent, e + 2);
                BitConverter.GetBytes(f.OffsetX).CopyTo(indxContent, e + 4);
                BitConverter.GetBytes(f.OffsetY).CopyTo(indxContent, e + 6);
                BitConverter.GetBytes(f.ShadowX).CopyTo(indxContent, e + 8);
                BitConverter.GetBytes(f.ShadowY).CopyTo(indxContent, e + 10);
                indxContent[e + 12] = f.Shadow;
                indxContent[e + 13] = f.Ver;
                BitConverter.GetBytes(f.Flags).CopyTo(indxContent, e + 14);
                BitConverter.GetBytes(f.Reserved).CopyTo(indxContent, e + 16);
                BitConverter.GetBytes(stored.Length).CopyTo(indxContent, e + 20);
                BitConverter.GetBytes(f.RawSize).CopyTo(indxContent, e + 32);
                ulong fh = stored.Length > 0 ? MlxHash.Xxh3_64(stored, stored.Length) : 0;
                BitConverter.GetBytes((uint)fh).CopyTo(indxContent, e + 36);
            }

            // 4. 块装配 + 布局收敛
            //    INDX/PAGE 内容含数据区绝对偏移，块区尺寸又取决于二者压缩后的长度 -> 互相依赖。
            //    策略：先按"压缩后实际长度"布局得到 dataBase，再用该 dataBase 回填重压；
            //    只要新一轮的块区结束 <= dataBase 即可（尾部补零），否则抬高 dataBase 再来一轮。
            ushort indxFlags = GetOrigFlags("INDX", 0x0101);
            ushort pageFlags = GetOrigFlags("PAGE", 0x0100);
            bool indxComp = (indxFlags & 0x0100) != 0;
            bool pageComp = (pageFlags & 0x0100) != 0;

            MlxBlock blkStrs = StrsBlock ?? MakeBlock("STRS", 0x0100, BuildStrsContent(), true);
            MlxBlock blkDict = DictBlockStored != null
                ? new MlxBlock("DICT", GetOrigVer("DICT", 1), GetOrigFlags("DICT", 0x0101), DictBlockStored, GetOrigHash("DICT"))
                : null;
            MlxBlock blkMeta = MetaBlock ?? MakeBlock("META", 0x0100, BuildMetaContent(DefaultLibName), true);

            List<MlxBlock> outBlocks = null;
            long[] blockOffs = null;
            long dataBase = 0;

            // 用给定 dataBase 回填 -> 压缩 -> 布局，块区结束位置 <= dataBase 即视为收敛

            for (int iter = 0; iter < 12; iter++)
            {
                for (int i = 0; i < n; i++)
                    BitConverter.GetBytes(dataBase + frameOffs[i]).CopyTo(indxContent, 16 + i * FrameEntrySize + 24);
                for (int pi = 0; pi < pageCount; pi++)
                {
                    long abs = pageRels[pi] < 0 ? dataBase : dataBase + pageRels[pi];
                    BitConverter.GetBytes(abs).CopyTo(pageContent, 8 + pi * 24);
                    BitConverter.GetBytes(pageLens[pi]).CopyTo(pageContent, 16 + pi * 24);
                }

                MlxBlock blkIndx = MakeBlock("INDX", indxFlags, indxContent, indxComp);
                MlxBlock blkPage = MakeBlock("PAGE", pageFlags, pageContent, pageComp);

                List<MlxBlock> list = new List<MlxBlock>();
                list.Add(blkIndx);
                list.Add(blkStrs);
                list.Add(blkPage);
                if (blkDict != null) list.Add(blkDict);
                list.Add(blkMeta);

                long[] offs = new long[list.Count];
                long p = HeaderLength + (long)list.Count * BlockEntrySize;
                for (int i = 0; i < list.Count; i++)
                {
                    p = Align8(p);
                    offs[i] = p;
                    p += list[i].Stored.Length;
                }
                long nd = Align8(p);

                outBlocks = list;
                blockOffs = offs;
                if (nd <= dataBase) break;   // 空间够用（尾部补零即可），与本轮内容自洽
                dataBase = nd + 4096;        // 抬高基址，并留 4KB 余量抵消回填后压缩长度的微小漂移
            }

            // 6. 装配输出
            long total = dataBase + dataLen;
            byte[] outb = new byte[total];

            Put(outb, 0, Magic);
            BitConverter.GetBytes(Version).CopyTo(outb, 0x08);
            BitConverter.GetBytes(HeaderLength).CopyTo(outb, 0x0C);
            BitConverter.GetBytes(Unknown10).CopyTo(outb, 0x10);
            BitConverter.GetBytes(Unknown14).CopyTo(outb, 0x14);
            BitConverter.GetBytes(n).CopyTo(outb, 0x18);
            BitConverter.GetBytes(outBlocks.Count).CopyTo(outb, 0x1C);
            BitConverter.GetBytes((long)HeaderLength).CopyTo(outb, 0x20);
            BitConverter.GetBytes(dataBase).CopyTo(outb, 0x28);
            BitConverter.GetBytes((uint)total).CopyTo(outb, 0x30);
            BitConverter.GetBytes(0u).CopyTo(outb, 0x34);
            if (Tail8 != null && Tail8.Length == 8) Put(outb, 0x38, Tail8);

            long bp = HeaderLength;
            for (int i = 0; i < outBlocks.Count; i++)
            {
                MlxBlock b = outBlocks[i];
                byte[] tagBytes = System.Text.Encoding.ASCII.GetBytes((b.Tag + "    ").Substring(0, 4));
                Put(outb, (int)bp, tagBytes);
                BitConverter.GetBytes(b.Ver).CopyTo(outb, (int)bp + 4);
                BitConverter.GetBytes(b.Flags).CopyTo(outb, (int)bp + 6);
                BitConverter.GetBytes(blockOffs[i]).CopyTo(outb, (int)bp + 8);
                BitConverter.GetBytes(b.Stored.Length).CopyTo(outb, (int)bp + 16);
                // rawLen：块内容解压后的真实长度（zstd 帧头自带内容大小；解不开则退回存储长）
                int rawLen = b.Stored.Length;
                if ((b.Flags & 0x0100) != 0)
                {
                    try { rawLen = new Decompressor().Unwrap(b.Stored, 1 << 30).Length; } catch { }
                }
                BitConverter.GetBytes(rawLen).CopyTo(outb, (int)bp + 20);
                if (b.OrigHash != null && b.OrigHash.Length == 8) Put(outb, (int)bp + 24, b.OrigHash);
                else Put(outb, (int)bp + 24, BitConverter.GetBytes(MlxHash.Xxh3_128Low(b.Stored, b.Stored.Length)));
                bp += BlockEntrySize;
            }

            foreach (MlxBlock b in outBlocks)
            {
                long aligned = Align8(bp);
                while (bp < aligned) outb[bp++] = 0;
                Put(outb, (int)bp, b.Stored);
                bp = aligned + b.Stored.Length;
            }
            // 数据区固定从 dataBase 起写（块区与数据区之间可能有收敛余量，留零即可）
            Buffer.BlockCopy(dataBytes, 0, outb, (int)dataBase, (int)dataLen);

            return outb;
        }

        private string DefaultLibName = "mlx";

        public void SetLibName(string name) { DefaultLibName = name; }

        private ushort GetOrigFlags(string tag, ushort def)
        {
            foreach (MlxBlock b in Blocks)
                if (b.Tag == tag) return b.Flags;
            return def;
        }

        private ushort GetOrigVer(string tag, ushort def)
        {
            foreach (MlxBlock b in Blocks)
                if (b.Tag == tag) return b.Ver;
            return def;
        }

        private byte[] GetOrigHash(string tag)
        {
            foreach (MlxBlock b in Blocks)
                if (b.Tag == tag) return b.OrigHash;
            return null;
        }

        private static long Align8(long v)
        {
            return (v + 7) & ~7L;
        }

        /// <summary>新建空 MLX 库的字节（供"新建"流程使用）。</summary>
        public static byte[] BuildNewFile(string libName)
        {
            MlxContainer c = new MlxContainer(null)
            {
                Version = 1,
                Unknown10 = 0,
                Unknown14 = 17,
                PageSizeHint = 0x80000,
                Tail8 = new byte[8],
                DefaultLibName = libName ?? "mlx",
            };
            c.Seal();
            return c.Rebuild();
        }
    }

    /// <summary>去重键：XXH3_64 + 长度。</summary>
    internal readonly struct MlxHashKey : IEquatable<MlxHashKey>
    {
        public readonly ulong Hash;
        public readonly int Length;

        public MlxHashKey(ulong hash, int length)
        {
            Hash = hash;
            Length = length;
        }

        public bool Equals(MlxHashKey other) { return Hash == other.Hash && Length == other.Length; }
        public override bool Equals(object obj) { return obj is MlxHashKey k && Equals(k); }
        public override int GetHashCode() { return (int)Hash ^ Length; }
    }

    /// <summary>
    /// XXH3（参考 xxHash v0.8.2 标量路径移植，seed=0）。
    /// Xxh3_64   = XXH3_64bits；
    /// Xxh3_128Low = XXH3_128bits 的低 64 位（块表哈希用）。
    /// 已与 Python xxhash 参考实现做过 0~200003 字节交叉验证。
    /// </summary>
    public static class MlxHash
    {
        static readonly byte[] KSecret =
        {
            0xb8,0xfe,0x6c,0x39,0x23,0xa4,0x4b,0xbe,0x7c,0x01,0x81,0x2c,0xf7,0x21,0xad,0x1c,
            0xde,0xd4,0x6d,0xe9,0x83,0x90,0x97,0xdb,0x72,0x40,0xa4,0xa4,0xb7,0xb3,0x67,0x1f,
            0xcb,0x79,0xe6,0x4e,0xcc,0xc0,0xe5,0x78,0x82,0x5a,0xd0,0x7d,0xcc,0xff,0x72,0x21,
            0xb8,0x08,0x46,0x74,0xf7,0x43,0x24,0x8e,0xe0,0x35,0x90,0xe6,0x81,0x3a,0x26,0x4c,
            0x3c,0x28,0x52,0xbb,0x91,0xc3,0x00,0xcb,0x88,0xd0,0x65,0x8b,0x1b,0x53,0x2e,0xa3,
            0x71,0x64,0x48,0x97,0xa2,0x0d,0xf9,0x4e,0x38,0x19,0xef,0x46,0xa9,0xde,0xac,0xd8,
            0xa8,0xfa,0x76,0x3f,0xe3,0x9c,0x34,0x3f,0xf9,0xdc,0xbb,0xc7,0xc7,0x0b,0x4f,0x1d,
            0x8a,0x51,0xe0,0x4b,0xcd,0xb4,0x59,0x31,0xc8,0x9f,0x7e,0xc9,0xd9,0x78,0x73,0x64,
            0xea,0xc5,0xac,0x83,0x34,0xd3,0xeb,0xc3,0xc5,0x81,0xa0,0xff,0xfa,0x13,0x63,0xeb,
            0x17,0x0d,0xdd,0x51,0xb7,0xf0,0xda,0x49,0xd3,0x16,0x55,0x26,0x29,0xd4,0x68,0x9e,
            0x2b,0x16,0xbe,0x58,0x7d,0x47,0xa1,0xfc,0x8f,0xf8,0xb8,0xd1,0x7a,0xd0,0x31,0xce,
            0x45,0xcb,0x3a,0x8f,0x95,0x16,0x04,0x28,0xaf,0xd7,0xfb,0xca,0xbb,0x4b,0x40,0x7e,
        };

        const ulong PRIME32_1 = 0x9E3779B1U;
        const ulong PRIME32_2 = 0x85EBCA77U;
        const ulong PRIME32_3 = 0xC2B2AE3DU;
        const ulong PRIME64_1 = 0x9E3779B185EBCA87UL;
        const ulong PRIME64_2 = 0xC2B2AE3D27D4EB4FUL;
        const ulong PRIME64_3 = 0x165667B19E3779F9UL;
        const ulong PRIME64_4 = 0x85EBCA77C2B2AE63UL;
        const ulong PRIME64_5 = 0x27D4EB2F165667C5UL;
        const ulong PRIME_MX1 = 0x165667919E3779F9UL;
        const ulong PRIME_MX2 = 0x9FB21C651E98DF25UL;

        static ulong Rd64(byte[] d, int o)
        {
            return d[o] | ((ulong)d[o + 1] << 8) | ((ulong)d[o + 2] << 16) | ((ulong)d[o + 3] << 24)
                 | ((ulong)d[o + 4] << 32) | ((ulong)d[o + 5] << 40) | ((ulong)d[o + 6] << 48) | ((ulong)d[o + 7] << 56);
        }

        static uint Rd32(byte[] d, int o)
        {
            return (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24));
        }

        static ulong Rotl(ulong x, int r) { return (x << r) | (x >> (64 - r)); }

        static ulong Swap64(ulong v)
        {
            return (v >> 56) | ((v >> 40) & 0xFF00UL) | ((v >> 24) & 0xFF0000UL) | ((v >> 8) & 0xFF000000UL)
                 | ((v << 8) & 0xFF00000000UL) | ((v << 24) & 0xFF0000000000UL) | ((v << 40) & 0xFF000000000000UL) | (v << 56);
        }

        static uint Swap32(uint v)
        {
            return (v >> 24) | ((v >> 8) & 0xFF00U) | ((v << 8) & 0xFF0000U) | (v << 24);
        }

        static void Mult64to128(ulong lhs, ulong rhs, out ulong lo, out ulong hi)
        {
            ulong lo_lo = (lhs & 0xFFFFFFFF) * (rhs & 0xFFFFFFFF);
            ulong hi_lo = (lhs >> 32) * (rhs & 0xFFFFFFFF);
            ulong lo_hi = (lhs & 0xFFFFFFFF) * (rhs >> 32);
            ulong hi_hi = (lhs >> 32) * (rhs >> 32);
            ulong cross = (lo_lo >> 32) + (hi_lo & 0xFFFFFFFF) + lo_hi;
            hi = (hi_lo >> 32) + (cross >> 32) + hi_hi;
            lo = (cross << 32) | (lo_lo & 0xFFFFFFFF);
        }

        static ulong Mul128Fold(ulong a, ulong b)
        {
            Mult64to128(a, b, out ulong lo, out ulong hi);
            return lo ^ hi;
        }

        static ulong Xxh64Avalanche(ulong h)
        {
            h ^= h >> 33; h *= PRIME64_2; h ^= h >> 29; h *= PRIME64_3; h ^= h >> 32;
            return h;
        }

        static ulong Xxh3Avalanche(ulong h)
        {
            h ^= h >> 37; h *= PRIME_MX1; h ^= h >> 32;
            return h;
        }

        static ulong Rrmxmx(ulong h, ulong len)
        {
            h ^= Rotl(h, 49) ^ Rotl(h, 24);
            h *= PRIME_MX2;
            h ^= (h >> 35) + len;
            h *= PRIME_MX2;
            return h ^ (h >> 28);
        }

        static ulong Len1to3_64(byte[] d, int len)
        {
            uint c1 = d[0], c2 = d[len >> 1], c3 = d[len - 1];
            uint combined = (c1 << 16) | (c2 << 24) | c3 | ((uint)len << 8);
            ulong bitflip = Rd32(KSecret, 0) ^ Rd32(KSecret, 4);
            return Xxh64Avalanche(combined ^ bitflip);
        }

        static ulong Len4to8_64(byte[] d, int len)
        {
            uint input1 = Rd32(d, 0);
            uint input2 = Rd32(d, len - 4);
            ulong bitflip = Rd64(KSecret, 8) ^ Rd64(KSecret, 16);
            ulong input64 = input2 + ((ulong)input1 << 32);
            return Rrmxmx(input64 ^ bitflip, (ulong)len);
        }

        static ulong Len9to16_64(byte[] d, int len)
        {
            ulong bitflip1 = Rd64(KSecret, 24) ^ Rd64(KSecret, 32);
            ulong bitflip2 = Rd64(KSecret, 40) ^ Rd64(KSecret, 48);
            ulong input_lo = Rd64(d, 0) ^ bitflip1;
            ulong input_hi = Rd64(d, len - 8) ^ bitflip2;
            ulong acc = (ulong)len + Swap64(input_lo) + input_hi + Mul128Fold(input_lo, input_hi);
            return Xxh3Avalanche(acc);
        }

        static ulong Mix16B(byte[] d, int inOff, int secOff)
        {
            ulong input_lo = Rd64(d, inOff);
            ulong input_hi = Rd64(d, inOff + 8);
            return Mul128Fold(input_lo ^ Rd64(KSecret, secOff), input_hi ^ Rd64(KSecret, secOff + 8));
        }

        static ulong Len17to128_64(byte[] d, int len)
        {
            ulong acc = (ulong)len * PRIME64_1;
            if (len > 32)
            {
                if (len > 64)
                {
                    if (len > 96)
                    {
                        acc += Mix16B(d, 48, 96);
                        acc += Mix16B(d, len - 64, 112);
                    }
                    acc += Mix16B(d, 32, 64);
                    acc += Mix16B(d, len - 48, 80);
                }
                acc += Mix16B(d, 16, 32);
                acc += Mix16B(d, len - 32, 48);
            }
            acc += Mix16B(d, 0, 0);
            acc += Mix16B(d, len - 16, 16);
            return Xxh3Avalanche(acc);
        }

        static ulong Len129to240_64(byte[] d, int len)
        {
            const int StartOffset = 3, LastOffset = 17;
            ulong acc = (ulong)len * PRIME64_1;
            int nbRounds = len / 16;
            for (int i = 0; i < 8; i++) acc += Mix16B(d, 16 * i, 16 * i);
            acc = Xxh3Avalanche(acc);
            ulong accEnd = Mix16B(d, len - 16, 136 - LastOffset);
            for (int i = 8; i < nbRounds; i++)
                accEnd += Mix16B(d, 16 * i, 16 * (i - 8) + StartOffset);
            return Xxh3Avalanche(acc + accEnd);
        }

        static void Accumulate512(ulong[] acc, byte[] d, int inOff, int secOff)
        {
            for (int lane = 0; lane < 8; lane++)
            {
                ulong dataVal = Rd64(d, inOff + lane * 8);
                ulong dataKey = dataVal ^ Rd64(KSecret, secOff + lane * 8);
                acc[lane ^ 1] += dataVal;
                acc[lane] += (dataKey & 0xFFFFFFFF) * (dataKey >> 32);
            }
        }

        static void ScrambleAcc(ulong[] acc, int secOff)
        {
            for (int lane = 0; lane < 8; lane++)
            {
                ulong a = acc[lane];
                a ^= a >> 47;
                a ^= Rd64(KSecret, secOff + lane * 8);
                a *= PRIME32_1;
                acc[lane] = a;
            }
        }

        static void HashLongLoop(ulong[] acc, byte[] d, int len, int secretSize)
        {
            int nbStripesPerBlock = (secretSize - 64) / 8;
            int blockLen = 64 * nbStripesPerBlock;
            int nbBlocks = (len - 1) / blockLen;
            for (int n = 0; n < nbBlocks; n++)
            {
                for (int s = 0; s < nbStripesPerBlock; s++)
                    Accumulate512(acc, d, n * blockLen + s * 64, s * 8);
                ScrambleAcc(acc, secretSize - 64);
            }
            int nbStripesLast = ((len - 1) - blockLen * nbBlocks) / 64;
            for (int s = 0; s < nbStripesLast; s++)
                Accumulate512(acc, d, nbBlocks * blockLen + s * 64, s * 8);
            Accumulate512(acc, d, len - 64, secretSize - 64 - 7);
        }

        static ulong MergeAccs(ulong[] acc, int secOff, ulong start)
        {
            ulong result = start;
            for (int i = 0; i < 4; i++)
                result += Mul128Fold(acc[2 * i] ^ Rd64(KSecret, secOff + 16 * i), acc[2 * i + 1] ^ Rd64(KSecret, secOff + 16 * i + 8));
            return Xxh3Avalanche(result);
        }

        public static ulong Xxh3_64(byte[] d, int len)
        {
            if (d == null || len == 0)
                return Xxh64Avalanche(Rd64(KSecret, 56) ^ Rd64(KSecret, 64));
            if (len <= 16)
            {
                if (len > 8) return Len9to16_64(d, len);
                if (len >= 4) return Len4to8_64(d, len);
                return Len1to3_64(d, len);
            }
            if (len <= 128) return Len17to128_64(d, len);
            if (len <= 240) return Len129to240_64(d, len);

            ulong[] acc = { PRIME32_3, PRIME64_1, PRIME64_2, PRIME64_3, PRIME64_4, PRIME32_2, PRIME64_5, PRIME32_1 };
            HashLongLoop(acc, d, len, 192);
            return MergeAccs(acc, 11, (ulong)len * PRIME64_1);
        }

        // ---------------- XXH3 128（低 64 位） ----------------

        static void Len1to3_128(byte[] d, int len, out ulong lo, out ulong hi)
        {
            uint c1 = d[0], c2 = d[len >> 1], c3 = d[len - 1];
            uint combinedl = (c1 << 16) | (c2 << 24) | c3 | ((uint)len << 8);
            uint combinedh = (uint)Rotl(Swap32(combinedl), 13);
            ulong bitflipl = Rd32(KSecret, 0) ^ Rd32(KSecret, 4);
            ulong bitfliph = Rd32(KSecret, 8) ^ Rd32(KSecret, 12);
            lo = Xxh64Avalanche(combinedl ^ bitflipl);
            hi = Xxh64Avalanche(combinedh ^ bitfliph);
        }

        static void Len4to8_128(byte[] d, int len, out ulong lo, out ulong hi)
        {
            uint input_lo = Rd32(d, 0);
            uint input_hi = Rd32(d, len - 4);
            ulong input64 = input_lo + ((ulong)input_hi << 32);
            ulong bitflip = Rd64(KSecret, 16) ^ Rd64(KSecret, 24);
            ulong keyed = input64 ^ bitflip;
            Mult64to128(keyed, PRIME64_1 + ((ulong)len << 2), out ulong mlo, out ulong mhi);
            mhi += mlo << 1;
            mlo ^= mhi >> 3;
            mlo ^= mlo >> 35; mlo *= PRIME_MX2; mlo ^= mlo >> 28;
            hi = Xxh3Avalanche(mhi);
            lo = mlo;
        }

        static void Len9to16_128(byte[] d, int len, out ulong lo, out ulong hi)
        {
            ulong bitflipl = Rd64(KSecret, 32) ^ Rd64(KSecret, 40);
            ulong bitfliph = Rd64(KSecret, 48) ^ Rd64(KSecret, 56);
            ulong input_lo = Rd64(d, 0);
            ulong input_hi = Rd64(d, len - 8);
            Mult64to128(input_lo ^ input_hi ^ bitflipl, PRIME64_1, out ulong mlo, out ulong mhi);
            mlo += ((ulong)(len - 1)) << 54;
            input_hi ^= bitfliph;
            mhi += input_hi + ((ulong)(uint)input_hi) * (PRIME32_2 - 1);
            mlo ^= Swap64(mhi);
            Mult64to128(mlo, PRIME64_2, out ulong hlo, out ulong hhi);
            hhi += mhi * PRIME64_2;
            lo = Xxh3Avalanche(hlo);
            hi = Xxh3Avalanche(hhi);
        }

        static void Len0to16_128(byte[] d, int len, out ulong lo, out ulong hi)
        {
            if (len > 8) { Len9to16_128(d, len, out lo, out hi); return; }
            if (len >= 4) { Len4to8_128(d, len, out lo, out hi); return; }
            if (len > 0) { Len1to3_128(d, len, out lo, out hi); return; }
            lo = Xxh64Avalanche(Rd64(KSecret, 64) ^ Rd64(KSecret, 72));
            hi = Xxh64Avalanche(Rd64(KSecret, 80) ^ Rd64(KSecret, 88));
        }

        static void Mix32B(ref ulong accLo, ref ulong accHi, byte[] d, int off1, int off2, int secOff)
        {
            accLo += Mix16B(d, off1, secOff);
            accLo ^= Rd64(d, off2) + Rd64(d, off2 + 8);
            accHi += Mix16B(d, off2, secOff + 16);
            accHi ^= Rd64(d, off1) + Rd64(d, off1 + 8);
        }

        static void Finalize128(ulong accLo, ulong accHi, int len, out ulong lo, out ulong hi)
        {
            lo = Xxh3Avalanche(accLo + accHi);
            hi = (ulong)0 - Xxh3Avalanche(accLo * PRIME64_1 + accHi * PRIME64_4 + ((ulong)len) * PRIME64_2);
        }

        static void Len17to128_128(byte[] d, int len, out ulong lo, out ulong hi)
        {
            ulong accLo = (ulong)len * PRIME64_1, accHi = 0UL;
            if (len > 32)
            {
                if (len > 64)
                {
                    if (len > 96) Mix32B(ref accLo, ref accHi, d, 48, len - 64, 96);
                    Mix32B(ref accLo, ref accHi, d, 32, len - 48, 64);
                }
                Mix32B(ref accLo, ref accHi, d, 16, len - 32, 32);
            }
            Mix32B(ref accLo, ref accHi, d, 0, len - 16, 0);
            Finalize128(accLo, accHi, len, out lo, out hi);
        }

        static void Len129to240_128(byte[] d, int len, out ulong lo, out ulong hi)
        {
            const int StartOffset = 3, LastOffset = 17;
            ulong accLo = (ulong)len * PRIME64_1, accHi = 0UL;
            for (int i = 32; i < 160; i += 32)
                Mix32B(ref accLo, ref accHi, d, i - 32, i - 16, i - 32);
            accLo = Xxh3Avalanche(accLo);
            accHi = Xxh3Avalanche(accHi);
            for (int i = 160; i <= len; i += 32)
                Mix32B(ref accLo, ref accHi, d, i - 32, i - 16, StartOffset + i - 160);
            Mix32B(ref accLo, ref accHi, d, len - 16, len - 32, 136 - LastOffset - 16);
            Finalize128(accLo, accHi, len, out lo, out hi);
        }

        public static ulong Xxh3_128Low(byte[] d, int len)
        {
            if (d == null || len == 0)
            {
                Len0to16_128(d, 0, out ulong l0, out ulong h0);
                return l0;
            }
            if (len <= 16) { Len0to16_128(d, len, out ulong l, out ulong h); return l; }
            if (len <= 128) { Len17to128_128(d, len, out ulong l, out ulong h); return l; }
            if (len <= 240) { Len129to240_128(d, len, out ulong l, out ulong h); return l; }

            ulong[] acc = { PRIME32_3, PRIME64_1, PRIME64_2, PRIME64_3, PRIME64_4, PRIME32_2, PRIME64_5, PRIME32_1 };
            HashLongLoop(acc, d, len, 192);
            return MergeAccs(acc, 11, (ulong)len * PRIME64_1);
        }
    }
}
