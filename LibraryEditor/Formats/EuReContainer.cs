using System;
using System.Collections.Generic;

namespace LibraryEditor.Formats
{
    /// <summary>
    /// .EuRe 容器读写器（EuReborn / Vortice 客户端，LibVersion=4）。
    /// 布局：int32 Version（4=明文，4000=Protected 加密） | int32 Count |
    ///       帧区（Count 帧连续排布） | 末尾索引表（Count × 12 字节）。
    /// 索引项 = int32 Position | int32 Length（主图 gzip 字节数） | int32 MaskLength。
    /// 每帧 13 字节头：int16 Width | int16 HeightStored(=实际Height^Width异或) |
    ///   int16 OffsetX | int16 OffsetY | int16 ShadowX | int16 ShadowY | byte Shadow(bit7=有Mask)。
    /// 帧负载 = gzip(BGRA8888 W*H*4)；若有 Mask，其后跟 8 字节 Mask 头 + Mask gzip，
    /// 整段作为 TrailingBytes 原样透传。
    /// 语料验证：6 个 Item 库全部 MaskLength=0、无 Mask 帧、帧严格连续 ——
    /// 未改动帧的 Position/Length/MaskLength 重算值与原值一致，保存可字节级还原。
    /// </summary>
    public sealed class EuReContainer
    {
        public const int ExpectedVersion = 4;
        public const int ProtectedVersion = 4000;
        public const int FrameHeaderLength = 13;     // 2+2+2+2+2+2+1
        public const int IndexEntryLength = 12;      // Position+Length+MaskLength

        public int Version { get; private set; }
        public int OriginalCount { get; private set; }
        public List<FrameRecord> Frames { get; private set; }

        /// <summary>各帧原始 MaskLength（仅诊断；重建时按 HasMask 语义回写，未改动帧结果一致）。</summary>
        public int[] OriginalMaskLengths = Array.Empty<int>();

        private EuReContainer()
        {
            Frames = new List<FrameRecord>();
        }

        public static EuReContainer Parse(byte[] data)
        {
            if (data == null) throw new EliteFormatException("data 为 null");
            if (data.Length < 8) throw new EliteFormatException("文件过短: " + data.Length);

            EuReContainer c = new EuReContainer();
            c.Version = BitConverter.ToInt32(data, 0);
            if (c.Version == ProtectedVersion)
                throw new EliteFormatException("该 .EuRe 是 4000 加密版（Protected），无法编辑");
            if (c.Version != ExpectedVersion)
                throw new EliteFormatException("EuRe 版本不符: " + c.Version + "（期望 " + ExpectedVersion + "）");

            c.OriginalCount = BitConverter.ToInt32(data, 4);
            int n = c.OriginalCount;
            if (n < 0) throw new EliteFormatException("Count 为负: " + n);

            long indexStartL = (long)data.Length - (long)IndexEntryLength * n;
            if (indexStartL < 8) throw new EliteFormatException("索引表越界: N=" + n + " len=" + data.Length);
            int indexStart = (int)indexStartL;

            int[] poss = new int[n];
            int[] lens = new int[n];
            c.OriginalMaskLengths = new int[n];
            for (int i = 0; i < n; i++)
            {
                poss[i] = BitConverter.ToInt32(data, indexStart + i * IndexEntryLength);
                lens[i] = BitConverter.ToInt32(data, indexStart + i * IndexEntryLength + 4);
                c.OriginalMaskLengths[i] = BitConverter.ToInt32(data, indexStart + i * IndexEntryLength + 8);
            }

            for (int i = 0; i < n; i++)
            {
                FrameRecord fr = new FrameRecord();
                int pos = poss[i];

                // 空帧：Position 无效（0 / 越界）→ 全空记录
                if (pos <= 0 || pos + FrameHeaderLength > indexStart)
                {
                    fr.Width = 0;
                    fr.Height = 0;
                    fr.TrailingBytes = Array.Empty<byte>();
                    c.Frames.Add(fr);
                    continue;
                }
                if (pos + FrameHeaderLength > data.Length)
                    throw new EliteFormatException("帧头越界: i=" + i + " pos=" + pos);

                fr.Width = BitConverter.ToInt16(data, pos + 0x00);
                fr.Height = (short)(BitConverter.ToInt16(data, pos + 0x02) ^ fr.Width);  // 异或还原
                fr.OffsetX = BitConverter.ToInt16(data, pos + 0x04);
                fr.OffsetY = BitConverter.ToInt16(data, pos + 0x06);
                fr.HeaderTail = FrameIo.Slice(data, pos + 0x08, pos + FrameHeaderLength); // ShadowX/ShadowY/Shadow

                int payloadStart = pos + FrameHeaderLength;
                long payloadEndL = (long)payloadStart + lens[i];
                if (payloadEndL > data.Length)
                    throw new EliteFormatException("帧负载越界: i=" + i + " pos=" + pos + " len=" + lens[i]);
                int payloadEnd = (int)payloadEndL;

                fr.Payload = FrameIo.Slice(data, payloadStart, payloadEnd);

                // 帧尾到下一帧起点之间的字节 = Mask 头 + Mask 负载（原样透传）
                int nextPos = indexStart;
                for (int k = i + 1; k < n; k++)
                {
                    if (poss[k] > 0) { nextPos = poss[k]; break; }
                }
                fr.TrailingBytes = (payloadEnd < nextPos && nextPos <= data.Length)
                    ? FrameIo.Slice(data, payloadEnd, nextPos)
                    : Array.Empty<byte>();

                c.Frames.Add(fr);
            }
            return c;
        }

