using System.Collections.Generic;
using System.IO;
using LibraryEditor.Formats;

namespace LibraryEditor
{
    /// <summary>
    /// .Hurricane 资源库（Hurricane ReVIVAL Mir2 引擎）。
    /// 42 字节 XOR-0x5A 魔数 + 版本/帧数/结束偏移 + 偏移表 + 16 字节帧头 + gzip(BGRA)。
    /// 复用 FrameLibrary 基座：帧模型 MLibraryV2.MImage，未改动帧保存时字节级还原。
    /// </summary>
    public sealed class HurricaneLibrary : FrameLibrary
    {
        private HurricaneContainer _container;

        public HurricaneLibrary(string filename)
        {
            _fileName = filename;
            Load();
        }

        protected override void EnsureContainer()
        {
            if (_container != null) return;

            if (!string.IsNullOrEmpty(_fileName) && File.Exists(_fileName))
                _container = HurricaneContainer.Parse(File.ReadAllBytes(_fileName));
            else
                _container = HurricaneContainer.Parse(HurricaneContainer.BuildEmptyFile());
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
