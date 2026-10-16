using System;
using System.Collections.Generic;
using System.Text;

namespace LibraryEditor.Formats
{
    /// <summary>
    /// WEMADE .WTL（ILIB）一帧的原始记录。
    /// v1.3 记录 16 字节：W/H/X/Y/SX/SY(int16×6) + Length(3 字节小端) + Shadow(bit7=有掩码)。
    /// v2.0 记录 20 字节：W/H/X/Y/SX/SY(int16×6) + Unknown1 + ImageTextureType + Unknown2 + MaskTextureType + Length(uint32)。
    /// 负载：v1.3 = "计数表 + DXT1 块"流（不压缩、不填充）；v2.0 = 裸 DEFLATE 流（区域按 4 字节向上对齐）。
    /// 独立掩码段（v2.0 Unknown2!=0 / v1.3 Shadow bit7）：紧跟图像负载之后。
    /// </summary>
    public sealed class WtlFrameRecord
    {
        public short Width;
        public short Height;
        public short X;
        public short Y;
        public short ShadowX;
        public short ShadowY;

        // ---- v2.0 记录专用字段 ----
        /// <summary>记录 +0x0C 未知名（实测 0..3），原样保留。</summary>
        public byte Unknown1;
        /// <summary>图像 DXT 类型：0/1=DXT1，3=DXT3，5=DXT5。注意：MaskTextureType 带 0x80 位时负载实为 raw 32bpp。</summary>
        public byte ImageTextureType;
        /// <summary>记录 +0x0E：1 表示存在独立掩码段（以此字节为准，与 MaskTextureType bit0 一致）。</summary>
        public byte Unknown2;
        /// <summary>记录 +0x0F。bit0=独立掩码段；bit7=整帧纹理按 raw 32bpp（BGRA，alpha 即掩码）存储。</summary>
        public byte MaskTextureType;

        // ---- v1.3 记录专用字段 ----
        /// <summary>v1.3 记录 +0x0F（bit7=有掩码，低 7 位为阴影值）。</summary>
        public byte ShadowByte;

        /// <summary>长度字段原值（v1.3 为 3 字节值；v2.0 为 uint32）。</summary>
        public int StoredLength;

        /// <summary>
        /// 图像负载区原样字节。v1.3 = StoredLength 字节；
        /// v2.0 = Pad4(StoredLength) 字节（含尾部对齐填充，DEFLATE 流本身 StoredLength 字节）。
        /// </summary>
        public byte[] Payload = Array.Empty<byte>();

        // ---- 独立掩码段 ----
        /// <summary>独立掩码负载原样字节；null = 无独立掩码段。</summary>
        public byte[] MaskPayload;

        /// <summary>掩码段长度字段原值（v2.0 为 int32；v1.3 为 3 字节值）。</summary>
        public int MaskStoredLength;

        // v1.3 掩码段自带 16 字节头
        public short MaskWidth, MaskHeight, MaskX, MaskY, MaskShadowX, MaskShadowY;
        /// <summary>v1.3 掩码段尾字节（阴影）。</summary>
        public byte MaskShadow;

        // ---- 元信息 ----
        /// <summary>在原文件中的绝对偏移；-1 = 新增帧。</summary>
        public int RawRecordOffset = -1;

        /// <summary>原文件中该记录的完整原始字节（头 + 负载 + 掩码段）；未改动帧保存时原样回写。</summary>
        public byte[] RawBytes;

        /// <summary>调用方置位表示字段已被替换（保存时按字段重新序列化）。</summary>
        public bool Modified;

        public bool HasSeparateMask
        {
            get { return MaskPayload != null; }
        }

        public WtlFrameRecord CloneShallow()
        {
            return new WtlFrameRecord
            {
                Width = Width, Height = Height, X = X, Y = Y, ShadowX = ShadowX, ShadowY = ShadowY,
                Unknown1 = Unknown1, ImageTextureType = ImageTextureType,
                Unknown2 = Unknown2, MaskTextureType = MaskTextureType,
                ShadowByte = ShadowByte,
                StoredLength = StoredLength, Payload = Payload,
                MaskPayload = MaskPayload, MaskStoredLength = MaskStoredLength,
                MaskWidth = MaskWidth, MaskHeight = MaskHeight, MaskX = MaskX, MaskY = MaskY,
                MaskShadowX = MaskShadowX, MaskShadowY = MaskShadowY, MaskShadow = MaskShadow,
                RawRecordOffset = RawRecordOffset, RawBytes = RawBytes, Modified = Modified,
            };
        }
    }