        /// <summary>重建完整文件字节。没有任何 Modified 时输出与原始字节完全一致。</summary>
        public byte[] Rebuild()
        {
            int n = Frames.Count;

            // 计算各帧 Position（帧区自 8 起，连续排布）
            int[] poss = new int[n];
            long cursor = 8;
            for (int i = 0; i < n; i++)
            {
                FrameRecord fr = Frames[i];
                poss[i] = (int)cursor;
                bool hasMask = fr.HeaderTail != null && fr.HeaderTail.Length >= 5 && (fr.HeaderTail[4] & 0x80) != 0;
                cursor += FrameHeaderLength
                        + (fr.Payload != null ? fr.Payload.Length : 0)
                        + (hasMask ? (fr.TrailingBytes != null ? fr.TrailingBytes.Length : 0) : 0);
            }
            int indexStart = (int)cursor;

            byte[] outb = new byte[indexStart + (long)IndexEntryLength * n];
            BitConverter.GetBytes(ExpectedVersion).CopyTo(outb, 0);
            BitConverter.GetBytes(n).CopyTo(outb, 4);

            int p = 8;
            for (int i = 0; i < n; i++)
            {
                FrameRecord fr = Frames[i];

                int storedHeight = fr.Height ^ fr.Width;   // 异或存储
                BitConverter.GetBytes(fr.Width).CopyTo(outb, p + 0x00);
                BitConverter.GetBytes(storedHeight).CopyTo(outb, p + 0x02);
                BitConverter.GetBytes(fr.OffsetX).CopyTo(outb, p + 0x04);
                BitConverter.GetBytes(fr.OffsetY).CopyTo(outb, p + 0x06);

                byte[] tail = fr.HeaderTail;
                for (int k = 0; k < 5; k++)
                    outb[p + 0x08 + k] = (tail != null && k < tail.Length) ? tail[k] : (byte)0;

                byte[] payload = fr.Payload ?? Array.Empty<byte>();
                if (payload.Length > 0)
                    Buffer.BlockCopy(payload, 0, outb, p + FrameHeaderLength, payload.Length);
                p += FrameHeaderLength + payload.Length;

                bool hasMask = tail != null && tail.Length >= 5 && (tail[4] & 0x80) != 0;
                byte[] mask = (hasMask ? (fr.TrailingBytes ?? Array.Empty<byte>()) : Array.Empty<byte>());
                if (mask.Length > 0)
                {
                    Buffer.BlockCopy(mask, 0, outb, p, mask.Length);
                    p += mask.Length;
                }

                // 索引项
                BitConverter.GetBytes(poss[i]).CopyTo(outb, indexStart + i * IndexEntryLength);
                BitConverter.GetBytes(payload.Length).CopyTo(outb, indexStart + i * IndexEntryLength + 4);
                BitConverter.GetBytes(mask.Length).CopyTo(outb, indexStart + i * IndexEntryLength + 8);
            }
            return outb;
        }

        /// <summary>空 .EuRe：Version=4 | Count=0，共 8 字节。</summary>
        public static byte[] BuildEmptyFile()
        {
            byte[] b = new byte[8];
            BitConverter.GetBytes(ExpectedVersion).CopyTo(b, 0);
            BitConverter.GetBytes(0).CopyTo(b, 4);
            return b;
        }
    }
}
