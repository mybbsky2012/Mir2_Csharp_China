using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Client.Utils
{
    public class Download
    {
        public FileInformation Info;
        public long CurrentBytes;
        public bool Completed;
    }

    public class FileInformation
    {
        public string FileName; //Relative.
        public int Length, Compressed;
        public DateTime Creation;

        //MD5（16 字节原始摘要）。旧清单没有此字段，为 null 时跳过校验。
        public byte[] MD5;

        //哈希追加段标记："MD51"。追加在旧格式末尾，旧版更新器读到 count 条记录即停止，完全兼容。
        public const int HashMarker = 0x3135444D; // "MD51" little-endian

        public FileInformation()
        {

        }
        public FileInformation(BinaryReader reader)
        {
            FileName = reader.ReadString();
            Length = reader.ReadInt32();
            Compressed = reader.ReadInt32();

            Creation = DateTime.FromBinary(reader.ReadInt64());
        }

        //读取整份清单（自动识别有无哈希段）
        public static List<FileInformation> ReadList(BinaryReader reader)
        {
            var list = new List<FileInformation>();

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
                list.Add(new FileInformation(reader));

            try
            {
                if (reader.ReadInt32() == HashMarker)
                {
                    for (int i = 0; i < count; i++)
                        list[i].MD5 = reader.ReadBytes(16);
                }
            }
            catch (EndOfStreamException)
            {
                //旧版清单：无哈希段
            }

            return list;
        }

        //写出整份清单（始终带哈希段，缺省补零）
        public static void WriteList(BinaryWriter writer, List<FileInformation> list)
        {
            writer.Write(list.Count);
            for (int i = 0; i < list.Count; i++)
                list[i].Save(writer);

            writer.Write(HashMarker);
            for (int i = 0; i < list.Count; i++)
            {
                byte[] md5 = list[i].MD5;
                if (md5 == null || md5.Length != 16)
                    md5 = new byte[16];
                writer.Write(md5);
            }
        }

        public static byte[] ComputeMD5(byte[] data)
        {
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
                return md5.ComputeHash(data);
        }

        public static byte[] ComputeMD5File(string path)
        {
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            using (FileStream fs = File.OpenRead(path))
                return md5.ComputeHash(fs);
        }

        public static bool Md5Equal(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != 16 || b.Length != 16) return false;
            for (int i = 0; i < 16; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write(FileName);
            writer.Write(Length);
            writer.Write(Compressed);
            writer.Write(Creation.ToBinary());
        }
    }
}
