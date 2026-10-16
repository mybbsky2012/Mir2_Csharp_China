using System;
using System.Collections.Generic;
using System.IO;
using LibraryEditor.Formats;

namespace LibraryEditor
{
    /// <summary>
    /// .Elite 资源库（Elite Origins 客户端）。
    /// 容器：uint32 Version=4 | uint32 Signature="FEIH" | int32 N | int32 Offsets[N] | uint32 FrameStart | 帧区 | 尾块 | 元数据块。
    /// 可寻址帧 = Offsets[1..N-1]，共 N-1 帧。帧头 17 字节 + gzip(BGRA W*H*4)，无负载前缀。
    /// </summary>
    public sealed class EliteLibrary : FrameLibrary
    {
        private EliteContainer _container;

        public EliteLibrary(string filename)
        {
            _fileName = filename;
            Load();
        }

        protected override void EnsureContainer()
        {
            if (_container != null) return;

            if (!string.IsNullOrEmpty(_fileName) && File.Exists(_fileName))
                _container = EliteContainer.Parse(File.ReadAllBytes(_fileName));
            else
                _container = EliteContainer.Parse(BuildEmptyFile());
        }

        protected override List<FrameRecord> ContainerFrames()
        {
            return _container.Frames;
        }

        protected override byte[] SerializeContainer()
        {
            return _container.Rebuild();
        }

        /// <summary>
        /// 空 .Elite：Version=4 | "FEIH" | N=0 | FrameStart=16 (12+4*0+4) | 元数据块 4 字节(RecordCount=0)，共 20 字节。
        /// 与实机 Monster\178.Elite 完全一致。
        /// </summary>
        private static byte[] BuildEmptyFile()
        {
            byte[] b = new byte[20];
            BitConverter.GetBytes(EliteContainer.ExpectedVersion).CopyTo(b, 0);
            BitConverter.GetBytes(EliteContainer.SignatureFEIH).CopyTo(b, 4);
            BitConverter.GetBytes(0).CopyTo(b, 8);
            BitConverter.GetBytes(12 + 4 * 0 + 4).CopyTo(b, 12);
            // b[16..19] = 元数据块 RecordCount = 0
            return b;
        }
    }
}