    /// <summary>
    /// WEMADE .WTL（ILIB）容器读写器。纯字节层，不依赖 WinForms / System.Drawing。
    ///
    /// 32 字节头：uint16 魔数(0x0002/0x0010) | 20 字节版本串("ILIB v1.3/v2.0-WEMADE") |
    ///            uint16 6000/7000 | uint32 未知名 | uint32 帧数 N。
    /// 索引表：v1.3 位于头后（N×uint32）；v2.0 位于文件尾（N×uint32）。0 = 空帧。
    /// 帧数据区：从首个非零偏移起连续排布、无缝铺满至数据区末尾
    ///          （v1.3 末尾 = 文件尾；v2.0 末尾 = 文件尾 - N×4）。
    ///
    /// 字节级不变式：容器未改动时，Rebuild() 输出与原始文件逐字节一致
    /// （含魔数、版本串填充杂字节、@0x18 未知名、v1.3 表尾垫字节、负载对齐填充）。
    /// </summary>
    public sealed class WtlContainer
    {
        public const int HeaderLength = 32;
        public const string VersionV13 = "ILIB v1.3-WEMADE";
        public const string VersionV20 = "ILIB v2.0-WEMADE";

        /// <summary>32 字节头原样字节（Rebuild 时仅回写 @0x1C 的帧数）。</summary>
        public byte[] Header = new byte[HeaderLength];

        public bool IsNewVersion;

        /// <summary>版本串（去除尾部 NUL 后；仅诊断用）。</summary>
        public string VersionText = string.Empty;

        /// <summary>帧记录，数据区顺序。</summary>
        public List<WtlFrameRecord> Records = new List<WtlFrameRecord>();

        /// <summary>索引 → Records 下标；-1 = 空帧槽。长度 = 帧数 N。</summary>
        public int[] EntryRecord = Array.Empty<int>();

        /// <summary>v1.3 索引表尾与首个非零偏移之间的垫字节（实测 0..数百字节，原样保留）。</summary>
        public byte[] TableGapBytes = Array.Empty<byte>();

        // ---------------- 识别 ----------------

        /// <summary>从文件头判断是否为本库支持的 WTL（v1.3 / v2.0 前缀均可，容忍版本串尾部杂字节）。</summary>
        public static bool IsWtl(byte[] head)
        {
            if (head == null || head.Length < 22) return false;
            string v = Encoding.ASCII.GetString(head, 2, 20);
            return v.StartsWith(VersionV13, StringComparison.Ordinal)
                || v.StartsWith(VersionV20, StringComparison.Ordinal);
        }

        // ---------------- 解析 ----------------

        public static WtlContainer Parse(byte[] data)
        {
            if (data == null) throw new EliteFormatException("WTL 数据为 null");
            if (data.Length < HeaderLength)
                throw new EliteFormatException("WTL 文件过短（不足 32 字节头）: " + data.Length);

            string ver = Encoding.ASCII.GetString(data, 2, 20).TrimEnd('\0');
            bool isNew = ver.StartsWith(VersionV20, StringComparison.Ordinal);
            if (!isNew && !ver.StartsWith(VersionV13, StringComparison.Ordinal))
                throw new EliteFormatException("无法识别的 WTL 版本串: \"" + ver + "\"");

            uint n32 = BitConverter.ToUInt32(data, 0x1C);
            if (n32 > int.MaxValue / 4)
                throw new EliteFormatException("WTL 帧数异常: " + n32);
            int n = (int)n32;

            long tableBase = isNew ? data.Length - 4L * n : HeaderLength;
            if (tableBase < HeaderLength || tableBase + 4L * n > data.Length)
                throw new EliteFormatException(
                    "WTL 索引表越界: 文件长度 " + data.Length + ", 帧数 " + n + ", 新版=" + isNew);

            int dataEnd = isNew ? (int)tableBase : data.Length;
            int v13TableEnd = HeaderLength + 4 * n;

            // ---- 先按数据区顺序解析全部记录（按偏移排序） ----
            Dictionary<int, WtlFrameRecord> byOffset = new Dictionary<int, WtlFrameRecord>();
            List<int> nzOffsets = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int off = BitConverter.ToInt32(data, (int)tableBase + 4 * i);
                if (off == 0) continue;
                if (!byOffset.ContainsKey(off))
                {
                    byOffset[off] = ParseRecord(data, off, dataEnd, isNew);
                    nzOffsets.Add(off);
                }
            }
            nzOffsets.Sort();

