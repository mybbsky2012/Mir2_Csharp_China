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
                _contents.AddRange(File.ReadAllLines(_fileName));
        }
        catch
        {
        }
    }
    #endregion

    #region Functions
    private string FindValue(string section, string key)
    {
        for (int a = 0; a < _contents.Count; a++)
            if (String.CompareOrdinal(_contents[a], "[" + section + "]") == 0)
                for (int b = a + 1; b < _contents.Count; b++)
                    if (String.CompareOrdinal(_contents[b].Split('=')[0], key) == 0)
                        return _contents[b].Split('=')[1];
                    else if (_contents[b].StartsWith("[") && _contents[b].EndsWith("]"))
                        return null;
        return null;
    }

    private int FindIndex(string section, string key)
    {
        for (int a = 0; a < _contents.Count; a++)
            if (String.CompareOrdinal(_contents[a], "[" + section + "]") == 0)
                for (int b = a + 1; b < _contents.Count; b++)
                    if (String.CompareOrdinal(_contents[b].Split('=')[0], key) == 0)
                        return b;
                    else if (_contents[b].StartsWith("[") && _contents[b].EndsWith("]"))
                    {
                        _contents.Insert(b - 1, key + "=");
                        return b - 1;
                    }
                    else if (_contents.Count - 1 == b)
                    {
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
        bool result;

        if (!bool.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public byte ReadByte(string section, string key, byte Default, bool writeWhenNull = true)
    {
        byte result;

        if (!byte.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }


        return result;
    }

    public sbyte ReadSByte(string section, string key, sbyte Default, bool writeWhenNull = true)
    {
        sbyte result;

        if (!sbyte.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }


        return result;
    }

    public ushort ReadUInt16(string section, string key, ushort Default, bool writeWhenNull = true)
    {
        ushort result;

        if (!ushort.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }


        return result;
    }

    public short ReadInt16(string section, string key, short Default, bool writeWhenNull = true)
    {
        short result;

        if (!short.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }


        return result;
    }

    public uint ReadUInt32(string section, string key, uint Default, bool writeWhenNull = true)
    {
        uint result;

        if (!uint.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public int ReadInt32(string section, string key, int Default, bool writeWhenNull = true)
    {
        int result;

        if (!int.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public ulong ReadUInt64(string section, string key, ulong Default, bool writeWhenNull = true)
    {
        ulong result;

        if (!ulong.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public long ReadInt64(string section, string key, long Default, bool writeWhenNull = true)
    {
        long result;

        if (!long.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }


        return result;
    }

    public float ReadSingle(string section, string key, float Default, bool writeWhenNull = true)
    {
        float result;

        if (!float.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public double ReadDouble(string section, string key, double Default, bool writeWhenNull = true)
    {
        double result;

        if (!double.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public decimal ReadDecimal(string section, string key, decimal Default, bool writeWhenNull = true)
    {
        decimal result;

        if (!decimal.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
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

        if (!char.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            if (writeWhenNull) Write(section, key, Default);
        }

        return result;
    }

    public Point ReadPoint(string section, string key, Point Default)
    {
        string temp = FindValue(section, key);
        int tempX, tempY;
        if (temp == null || !int.TryParse(temp.Split(',')[0], out tempX))
        {
            Write(section, key, Default);
            return Default;
        }
        if (!int.TryParse(temp.Split(',')[1], out tempY))
        {
            Write(section, key, Default);
            return Default;
        }

        return new Point(tempX, tempY);
    }

    public Size ReadSize(string section, string key, Size Default)
    {
        string temp = FindValue(section, key);
        int tempX, tempY;
        if (!int.TryParse(temp.Split(',')[0], out tempX))
        {
            Write(section, key, Default);
            return Default;
        }
        if (!int.TryParse(temp.Split(',')[1], out tempY))
        {
            Write(section, key, Default);
            return Default;
        }

        return new Size(tempX, tempY);
    }

    public TimeSpan ReadTimeSpan(string section, string key, TimeSpan Default)
    {
        TimeSpan result;

        if (!TimeSpan.TryParse(FindValue(section, key), out result))
        {
            result = Default;
            Write(section, key, Default);
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