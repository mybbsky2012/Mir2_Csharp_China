using System;
using System.Collections.Generic;

namespace LibraryEditor.Formats
{
    /// <summary>
    /// WeMade 资源库（.WIX 索引 + .WIL 数据）中的一帧原始记录。
    /// 记录 = 16 字节头 + Size 字节负载：
    ///   +0x00 int16 Width | +0x02 int16 Height | +0x04 int16 X | +0x06 int16 Y |
    ///   +0x08 int16 Unknown1 | +0x0A int16 Unknown2 | +0x0C uint32 Size。
    /// Unknown1/2 语义未知，按原始值保存/回写（新增帧写 0）。
    /// </summary>
    public sealed class WeMadeFrameRecord
    {
        public short Width;
        public short Height;
        public short OffsetX;
        public short OffsetY;
        public short Unknown1;
        public short Unknown2;

        /// <summary>原始负载字节（长度 = 头 +0x0C 的 Size）；空帧 = Array.Empty&lt;byte&gt;()。</summary>
        public byte[] Payload = Array.Empty<byte>();

        /// <summary>该记录头在 .WIL 内的原始偏移（仅供诊断；重建时按顺序紧凑排布）。</summary>
        public int RawRecordOffset;

        /// <summary>调用方置位表示 Payload/坐标已被替换。</summary>
        public bool Modified;

        public WeMadeFrameRecord CloneShallow()
        {
            return new WeMadeFrameRecord
            {
                Width = Width,
                Height = Height,
                OffsetX = OffsetX,
                OffsetY = OffsetY,
                Unknown1 = Unknown1,
                Unknown2 = Unknown2,
                Payload = Payload,
                RawRecordOffset = RawRecordOffset,
                Modified = Modified,
            };
        }
    }

    /// <summary>
    /// WeMade（WEMADE Entertainment）WIX(索引) + WIL(数据) 容器读写器。纯字节层，不依赖 WinForms / System.Drawing。
    ///
    /// WIX：byte[26] 签名 | 保留头至 0x33 | uint32 Count @0x2C | Count × uint32 帧偏移（自 0x34=52 起）。
    /// WIL：byte[60] 保留头 | 帧记录连续存放（首帧固定 @60）直至 EOF。
    ///
    /// 字节级不变式：两文件都未改动时，RebuildWix()/RebuildWil() 输出与原始文件逐字节一致
    /// （语料 163/163 对均验证：偏移自 60 起严格连续、末帧恰好止于 EOF）。
    /// </summary>
    public sealed class WeMadeContainer
    {
        public const int WixHeaderLength = 52;    // 0x00..0x33 原样保留（含 @0x2C 的 Count）
        public const int WilHeaderLength = 60;    // 0x00..0x3B 原样保留，首帧固定 @60
        public const int FrameHeaderLength = 16;

        // ---- WIX ----
        public byte[] WixHeader = new byte[WixHeaderLength];
        public uint WixDeclaredCount;

        // ---- WIL ----
        public byte[] WilHeader = new byte[WilHeaderLength];
        public List<WeMadeFrameRecord> Frames = new List<WeMadeFrameRecord>();

        /// <summary>解析出的子类型（0=普通 wil / 1=wzl / 2=bo16bit wil / 3=wemade mir3 / 4=shanda miz / 5=32bit wil）。</summary>
        public byte Subtype;

        /// <summary>解析出的帧偏移表（与 Frames 一一对应，仅诊断/校验用）。</summary>
        public int[] Offsets = Array.Empty<int>();

        public static WeMadeContainer Parse(byte[] wix, byte[] wil)
        {
            return Parse(wix, wil, 0);
        }

