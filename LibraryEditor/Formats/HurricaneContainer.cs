using System;
using System.Collections.Generic;

namespace LibraryEditor.Formats
{
    /// <summary>
    /// .Hurricane 容器读写器（Hurricane ReVIVAL Mir2 引擎）。
    /// 布局：42 字节魔数（原文 "HURRICANEREVIVALMIR2x047x095X0378X12sXaqw5"，整段 XOR 0x5A 存储）|
    ///       int32 版本(语料=3) | int32 帧数 | int32 数据结束偏移 |
    ///       帧数×int32 偏移表（索引紧贴帧区：索引区结尾 = 第一帧偏移）。
    /// 每帧 17 字节头，与 Elite 的 FrameIo.HeaderSize 完全同构：
    ///   int16 Width | int16 Height | int16 OffsetX | int16 OffsetY |
    ///   int16 ShadowX | int16 ShadowY | byte Shadow | int32 GZipLength(@0x0D)；
    /// 帧负载 = gzip(BGRA8888 W*H*4)，帧区连续无间隙。
    /// 语料验证：BuffIcon.Hurricane 273 帧全部解码成功，模拟重建与原文件字节级一致；
    /// 数据结束偏移之后还有 4 字节 0（作为 TrailingBytes 原样保留）。
    /// 铁律同 Elite/ALib：未修改帧的 Payload 持有原始 gzip 字节引用，保存字节级还原。
    /// </summary>
    public sealed class HurricaneContainer
    {
        public const int MagicLength = 0x2A;        // 42 字节 XOR 魔数
        public const int HeaderLength = 0x36;       // 42 + 版本4 + 帧数4 + 结束偏移4
        public const byte XorKey = 0x5A;            // 魔数加密密钥

        /// <summary>语料中的魔数原文（新建空库时用）。</summary>
        public const string DefaultMagicText = "HURRICANEREVIVALMIR2x047x095X0378X12sXaqw5";

        /// <summary>版本号（语料 = 3）。</summary>
        public int Version { get; private set; }

        public List<FrameRecord> Frames { get; private set; }

        /// <summary>数据结束偏移之后的残余字节（语料为 4 字节 0），重建时原样回写。</summary>
        public byte[] TrailingBytes { get; private set; }

        /// <summary>原始 42 字节魔数（加密形态），重建时原样回写以保证字节级一致。</summary>
        private readonly byte[] _magic;

        private HurricaneContainer(byte[] magic)
        {
            _magic = magic;
            Frames = new List<FrameRecord>();
            TrailingBytes = Array.Empty<byte>();
        }

        /// <summary>按魔数判断字节流是否为 Hurricane 格式（前若干字节 XOR 0x5A 后以 HURRICANE 开头）。</summary>
        public static bool LooksLikeHurricane(byte[] data)
        {
            if (data == null || data.Length < 10) return false;
            for (int i = 0; i < 9; i++)
            {
                if ((byte)(data[i] ^ XorKey) != (byte)"HURRICANE"[i]) return false;
            }
            return true;
        }

        public static HurricaneContainer Parse(byte[] data)
        {
            if (data == null) throw new EliteFormatException("data 为 null");
            if (data.Length < HeaderLength) throw new EliteFormatException("文件过短: " + data.Length);
            if (!LooksLikeHurricane(data))
                throw new EliteFormatException("魔数不符：前 42 字节 XOR 0x5A 后不是 HURRICANEREVIVALMIR2...，不是 .Hurricane 库");

            byte[] magic = new byte[MagicLength];
            Buffer.BlockCopy(data, 0, magic, 0, MagicLength);

            HurricaneContainer c = new HurricaneContainer(magic);
            c.Version = BitConverter.ToInt32(data, MagicLength);

            int n = BitConverter.ToInt32(data, MagicLength + 4);
            if (n < 0) throw new EliteFormatException("帧数为负: " + n);
            if ((long)HeaderLength + 4L * n > data.Length)
                throw new EliteFormatException("偏移表越界: 帧数=" + n + " 文件长=" + data.Length);

            int[] offs = new int[n];
            for (int i = 0; i < n; i++)
                offs[i] = BitConverter.ToInt32(data, HeaderLength + 4 * i);

            int dataEnd = HeaderLength + 4 * n;   // 无帧/全部空帧时的数据区基线

            for (int i = 0; i < n; i++)
            {
                FrameRecord fr = new FrameRecord();
                int pos = offs[i];

                // 空帧：偏移无效 → 全空记录（保留帧数，维持索引对位）
                if (pos <= 0 || pos + FrameIo.HeaderSize > data.Length)
                {
                    fr.Width = 0;
                    fr.Height = 0;
                    fr.HeaderTail = new byte[FrameIo.HeaderTailSize];
                    fr.Payload = Array.Empty<byte>();
                    c.Frames.Add(fr);
                    continue;
                }

                int end;
                FrameIo.ReadFrame(data, pos, fr, out end);   // 17 字节头 + gzip，与 Elite 同构
                if (end > dataEnd) dataEnd = end;

                c.Frames.Add(fr);
            }

            // 数据结束偏移之后到文件末尾的字节原样保留（语料 = 4 字节 0）
            c.TrailingBytes = dataEnd < data.Length
                ? FrameIo.Slice(data, dataEnd, data.Length)
                : Array.Empty<byte>();

            return c;
        }

