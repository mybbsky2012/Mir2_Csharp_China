using System;
using System.Collections.Generic;
using System.IO;
using LibraryEditor.Formats;

namespace LibraryEditor
{
    /// <summary>
    /// .EuRe 资源库（EuReborn / Vortice 客户端，LibVersion=4）。
    /// 帧头 13 字节（Height 与 Width 异或存储）+ gzip(BGRA W*H*4)，索引表在文件末尾。
    /// 复用 FrameLibrary 基座：帧模型 MLibraryV2.MImage，未改动帧保存时字节级还原。
    /// </summary>
    public sealed class EuReLibrary : FrameLibrary
    {
        private EuReContainer _container;

        public EuReLibrary(string filename)
        {
            _fileName = filename;
            Load();
        }

        protected override void EnsureContainer()
        {
            if (_container != null) return;

            if (!string.IsNullOrEmpty(_fileName) && File.Exists(_fileName))
                _container = EuReContainer.Parse(File.ReadAllBytes(_fileName));
            else
                _container = EuReContainer.Parse(EuReContainer.BuildEmptyFile());
        }

        protected override List<FrameRecord> ContainerFrames()
        {
            return _container.Frames;
        }

        protected override byte[] SerializeContainer()
        {
            return _container.Rebuild();
        }
    }
}