            // ---- 记录区必须无缝铺满数据区（否则文件布局未被理解，拒绝打开以防写坏） ----
            for (int k = 0; k < nzOffsets.Count; k++)
            {
                WtlFrameRecord fr = byOffset[nzOffsets[k]];
                int end = fr.RawRecordOffset + fr.RawBytes.Length;
                int next = (k + 1 < nzOffsets.Count) ? nzOffsets[k + 1] : dataEnd;
                if (end != next)
                    throw new EliteFormatException(
                        "WTL 帧记录区不连续: 记录 @" + fr.RawRecordOffset + " 结束于 " + end +
                        ", 期望下一记录偏移 " + next);
            }

            WtlContainer c = new WtlContainer();
            Buffer.BlockCopy(data, 0, c.Header, 0, HeaderLength);
            c.IsNewVersion = isNew;
            c.VersionText = ver;

            // v1.3 表尾垫字节（[表尾, 首个非零偏移) 区间原样保留）
            if (!isNew)
            {
                int gapStart = v13TableEnd;
                int gapEnd = nzOffsets.Count > 0 ? nzOffsets[0] : dataEnd;
                if (gapEnd < gapStart)
                    throw new EliteFormatException("WTL v1.3 首帧偏移落入索引表: " +
                        (nzOffsets.Count > 0 ? nzOffsets[0].ToString() : "无"));
                if (gapEnd > gapStart)
                    c.TableGapBytes = FrameIo.Slice(data, gapStart, gapEnd);
            }

            // 记录列表（数据区顺序）+ 索引映射（偏移相同 = 共享帧，映射到同一记录）
            c.Records = new List<WtlFrameRecord>(nzOffsets.Count);
            Dictionary<int, int> offsetToIndex = new Dictionary<int, int>(nzOffsets.Count);
            for (int k = 0; k < nzOffsets.Count; k++)
            {
                offsetToIndex[nzOffsets[k]] = k;
                c.Records.Add(byOffset[nzOffsets[k]]);
            }

            c.EntryRecord = new int[n];
            for (int i = 0; i < n; i++)
            {
                int off = BitConverter.ToInt32(data, (int)tableBase + 4 * i);
                int ri;
                c.EntryRecord[i] = (off != 0 && offsetToIndex.TryGetValue(off, out ri)) ? ri : -1;
            }

            return c;
        }

        private static WtlFrameRecord ParseRecord(byte[] data, int off, int dataEnd, bool isNew)
        {
            if (off < 0 || off >= dataEnd)
                throw new EliteFormatException("WTL 帧偏移越界: " + off + " (数据区末尾 " + dataEnd + ")");

            WtlFrameRecord fr = new WtlFrameRecord();
            fr.RawRecordOffset = off;
            fr.Width = BitConverter.ToInt16(data, off + 0x00);
            fr.Height = BitConverter.ToInt16(data, off + 0x02);
            fr.X = BitConverter.ToInt16(data, off + 0x04);
            fr.Y = BitConverter.ToInt16(data, off + 0x06);
            fr.ShadowX = BitConverter.ToInt16(data, off + 0x08);
            fr.ShadowY = BitConverter.ToInt16(data, off + 0x0A);

            int headerLen, payloadLen;
            if (isNew)
            {
                headerLen = 20;
                fr.Unknown1 = data[off + 0x0C];
                fr.ImageTextureType = data[off + 0x0D];
                fr.Unknown2 = data[off + 0x0E];
                fr.MaskTextureType = data[off + 0x0F];
                fr.StoredLength = (int)BitConverter.ToUInt32(data, off + 0x10);
                if (fr.StoredLength < 0)
                    throw new EliteFormatException("WTL v2.0 帧长度异常: " + fr.StoredLength + " @" + off);
                payloadLen = Pad4(fr.StoredLength);
            }
            else
            {
                headerLen = 16;
                fr.StoredLength = data[off + 0x0C] | (data[off + 0x0D] << 8) | (data[off + 0x0E] << 16);
                fr.ShadowByte = data[off + 0x0F];
                payloadLen = fr.StoredLength;
            }

            int pos = off + headerLen;
            if (pos + (long)payloadLen > dataEnd)
                throw new EliteFormatException("WTL 帧负载越界: @" + off + " 长度 " + payloadLen);

            fr.Payload = FrameIo.Slice(data, pos, pos + payloadLen);
            pos += payloadLen;

            // 独立掩码段
            bool hasMaskSeg = isNew ? fr.Unknown2 != 0 : (fr.ShadowByte & 0x80) != 0;
            if (hasMaskSeg)
            {
                if (isNew)
                {
                    if (pos + 4 > dataEnd)
                        throw new EliteFormatException("WTL v2.0 掩码长度越界: @" + off);
                    fr.MaskStoredLength = BitConverter.ToInt32(data, pos);
                    if (fr.MaskStoredLength < 0)
                        throw new EliteFormatException("WTL v2.0 掩码长度异常: " + fr.MaskStoredLength + " @" + off);
                    pos += 4;
                    int maskLen = Pad4(fr.MaskStoredLength);
                    if (pos + (long)maskLen > dataEnd)
                        throw new EliteFormatException("WTL v2.0 掩码负载越界: @" + off);
                    fr.MaskPayload = FrameIo.Slice(data, pos, pos + maskLen);
                    pos += maskLen;
                }
                else
                {
                    if (pos + 16 > dataEnd)
                        throw new EliteFormatException("WTL v1.3 掩码头越界: @" + off);
                    fr.MaskWidth = BitConverter.ToInt16(data, pos + 0x00);
                    fr.MaskHeight = BitConverter.ToInt16(data, pos + 0x02);
                    fr.MaskX = BitConverter.ToInt16(data, pos + 0x04);
                    fr.MaskY = BitConverter.ToInt16(data, pos + 0x06);
                    fr.MaskShadowX = BitConverter.ToInt16(data, pos + 0x08);
                    fr.MaskShadowY = BitConverter.ToInt16(data, pos + 0x0A);
                    fr.MaskStoredLength = data[pos + 0x0C] | (data[pos + 0x0D] << 8) | (data[pos + 0x0E] << 16);
                    fr.MaskShadow = data[pos + 0x0F];
                    pos += 16;
                    if (pos + (long)fr.MaskStoredLength > dataEnd)
                        throw new EliteFormatException("WTL v1.3 掩码负载越界: @" + off);
                    fr.MaskPayload = FrameIo.Slice(data, pos, pos + fr.MaskStoredLength);
                    pos += fr.MaskStoredLength;
                }
            }

            fr.RawBytes = FrameIo.Slice(data, off, pos);
            return fr;
        }

