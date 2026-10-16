using System.Collections.Generic;
using System.Drawing;

namespace LibraryEditor
{
    /// <summary>
    /// 图像资源库的统一抽象。
    /// 成员集合严格按 LMain 对 _library 的实际调用点裁剪（见 LMain.cs 中 _library.xxx 的用法）。
    /// 所有实现复用 MLibraryV2.MImage 作为帧模型，因此 LMain 内
    /// _selectedImage.Image/.X/.Y/.Width/.Height 等用法无需改动。
    /// </summary>
    public interface ILibrary
    {
        string FileName { get; set; }

        List<MLibraryV2.MImage> Images { get; }

        /// <summary>动作帧定义。Elite/ALib 容器没有 frames 段，返回可写的空 FrameSet。</summary>
        FrameSet Frames { get; set; }

        int Count { get; }

        void Save();

        void Close();

        MLibraryV2.MImage GetMImage(int index);

        Bitmap GetPreview(int index);

        void AddImage(Bitmap image, short x, short y, bool removeBlack = true);

        /// <summary>带遮罩层的添加（仅 .Lib 系列真正支持；Elite/ALib 无遮罩层，实现会忽略 MaskImage）。</summary>
        void AddImage(Bitmap image, Bitmap MaskImage, short x, short y, bool removeBlack = true);

        void InsertImage(int index, Bitmap image, short x, short y, bool removeBlack = true);

        void ReplaceImage(int index, Bitmap image, short xOffset, short yOffset, bool removeBlack = true);

        void RemoveImage(int index);

        void RemoveBlanks(bool safe = false);
    }
}
