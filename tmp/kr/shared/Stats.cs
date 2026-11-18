using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public sealed class Stats : IEquatable<Stats>
{
	public SortedDictionary<Stat, int> Values { get; set; } = new SortedDictionary<Stat, int>();


	public int Count => Values.Sum((KeyValuePair<Stat, int> pair) => Math.Abs(pair.Value));

	public int this[Stat stat]
	{
		get
		{
			if (Values.TryGetValue(stat, out var value))
			{
				return value;
			}
			return 0;
		}
		set
		{
			if (value == 0)
			{
				if (Values.ContainsKey(stat))
				{
					Values.Remove(stat);
				}
			}
			else
			{
				Values[stat] = value;
			}
		}
	}

	public Stats()
	{
	}

	public Stats(Stats stats)
	{
		foreach (KeyValuePair<Stat, int> value in stats.Values)
		{
			this[value.Key] += value.Value;
		}
	}

	public Stats(BinaryReader reader, int version = int.MaxValue, int customVersion = int.MaxValue)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			Values[(Stat)reader.ReadByte()] = reader.ReadInt32();
		}
	}

	public void Add(Stats stats)
	{
		foreach (KeyValuePair<Stat, int> value in stats.Values)
		{
			this[value.Key] += value.Value;
		}
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write(Values.Count);
		foreach (KeyValuePair<Stat, int> value in Values)
		{
			writer.Write((byte)value.Key);
			writer.Write(value.Value);
		}
	}

	public void Clear()
	{
		Values.Clear();
	}

	public bool Equals(Stats other)
	{
		if (Values.Count != other.Values.Count)
		{
			return false;
		}
		foreach (KeyValuePair<Stat, int> value in Values)
		{
			if (other[value.Key] != value.Value)
			{
				return false;
			}
		}
		return true;
	}
}
