using System.Drawing;

public class InIReader
{
    #region Fields
    private readonly List<string> _contents;
    private readonly string _fileName;

    //★批量写入（11-04）：默认行为完全不变——不调 BeginBatch 时 Write 仍然立即落盘。
    //BeginBatch 之后 Write 只改内存并标记脏，EndBatch/Flush 时把整份内容合并成一次写盘。
    //原因：原来每个键都 File.WriteAllLines 整份重写，一次 Settings.Save() ≈120 个键 = 120 次
    //「开→写→关」，本机 190ms，机械盘/杀软实时扫描时放大到秒级，于是关内挂面板卡几秒。
    private bool _batching;
    private bool _dirty;
    #endregion

    #region Constructor
    public InIReader(string fileName)
    {
        _fileName = fileName;

        if (!Directory.Exists(Path.GetDirectoryName(fileName)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fileName));
        }

        _contents = new List<string>();
        try
        {
            if (File.Exists(_fileName))
            {
                //★修复（11-20）：带 BOM 的文件第一行是「\uFEFF[Section]」，原样比对会让整个段读不到；
                //  顺带清行尾空白（\r / 空格 / 制表），段头带尾随空格也能匹配上。
                foreach (string line in File.ReadAllLines(_fileName))
                    _contents.Add(line.TrimEnd('\uFEFF', '\r', '\t', ' '));
            }
        }
        catch
        {
        }
    }
    #endregion

    #region Functions
    //★修复（11-20）：原来键/段匹配是「Split('=')[0] 直接 CompareOrdinal」——
    //  「DebugMode = True」（等号带空格）、「debugmode=true」（大小写）都读不到，
    //  ReadBoolean 还会以为键不存在而回写一行 Default，把用户手改的行遮住（配置改了不生效的真凶）。
    //  现在统一：去空白、忽略大小写、容错畸形行（无等号跳过）、同段重复键以第一行为准。
    private static string Normalize(string text)
    {
        if (text == null) return string.Empty;
        return text.Trim().TrimStart('\uFEFF').TrimEnd();
    }

    private static bool IsSectionLine(string line, string section)
    {
        string t = Normalize(line);
        if (t.Length < 2 || !t.StartsWith("[") || !t.EndsWith("]")) return false;
        return string.Equals(t.Substring(1, t.Length - 2).Trim(), section, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>取「key=」后面的值（第一个 = 之后全部内容，去两端空白）；无等号返回 null</summary>
    private static string SplitKey(string line, out string key)
    {
        string t = Normalize(line);
        int idx = t.IndexOf('=');
        if (idx < 0) { key = t; return null; }
        key = t.Substring(0, idx).Trim();
        return t.Substring(idx + 1).Trim();
    }

    private string FindValue(string section, string key)
    {
        for (int a = 0; a < _contents.Count; a++)
        {
            if (!IsSectionLine(_contents[a], section)) continue;

            for (int b = a + 1; b < _contents.Count; b++)
            {
                string line = Normalize(_contents[b]);
                if (line.StartsWith("[") && line.EndsWith("]")) break;   // 下一段开始
                if (line.Length == 0) continue;                          // 空行/注释行跳过

                string k;
                string v = SplitKey(line, out k);
                if (v == null) continue;                                 // 畸形行（没有 =）

                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    return v;
            }
            return null;
        }
        return null;
    }

    private int FindIndex(string section, string key)
    {
        for (int a = 0; a < _contents.Count; a++)
        {
            if (!IsSectionLine(_contents[a], section)) continue;

            // 第一轮：找已有键（同时清掉同段重复键——保留第一行，避免旧值遮住新写入）
            int found = -1;
            for (int b = a + 1; b < _contents.Count; b++)
            {
                string line = Normalize(_contents[b]);
                if (line.StartsWith("[") && line.EndsWith("]")) break;
                if (line.Length == 0) continue;

                string k;
                SplitKey(line, out k);
                if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) continue;

                if (found < 0)
                {
                    found = b;
                }
                else
                {
                    _contents.RemoveAt(b);   // 重复键：删掉后面的旧行
                    b--;
                }
            }
            if (found >= 0) return found;

            // 第二轮：键不存在 → 插到段尾（下一个段头/空行/文件末之前）
            for (int b = a + 1; b < _contents.Count; b++)
            {
                string line = Normalize(_contents[b]);
                bool nextSection = line.StartsWith("[") && line.EndsWith("]");
                if (nextSection || line.Length == 0 || b == _contents.Count - 1)
                {
                    int insertAt = (b == _contents.Count - 1 && !nextSection && line.Length > 0) ? b + 1 : b;
                    _contents.Insert(insertAt, key + "=");
                    return insertAt;
                }
            }
            _contents.Add(key + "=");
            return _contents.Count - 1;
        }

        if (_contents.Count > 0)
            _contents.Add("");

        _contents.Add("[" + section + "]");
        _contents.Add(key + "=");
        return _contents.Count - 1;
    }

    public void Save()
    {
        if (_batching)
        {
            _dirty = true;
            return;
        }

        WriteToDisk();
    }

    /// <summary>开始批量写入：Write 只改内存，不落盘</summary>
    public void BeginBatch()
    {
        _batching = true;
    }

    /// <summary>结束批量写入，有改动则合并成一次写盘</summary>
    public void EndBatch()
    {
        _batching = false;

        if (!_dirty) return;

        _dirty = false;
        WriteToDisk();
    }

    /// <summary>有未落盘的改动时立即写盘（批量模式中途要强制落盘时用）</summary>
    public void Flush()
    {
        if (!_dirty) return;

        _dirty = false;
        WriteToDisk();
    }

    /// <summary>是否还有未落盘的改动</summary>
    public bool IsDirty
    {
        get { return _dirty; }
    }

    private void WriteToDisk()
    {
        try
        {
            File.WriteAllLines(_fileName, _contents);
        }
        catch
        {
        }
    }
    #endregion

    #region Read
    public bool ReadBoolean(string section, string key, bool Default, bool writeWhenNull = true)
    {
        //★修复（11-20）：①支持 1/0 写法（原来写 DebugMode=1 会被当成「没有键」回写成 False）；
        //  ②键存在但解析失败时（如写了 yes）用默认值但**不回写**——原来会把用户手改的行直接覆盖掉。
        string raw = FindValue(section, key);

        if (raw == null)
        {
            if (writeWhenNull) Write(section, key, Default);
            return Default;
        }

        if (raw == "1") return true;
        if (raw == "0") return false;

        bool result;
        if (!bool.TryParse(raw, out result))
            return Default;     // 不回写，保留用户原文

        return result;
    }

    public byte ReadByte(string section, string key, byte Default, bool writeWhenNull = true)
    {
        byte result;

        string raw = FindValue(section, key);
        if (raw == null)
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }
        else if (!byte.TryParse(raw, out result))
        {
            result = Default;   //★11-23：键存在但解析失败 → 不回写（对齐 ReadBoolean）
        }


        return result;
    }

    public sbyte ReadSByte(string section, string key, sbyte Default, bool writeWhenNull = true)
    {
        sbyte result;

        string raw = FindValue(section, key);
        if (raw == null)
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }
        else if (!sbyte.TryParse(raw, out result))
        {
            result = Default;   //★11-23：键存在但解析失败 → 不回写（对齐 ReadBoolean）
        }


        return result;
    }

    public ushort ReadUInt16(string section, string key, ushort Default, bool writeWhenNull = true)
    {
        ushort result;

        string raw = FindValue(section, key);
        if (raw == null)
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }
        else if (!ushort.TryParse(raw, out result))
        {
            result = Default;   //★11-23：键存在但解析失败 → 不回写（对齐 ReadBoolean）
        }


        return result;
    }

    public short ReadInt16(string section, string key, short Default, bool writeWhenNull = true)
    {
        short result;

        string raw = FindValue(section, key);
        if (raw == null)
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }
        else if (!short.TryParse(raw, out result))
        {
            result = Default;   //★11-23：键存在但解析失败 → 不回写（对齐 ReadBoolean）
        }


        return result;
    }

    public uint ReadUInt32(string section, string key, uint Default, bool writeWhenNull = true)
    {
        uint result;

        string raw = FindValue(section, key);
        if (raw == null)
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }
        else if (!uint.TryParse(raw, out result))
        {
            result = Default;   //★11-23：键存在但解析失败 → 不回写（对齐 ReadBoolean）
        }

        return result;
    }

    public int ReadInt32(string section, string key, int Default, bool writeWhenNull = true)
    {
        int result;

        string raw = FindValue(section, key);
        if (raw == null)
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }
        else if (!int.TryParse(raw, out result))
        {
            result = Default;   //★11-23：键存在但解析失败 → 不回写（对齐 ReadBoolean）
        }

        return result;
    }

    public ulong ReadUInt64(string section, string key, ulong Default, bool writeWhenNull = true)
    {
        ulong result;

        string raw = FindValue(section, key);
        if (raw == null)
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }
        else if (!ulong.TryParse(raw, out result))
        {
            result = Default;   //★11-23：键存在但解析失败 → 不回写（对齐 ReadBoolean）
        }

        return result;
    }

    public long ReadInt64(string section, string key, long Default, bool writeWhenNull = true)
    {
        long result;

        string raw = FindValue(section, key);
        if (raw == null)
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }
        else if (!long.TryParse(raw, out result))
        {
            result = Default;   //★11-23：键存在但解析失败 → 不回写（对齐 ReadBoolean）
        }


        return result;
    }

    public float ReadSingle(string section, string key, float Default, bool writeWhenNull = true)
    {
        float result;

        string raw = FindValue(section, key);
        if (raw == null)
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }
        else if (!float.TryParse(raw, out result))
        {
            result = Default;   //★11-23：键存在但解析失败 → 不回写（对齐 ReadBoolean）
        }

        return result;
    }

    public double ReadDouble(string section, string key, double Default, bool writeWhenNull = true)
    {
        double result;

        string raw = FindValue(section, key);
        if (raw == null)
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }
        else if (!double.TryParse(raw, out result))
        {
            result = Default;   //★11-23：键存在但解析失败 → 不回写（对齐 ReadBoolean）
        }

        return result;
    }

    public decimal ReadDecimal(string section, string key, decimal Default, bool writeWhenNull = true)
    {
        decimal result;

        string raw = FindValue(section, key);
        if (raw == null)
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }
        else if (!decimal.TryParse(raw, out result))
        {
            result = Default;   //★11-23：键存在但解析失败 → 不回写（对齐 ReadBoolean）
        }

        return result;
    }

    public string ReadString(string section, string key, string Default, bool writeWhenNull = true)
    {
        string result = FindValue(section, key);

        if (string.IsNullOrEmpty(result))
        {
            result = Default;

            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public char ReadChar(string section, string key, char Default, bool writeWhenNull = true)
    {
        char result;

        string raw = FindValue(section, key);
        if (raw == null)
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }
        else if (!char.TryParse(raw, out result))
        {
            result = Default;   //★11-23：键存在但解析失败 → 不回写（对齐 ReadBoolean）
        }

        return result;
    }

    //★11-23：原来 ReadPoint/ReadSize 有两处崩溃级边界——
    //  1) ReadSize 没判 temp==null（键被删一行 → NRE 启动即崩）；
    //  2) 值只有单个数字（如「X=100」缺逗号）时 Split(',')[1] 直接 IndexOutOfRange。
    //  现在统一：键缺失 → 写入默认值；键存在但内容坏 → 用默认值但不回写（对齐 ReadBoolean）。
    private static bool TryReadPair(string raw, out int x, out int y)
    {
        x = 0; y = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        string[] parts = raw.Split(',');
        return parts.Length >= 2
            && int.TryParse(parts[0].Trim(), out x)
            && int.TryParse(parts[1].Trim(), out y);
    }

    public Point ReadPoint(string section, string key, Point Default)
    {
        string temp = FindValue(section, key);

        if (temp == null)
        {
            Write(section, key, Default);
            return Default;
        }

        int tempX, tempY;
        if (!TryReadPair(temp, out tempX, out tempY))
            return Default;   // 内容坏：不回写，保留用户原文

        return new Point(tempX, tempY);
    }

    public Size ReadSize(string section, string key, Size Default)
    {
        string temp = FindValue(section, key);

        if (temp == null)
        {
            Write(section, key, Default);
            return Default;
        }

        int tempX, tempY;
        if (!TryReadPair(temp, out tempX, out tempY))
            return Default;   // 内容坏：不回写，保留用户原文

        return new Size(tempX, tempY);
    }

    public TimeSpan ReadTimeSpan(string section, string key, TimeSpan Default)
    {
        TimeSpan result;

        string raw = FindValue(section, key);
        if (raw == null)
        {
            result = Default;
            Write(section, key, Default);
        }
        else if (!TimeSpan.TryParse(raw, out result))
        {
            result = Default;   //★11-23：键存在但解析失败 → 不回写（对齐 ReadBoolean）
        }


        return result;
    }

    public float ReadFloat(string section, string key, float Default)
    {
        float result;

        if (!float.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            Write(section, key, Default);
        }

        return result;
    }
    #endregion

    #region Write
    public void Write(string section, string key, bool value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, byte value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, sbyte value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, ushort value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, short value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, uint value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, int value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, ulong value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, long value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, float value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, double value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, decimal value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, string value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, char value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }

    public void Write(string section, string key, Point value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value.X + "," + value.Y;
        Save();
    }

    public void Write(string section, string key, Size value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value.Width + "," + value.Height;
        Save();
    }

    public void Write(string section, string key, TimeSpan value)
    {
        _contents[FindIndex(section, key)] = key + "=" + value;
        Save();
    }
    #endregion
}