        /// <summary>重建完整文件字节。未改动任何帧时输出与原始字节一致（语料已验证）。</summary>
        public byte[] Rebuild()
        {
            int n = Frames.Count;
            int indexEnd = HeaderLength + 4 * n;

            // 帧区连续排布，计算各帧偏移
            int[] poss = new int[n];
            long cursor = indexEnd;
            for (int i = 0; i < n; i++)
            {
                FrameRecord fr = Frames[i];
                poss[i] = (int)cursor;
                cursor += FrameIo.HeaderSize + (fr.Payload != null ? fr.Payload.Length : 0);
            }
            int dataEnd = (int)cursor;

            byte[] outb = new byte[dataEnd + (long)(TrailingBytes != null ? TrailingBytes.Length : 0)];

            // 魔数：优先原样回写；空容器（新建）用默认魔数生成
            byte[] magic = _magic ?? EncodeMagic(DefaultMagicText);
            Buffer.BlockCopy(magic, 0, outb, 0, MagicLength);
            BitConverter.GetBytes(Version).CopyTo(outb, MagicLength);
            BitConverter.GetBytes(n).CopyTo(outb, MagicLength + 4);
            BitConverter.GetBytes(dataEnd).CopyTo(outb, MagicLength + 8);

            for (int i = 0; i < n; i++)
                BitConverter.GetBytes(poss[i]).CopyTo(outb, HeaderLength + 4 * i);

            int p = indexEnd;
            for (int i = 0; i < n; i++)
            {
                FrameRecord fr = Frames[i];

                BitConverter.GetBytes(fr.Width).CopyTo(outb, p + 0x00);
                BitConverter.GetBytes(fr.Height).CopyTo(outb, p + 0x02);
                BitConverter.GetBytes(fr.OffsetX).CopyTo(outb, p + 0x04);
                BitConverter.GetBytes(fr.OffsetY).CopyTo(outb, p + 0x06);

                byte[] tail = fr.HeaderTail;
                for (int k = 0; k < FrameIo.HeaderTailSize; k++)
                    outb[p + 0x08 + k] = (tail != null && k < tail.Length) ? tail[k] : (byte)0;

                byte[] payload = fr.Payload ?? Array.Empty<byte>();
                BitConverter.GetBytes(payload.Length).CopyTo(outb, p + FrameIo.GZipLengthOffset);
                if (payload.Length > 0)
                    Buffer.BlockCopy(payload, 0, outb, p + FrameIo.HeaderSize, payload.Length);

                p += FrameIo.HeaderSize + payload.Length;
            }

            if (TrailingBytes != null && TrailingBytes.Length > 0)
                Buffer.BlockCopy(TrailingBytes, 0, outb, dataEnd, TrailingBytes.Length);

            return outb;
        }

        /// <summary>空 .Hurricane 库：魔数 + 版本3 + 帧数0 + 结束偏移，共 0x36 字节。</summary>
        public static byte[] BuildEmptyFile()
        {
            HurricaneContainer c = new HurricaneContainer(EncodeMagic(DefaultMagicText));
            c.Version = 3;
            return c.Rebuild();
        }

        /// <summary>魔数原文 → XOR 0x5A 加密字节。</summary>
        public static byte[] EncodeMagic(string text)
        {
            byte[] src = System.Text.Encoding.ASCII.GetBytes(text ?? DefaultMagicText);
            byte[] outb = new byte[MagicLength];
            for (int i = 0; i < MagicLength; i++)
                outb[i] = (byte)((i < src.Length ? src[i] : (byte)0) ^ XorKey);
            return outb;
        }
    }
}
