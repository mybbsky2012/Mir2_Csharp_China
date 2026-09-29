using NAudio.Wave;
using System;
using Client.Utils;

namespace Client.MirSounds.Libraries
{
    class CachedSound
    {
        public int Index { get; private set; }
        public long ExpireTime { get; set; }
        public float[] AudioData { get; private set; }
        public WaveFormat WaveFormat { get; private set; }
        public CachedSound(int index, string fileName)
        {
            Index = index;

            fileName = Path.Combine(Settings.SoundPath, fileName);
            string fileType = Path.GetExtension(fileName);

            // attempt to find file
            if (String.IsNullOrEmpty(fileType))
            {
                foreach (String ext in SoundManager.SupportedFileTypes)
                {
                    string candidate = $"{fileName}{ext}";

                    // 微端：本地没有该音效时按需下载。音效是整个资源里数量最多的一块，
                    // 不预下载能省下最多体积；下载失败会走负缓存，不会反复重试。
                    if (!File.Exists(candidate))
                        ResourceDownloader.EnsureLocalFile(candidate);

                    if (File.Exists(candidate))
                    {
                        fileName = candidate;
                        fileType = ext;

                        break;
                    }
                }
            }
            else if (!File.Exists(fileName))
            {
                // 微端：SoundList.lst 映射出来的名字自带扩展名（如 "1.wav"），
                // 上面那个逐扩展名试探分支不会执行 —— 必须在这里单独补按需下载，
                // 否则索引表正常之后所有音效反而一个都下载不下来（整局无声）。
                ResourceDownloader.EnsureLocalFile(fileName);
            }

            if (SoundManager.SupportedFileTypes.Contains(fileType) &&
                File.Exists(fileName))
            {
                using (var audioFileReader = new AudioFileReader(fileName))
                {
                    WaveFormat = audioFileReader.WaveFormat;
                    var wholeFile = new List<float>((int)(audioFileReader.Length / 4));
                    var readBuffer = new float[audioFileReader.WaveFormat.SampleRate * audioFileReader.WaveFormat.Channels];
                    int samplesRead;
                    while ((samplesRead = audioFileReader.Read(readBuffer, 0, readBuffer.Length)) > 0)
                    {
                        wholeFile.AddRange(readBuffer.Take(samplesRead));
                    }
                    AudioData = wholeFile.ToArray();
                }
            }

            // 微端：本地文件还在排队下载时 AudioData 为空，这条缓存默认要等 30 秒的
            // 清理周期才会被移除重试 —— 缩短到 5 秒，让音效在下载完成后尽快接上。
            if (AudioData == null)
                ExpireTime = CMain.Time + 5000;
        }
    }
}
