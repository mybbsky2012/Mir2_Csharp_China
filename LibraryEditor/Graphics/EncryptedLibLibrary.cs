using System;
using System.Collections.Generic;
using LibraryEditor.Formats;

namespace LibraryEditor
{
    /// <summary>
    /// 「加密lib」（Artificial Mir 32bit 资源库，如 items_32bit.Lib / dnitems_32bit.Lib）。
    /// 容器与 MLX 完全一致（89 4D 4C 58 …），但帧编码多了 Ver=8 的 8 位调色板帧：
    ///   解包后布局：u16 色数 | 色数 × u16 RGB565 调色板 | Width×Height 个 8 位索引；
    ///   索引 0 = 透明。三种帧编码（Ver 1=RGB565 / 2=BGRA32 / 4=DXT1）之外绝大多数帧都是 Ver=8。
    /// 因容器同构，就地编辑/保存/导出标准 Lib 全部沿用 MlxLibrary 的实现。
    /// </summary>
    public sealed class EncryptedLibLibrary : MlxLibrary
    {
        public EncryptedLibLibrary(string filename) : base(filename) { }

        public EncryptedLibLibrary(MlxContainer container, string filename) : base(container, filename) { }

        /// <summary>Ver=8：8 位调色板帧（u16 色数 + RGB565 调色板 + 8 位索引），其余交给基类。</summary>
        protected override byte[] DecodeToBgra(MlxFrame f)
        {
            if (f.Ver == 8)
            {
                int w = f.Width, h = f.Height;
                if (w <= 0 || h <= 0) return null;

                byte[] raw = DecodeRaw(f);
                if (raw == null || raw.Length < 2) return null;

                int colors = raw[0] | (raw[1] << 8);
                long palOff = 2;
                long idxOff = 2L + (long)colors * 2;
                if (colors <= 0 || idxOff + (long)w * h > raw.Length) return null;

                byte[] bgra = new byte[w * h * 4];
                for (int i = 0; i < w * h; i++)
                {
                    int v = raw[idxOff + i];
                    if (v == 0 || v >= colors) continue;   // 索引 0 / 越界 → 透明

                    int p = (int)palOff + v * 2;
                    ushort c = (ushort)(raw[p] | (raw[p + 1] << 8));
                    bgra[i * 4 + 0] = (byte)(((c & 0x1F) * 255 + 15) / 31);
                    bgra[i * 4 + 1] = (byte)((((c >> 5) & 0x3F) * 255 + 31) / 63);
                    bgra[i * 4 + 2] = (byte)((((c >> 11) & 0x1F) * 255 + 15) / 31);
                    bgra[i * 4 + 3] = 255;
                }
                return bgra;
            }

            return base.DecodeToBgra(f);
        }
    }
}