        public static int Pad4(int x)
        {
            return x + ((4 - (x % 4)) % 4);
        }

        // ---------------- 重建 ----------------

        /// <summary>
        /// 重建完整文件字节。容器未改动（所有记录 Modified==false 且 RawBytes 保留）时输出与原文件逐字节一致。
        /// </summary>
        public byte[] Rebuild()
        {
            int n = EntryRecord.Length;
            long tableLen = 4L * n;
            bool hasGap = !IsNewVersion && TableGapBytes != null && TableGapBytes.Length > 0;
            long dataStart = HeaderLength + (IsNewVersion ? 0 : tableLen + (hasGap ? TableGapBytes.Length : 0));

            // 记录偏移与总长
            int[] recOff = new int[Records.Count];
            long cur = dataStart;
            for (int r = 0; r < Records.Count; r++)
            {
                recOff[r] = (int)cur;
                cur += RecordSize(Records[r]);
            }
            long total = cur + (IsNewVersion ? tableLen : 0);

            byte[] outBuf = new byte[total];
            Buffer.BlockCopy(Header, 0, outBuf, 0, HeaderLength);
            BitConverter.GetBytes((uint)n).CopyTo(outBuf, 0x1C);

            // 索引表
            long tablePos = IsNewVersion ? total - tableLen : HeaderLength;
            for (int i = 0; i < n; i++)
            {
                int ri = EntryRecord[i];
                int v = (ri >= 0 && ri < recOff.Length) ? recOff[ri] : 0;
                BitConverter.GetBytes((uint)v).CopyTo(outBuf, (int)tablePos + 4 * i);
            }

            // v1.3 表尾垫字节
            if (hasGap)
                Buffer.BlockCopy(TableGapBytes, 0, outBuf, HeaderLength + (int)tableLen, TableGapBytes.Length);

            // 记录
            for (int r = 0; r < Records.Count; r++)
                WriteRecord(outBuf, recOff[r], Records[r]);

            return outBuf;
        }

        /// <summary>记录序列化后的总字节数。</summary>
        public int RecordSize(WtlFrameRecord fr)
        {
            int size = IsNewVersion ? 20 : 16;
            size += fr.Payload != null ? fr.Payload.Length : 0;
            if (fr.HasSeparateMask)
                size += (IsNewVersion ? 4 : 16) + fr.MaskPayload.Length;
            return size;
        }

