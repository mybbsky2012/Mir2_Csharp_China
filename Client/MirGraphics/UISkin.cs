using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using SlimDX.Direct3D9;

namespace Client.MirGraphics
{
    /// <summary>
    /// 客户端皮肤包：用外部 PNG 覆盖 .Lib 里的贴图。
    ///
    /// 原理
    ///   整个客户端的贴图（登录背景、选人背景、载入画面、主界面、背包、技能栏、
    ///   按钮、标题字……）最终都从 MLibrary 取，寻址方式统一是「库名 + 索引」。
    ///   这里在纹理建好后把 MImage.Image 换成外部 PNG，并强制缩放到原图尺寸。
    ///   所以控件坐标、面板尺寸、点击区域、绘制偏移全部保持原样 —— 只换像素，不动布局。
    ///
    /// 目录约定（相对客户端根目录，即 Client.exe 所在目录）
    ///   UI\skin.ini                        ->  [Skin]
    ///                                          Name=我的皮肤
    ///   UI\Skin\我的皮肤\Prguse\24.png     ->  覆盖 Prguse.lib 的索引 24
    ///   UI\Skin\我的皮肤\Title\197.png     ->  覆盖 Title.lib 的索引 197
    ///   UI\Skin\我的皮肤\Background\10.png ->  覆盖游戏内地图背景
    ///
    /// 文件名就是索引号。找不到对应 PNG 的索引会自动回落到原 .Lib 的图，
    /// 所以只想换几张就只放几张，不用整库补齐。
    /// </summary>
    public static class UISkin
    {
        //★11-20：锚定 exe 目录（原来相对路径受「快捷方式起始位置」影响，皮肤可能读不到）
        private static string SettingFile = Path.Combine(AppContext.BaseDirectory, @"UI\skin.ini");
        private const string SkinRoot = @".\UI\Skin";

        /// <summary>皮肤包是否启用（skin.ini 里配了名字，且目录真实存在）。</summary>
        public static bool Enabled { get; private set; }

        /// <summary>当前皮肤名。</summary>
        public static string Name { get; private set; }

        /// <summary>当前皮肤目录。</summary>
        public static string Root { get; private set; }

        /// <summary>已成功替换的贴图张数（排查用）。</summary>
        public static int Applied;

        // 库名 -> (索引 -> png 路径)。按库懒扫描，避免启动时遍历几千个文件。
        private static readonly Dictionary<string, Dictionary<int, string>> _files
            = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 启动时调用一次。任何异常都静默回落到原图 —— 皮肤包坏了绝不能让人进不了游戏。
        /// </summary>
        public static void Load()
        {
            Enabled = false;
            Name = string.Empty;
            Root = string.Empty;
            Applied = 0;
            _files.Clear();

            try
            {
                if (!File.Exists(SettingFile)) return;

                var reader = new InIReader(SettingFile);
                string name = (reader.ReadString("Skin", "Name", string.Empty) ?? string.Empty).Trim();
                if (name.Length == 0) return;

                // 防止 "..\.." 之类跑到皮肤目录外面去
                if (name.Contains("..") || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return;

                string root = Path.Combine(SkinRoot, name);
                if (!Directory.Exists(root)) return;

                Name = name;
                Root = root;
                Enabled = true;
            }
            catch
            {
                Enabled = false;
            }
        }

        /// <summary>
        /// MLibrary 在每次纹理重建后调用一次。命中则替换 mi.Image（尺寸已对齐原图）。
        /// 不命中或未启用时只是打个标记，不影响原流程。
        /// </summary>
        public static void TryApply(string libName, int index, MImage mi)
        {
            if (mi == null) return;

            // 无论命中与否都标记成「试过了」，避免每帧去查磁盘
            mi.SkinApplied = true;

            if (!Enabled) return;
            if (mi.Image == null || mi.Image.Disposed) return;
            if (mi.Width <= 0 || mi.Height <= 0) return;

            string file = FindFile(libName, index);
            if (file == null) return;

            SlimDX.Direct3D9.Texture tex = CreateTexture(file, mi.Width, mi.Height);
            if (tex == null) return;   // 这张图有问题就保留原图，绝不能把 Image 置空

            SlimDX.Direct3D9.Texture old = mi.Image;

            mi.Image = tex;            //★先换再释放：万一 Dispose 抛异常，Image 也不会变成已释放对象
            Applied++;

            if (old != null)
            {
                try { if (!old.Disposed) old.Dispose(); }
                catch { }
            }
        }

        private static string FindFile(string libName, int index)
        {
            if (string.IsNullOrEmpty(libName)) return null;

            if (!_files.TryGetValue(libName, out Dictionary<int, string> map))
            {
                map = new Dictionary<int, string>();

                try
                {
                    string dir = Path.Combine(Root, libName);
                    if (Directory.Exists(dir))
                    {
                        string[] pngs = Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly);
                        for (int i = 0; i < pngs.Length; i++)
                        {
                            string bare = Path.GetFileNameWithoutExtension(pngs[i]);
                            if (int.TryParse(bare, out int idx) && !map.ContainsKey(idx))
                                map[idx] = pngs[i];
                        }
                    }
                }
                catch { }

                _files[libName] = map;
            }

            return map.TryGetValue(index, out string path) ? path : null;
        }

