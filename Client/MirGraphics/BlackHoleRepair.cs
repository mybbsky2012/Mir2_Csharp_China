using System;

namespace Client.MirGraphics
{
    /// <summary>
    /// 修补「去黑底时把怪物身上的黑色部位也一起抠掉」的图库。
    ///
    /// 现象：629 业火双刀 / 631 业火女王 / 640 冰焰鬼法 这类暗色韩服怪，
    /// 身上一片片透明，直接看到地面，看着像"渲染透明过度、黑色被搞没了"。
    /// 原因不在渲染，而在图库数据本身：这些图去底用的是"颜色接近纯黑就把 alpha 设成 0"，
    /// 怪物身上本来就黑的甲片缝隙、翅膀阴影被一并抠掉 —— alpha 变 0，颜色还留在数据里。
    ///
    /// 做法（重建背景，只补「不该透明」的地方，轮廓一点不变）：
    ///   1. 把 alpha=0 当作背景，先腐蚀 2 次 —— 比 5 像素窄的缝隙会断开；
    ///   2. 只在腐蚀后的背景上、从画布四边做洪水填充 —— 只有真正和画布边缘连通的才算背景；
    ///   3. 把背景膨胀 2 次还原；
    ///   4. 剩下的透明像素（被身体夹住的洞、只通过细缝连出去的缝）一律补成不透明，
    ///      颜色取周围 5×5 内不透明像素的平均色（相邻已补的像素也参与，避免补出黑点）。
    ///
    /// 只处理怪物图库（Data\Monster\*.Lib），玩家 / 装备 / UI 图库一律不碰。
    /// </summary>
    internal static class BlackHoleRepair
    {
        public static void Repair(byte[] raw, int w, int h)
        {
            if (raw == null || w < 5 || h < 5) return;

            int srcRow = w * 4;
            if (raw.Length < srcRow * h) return;

            const int r = 2;                 // 腐蚀/膨胀次数（能补上宽度 ≤ 4 像素的缝）
            int pad = r + 2;                 // 外扩边距要大于 r，否则腐蚀会把画布边缘啃掉
            int pw = w + pad * 2, ph = h + pad * 2;

            bool[] bg = new bool[pw * ph];
            for (int i = 0; i < bg.Length; i++) bg[i] = true;   // 外扩区默认全透明

            for (int y = 0; y < h; y++)
            {
                int src = y * srcRow;
                int dst = (y + pad) * pw + pad;
                for (int x = 0; x < w; x++)
                    bg[dst + x] = raw[src + (x << 2) + 3] == 0;
            }

            bool[] core = Erode3(bg, pw, ph, r);

            bool[] reached = new bool[pw * ph];
            int[] stack = new int[pw * ph];
            int top = 0;

            for (int y = 0; y < ph; y++)
            {
                for (int x = 0; x < pw; x++)
                {
                    if (x >= pad && x < pw - pad && y >= pad && y < ph - pad) continue;
                    int i = y * pw + x;
                    if (core[i] && !reached[i]) { reached[i] = true; stack[top++] = i; }
                }
            }

            while (top > 0)
            {
                int c = stack[--top];
                int cx = c % pw, cy = c / pw;

                if (cx > 0) { int n = c - 1; if (core[n] && !reached[n]) { reached[n] = true; stack[top++] = n; } }
                if (cx < pw - 1) { int n = c + 1; if (core[n] && !reached[n]) { reached[n] = true; stack[top++] = n; } }
                if (cy > 0) { int n = c - pw; if (core[n] && !reached[n]) { reached[n] = true; stack[top++] = n; } }
                if (cy < ph - 1) { int n = c + pw; if (core[n] && !reached[n]) { reached[n] = true; stack[top++] = n; } }
            }

            bool[] realBg = Dilate3(reached, pw, ph, r);

            for (int y = 0; y < h; y++)
            {
                int src = y * srcRow;
                int mrow = (y + pad) * pw + pad;

                for (int x = 0; x < w; x++)
                {
                    int p = src + (x << 2);
                    if (raw[p + 3] != 0) continue;      // 本来就不透明
                    if (realBg[mrow + x]) continue;     // 真背景，保持透明

                    int rs = 0, gs = 0, bs = 0, cnt = 0;
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= h) continue;
                        int row2 = ny * srcRow;
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= w) continue;
                            int q = row2 + (nx << 2);
                            if (raw[q + 3] == 0) continue;
                            rs += raw[q + 2]; gs += raw[q + 1]; bs += raw[q]; cnt++;
                        }
                    }

                    // 周围一个不透明像素都没有时保持原色
                    if (cnt > 0)
                    {
                        raw[p] = (byte)(bs / cnt);
                        raw[p + 1] = (byte)(gs / cnt);
                        raw[p + 2] = (byte)(rs / cnt);
                    }
                    raw[p + 3] = 255;
                }
            }
        }

        /// <summary>3×3 腐蚀，重复 times 次（true = 透明）。</summary>
        private static bool[] Erode3(bool[] m, int pw, int ph, int times)
        {
            bool[] cur = m;
            for (int t = 0; t < times; t++)
            {
                bool[] next = new bool[m.Length];
                for (int y = 1; y < ph - 1; y++)
                {
                    int row = y * pw;
                    for (int x = 1; x < pw - 1; x++)
                    {
                        int i = row + x;
                        if (!cur[i]) continue;
                        if (!cur[i - 1] || !cur[i + 1] || !cur[i - pw] || !cur[i + pw]) continue;
                        if (!cur[i - pw - 1] || !cur[i - pw + 1] || !cur[i + pw - 1] || !cur[i + pw + 1]) continue;
                        next[i] = true;
                    }
                }
                cur = next;
            }
            return cur;
        }

        /// <summary>3×3 膨胀，重复 times 次（true = 透明）。</summary>
        private static bool[] Dilate3(bool[] m, int pw, int ph, int times)
        {
            bool[] cur = m;
            for (int t = 0; t < times; t++)
            {
                bool[] next = (bool[])cur.Clone();
                for (int y = 1; y < ph - 1; y++)
                {
                    int row = y * pw;
                    for (int x = 1; x < pw - 1; x++)
                    {
                        int i = row + x;
                        if (cur[i]) continue;          // 已标记，clone 里已经是 true
                        bool hit = cur[i - 1] || cur[i + 1] || cur[i - pw] || cur[i + pw]
                                || cur[i - pw - 1] || cur[i - pw + 1] || cur[i + pw - 1] || cur[i + pw + 1];
                        if (hit) next[i] = true;
                    }
                }
                cur = next;
            }
            return cur;
        }
    }
}
