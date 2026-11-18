using System;
using System.Collections.Generic;
using System.IO;

public class Awake
{
	public static byte AwakeSuccessRate = 70;

	public static byte AwakeHitRate = 70;

	public static int MaxAwakeLevel = 5;

	public static byte Awake_WeaponRate = 1;

	public static byte Awake_HelmetRate = 1;

	public static byte Awake_ArmorRate = 5;

	public static byte AwakeChanceMin = 1;

	public static float[] AwakeMaterialRate = new float[4] { 1f, 1f, 1f, 1f };

	public static byte[] AwakeChanceMax = new byte[4] { 1, 2, 3, 4 };

	public static List<List<byte>[]> AwakeMaterials = new List<List<byte>[]>();

	public AwakeType Type;

	private readonly List<byte> listAwake = new List<byte>();

	public Awake()
	{
	}

	public Awake(BinaryReader reader)
	{
		Type = (AwakeType)reader.ReadByte();
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			listAwake.Add(reader.ReadByte());
		}
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write((byte)Type);
		writer.Write(listAwake.Count);
		foreach (byte item in listAwake)
		{
			writer.Write(item);
		}
	}

	public bool IsMaxLevel()
	{
		return listAwake.Count == MaxAwakeLevel;
	}

	public int GetAwakeLevel()
	{
		return listAwake.Count;
	}

	public byte GetAwakeValue()
	{
		byte b = 0;
		foreach (byte item in listAwake)
		{
			b += item;
		}
		return b;
	}

	public bool CheckAwakening(UserItem item, AwakeType type)
	{
		if (item.Info.Bind.HasFlag(BindMode.DontUpgrade))
		{
			return false;
		}
		if (!item.Info.CanAwakening)
		{
			return false;
		}
		if (item.Info.Grade == ItemGrade.None)
		{
			return false;
		}
		if (IsMaxLevel())
		{
			return false;
		}
		if (Type == AwakeType.None)
		{
			if (item.Info.Type == ItemType.武器)
			{
				if (type == AwakeType.物理攻击 || type == AwakeType.魔法攻击 || type == AwakeType.道术攻击)
				{
					Type = type;
					return true;
				}
				return false;
			}
			if (item.Info.Type == ItemType.头盔)
			{
				if (type == AwakeType.物理防御 || type == AwakeType.魔法防御)
				{
					Type = type;
					return true;
				}
				return false;
			}
			if (item.Info.Type == ItemType.盔甲)
			{
				if (type == AwakeType.生命法力值)
				{
					Type = type;
					return true;
				}
				return false;
			}
			return false;
		}
		if (Type == type)
		{
			return true;
		}
		return false;
	}

	public int UpgradeAwake(UserItem item, AwakeType type, out bool[] isHit)
	{
		isHit = null;
		if (!CheckAwakening(item, type))
		{
			return -1;
		}
		if (new Random(DateTime.Now.Millisecond).Next(0, 100) <= AwakeSuccessRate)
		{
			isHit = Awakening(item);
			return 1;
		}
		isHit = MakeHit(1, out var _);
		return 0;
	}

	public int RemoveAwake()
	{
		if (listAwake.Count > 0)
		{
			listAwake.Remove(listAwake[listAwake.Count - 1]);
			if (listAwake.Count == 0)
			{
				Type = AwakeType.None;
			}
			return 1;
		}
		Type = AwakeType.None;
		return 0;
	}

	public int GetAwakeLevelValue(int i)
	{
		return listAwake[i];
	}

	public byte GetDC()
	{
		if (Type != AwakeType.物理攻击)
		{
			return 0;
		}
		return GetAwakeValue();
	}

	public byte GetMC()
	{
		if (Type != AwakeType.魔法攻击)
		{
			return 0;
		}
		return GetAwakeValue();
	}

	public byte GetSC()
	{
		if (Type != AwakeType.道术攻击)
		{
			return 0;
		}
		return GetAwakeValue();
	}

	public byte GetAC()
	{
		if (Type != AwakeType.物理防御)
		{
			return 0;
		}
		return GetAwakeValue();
	}

	public byte GetMAC()
	{
		if (Type != AwakeType.魔法防御)
		{
			return 0;
		}
		return GetAwakeValue();
	}

	public byte GetHPMP()
	{
		if (Type != AwakeType.生命法力值)
		{
			return 0;
		}
		return GetAwakeValue();
	}

	private bool[] MakeHit(int maxValue, out int makeValue)
	{
		float num = (float)maxValue / 5f;
		float num2 = 0f;
		bool[] array = new bool[5];
		Random random = new Random(DateTime.Now.Millisecond);
		for (int i = 0; i < 5; i++)
		{
			if (random.Next(0, 100) < AwakeHitRate)
			{
				num2 += num;
				array[i] = true;
			}
			else
			{
				array[i] = false;
			}
		}
		makeValue = ((num2 <= 1f) ? 1 : ((int)num2));
		return array;
	}

	private bool[] Awakening(UserItem item)
	{
		int awakeChanceMin = AwakeChanceMin;
		int maxValue = ((AwakeChanceMax[(uint)(item.Info.Grade - 1)] < awakeChanceMin) ? awakeChanceMin : AwakeChanceMax[(uint)(item.Info.Grade - 1)]);
		int makeValue;
		bool[] result = MakeHit(maxValue, out makeValue);
		makeValue = item.Info.Type switch
		{
			ItemType.武器 => makeValue * Awake_WeaponRate, 
			ItemType.盔甲 => makeValue * Awake_ArmorRate, 
			ItemType.头盔 => makeValue * Awake_HelmetRate, 
			_ => 0, 
		};
		listAwake.Add((byte)makeValue);
		return result;
	}
}