        /// <summary>
        /// 把 PNG 缩放成 width×height 再转成 A8R8G8B8 纹理。
        /// 尺寸必须和原 .Lib 的图一致，否则 Draw 里的源矩形 (0,0,mi.Width,mi.Height) 会裁掉画面。
        /// </summary>
        private static unsafe SlimDX.Direct3D9.Texture CreateTexture(string file, int width, int height)
        {
            Bitmap dst = null;
            SlimDX.Direct3D9.Texture tex = null;

            try
            {
                using (var src = new Bitmap(file))
                {
                    dst = new Bitmap(width, height, PixelFormat.Format32bppArgb);

                    using (var g = Graphics.FromImage(dst))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.DrawImage(src,
                                    new Rectangle(0, 0, width, height),
                                    new Rectangle(0, 0, src.Width, src.Height),
                                    GraphicsUnit.Pixel);
                    }
                }

                tex = new SlimDX.Direct3D9.Texture(DXManager.Device, width, height, 1,
                                                   Usage.None, Format.A8R8G8B8, Pool.Managed);

                SlimDX.DataRectangle locked = tex.LockRectangle(0, LockFlags.Discard);
                try
                {
                    //★必须用 D3D 返回的真实 pitch。原写法硬算 width*4，
                    //  一旦 pitch 比 width*4 大（对齐补齐）就会写越界 → 堆被写坏 → 系统级 0xc0000005。
                    int pitch = locked.Pitch;
                    if (pitch <= 0) pitch = width * 4;

                    int rowBytes = width * 4;
                    if (rowBytes > pitch) rowBytes = pitch;

                    BitmapData data = dst.LockBits(new Rectangle(0, 0, width, height),
                                                   ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        byte* srcPtr = (byte*)data.Scan0;
                        byte* dstPtr = (byte*)locked.Data.DataPointer;

                        for (int y = 0; y < height; y++)
                        {
                            Buffer.MemoryCopy(srcPtr + (long)y * data.Stride,
                                              dstPtr + (long)y * pitch,
                                              pitch, rowBytes);
                        }
                    }
                    finally
                    {
                        dst.UnlockBits(data);
                    }
                }
                finally
                {
                    //★只解除锁定；原写法先 Dispose(locked.Data) 再 Unlock 属于重复释放
                    tex.UnlockRectangle(0);
                }

                return tex;
            }
            catch
            {
                // 单张图画坏了就当没这张，别影响整局游戏
                if (tex != null)
                {
                    try { if (!tex.Disposed) tex.Dispose(); }
                    catch { }
                }

                return null;
            }
            finally
            {
                if (dst != null)
                {
                    try { dst.Dispose(); }
                    catch { }
                }
            }
        }
    }
}
