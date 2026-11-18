using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

public class ItemInfo
{
	public int Index;

	public string Name = string.Empty;

	public ItemType Type;

	public ItemGrade Grade;

	public RequiredType RequiredType;

	public RequiredClass RequiredClass = RequiredClass.全职业;

	public RequiredGender RequiredGender = RequiredGender.性别不限;

	public ItemSet Set;

	public short Shape;

	public byte Weight;

	public byte Light;

	public byte RequiredAmount;

	public ushort Image;

	public ushort Durability;

	public uint Price;

	public ushort StackSize = 1;

	public bool StartItem;

	public byte Effect;

	public bool NeedIdentify;

	public bool ShowGroupPickup;

	public bool GlobalDropNotify;

	public bool ClassBased;

	public bool LevelBased;

	public bool CanMine;

	public bool CanFastRun;

	public bool CanAwakening;

	public BindMode Bind;

	public SpecialItemMode Unique;

	public byte RandomStatsId;

	public RandomItemStat RandomStats;

	public string ToolTip = string.Empty;

	public byte Slots;

	public Stats Stats;

	public bool IsConsumable
	{
		get
		{
			if (Type != ItemType.药水 && Type != ItemType.卷轴 && Type != ItemType.坐骑食物 && Type != ItemType.外形物品)
			{
				return Type == ItemType.特殊消耗品;
			}
			return true;
		}
	}

	public bool IsFishingRod => Globals.FishingRodShapes.Contains(Shape);

	public string FriendlyName => Regex.Replace(Regex.Replace(Name, "\\d+$", string.Empty), "\\[[^]]*\\]", string.Empty);

	public ItemInfo()
	{
		Stats = new Stats();
	}

