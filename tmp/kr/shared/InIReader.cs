using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

public class InIReader
{
	private readonly List<string> _contents;

	private readonly string _fileName;

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
				_contents.AddRange(File.ReadAllLines(_fileName));
			}
		}
		catch
		{
		}
	}

	private string FindValue(string section, string key)
	{
		for (int i = 0; i < _contents.Count; i++)
		{
			if (string.CompareOrdinal(_contents[i], "[" + section + "]") != 0)
			{
				continue;
			}
			for (int j = i + 1; j < _contents.Count; j++)
			{
				if (string.CompareOrdinal(_contents[j].Split('=')[0], key) == 0)
				{
					return _contents[j].Split('=')[1];
				}
				if (_contents[j].StartsWith("[") && _contents[j].EndsWith("]"))
				{
					return null;
				}
			}
		}
		return null;
	}

	private int FindIndex(string section, string key)
	{
		for (int i = 0; i < _contents.Count; i++)
		{
			if (string.CompareOrdinal(_contents[i], "[" + section + "]") != 0)
			{
				continue;
			}
			for (int j = i + 1; j < _contents.Count; j++)
			{
				if (string.CompareOrdinal(_contents[j].Split('=')[0], key) == 0)
				{
					return j;
				}
				if (_contents[j].StartsWith("[") && _contents[j].EndsWith("]"))
				{
					_contents.Insert(j - 1, key + "=");
					return j - 1;
				}
				if (_contents.Count - 1 == j)
				{
					_contents.Add(key + "=");
					return _contents.Count - 1;
				}
			}
		}
		if (_contents.Count > 0)
		{
			_contents.Add("");
		}
		_contents.Add("[" + section + "]");
		_contents.Add(key + "=");
		return _contents.Count - 1;
	}

	public void Save()
	{
		try
		{
			File.WriteAllLines(_fileName, _contents);
		}
		catch
		{
		}
	}

	public bool ReadBoolean(string section, string key, bool Default, bool writeWhenNull = true)
	{
		if (!bool.TryParse(FindValue(section, key), out var result))
		{
			result = Default;
			if (writeWhenNull)
			{
				Write(section, key, Default);
			}
		}
		return result;
	}

	public byte ReadByte(string section, string key, byte Default, bool writeWhenNull = true)
	{
		if (!byte.TryParse(FindValue(section, key), out var result))
		{
			result = Default;
			if (writeWhenNull)
			{
				Write(section, key, Default);
			}
		}
		return result;
	}

	public sbyte ReadSByte(string section, string key, sbyte Default, bool writeWhenNull = true)
	{
		if (!sbyte.TryParse(FindValue(section, key), out var result))
		{
			result = Default;
			if (writeWhenNull)
			{
				Write(section, key, Default);
			}
		}
		return result;
	}

	public ushort ReadUInt16(string section, string key, ushort Default, bool writeWhenNull = true)
	{
		if (!ushort.TryParse(FindValue(section, key), out var result))
		{
			result = Default;
			if (writeWhenNull)
			{
				Write(section, key, Default);
			}
		}
		return result;
	}

	public short ReadInt16(string section, string key, short Default, bool writeWhenNull = true)
	{
		if (!short.TryParse(FindValue(section, key), out var result))
		{
			result = Default;
			if (writeWhenNull)
			{
				Write(section, key, Default);
			}
		}
		return result;
	}

	public uint ReadUInt32(string section, string key, uint Default, bool writeWhenNull = true)
	{
		if (!uint.TryParse(FindValue(section, key), out var result))
		{
			result = Default;
			if (writeWhenNull)
			{
				Write(section, key, Default);
			}
		}
		return result;
	}

	public int ReadInt32(string section, string key, int Default, bool writeWhenNull = true)
	{
		if (!int.TryParse(FindValue(section, key), out var result))
		{
			result = Default;
			if (writeWhenNull)
			{
				Write(section, key, Default);
			}
		}
		return result;
	}

	public ulong ReadUInt64(string section, string key, ulong Default, bool writeWhenNull = true)
	{
		if (!ulong.TryParse(FindValue(section, key), out var result))
		{
			result = Default;
			if (writeWhenNull)
			{
				Write(section, key, Default);
			}
		}
		return result;
	}

	public long ReadInt64(string section, string key, long Default, bool writeWhenNull = true)
	{
		if (!long.TryParse(FindValue(section, key), out var result))
		{
			result = Default;
			if (writeWhenNull)
			{
				Write(section, key, Default);
			}
		}
		return result;
	}

	public float ReadSingle(string section, string key, float Default, bool writeWhenNull = true)
	{
		if (!float.TryParse(FindValue(section, key), out var result))
		{
			result = Default;
			if (writeWhenNull)
			{
				Write(section, key, Default);
			}
		}
		return result;
	}

	public double ReadDouble(string section, string key, double Default, bool writeWhenNull = true)
	{
		if (!double.TryParse(FindValue(section, key), out var result))
		{
			result = Default;
			if (writeWhenNull)
			{
				Write(section, key, Default);
			}
		}
		return result;
	}

	public decimal ReadDecimal(string section, string key, decimal Default, bool writeWhenNull = true)
	{
		if (!decimal.TryParse(FindValue(section, key), out var result))
		{
			result = Default;
			if (writeWhenNull)
			{
				Write(section, key, Default);
			}
		}
		return result;
	}

	public string ReadString(string section, string key, string Default, bool writeWhenNull = true)
	{
		string text = FindValue(section, key);
		if (string.IsNullOrEmpty(text))
		{
			text = Default;
			if (writeWhenNull)
			{
				Write(section, key, Default);
			}
		}
		return text;
	}

	public char ReadChar(string section, string key, char Default, bool writeWhenNull = true)
	{
		if (!char.TryParse(FindValue(section, key), out var result))
		{
			result = Default;
			if (writeWhenNull)
			{
				Write(section, key, Default);
			}
		}
		return result;
	}

	public Point ReadPoint(string section, string key, Point Default)
	{
		string text = FindValue(section, key);
		if (text == null || !int.TryParse(text.Split(',')[0], out var result))
		{
			Write(section, key, Default);
			return Default;
		}
		if (!int.TryParse(text.Split(',')[1], out var result2))
		{
			Write(section, key, Default);
			return Default;
		}
		return new Point(result, result2);
	}

	public Size ReadSize(string section, string key, Size Default)
	{
		string text = FindValue(section, key);
		if (!int.TryParse(text.Split(',')[0], out var result))
		{
			Write(section, key, Default);
			return Default;
		}
		if (!int.TryParse(text.Split(',')[1], out var result2))
		{
			Write(section, key, Default);
			return Default;
		}
		return new Size(result, result2);
	}

	public TimeSpan ReadTimeSpan(string section, string key, TimeSpan Default)
	{
		if (!TimeSpan.TryParse(FindValue(section, key), out var result))
		{
			result = Default;
			Write(section, key, Default);
		}
		return result;
	}

	public float ReadFloat(string section, string key, float Default)
	{
		if (!float.TryParse(FindValue(section, key), out var result))
		{
			result = Default;
			Write(section, key, Default);
		}
		return result;
	}

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
		List<string> contents = _contents;
		int index = FindIndex(section, key);
		ReadOnlySpan<char> readOnlySpan = key;
		char reference = '=';
		ReadOnlySpan<char> readOnlySpan2 = new ReadOnlySpan<char>(ref reference);
		char reference2 = value;
		contents[index] = string.Concat(readOnlySpan, readOnlySpan2, new ReadOnlySpan<char>(ref reference2));
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
}