        public static WeMadeContainer Parse(byte[] wix, byte[] wil, byte subtype)
        {
            if (wix == null) throw new EliteFormatException("WIX 数据为 null");
            if (wil == null) throw new EliteFormatException("WIL 数据为 null");
            if (wix.Length < WixHeaderLength) throw new EliteFormatException("WIX 文件过短: " + wix.Length);
            if (wil.Length < WilHeaderLength) throw new EliteFormatException("WIL 文件过短: " + wil.Length);

            WeMadeContainer c = new WeMadeContainer();
            c.Subtype = subtype;

            Buffer.BlockCopy(wix, 0, c.WixHeader, 0, WixHeaderLength);
            Buffer.BlockCopy(wil, 0, c.WilHeader, 0, WilHeaderLength);
            c.WixDeclaredCount = BitConverter.ToUInt32(wix, 0x2C);

            long rawN = (wix.Length - WixHeaderLength) / 4L;
            if ((wix.Length - WixHeaderLength) % 4 != 0)
                throw new EliteFormatException("WIX 偏移区长度非 4 的倍数: " + (wix.Length - WixHeaderLength));
            int n = (int)rawN;

            c.Offsets = new int[n];
            for (int i = 0; i < n; i++)
                c.Offsets[i] = BitConverter.ToInt32(wix, WixHeaderLength + 4 * i);

            for (int i = 0; i < n; i++)
            {
                int o = c.Offsets[i];
                if (o < WilHeaderLength || o + FrameHeaderLength > wil.Length)
                    throw new EliteFormatException("WIL 帧偏移越界: 条目=" + i + " 偏移=" + o + " 长度=" + wil.Length);

                WeMadeFrameRecord fr = new WeMadeFrameRecord();
                fr.RawRecordOffset = o;
                fr.Width = BitConverter.ToInt16(wil, o + 0x00);
                fr.Height = BitConverter.ToInt16(wil, o + 0x02);
                fr.OffsetX = BitConverter.ToInt16(wil, o + 0x04);
                fr.OffsetY = BitConverter.ToInt16(wil, o + 0x06);
                fr.Unknown1 = BitConverter.ToInt16(wil, o + 0x08);
                fr.Unknown2 = BitConverter.ToInt16(wil, o + 0x0A);
                uint size = BitConverter.ToUInt32(wil, o + 0x0C);

                long end = (long)o + FrameHeaderLength + size;
                if (end > wil.Length)
                    throw new EliteFormatException("WIL 帧负载越界: 条目=" + i + " 大小=" + size + " 长度=" + wil.Length);

                fr.Payload = size == 0 ? Array.Empty<byte>() : FrameIo.Slice(wil, o + FrameHeaderLength, (int)end);
                c.Frames.Add(fr);
            }

            return c;
        }

        /// <summary>按当前 Frames 顺序紧凑排布（自 60 起）计算各帧偏移。</summary>
        public int[] ComputeFrameOffsets()
        {
            int[] offs = new int[Frames.Count];
            long cur = WilHeaderLength;
            for (int i = 0; i < Frames.Count; i++)
            {
                offs[i] = (int)cur;
                cur += FrameHeaderLength + (Frames[i].Payload != null ? Frames[i].Payload.Length : 0);
            }
            return offs;
        }

        /// <summary>重建 WIX 字节。未改动时与原始 WIX 逐字节一致。</summary>
        public byte[] RebuildWix()
        {
            int n = Frames.Count;
            byte[] outb = new byte[WixHeaderLength + 4 * n];
            Buffer.BlockCopy(WixHeader, 0, outb, 0, WixHeaderLength);

            // 头部 0x2C 已含原始 Count；仅当帧数变化时改写。
            if ((uint)n != WixDeclaredCount)
                BitConverter.GetBytes((uint)n).CopyTo(outb, 0x2C);

            int[] offs = ComputeFrameOffsets();
            for (int i = 0; i < n; i++)
                BitConverter.GetBytes(offs[i]).CopyTo(outb, WixHeaderLength + 4 * i);
            return outb;
        }

        /// <summary>重建 WIL 字节。未改动时与原始 WIL 逐字节一致。</summary>
        public byte[] RebuildWil()
        {
            long total = WilHeaderLength;
            for (int i = 0; i < Frames.Count; i++)
                total += FrameHeaderLength + (Frames[i].Payload != null ? Frames[i].Payload.Length : 0);

            byte[] outb = new byte[total];
            Buffer.BlockCopy(WilHeader, 0, outb, 0, WilHeaderLength);

            int p = WilHeaderLength;
            for (int i = 0; i < Frames.Count; i++)
            {
                WeMadeFrameRecord fr = Frames[i];
                byte[] payload = fr.Payload ?? Array.Empty<byte>();

                BitConverter.GetBytes(fr.Width).CopyTo(outb, p + 0x00);
                BitConverter.GetBytes(fr.Height).CopyTo(outb, p + 0x02);
                BitConverter.GetBytes(fr.OffsetX).CopyTo(outb, p + 0x04);
                BitConverter.GetBytes(fr.OffsetY).CopyTo(outb, p + 0x06);
                BitConverter.GetBytes(fr.Unknown1).CopyTo(outb, p + 0x08);
                BitConverter.GetBytes(fr.Unknown2).CopyTo(outb, p + 0x0A);
                BitConverter.GetBytes((uint)payload.Length).CopyTo(outb, p + 0x0C);
                if (payload.Length > 0)
                    Buffer.BlockCopy(payload, 0, outb, p + FrameHeaderLength, payload.Length);

                p += FrameHeaderLength + payload.Length;
            }
            return outb;
        }
    }
}
