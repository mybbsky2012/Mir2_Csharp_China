using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace LibraryEditor.Formats
{
    /// <summary>
    /// 一帧的原始数据（不含任何语义解释）。
    /// 头 17 字节 = Width/Height/OffsetX/OffsetY(int16,int16,int16,int16) + HeaderTail(5 字节) + GZipLength(uint32 @ +0x0D)。
    /// </summary>
    public sealed class FrameRecord
    {
        // 头部 +0x00..+0x07
        public short Width;
        public short Height;
        public short OffsetX;
        public short OffsetY;

        // 头部 +0x08..+0x0C 的 5 字节，原样保存/回写（ShadowX/ShadowY/Shadow，不解释语义）
        public byte[] HeaderTail;

        // 原始 gzip 字节，长度 = 头部 +0x0D 的 uint32；空记录 = Array.Empty<byte>()
        public byte[] Payload;

        // 本帧结束 -> 下一帧起点之间的未索引字节（帧间间隙）；无则 Array.Empty<byte>()
        public byte[] TrailingBytes;

        // 调用方置位表示 Payload 已被替换
        public bool Modified;

        public FrameRecord()
        {
            HeaderTail = new byte[FrameIo.HeaderSize - 0x0D]; // 5 字节
            Payload = Array.Empty<byte>();
            TrailingBytes = Array.Empty<byte>();
        }
    }

    /// <summary>解析/重建容器失败时抛出。</summary>
    public sealed class EliteFormatException : Exception
    {
        public EliteFormatException(string message) : base(message) { }
    }

    /// <summary>
    /// 帧头 / gzip 编解码的共享实现（Elite 与 ALib 复用）。
    /// 纯字节操作，不依赖 WinForms / System.Drawing。
    /// </summary>
    internal static class FrameIo
    {
        public const int HeaderSize = 0x11;          // 17 字节头
        public const int HeaderTailSize = 5;         // +0x08..+0x0C
        public const int GZipLengthOffset = 0x0D;    // uint32

        /// <summary>从 data 的 pos 处读取一帧。end = pos + 17 + gzipLen。</summary>
        public static void ReadFrame(byte[] data, int pos, FrameRecord fr, out int end)
        {
            if (pos < 0 || pos + HeaderSize > data.Length)
                throw new EliteFormatException("帧头越界: pos=" + pos + " len=" + data.Length);

            fr.Width = BitConverter.ToInt16(data, pos + 0x00);
            fr.Height = BitConverter.ToInt16(data, pos + 0x02);
            fr.OffsetX = BitConverter.ToInt16(data, pos + 0x04);
            fr.OffsetY = BitConverter.ToInt16(data, pos + 0x06);

            fr.HeaderTail = new byte[HeaderTailSize];
            Buffer.BlockCopy(data, pos + 0x08, fr.HeaderTail, 0, HeaderTailSize);

            uint gzipLen = BitConverter.ToUInt32(data, pos + GZipLengthOffset);
            long frameEnd = (long)pos + HeaderSize + gzipLen;
            if (frameEnd > data.Length)
                throw new EliteFormatException("帧负载越界: pos=" + pos + " gzipLen=" + gzipLen + " len=" + data.Length);

            if (gzipLen == 0)
            {
                fr.Payload = Array.Empty<byte>();
            }
            else
            {
                fr.Payload = new byte[(int)gzipLen];
                Buffer.BlockCopy(data, pos + HeaderSize, fr.Payload, 0, (int)gzipLen);
            }
            end = (int)frameEnd;
        }

        /// <summary>把一帧（17 字节头 + 负载）写入目标数组的 pos 处。</summary>
        public static int WriteFrame(byte[] dst, int pos, FrameRecord fr)
        {
            BitConverter.GetBytes(fr.Width).CopyTo(dst, pos + 0x00);
            BitConverter.GetBytes(fr.Height).CopyTo(dst, pos + 0x02);
            BitConverter.GetBytes(fr.OffsetX).CopyTo(dst, pos + 0x04);
            BitConverter.GetBytes(fr.OffsetY).CopyTo(dst, pos + 0x06);

            byte[] tail = fr.HeaderTail ?? Array.Empty<byte>();
            for (int i = 0; i < HeaderTailSize; i++)
                dst[pos + 0x08 + i] = i < tail.Length ? tail[i] : (byte)0;

            byte[] payload = fr.Payload ?? Array.Empty<byte>();
            BitConverter.GetBytes((uint)payload.Length).CopyTo(dst, pos + GZipLengthOffset);
            if (payload.Length > 0)
                Buffer.BlockCopy(payload, 0, dst, pos + HeaderSize, payload.Length);

            return pos + HeaderSize + payload.Length;
        }

        public static byte[] Slice(byte[] data, int start, int end)
        {
            if (end <= start) return Array.Empty<byte>();
            byte[] r = new byte[end - start];
            Buffer.BlockCopy(data, start, r, 0, end - start);
            return r;
        }

        /// <summary>确定性 gzip 压缩（GZipStream 头部 MTIME 恒为 0，同一输入输出可复现）。</summary>
        public static byte[] GzipCompress(byte[] raw)
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

        public static byte[] GzipDecompress(byte[] gz)
        {
            using (MemoryStream inMs = new MemoryStream(gz, false))
            using (GZipStream g = new GZipStream(inMs, CompressionMode.Decompress))
            using (MemoryStream outMs = new MemoryStream())
            {
                g.CopyTo(outMs, 81920);
                return outMs.ToArray();
            }
        }

        public static bool IsGzip(byte[] b)
        {
            return b != null && b.Length >= 2 && b[0] == 0x1F && b[1] == 0x8B;
        }
    }

    /// <summary>
    /// .Elite 容器读写器（Elite Origins 客户端）。
    /// 布局：uint32 Version=4 | uint32 Signature="FEIH" | int32 N | int32 Offsets[N] | uint32 FrameStart | 帧区 | 尾块 | 元数据块。
    /// Offsets[0] = 元数据块偏移；可寻址帧 = Offsets[1..N-1]，共 N-1 帧。
    /// </summary>
    public sealed class EliteContainer
    {
        public const uint ExpectedVersion = 4;
        public const uint SignatureFEIH = 0x48494546; // "FEIH" 小端

        public uint Version { get; private set; }
        public uint Signature { get; private set; }
        public uint FrameStartField { get; private set; }
        public int OriginalCount { get; private set; }
        public List<FrameRecord> Frames { get; private set; }
        public byte[] TailBlob { get; private set; }
        public byte[] MetaBlock { get; private set; }

        private EliteContainer()
        {
            Frames = new List<FrameRecord>();
            TailBlob = Array.Empty<byte>();
            MetaBlock = Array.Empty<byte>();
        }

        public static EliteContainer Parse(byte[] data)
        {
            if (data == null) throw new EliteFormatException("data 为 null");
            if (data.Length < 12) throw new EliteFormatException("文件过短: " + data.Length);

            EliteContainer c = new EliteContainer();
            c.Version = BitConverter.ToUInt32(data, 0);
            c.Signature = BitConverter.ToUInt32(data, 4);
            c.OriginalCount = BitConverter.ToInt32(data, 8);
            int n = c.OriginalCount;

            if (n < 0) throw new EliteFormatException("N 为负: " + n);
            long offTableEndL = 12L + 4L * n;
            if (offTableEndL + 4 > data.Length)
                throw new EliteFormatException("偏移表越界: N=" + n + " len=" + data.Length);
            int offTableEnd = (int)offTableEndL;
            int frameStartPos = offTableEnd + 4;

            int[] offs = new int[n];
            for (int i = 0; i < n; i++)
                offs[i] = BitConverter.ToInt32(data, 12 + 4 * i);

            c.FrameStartField = BitConverter.ToUInt32(data, offTableEnd);

            if (n == 0)
            {
                // 无偏移表：12 字节头 + FrameStart(4) + 元数据块。整段（可能为 4 字节 RecordCount=0）作为元数据块透传。
                c.TailBlob = Array.Empty<byte>();
                c.MetaBlock = FrameIo.Slice(data, frameStartPos, data.Length);
                return c;
            }

            // 帧 1..n-1
            int lastEnd = frameStartPos;
            for (int k = 1; k < n; k++)
            {
                int pos = offs[k];
                if (pos < frameStartPos || pos > data.Length)
                    throw new EliteFormatException("帧偏移越界: k=" + k + " off=" + pos);

                FrameRecord fr = new FrameRecord();
                int end;
                FrameIo.ReadFrame(data, pos, fr, out end);

                if (k < n - 1)
                {
                    int nextPos = offs[k + 1];
                    if (nextPos < end || nextPos > data.Length)
                        throw new EliteFormatException("帧间偏移不一致: k=" + k + " end=" + end + " next=" + nextPos);
                    fr.TrailingBytes = FrameIo.Slice(data, end, nextPos);
                }
                else
                {
                    fr.TrailingBytes = Array.Empty<byte>();
                }

                c.Frames.Add(fr);
                lastEnd = end;
            }

            int metaStart = offs[0];
            if (metaStart < lastEnd) metaStart = lastEnd;
            if (metaStart > data.Length) throw new EliteFormatException("元数据块偏移越界: " + metaStart);

            c.TailBlob = FrameIo.Slice(data, lastEnd, metaStart);
            c.MetaBlock = FrameIo.Slice(data, metaStart, data.Length);
            return c;
        }

        /// <summary>重建完整文件字节。没有任何 Modified 时输出与原始字节完全一致。</summary>
        public byte[] Rebuild()
        {
            int frameCount = Frames.Count;
            int n;
            if (frameCount == 0 && OriginalCount == 0) n = 0;   // 保持 N==0 布局
            else n = frameCount + 1;

            int offTableEnd = 12 + 4 * n;
            int frameStartPos = offTableEnd + 4;

            int[] offs = new int[n];
            long cursor = frameStartPos;
            for (int k = 0; k < frameCount; k++)
            {
                FrameRecord fr = Frames[k];
                if (n > 0) offs[k + 1] = (int)cursor;
                cursor += FrameIo.HeaderSize + (fr.Payload != null ? fr.Payload.Length : 0)
                        + (fr.TrailingBytes != null ? fr.TrailingBytes.Length : 0);
            }
            int metaOffset = (int)cursor + TailBlob.Length;
            if (n > 0) offs[0] = metaOffset;

            long total = cursor + TailBlob.Length + MetaBlock.Length;
            byte[] outb = new byte[total];

            // 头
            BitConverter.GetBytes(Version).CopyTo(outb, 0);
            BitConverter.GetBytes(Signature).CopyTo(outb, 4);
            BitConverter.GetBytes(n).CopyTo(outb, 8);
            for (int i = 0; i < n; i++)
                BitConverter.GetBytes(offs[i]).CopyTo(outb, 12 + 4 * i);

            // FrameStart：N 未变时透传原值，N 变化时写 12+4N+4
            uint frameStartValue = (n == OriginalCount) ? FrameStartField : (uint)frameStartPos;
            BitConverter.GetBytes(frameStartValue).CopyTo(outb, offTableEnd);

            // 帧区 + 帧间间隙 + 尾块 + 元数据块
            int p = frameStartPos;
            for (int k = 0; k < frameCount; k++)
            {
                FrameRecord fr = Frames[k];
                p = FrameIo.WriteFrame(outb, p, fr);
                byte[] tr = fr.TrailingBytes ?? Array.Empty<byte>();
                if (tr.Length > 0)
                {
                    Buffer.BlockCopy(tr, 0, outb, p, tr.Length);
                    p += tr.Length;
                }
            }
            if (TailBlob.Length > 0)
            {
                Buffer.BlockCopy(TailBlob, 0, outb, p, TailBlob.Length);
                p += TailBlob.Length;
            }
            if (MetaBlock.Length > 0)
            {
                Buffer.BlockCopy(MetaBlock, 0, outb, p, MetaBlock.Length);
                p += MetaBlock.Length;
            }
            return outb;
        }
    }
}
