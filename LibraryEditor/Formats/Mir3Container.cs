using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace LibraryEditor.Formats
{
    /// <summary>
    /// Mir3（传奇3）一帧的原始数据：16 字节头 + 裸 zlib 负载。
    /// 头 = Width/Height/OffsetX/OffsetY(int16) + CompLen(uint32 @+0x08) + Reserved(uint32 @+0x0C)。
    /// </summary>
    public sealed class Mir3FrameRecord
    {
        public short Width;
        public short Height;
        public short OffsetX;
        public short OffsetY;

        /// <summary>头 +0x0C 的未知名（同一文件内恒定，实测 0/2/256/258/259/0x4B6C00/0x4B6D00），原样保留。</summary>
        public uint Reserved;

        /// <summary>原始裸 zlib 字节（长度 = 头 +0x08 的 CompLen）；空帧 = Array.Empty&lt;byte&gt;()。</summary>
        public byte[] Payload = Array.Empty<byte>();

        /// <summary>调用方置位表示 Payload/坐标已被替换。</summary>
        public bool Modified;

        public Mir3FrameRecord CloneShallow()
        {
            return new Mir3FrameRecord
            {
                Width = Width,
                Height = Height,
                OffsetX = OffsetX,
                OffsetY = OffsetY,
                Reserved = Reserved,
                Payload = Payload,
                Modified = Modified,
            };
        }
    }

    /// <summary>
    /// Mir3（传奇3）MIX(索引) + MIZ(数据) 容器读写器。纯字节层，不依赖 WinForms / System.Drawing。
    ///
    /// MIX：byte[16] 签名 | uint32 N | uint32 未知名 | int32[Nt] 偏移表（Nt=(len-24)/4，可能 &gt; N）。
    /// MIZ：byte[16] 签名 | uint32 N | uint32 未知名 | 帧记录连续存放直至 EOF。
    ///
    /// 字节级不变式：两文件都未改动时，RebuildMix()/RebuildMiz() 输出与原始文件逐字节一致。
    /// </summary>
    public sealed class Mir3Container
    {
        public const int SignatureLength = 16;
        public const int FileHeaderLength = 24;   // 16 签名 + N + unknown
        public const int FrameHeaderLength = 16;

        /// <summary>签名变体 A：0F 28 43 29 20 4D 49 52 33 2C 20 4D 49 52 33 2E（"\x0F(C) MIR3, MIR3."）。</summary>
        public static readonly byte[] SignatureA = new byte[]
        {
            0x0F, 0x28, 0x43, 0x29, 0x20, 0x4D, 0x49, 0x52,
            0x33, 0x2C, 0x20, 0x4D, 0x49, 0x52, 0x33, 0x2E
        };

        /// <summary>签名变体 B 的前 5 字节：04 4D 69 72 33（"\x04Mir3"）。</summary>
        public static readonly byte[] SignaturePrefixB = new byte[] { 0x04, 0x4D, 0x69, 0x72, 0x33 };

        // ---- MIX ----
        public byte[] MixSignature = new byte[SignatureLength];
        public uint MixDeclaredCount;     // MIX 头部声明的 N
        public uint MixUnknown;           // MIX @0x14
        public int[] Entries = Array.Empty<int>();   // Nt 条，原样（0=空帧，24=重复引用第0帧）
        private int _mixOriginalEntryCount;

        // ---- MIZ ----
        public byte[] MizSignature = new byte[SignatureLength];
        public uint MizDeclaredCount;     // MIZ 头部声明的 N
        public uint MizUnknown;           // MIZ @0x14
        public List<Mir3FrameRecord> Frames = new List<Mir3FrameRecord>();
        private int _mizOriginalFrameCount;

        /// <summary>每个 MIX 条目对应的帧索引（-1 = 空帧槽）。长度 == Entries.Length。</summary>
        public int[] EntryFrameIndex = Array.Empty<int>();

        // ---------------- 识别 ----------------

        /// <summary>按魔数判断是否为 Mir3 文件（MIX/MIZ 均适用）。</summary>
        public static bool IsMir3(byte[] head)
        {
            if (head == null) return false;
            if (head.Length >= SignatureLength)
            {
                bool a = true;
                for (int i = 0; i < SignatureLength; i++)
                    if (head[i] != SignatureA[i]) { a = false; break; }
                if (a) return true;
            }
            if (head.Length >= SignaturePrefixB.Length)
            {
                bool b = true;
                for (int i = 0; i < SignaturePrefixB.Length; i++)
                    if (head[i] != SignaturePrefixB[i]) { b = false; break; }
                if (b) return true;
            }
            return false;
        }

        // ---------------- 解析 ----------------

        public static Mir3Container Parse(byte[] mixData, byte[] mizData)
        {
            if (mixData == null) throw new EliteFormatException("MIX 数据为 null");
            if (mizData == null) throw new EliteFormatException("MIZ 数据为 null");
            if (mixData.Length < FileHeaderLength) throw new EliteFormatException("MIX 文件过短: " + mixData.Length);
            if (mizData.Length < FileHeaderLength) throw new EliteFormatException("MIZ 文件过短: " + mizData.Length);

            Mir3Container c = new Mir3Container();

            c.MixSignature = FrameIo.Slice(mixData, 0, SignatureLength);
            c.MixDeclaredCount = BitConverter.ToUInt32(mixData, 0x10);
            c.MixUnknown = BitConverter.ToUInt32(mixData, 0x14);

            c.MizSignature = FrameIo.Slice(mizData, 0, SignatureLength);
            c.MizDeclaredCount = BitConverter.ToUInt32(mizData, 0x10);
            c.MizUnknown = BitConverter.ToUInt32(mizData, 0x14);

            // ---- 顺序扫描 MIZ 帧记录，必须恰好终止于 EOF ----
            Dictionary<int, int> offsetToIndex = new Dictionary<int, int>();
            int pos = FileHeaderLength;
            while (pos < mizData.Length)
            {
                if (pos + FrameHeaderLength > mizData.Length)
                    throw new EliteFormatException("MIZ 帧头越界: pos=" + pos + " len=" + mizData.Length);

                Mir3FrameRecord fr = new Mir3FrameRecord();
                fr.Width = BitConverter.ToInt16(mizData, pos + 0x00);
                fr.Height = BitConverter.ToInt16(mizData, pos + 0x02);
                fr.OffsetX = BitConverter.ToInt16(mizData, pos + 0x04);
                fr.OffsetY = BitConverter.ToInt16(mizData, pos + 0x06);
                uint compLen = BitConverter.ToUInt32(mizData, pos + 0x08);
                fr.Reserved = BitConverter.ToUInt32(mizData, pos + 0x0C);

                long end = (long)pos + FrameHeaderLength + compLen;
                if (end > mizData.Length)
                    throw new EliteFormatException("MIZ 帧负载越界: pos=" + pos + " compLen=" + compLen + " len=" + mizData.Length);

                fr.Payload = compLen == 0 ? Array.Empty<byte>() : FrameIo.Slice(mizData, pos + FrameHeaderLength, (int)end);

                offsetToIndex[pos] = c.Frames.Count;
                c.Frames.Add(fr);
                pos = (int)end;
            }
            // while 条件保证 pos == mizData.Length（顺序扫描恰好到 EOF）

            // ---- MIX 偏移表：Nt = (len-24)/4 ----
            long ntL = (mixData.Length - FileHeaderLength) / 4L;
            if ((mixData.Length - FileHeaderLength) % 4 != 0)
                throw new EliteFormatException("MIX 条目区长度非 4 的倍数: " + mixData.Length);
            int nt = (int)ntL;
            c._mixOriginalEntryCount = nt;

            c.Entries = new int[nt];
            c.EntryFrameIndex = new int[nt];
            for (int i = 0; i < nt; i++)
            {
                int v = BitConverter.ToInt32(mixData, FileHeaderLength + 4 * i);
                c.Entries[i] = v;
                if (v == 0)
                {
                    c.EntryFrameIndex[i] = -1;
                }
                else
                {
                    int idx;
                    if (!offsetToIndex.TryGetValue(v, out idx))
                        throw new EliteFormatException("MIX 条目值未落在帧起点: 条目 " + i + " 值 " + v);
                    c.EntryFrameIndex[i] = idx;
                }
            }

            c._mizOriginalFrameCount = c.Frames.Count;
            return c;
        }

        /// <summary>帧索引 → MIZ 内绝对偏移（以此顺序紧凑排布）。</summary>
        public int[] ComputeFrameOffsets()
        {
            int[] offs = new int[Frames.Count];
            long cur = FileHeaderLength;
            for (int i = 0; i < Frames.Count; i++)
            {
                offs[i] = (int)cur;
                cur += FrameHeaderLength + (Frames[i].Payload != null ? Frames[i].Payload.Length : 0);
            }
            return offs;
        }

        // ---------------- 重建 ----------------

        /// <summary>重建 MIX 字节。未改动时与原始 MIX 逐字节一致。</summary>
        public byte[] RebuildMix()
        {
            int nt = Entries.Length;
            uint countField = (nt == _mixOriginalEntryCount) ? MixDeclaredCount : (uint)nt;

            long total = FileHeaderLength + 4L * nt;
            byte[] outb = new byte[total];

            Buffer.BlockCopy(MixSignature, 0, outb, 0, SignatureLength);
            BitConverter.GetBytes(countField).CopyTo(outb, 0x10);
            BitConverter.GetBytes(MixUnknown).CopyTo(outb, 0x14);
            for (int i = 0; i < nt; i++)
                BitConverter.GetBytes(Entries[i]).CopyTo(outb, FileHeaderLength + 4 * i);
            return outb;
        }

        /// <summary>重建 MIZ 字节。未改动时与原始 MIZ 逐字节一致（含孤立帧原样保留）。</summary>
        public byte[] RebuildMiz()
        {
            int nf = Frames.Count;
            uint countField = (nf == _mizOriginalFrameCount) ? MizDeclaredCount : (uint)nf;

            long total = FileHeaderLength;
            for (int i = 0; i < nf; i++)
                total += FrameHeaderLength + (Frames[i].Payload != null ? Frames[i].Payload.Length : 0);

            byte[] outb = new byte[total];
            Buffer.BlockCopy(MizSignature, 0, outb, 0, SignatureLength);
            BitConverter.GetBytes(countField).CopyTo(outb, 0x10);
            BitConverter.GetBytes(MizUnknown).CopyTo(outb, 0x14);

            int p = FileHeaderLength;
            for (int i = 0; i < nf; i++)
            {
                Mir3FrameRecord fr = Frames[i];
                byte[] payload = fr.Payload ?? Array.Empty<byte>();
                BitConverter.GetBytes(fr.Width).CopyTo(outb, p + 0x00);
                BitConverter.GetBytes(fr.Height).CopyTo(outb, p + 0x02);
                BitConverter.GetBytes(fr.OffsetX).CopyTo(outb, p + 0x04);
                BitConverter.GetBytes(fr.OffsetY).CopyTo(outb, p + 0x06);
                BitConverter.GetBytes((uint)payload.Length).CopyTo(outb, p + 0x08);
                BitConverter.GetBytes(fr.Reserved).CopyTo(outb, p + 0x0C);
                if (payload.Length > 0)
                    Buffer.BlockCopy(payload, 0, outb, p + FrameHeaderLength, payload.Length);
                p += FrameHeaderLength + payload.Length;
            }
            return outb;
        }

        /// <summary>用当前 Frames 顺序重算偏移表并写回 Entries（供编辑后保存用）。</summary>
        public void RefreshEntriesFromFrameIndex()
        {
            int[] offs = ComputeFrameOffsets();
            for (int i = 0; i < Entries.Length; i++)
            {
                int fi = EntryFrameIndex[i];
                Entries[i] = (fi >= 0 && fi < offs.Length) ? offs[fi] : 0;
            }
        }

        // ---------------- zlib ----------------

        /// <summary>裸 zlib 压缩（非 gzip）。</summary>
        public static byte[] ZlibCompress(byte[] raw)
        {
            if (raw == null) raw = Array.Empty<byte>();
            using (MemoryStream ms = new MemoryStream())
            {
                using (ZLibStream z = new ZLibStream(ms, CompressionLevel.Optimal, true))
                {
                    z.Write(raw, 0, raw.Length);
                }
                return ms.ToArray();
            }
        }

        /// <summary>裸 zlib 解压。</summary>
        public static byte[] ZlibDecompress(byte[] z)
        {
            if (z == null || z.Length == 0) return Array.Empty<byte>();
            using (MemoryStream inMs = new MemoryStream(z, false))
            using (ZLibStream zs = new ZLibStream(inMs, CompressionMode.Decompress))
            using (MemoryStream outMs = new MemoryStream())
            {
                zs.CopyTo(outMs, 81920);
                return outMs.ToArray();
            }
        }
    }
}
