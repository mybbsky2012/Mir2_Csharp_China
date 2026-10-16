using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using LibraryEditor.Formats;

namespace LibraryEditor
{
    /// <summary>
    /// 「偏移表 + 17 字节帧头 + gzip 负载」类容器的共用实现（Elite / ALib）。
    /// 帧模型复用 MLibraryV2.MImage，因此 LMain 中 _selectedImage.Image/.X/.Y/.Width/.Height
    /// 以及 _library.Images[i].Image 等既有用法无需任何改动。
    ///
    /// 铁律：未修改的帧其 FBytes 与容器解析出的原始 gzip 字节是同一个数组引用，
    /// 保存时原样回写 —— 保证「打开 → 未改动 → 保存」字节级一致。
    /// </summary>
    public abstract class FrameLibrary : ILibrary
    {
        protected string _fileName;
        protected bool _initialized;
        protected FrameSet _frames = new FrameSet();

        public List<MLibraryV2.MImage> Images { get; protected set; } = new List<MLibraryV2.MImage>();

        public int Count { get { return Images == null ? 0 : Images.Count; } }

        public string FileName
        {
            get { return _fileName; }
            set { _fileName = value; }
        }

        /// <summary>Elite/ALib 容器没有动作帧段；返回一个可写的空 FrameSet（写盘时被忽略）。</summary>
        public FrameSet Frames
        {
            get { return _frames; }
            set { _frames = value ?? new FrameSet(); }
        }

        /// <summary>解压负载时要跳过的常量前缀长度：ALib=10，Elite=0。</summary>
        protected virtual int PayloadSkip { get { return 0; } }

        /// <summary>重新编码负载时前置的常量前缀：ALib 非空，Elite 为 null。</summary>
        protected virtual byte[] PayloadPrefix { get { return null; } }

        // ---------------- 子类实现：容器接入 ----------------

        /// <summary>确保容器已就绪：文件存在则解析，否则构造一个空容器。</summary>
        protected abstract void EnsureContainer();

        /// <summary>取容器的帧记录列表（允许增删改）。</summary>
        protected abstract List<FrameRecord> ContainerFrames();

        /// <summary>把容器序列化为完整文件字节。</summary>
        protected abstract byte[] SerializeContainer();

        // ---------------- 生命周期 ----------------

        protected void Load()
        {
            _initialized = true;
            Images = new List<MLibraryV2.MImage>();

            if (string.IsNullOrEmpty(_fileName) || !File.Exists(_fileName))
                return;

            EnsureContainer();

            List<FrameRecord> frames = ContainerFrames();
            // 预分配：十万帧级的库靠 List 自动扩容会反复搬移大数组
            Images = new List<MLibraryV2.MImage>(frames.Count);
            for (int i = 0; i < frames.Count; i++)
                Images.Add(FromFrameRecord(frames[i]));
            // 位图改为按需解码（EnsureTexture）：上万帧的库一次性解码会耗尽 GDI 句柄与内存
        }

        /// <summary>按需解码指定帧的位图。</summary>
        public void EnsureTexture(int index)
        {
            if (Images == null || index < 0 || index >= Images.Count) return;

            MLibraryV2.MImage mi = Images[index];
            if (mi == null || mi.TextureValid || mi.Image != null) return;
            if (!MLibraryV2.Load || mi.FBytes == null || mi.FBytes.Length == 0) return;

            try
            {
                mi.CreateTexture(null);
                mi.TextureValid = true;
            }
            catch
            {
                // 单帧损坏（截断 / 非法 gzip / 尺寸异常）不应导致整库无法浏览，预览显示占位图。
                mi.Image = null;
                mi.Preview = null;
            }
        }

        private MLibraryV2.MImage FromFrameRecord(FrameRecord fr)
        {
            byte[] payload = fr.Payload ?? Array.Empty<byte>();

            // 直接持有原始 gzip 字节引用，不做拷贝：未改动帧保存时可字节级还原。
            MLibraryV2.MImage mi = new MLibraryV2.MImage(payload, fr.Width, fr.Height)
            {
                X = fr.OffsetX,
                Y = fr.OffsetY,
                ShadowX = ReadInt16(fr.HeaderTail, 0),
                ShadowY = ReadInt16(fr.HeaderTail, 2),
                Shadow = ReadUInt8(fr.HeaderTail, 4),
                Length = payload.Length,
                HasMask = false,          // Elite/ALib 该字节不含 mask 语义，禁止按 bit7 再读一层
                PayloadSkip = PayloadSkip,
                PayloadPrefix = PayloadPrefix,
            };
            return mi;
        }

        private static short ReadInt16(byte[] b, int off)
        {
            if (b == null || b.Length < off + 2) return 0;
            return (short)(b[off] | (b[off + 1] << 8));
        }

        private static byte ReadUInt8(byte[] b, int off)
        {
            if (b == null || b.Length <= off) return 0;
            return b[off];
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(_fileName)) return;

            EnsureContainer();
            SyncToContainer();
            File.WriteAllBytes(_fileName, SerializeContainer());
        }