	public ItemInfo(BinaryReader reader, int version = int.MaxValue, int customVersion = int.MaxValue)
	{
		Index = reader.ReadInt32();
		Name = reader.ReadString();
		Type = (ItemType)reader.ReadByte();
		Grade = (ItemGrade)reader.ReadByte();
		RequiredType = (RequiredType)reader.ReadByte();
		RequiredClass = (RequiredClass)reader.ReadByte();
		RequiredGender = (RequiredGender)reader.ReadByte();
		Set = (ItemSet)reader.ReadByte();
		Shape = reader.ReadInt16();
		Weight = reader.ReadByte();
		Light = reader.ReadByte();
		RequiredAmount = reader.ReadByte();
		Image = reader.ReadUInt16();
		Durability = reader.ReadUInt16();
		if (version <= 84)
		{
			StackSize = (ushort)reader.ReadUInt32();
		}
		else
		{
			StackSize = reader.ReadUInt16();
		}
		Price = reader.ReadUInt32();
		if (version <= 84)
		{
			Stats = new Stats();
			Stats[Stat.MinAC] = reader.ReadByte();
			Stats[Stat.MaxAC] = reader.ReadByte();
			Stats[Stat.MinMAC] = reader.ReadByte();
			Stats[Stat.MaxMAC] = reader.ReadByte();
			Stats[Stat.MinDC] = reader.ReadByte();
			Stats[Stat.MaxDC] = reader.ReadByte();
			Stats[Stat.MinMC] = reader.ReadByte();
			Stats[Stat.MaxMC] = reader.ReadByte();
			Stats[Stat.MinSC] = reader.ReadByte();
			Stats[Stat.MaxSC] = reader.ReadByte();
			Stats[Stat.HP] = reader.ReadUInt16();
			Stats[Stat.MP] = reader.ReadUInt16();
			Stats[Stat.准确] = reader.ReadByte();
			Stats[Stat.敏捷] = reader.ReadByte();
			Stats[Stat.幸运] = reader.ReadSByte();
			Stats[Stat.攻击速度] = reader.ReadSByte();
		}
		StartItem = reader.ReadBoolean();
		if (version <= 84)
		{
			Stats[Stat.背包负重] = reader.ReadByte();
			Stats[Stat.腕力负重] = reader.ReadByte();
			Stats[Stat.装备负重] = reader.ReadByte();
		}
		Effect = reader.ReadByte();
		if (version <= 84)
		{
			Stats[Stat.强度] = reader.ReadByte();
			Stats[Stat.魔法躲避] = reader.ReadByte();
			Stats[Stat.毒物躲避] = reader.ReadByte();
			Stats[Stat.生命恢复] = reader.ReadByte();
			Stats[Stat.法力恢复] = reader.ReadByte();
			Stats[Stat.中毒恢复] = reader.ReadByte();
			Stats[Stat.生命值数率] = reader.ReadByte();
			Stats[Stat.法力值数率] = reader.ReadByte();
			Stats[Stat.暴击倍率] = reader.ReadByte();
			Stats[Stat.暴击伤害] = reader.ReadByte();
		}
		byte b = reader.ReadByte();
		NeedIdentify = (b & 1) == 1;
		ShowGroupPickup = (b & 2) == 2;
		ClassBased = (b & 4) == 4;
		LevelBased = (b & 8) == 8;
		CanMine = (b & 0x10) == 16;
		if (version >= 77)
		{
			GlobalDropNotify = (b & 0x20) == 32;
		}
		if (version <= 84)
		{
			Stats[Stat.最大防御数率] = reader.ReadByte();
			Stats[Stat.最大魔御数率] = reader.ReadByte();
			Stats[Stat.神圣] = reader.ReadByte();
			Stats[Stat.冰冻伤害] = reader.ReadByte();
			Stats[Stat.毒素伤害] = reader.ReadByte();
		}
		Bind = (BindMode)reader.ReadInt16();
		if (version <= 84)
		{
			Stats[Stat.反弹伤害] = reader.ReadByte();
			Stats[Stat.吸血数率] = reader.ReadByte();
		}
		Unique = (SpecialItemMode)reader.ReadInt16();
		RandomStatsId = reader.ReadByte();
		CanFastRun = reader.ReadBoolean();
		CanAwakening = reader.ReadBoolean();
		if (version > 83)
		{
			Slots = reader.ReadByte();
		}
		if (version > 84)
		{
			Stats = new Stats(reader, version, customVersion);
		}
		if (reader.ReadBoolean())
		{
			ToolTip = reader.ReadString();
		}
		if (version < 70 && Type == ItemType.戒指 && Unique != 0)
		{
			Bind |= BindMode.NoWeddingRing;
		}
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write(Index);
		writer.Write(Name);
		writer.Write((byte)Type);
		writer.Write((byte)Grade);
		writer.Write((byte)RequiredType);
		writer.Write((byte)RequiredClass);
		writer.Write((byte)RequiredGender);
		writer.Write((byte)Set);
		writer.Write(Shape);
		writer.Write(Weight);
		writer.Write(Light);
		writer.Write(RequiredAmount);
		writer.Write(Image);
		writer.Write(Durability);
		writer.Write(StackSize);
		writer.Write(Price);
		writer.Write(StartItem);
		writer.Write(Effect);
		byte b = 0;
		if (NeedIdentify)
		{
			b = (byte)(b | 1u);
		}
		if (ShowGroupPickup)
		{
			b = (byte)(b | 2u);
		}
		if (ClassBased)
		{
			b = (byte)(b | 4u);
		}
		if (LevelBased)
		{
			b = (byte)(b | 8u);
		}
		if (CanMine)
		{
			b = (byte)(b | 0x10u);
		}
		if (GlobalDropNotify)
		{
			b = (byte)(b | 0x20u);
		}
		writer.Write(b);
		writer.Write((short)Bind);
		writer.Write((short)Unique);
		writer.Write(RandomStatsId);
		writer.Write(CanFastRun);
		writer.Write(CanAwakening);
		writer.Write(Slots);
		Stats.Save(writer);
		writer.Write(ToolTip != null);
		if (ToolTip != null)
		{
			writer.Write(ToolTip);
		}
	}

	public static ItemInfo FromText(string text)
	{
		return null;
	}

	public string ToText()
	{
		return null;
	}

	public override string ToString()
	{
		return $"{Index}: {Name}";
	}
}
