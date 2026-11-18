using System.Collections.Generic;
using System.IO;

public class BaseStats
{
	public MirClass Job;

	public List<BaseStat> Stats = new List<BaseStat>();

	public Stats Caps = new Stats();

	public BaseStats(MirClass job)
	{
		Job = job;
		switch (job)
		{
		case MirClass.战士:
			Stats.Add(new BaseStat(Stat.HP)
			{
				FormulaType = StatFormula.Health,
				Base = 14,
				Gain = 4f,
				GainRate = 4.5f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MP)
			{
				FormulaType = StatFormula.Mana,
				Base = 11,
				Gain = 3.5f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.背包负重)
			{
				FormulaType = StatFormula.Weight,
				Base = 50,
				Gain = 3f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.装备负重)
			{
				FormulaType = StatFormula.Weight,
				Base = 15,
				Gain = 20f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.腕力负重)
			{
				FormulaType = StatFormula.Weight,
				Base = 12,
				Gain = 13f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MinAC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 0f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MaxAC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 7f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MinDC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 5f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MaxDC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 5f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.敏捷)
			{
				FormulaType = StatFormula.Stat,
				Base = 15,
				Gain = 0f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.准确)
			{
				FormulaType = StatFormula.Stat,
				Base = 5,
				Gain = 0f,
				GainRate = 0f,
				Max = 0
			});
			break;
		case MirClass.法师:
			Stats.Add(new BaseStat(Stat.HP)
			{
				FormulaType = StatFormula.Health,
				Base = 14,
				Gain = 15f,
				GainRate = 1.8f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MP)
			{
				FormulaType = StatFormula.Mana,
				Base = 13,
				Gain = 5f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.背包负重)
			{
				FormulaType = StatFormula.Weight,
				Base = 50,
				Gain = 5f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.装备负重)
			{
				FormulaType = StatFormula.Weight,
				Base = 15,
				Gain = 100f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.腕力负重)
			{
				FormulaType = StatFormula.Weight,
				Base = 12,
				Gain = 90f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MinDC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 7f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MaxDC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 7f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MinMC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 7f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MaxMC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 7f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.敏捷)
			{
				FormulaType = StatFormula.Stat,
				Base = 15,
				Gain = 0f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.准确)
			{
				FormulaType = StatFormula.Stat,
				Base = 5,
				Gain = 0f,
				GainRate = 0f,
				Max = 0
			});
			break;
		case MirClass.道士:
			Stats.Add(new BaseStat(Stat.HP)
			{
				FormulaType = StatFormula.Health,
				Base = 14,
				Gain = 6f,
				GainRate = 2.5f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MP)
			{
				FormulaType = StatFormula.Mana,
				Base = 13,
				Gain = 8f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.背包负重)
			{
				FormulaType = StatFormula.Weight,
				Base = 50,
				Gain = 4f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.装备负重)
			{
				FormulaType = StatFormula.Weight,
				Base = 15,
				Gain = 50f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.腕力负重)
			{
				FormulaType = StatFormula.Weight,
				Base = 12,
				Gain = 42f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MinMAC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 12f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MaxMAC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 6f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MinDC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 7f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MaxDC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 7f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MinSC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 7f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MaxSC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 7f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.敏捷)
			{
				FormulaType = StatFormula.Stat,
				Base = 18,
				Gain = 0f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.准确)
			{
				FormulaType = StatFormula.Stat,
				Base = 5,
				Gain = 0f,
				GainRate = 0f,
				Max = 0
			});
			break;
		case MirClass.刺客:
			Stats.Add(new BaseStat(Stat.HP)
			{
				FormulaType = StatFormula.Health,
				Base = 14,
				Gain = 4f,
				GainRate = 3.25f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MP)
			{
				FormulaType = StatFormula.Mana,
				Base = 11,
				Gain = 5f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.背包负重)
			{
				FormulaType = StatFormula.Weight,
				Base = 50,
				Gain = 3.5f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.装备负重)
			{
				FormulaType = StatFormula.Weight,
				Base = 15,
				Gain = 33f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.腕力负重)
			{
				FormulaType = StatFormula.Weight,
				Base = 12,
				Gain = 30f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MinDC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 8f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MaxDC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 8f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.敏捷)
			{
				FormulaType = StatFormula.Stat,
				Base = 20,
				Gain = 0f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.准确)
			{
				FormulaType = StatFormula.Stat,
				Base = 5,
				Gain = 0f,
				GainRate = 0f,
				Max = 0
			});
			break;
		case MirClass.弓箭:
			Stats.Add(new BaseStat(Stat.HP)
			{
				FormulaType = StatFormula.Health,
				Base = 14,
				Gain = 4f,
				GainRate = 3.25f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MP)
			{
				FormulaType = StatFormula.Mana,
				Base = 11,
				Gain = 4f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.背包负重)
			{
				FormulaType = StatFormula.Weight,
				Base = 50,
				Gain = 4f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.装备负重)
			{
				FormulaType = StatFormula.Weight,
				Base = 15,
				Gain = 33f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.腕力负重)
			{
				FormulaType = StatFormula.Weight,
				Base = 12,
				Gain = 30f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MinDC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 8f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MaxDC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 8f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MinMC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 8f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.MaxMC)
			{
				FormulaType = StatFormula.Stat,
				Base = 0,
				Gain = 8f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.敏捷)
			{
				FormulaType = StatFormula.Stat,
				Base = 15,
				Gain = 0f,
				GainRate = 0f,
				Max = 0
			});
			Stats.Add(new BaseStat(Stat.准确)
			{
				FormulaType = StatFormula.Stat,
				Base = 8,
				Gain = 0f,
				GainRate = 0f,
				Max = 0
			});
			break;
		}
		Caps[Stat.魔法躲避] = 2;
		Caps[Stat.毒物躲避] = 6;
		Caps[Stat.暴击倍率] = 18;
		Caps[Stat.暴击伤害] = 10;
		Caps[Stat.冰冻伤害] = 6;
		Caps[Stat.毒素伤害] = 6;
		Caps[Stat.生命恢复] = 8;
		Caps[Stat.法力恢复] = 8;
		Caps[Stat.中毒恢复] = 6;
	}

	public BaseStats(BinaryReader reader)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			Stats.Add(new BaseStat((Stat)reader.ReadByte())
			{
				FormulaType = (StatFormula)reader.ReadByte(),
				Base = reader.ReadInt32(),
				Gain = reader.ReadSingle(),
				GainRate = reader.ReadSingle(),
				Max = reader.ReadInt32()
			});
		}
		Caps = new Stats(reader);
	}

	public void Save(BinaryWriter writer)
	{
		writer.Write(Stats.Count);
		foreach (BaseStat stat in Stats)
		{
			writer.Write((byte)stat.Type);
			writer.Write((byte)stat.FormulaType);
			writer.Write(stat.Base);
			writer.Write(stat.Gain);
			writer.Write(stat.GainRate);
			writer.Write(stat.Max);
		}
		Caps.Save(writer);
	}
}