        /// <summary>把帧模型回写到容器的 FrameRecord，再交由容器重建字节。</summary>
        protected void SyncToContainer()
        {
            List<FrameRecord> frames = ContainerFrames();
            if (frames == null) return;

            while (frames.Count > Images.Count)
                frames.RemoveAt(frames.Count - 1);
            while (frames.Count < Images.Count)
                frames.Add(new FrameRecord());

            for (int i = 0; i < Images.Count; i++)
            {
                MLibraryV2.MImage mi = Images[i];
                if (mi == null) continue;

                FrameRecord fr = frames[i];
                fr.Width = mi.Width;
                fr.Height = mi.Height;
                fr.OffsetX = mi.X;
                fr.OffsetY = mi.Y;
                fr.HeaderTail = new byte[]
                {
                    (byte)(mi.ShadowX & 0xFF), (byte)((mi.ShadowX >> 8) & 0xFF),
                    (byte)(mi.ShadowY & 0xFF), (byte)((mi.ShadowY >> 8) & 0xFF),
                    mi.Shadow          // 原样保留（含 bit7），禁止按 mask 语义改写
                };
                fr.Payload = mi.FBytes ?? Array.Empty<byte>();
            }
        }

        public void Close()
        {
            if (Images == null) return;
            for (int i = 0; i < Images.Count; i++)
                DisposeImages(Images[i]);
        }

        private static void DisposeImages(MLibraryV2.MImage mi)
        {
            if (mi == null) return;
            if (mi.Preview != null) { mi.Preview.Dispose(); mi.Preview = null; }
            if (mi.Image != null) { mi.Image.Dispose(); mi.Image = null; }
            if (mi.MaskImage != null) { mi.MaskImage.Dispose(); mi.MaskImage = null; }
            mi.TextureValid = false;
        }

        // ---------------- 帧模型操作（行为对齐 MLibraryV2）----------------

        public MLibraryV2.MImage GetMImage(int index)
        {
            if (Images == null || index < 0 || index >= Images.Count) return null;
            EnsureTexture(index);
            return Images[index];
        }

        public Bitmap GetPreview(int index)
        {
            if (Images == null || index < 0 || index >= Images.Count) return new Bitmap(1, 1);

            EnsureTexture(index);

            MLibraryV2.MImage image = Images[index];
            if (image == null || image.Image == null) return new Bitmap(1, 1);

            if (image.Preview == null) image.CreatePreview();
            return image.Preview;
        }

        protected MLibraryV2.MImage CreateFromBitmap(Bitmap image, short x, short y, bool removeBlack)
        {
            return new MLibraryV2.MImage(image, removeBlack, PayloadSkip, PayloadPrefix)
            {
                X = x,
                Y = y,
                HasMask = false,
            };
        }

        public void AddImage(Bitmap image, short x, short y, bool removeBlack = true)
        {
            Images.Add(CreateFromBitmap(image, x, y, removeBlack));
        }

        /// <summary>Elite/ALib 容器没有遮罩层，忽略 MaskImage（避免把 mask 误写进帧流）。</summary>
        public void AddImage(Bitmap image, Bitmap MaskImage, short x, short y, bool removeBlack = true)
        {
            Images.Add(CreateFromBitmap(image, x, y, removeBlack));
        }

        public void InsertImage(int index, Bitmap image, short x, short y, bool removeBlack = true)
        {
            if (index < 0) index = 0;
            if (index > Images.Count) index = Images.Count;
            Images.Insert(index, CreateFromBitmap(image, x, y, removeBlack));
        }

        public void ReplaceImage(int index, Bitmap image, short xOffset, short yOffset, bool removeBlack = true)
        {
            if (Images == null || index < 0 || index >= Images.Count) return;

            MLibraryV2.MImage old = Images[index];
            MLibraryV2.MImage mImage = CreateFromBitmap(image, xOffset, yOffset, removeBlack);

            // 保留原帧的阴影与保留字节，避免替换图像后帧头语义漂移
            if (old != null)
            {
                mImage.ShadowX = old.ShadowX;
                mImage.ShadowY = old.ShadowY;
                mImage.Shadow = old.Shadow;
            }

            DisposeImages(old);
            Images[index] = mImage;
        }

        public void RemoveImage(int index)
        {
            if (Images == null) return;

            if (Images.Count <= 1)
            {
                for (int i = 0; i < Images.Count; i++) DisposeImages(Images[i]);
                Images.Clear();
                return;
            }

            if (index < 0 || index >= Images.Count) return;
            DisposeImages(Images[index]);
            Images.RemoveAt(index);
        }

        public void RemoveBlanks(bool safe = false)
        {
            if (Images == null) return;

            for (int i = Images.Count - 1; i >= 0; i--)
            {
                MLibraryV2.MImage mi = Images[i];
                if (mi == null) continue;

                if (mi.FBytes == null || mi.FBytes.Length <= 24)
                {
                    if (!safe)
                        RemoveImage(i);
                    else if (mi.X == 0 && mi.Y == 0)
                        RemoveImage(i);
                }
            }
        }
    }
}
