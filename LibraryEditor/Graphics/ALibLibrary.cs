using System;
using System.Collections.Generic;
using System.IO;
using LibraryEditor.Formats;

namespace LibraryEditor
{
    /// <summary>
    /// .ALib 资源库（domino 客户端）。
    /// 容器：int32 Version=2 | int32 N | int32 Offsets[N] | 帧区（无尾块、无元数据块）。
    /// 全部 N 项偏移都是帧；偏移严格递增、相邻零间隙、最后一帧恰好到 EOF。
    /// 帧记录与 .Elite 同构，但解压负载 = 10 字节常量前缀 + BGRA8888(W*H*4)。
    /// </summary>
    public sealed class ALibLibrary : FrameLibrary
    {
        private ALibContainer _container;

        public ALibLibrary(string filename)
        {
            _fileName = filename;
            Load();
        }

        protected override int PayloadSkip { get { return ALibContainer.PayloadPrefixLength; } }

        protected override byte[] PayloadPrefix { get { return ALibContainer.PayloadPrefix; } }

        protected override void EnsureContainer()
        {
            if (_container != null) return;

            if (!string.IsNullOrEmpty(_fileName) && File.Exists(_fileName))
                _container = ALibContainer.Parse(File.ReadAllBytes(_fileName));
            else
                _container = ALibContainer.Parse(BuildEmptyFile());
        }

        protected override List<FrameRecord> ContainerFrames()
        {
            return _container.Frames;
        }

        protected override byte[] SerializeContainer()
        {
            return _container.Rebuild();
        }

        /// <summary>空 .ALib：Version=2 | N=0，共 8 字节。</summary>
        private static byte[] BuildEmptyFile()
        {
            byte[] b = new byte[8];
            BitConverter.GetBytes(ALibContainer.ExpectedVersion).CopyTo(b, 0);
            BitConverter.GetBytes(0).CopyTo(b, 4);
            return b;
        }
    }
}