        private void WriteRecord(byte[] dst, int pos, WtlFrameRecord fr)
        {
            // 未改动帧：原样回写（含全部保留字节与对齐填充）
            if (!fr.Modified && fr.RawBytes != null)
            {
                Buffer.BlockCopy(fr.RawBytes, 0, dst, pos, fr.RawBytes.Length);
                return;
            }

            byte[] payload = fr.Payload ?? Array.Empty<byte>();

            if (IsNewVersion)
            {
                BitConverter.GetBytes(fr.Width).CopyTo(dst, pos + 0x00);
                BitConverter.GetBytes(fr.Height).CopyTo(dst, pos + 0x02);
                BitConverter.GetBytes(fr.X).CopyTo(dst, pos + 0x04);
                BitConverter.GetBytes(fr.Y).CopyTo(dst, pos + 0x06);
                BitConverter.GetBytes(fr.ShadowX).CopyTo(dst, pos + 0x08);
                BitConverter.GetBytes(fr.ShadowY).CopyTo(dst, pos + 0x0A);
                dst[pos + 0x0C] = fr.Unknown1;
                dst[pos + 0x0D] = fr.ImageTextureType;
                dst[pos + 0x0E] = fr.HasSeparateMask ? (byte)1 : (byte)0;
                dst[pos + 0x0F] = fr.MaskTextureType;
                BitConverter.GetBytes((uint)fr.StoredLength).CopyTo(dst, pos + 0x10);
                Buffer.BlockCopy(payload, 0, dst, pos + 20, payload.Length);
                pos += 20 + payload.Length;

                if (fr.HasSeparateMask)
                {
                    BitConverter.GetBytes(fr.MaskStoredLength).CopyTo(dst, pos);
                    pos += 4;
                    Buffer.BlockCopy(fr.MaskPayload, 0, dst, pos, fr.MaskPayload.Length);
                }
            }
            else
            {
                int len = fr.StoredLength;
                byte shadow = fr.HasSeparateMask ? (byte)(fr.ShadowByte | 0x80) : (byte)(fr.ShadowByte & 0x7F);
                BitConverter.GetBytes(fr.Width).CopyTo(dst, pos + 0x00);
                BitConverter.GetBytes(fr.Height).CopyTo(dst, pos + 0x02);
                BitConverter.GetBytes(fr.X).CopyTo(dst, pos + 0x04);
                BitConverter.GetBytes(fr.Y).CopyTo(dst, pos + 0x06);
                BitConverter.GetBytes(fr.ShadowX).CopyTo(dst, pos + 0x08);
                BitConverter.GetBytes(fr.ShadowY).CopyTo(dst, pos + 0x0A);
                dst[pos + 0x0C] = (byte)len;
                dst[pos + 0x0D] = (byte)(len >> 8);
                dst[pos + 0x0E] = (byte)(len >> 16);
                dst[pos + 0x0F] = shadow;

                // v1.3 图像负载：StoredLength 字节，不压缩、不填充，紧跟 16 字节记录头
                int imgLen = fr.Payload != null ? fr.Payload.Length : 0;
                if (imgLen > 0)
                    Buffer.BlockCopy(fr.Payload, 0, dst, pos + 16, imgLen);
                pos += 16 + imgLen;

                if (fr.HasSeparateMask)
                {
                    int ml = fr.MaskStoredLength;
                    BitConverter.GetBytes(fr.MaskWidth).CopyTo(dst, pos + 0x00);
                    BitConverter.GetBytes(fr.MaskHeight).CopyTo(dst, pos + 0x02);
                    BitConverter.GetBytes(fr.MaskX).CopyTo(dst, pos + 0x04);
                    BitConverter.GetBytes(fr.MaskY).CopyTo(dst, pos + 0x06);
                    BitConverter.GetBytes(fr.MaskShadowX).CopyTo(dst, pos + 0x08);
                    BitConverter.GetBytes(fr.MaskShadowY).CopyTo(dst, pos + 0x0A);
                    dst[pos + 0x0C] = (byte)ml;
                    dst[pos + 0x0D] = (byte)(ml >> 8);
                    dst[pos + 0x0E] = (byte)(ml >> 16);
                    dst[pos + 0x0F] = fr.MaskShadow;
                    pos += 16;

                    // v1.3 独立掩码负载
                    int mskLen = fr.MaskPayload != null ? fr.MaskPayload.Length : 0;
                    if (mskLen > 0)
                        Buffer.BlockCopy(fr.MaskPayload, 0, dst, pos, mskLen);
                }
            }
        }
    }
}
