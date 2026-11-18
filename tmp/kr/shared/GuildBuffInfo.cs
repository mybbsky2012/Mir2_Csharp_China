using System.Collections.Generic;
using System.IO;

public class GuildBuffInfo
{
	public int Id;

	public int Icon;

	public string Name = "";

	public byte LevelRequirement;

	public byte PointsRequirement = 1;

	public int TimeLimit;

	public int ActivationCost;

	public Stats Stats;

	public GuildBuffInfo()
	{
		Stats = new Stats();
	}

	public GuildBuffInfo(BinaryReader reader)
	{
		Id = reader.ReadInt32();
		Icon = reader.ReadInt32();
		Name = reader.ReadString();
		LevelRequirement = reader.ReadByte();
		PointsRequirement = reader.ReadByte();
		TimeLimit = reader.ReadInt32();
		ActivationCost = reader.ReadInt32();
		Stats = new Stats(reader);
	}

	public GuildBuffInfo(InIReader reader, int i)
	{
		Id = reader.ReadInt32("Buff-" + i, "Id", 0);
		Icon = reader.ReadInt32("Buff-" + i, "Icon", 0);
		Name = reader.ReadString("Buff-" + i, "Name", "");
		LevelRequirement = reader.ReadByte("Buff-" + i, "LevelReq", 0);
		PointsRequirement = reader.ReadByte("Buff-" + i, "PointsReq", 1);
		TimeLimit = reader.ReadInt32("Buff-" + i, "TimeLimit", 0);
		ActivationCost = reader.ReadInt32("Buff-" + i, "ActivationCost", 0);
		Stats = new Stats();
		Stats[Stat.MaxAC] = reader.ReadByte("Buff-" + i, "BuffAc", 0);
		Stats[Stat.MaxMAC] = reader.ReadByte("Buff-" + i, "BuffMAC", 0);
		Stats[Stat.MaxDC] = reader.ReadByte("Buff-" + i, "BuffDc", 0);
		Stats[Stat.MaxMC] = reader.ReadByte("Buff-" + i, "BuffMc", 0);
		Stats[Stat.MaxSC] = reader.ReadByte("Buff-" + i, "BuffSc", 0);
		Stats[Stat.HP] = reader.ReadInt32("Buff-" + i, "BuffMaxHp", 0);
		Stats[Stat.MP] = reader.ReadInt32("Buff-" + i, "BuffMaxMp", 0);
		Stats[Stat.采矿出矿数率] = reader.ReadByte("Buff-" + i, "BuffMineRate", 0);
		Stats[Stat.宝石成功数率] = reader.ReadByte("Buff-" + i, "BuffGemRate", 0);
		Stats[Stat.钓鱼成功数率] = reader.ReadByte("Buff-" + i, "BuffFishRate", 0);
		Stats[Stat.经验增长数率] = reader.ReadByte("Buff-" + i, "BuffExpRate", 0);
		Stats[Stat.大师概率数率] = reader.ReadByte("Buff-" + i, "BuffCraftRate", 0);
		Stats[Stat.技能熟练度倍率] = reader.ReadByte("Buff-" + i, "BuffSkillRate", 0);
		Stats[Stat.生命恢复] = reader.ReadByte("Buff-" + i, "BuffHpRegen", 0);
		Stats[Stat.法力恢复] = reader.ReadByte("Buff-" + i, "BuffMpRegen", 0);
		Stats[Stat.攻击增伤] = reader.ReadByte("Buff-" + i, "BuffAttack", 0);
		Stats[Stat.物品掉落数率] = reader.ReadByte("Buff-" + i, "BuffDropRate", 0);
		Stats[Stat.金币收益数率] = reader.ReadByte("Buff-" + i, "BuffGoldRate", 0);
	}

	public void Save(InIReader reader, int i)
	{
		reader.Write("Buff-" + i, "Id", Id);
		reader.Write("Buff-" + i, "Icon", Icon);
		reader.Write("Buff-" + i, "Name", Name);
		reader.Write("Buff-" + i, "LevelReq", LevelRequirement);
		reader.Write("Buff-" + i, "PointsReq", PointsRequirement);
		reader.Write("Buff-" + i, "TimeLimit", TimeLimit);
		reader.Write("Buff-" + i, "ActivationCost", ActivationCost);
		reader.Write("Buff-" + i, "BuffAc", Stats[Stat.MaxAC]);
		reader.Write("Buff-" + i, "BuffMAC", Stats[Stat.MaxMAC]);
		reader.Write("Buff-" + i, "BuffDc", Stats[Stat.MaxDC]);
		reader.Write("Buff-" + i, "BuffMc", Stats[Stat.MaxMC]);
		reader.Write("Buff-" + i, "BuffSc", Stats[Stat.MaxSC]);
		reader.Write("Buff-" + i, "BuffMaxHp", Stats[Stat.HP]);
		reader.Write("Buff-" + i, "BuffMaxMp", Stats[Stat.MP]);
		reader.Write("Buff-" + i, "BuffMineRate", Stats[Stat.采矿出矿数率]);
		reader.Write("Buff-" + i, "BuffGemRate", Stats[Stat.宝石成功数率]);
		reader.Write("Buff-" + i, "BuffFishRate", Stats[Stat.钓鱼成功数率]);
		reader.Write("Buff-" + i, "BuffExpRate", Stats[Stat.经验增长数率]);
		reader.Write("Buff-" + i, "BuffCraftRate", Stats[Stat.大师概率数率]);
		reader.Write("Buff-" + i, "BuffSkillRate", Stats[Stat.技能熟练度倍率]);
		reader.Write("Buff-" + i, "BuffHpRegen", Stats[Stat.生命恢复]);
		reader.Write("Buff-" + i, "BuffMpRegen", Stats[Stat.法力恢复]);
		reader.Write("Buff-" + i, "BuffAttack", Stats[Stat.攻击增伤]);
		reader.Write("Buff-" + i, "BuffDropRate", Stats[Stat.物品掉落数率]);
		reader.Write("Buff-" + i, "BuffGoldRate", Stats[Stat.金币收益数率]);
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write(Id);
		writer.Write(Icon);
		writer.Write(Name);
		writer.Write(LevelRequirement);
		writer.Write(PointsRequirement);
		writer.Write(TimeLimit);
		writer.Write(ActivationCost);
		Stats.Save(writer);
	}

	public override string ToString()
	{
		return $"{Id}: {Name}";
	}

	public string ShowStats()
	{
		string text = string.Empty;
		foreach (KeyValuePair<Stat, int> value2 in Stats.Values)
		{
			string value = ((value2.Value < 0) ? "降低" : "提高");
			string text2 = $"{value} {value2.Key} : {value2.Value}{(value2.Key.ToString().Contains("数率") ? "%" : "")}\n";
			text += text2;
		}
		return text;
	}
}
