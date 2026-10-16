using System;
using System.Collections.Generic;

namespace LibraryEditor.Formats
{
    /// <summary>
    /// .ALib 容器读写器（domino 客户端）。
    /// 布局：int32 Version=2 | int32 N | int32 Offsets[N] | 帧区（无尾块、无元数据块）。
    /// 全部 N 项偏移都是帧；偏移严格递增、相邻零间隙、最后一帧恰好到 EOF。
    /// 各帧记录与 .Elite 同构（17 字节头 + gzip），但解压后为「10 字节常量前缀 + BGRA8888 W*H*4」。
    /// </summary>
    public sealed class ALibContainer
    {
        public const int ExpectedVersion = 2;
        public const int PayloadPrefixLength = 10;

        /// <summary>解压负载的 10 字节常量前缀：FF 64 00 01 00 FF 00 01 FF C8</summary>
        public static readonly byte[] PayloadPrefix =
            new byte[] { 0xFF, 0x64, 0x00, 0x01, 0x00, 0xFF, 0x00, 0x01, 0xFF, 0xC8 };

        public int Version { get; private set; }
        public int OriginalCount { get; private set; }
        public List<FrameRecord> Frames { get; private set; }

        private ALibContainer()
        {
            Frames = new List<FrameRecord>();
        }

        public static ALibContainer Parse(byte[] data)
        {
            if (data == null) throw new EliteFormatException("data 为 null");
            if (data.Length < 8) throw new EliteFormatException("文件过短: " + data.Length);

            ALibContainer c = new ALibContainer();
            c.Version = BitConverter.ToInt32(data, 0);
            c.OriginalCount = BitConverter.ToInt32(data, 4);
            int n = c.OriginalCount;

            if (n < 0) throw new EliteFormatException("N 为负: " + n);
            long offTableEndL = 8L + 4L * n;
            if (offTableEndL > data.Length)
                throw new EliteFormatException("偏移表越界: N=" + n + " len=" + data.Length);
            int frameStartPos = (int)offTableEndL;

            int[] offs = new int[n];
            for (int i = 0; i < n; i++)
                offs[i] = BitConverter.ToInt32(data, 8 + 4 * i);

            for (int i = 0; i < n; i++)
            {
                int pos = offs[i];
                if (pos < frameStartPos || pos > data.Length)
                    throw new EliteFormatException("帧偏移越界: i=" + i + " off=" + pos);

                FrameRecord fr = new FrameRecord();
                int end;
                FrameIo.ReadFrame(data, pos, fr, out end);

                int nextPos = (i < n - 1) ? offs[i + 1] : data.Length;
                if (nextPos < end || nextPos > data.Length)
                    throw new EliteFormatException("帧间偏移不一致: i=" + i + " end=" + end + " next=" + nextPos);

                fr.TrailingBytes = FrameIo.Slice(data, end, nextPos);
                c.Frames.Add(fr);
            }
            return c;
        }

        /// <summary>重建完整文件字节。没有任何 Modified 时输出与原始字节完全一致。</summary>
        public byte[] Rebuild()
        {
            int n = Frames.Count;
            int frameStartPos = 8 + 4 * n;

            int[] offs = new int[n];
            long cursor = frameStartPos;
            for (int i = 0; i < n; i++)
            {
                FrameRecord fr = Frames[i];
                offs[i] = (int)cursor;
                cursor += FrameIo.HeaderSize + (fr.Payload != null ? fr.Payload.Length : 0)
                        + (fr.TrailingBytes != null ? fr.TrailingBytes.Length : 0);
            }

            byte[] outb = new byte[cursor];
            BitConverter.GetBytes(Version).CopyTo(outb, 0);
            BitConverter.GetBytes(n).CopyTo(outb, 4);
            for (int i = 0; i < n; i++)
                BitConverter.GetBytes(offs[i]).CopyTo(outb, 8 + 4 * i);

            int p = frameStartPos;
            for (int i = 0; i < n; i++)
            {
                FrameRecord fr = Frames[i];
                p = FrameIo.WriteFrame(outb, p, fr);
                byte[] tr = fr.TrailingBytes ?? Array.Empty<byte>();
                if (tr.Length > 0)
                {
                    Buffer.BlockCopy(tr, 0, outb, p, tr.Length);
                    p += tr.Length;
                }
            }
            return outb;
        }

        /// <summary>
        /// 由 BGRA8888 原始像素构造可直接赋给 FrameRecord.Payload 的负载字节（确定性 gzip）。
        /// prependPrefix=true 时在 BGRA 前加 10 字节常量前缀（ALib 恒为 true）。
        /// </summary>
        public static byte[] BuildPayload(byte[] bgra, bool prependPrefix = true)
        {
            if (bgra == null) bgra = Array.Empty<byte>();
            byte[] raw;
            if (prependPrefix)
            {
                raw = new byte[PayloadPrefixLength + bgra.Length];
                Buffer.BlockCopy(PayloadPrefix, 0, raw, 0, PayloadPrefixLength);
                Buffer.BlockCopy(bgra, 0, raw, PayloadPrefixLength, bgra.Length);
            }
            else
            {
                raw = bgra;
            }
            return FrameIo.GzipCompress(raw);
        }

        /// <summary>
        /// 从帧负载（gzip 字节；若已是明文则原样处理）提取 BGRA8888。
        /// stripPrefix=true 时跳过 10 字节常量前缀。输出长度固定为 expectedBgraLength。
        /// </summary>
        public static byte[] ExtractBgra(byte[] gzipPayload, int expectedBgraLength, bool stripPrefix = true)
        {
            if (gzipPayload == null) gzipPayload = Array.Empty<byte>();
            byte[] raw = FrameIo.IsGzip(gzipPayload) ? FrameIo.GzipDecompress(gzipPayload) : gzipPayload;

            int skip = stripPrefix ? PayloadPrefixLength : 0;
            int avail = raw.Length - skip;
            if (avail < 0) avail = 0;
            if (avail < expectedBgraLength)
                throw new EliteFormatException("BGRA 长度不足: 实得 " + avail + " 期望 " + expectedBgraLength);

            byte[] bgra = new byte[expectedBgraLength];
            if (expectedBgraLength > 0)
                Buffer.BlockCopy(raw, skip, bgra, 0, expectedBgraLength);
            return bgra;
        }
    }
}
