using System;

public class BaseStat
{
	public StatFormula FormulaType;

	public Stat Type;

	public int Base;

	public float Gain;

	public float GainRate;

	public int Max;

	public BaseStat(Stat type)
	{
		Type = type;
	}

	public int Calculate(MirClass job, int level)
	{
		if (Gain == 0f)
		{
			return Base;
		}
		if (FormulaType == StatFormula.Health)
		{
			if (job == MirClass.战士)
			{
				return (int)Math.Min((Max > 0) ? Max : int.MaxValue, (float)Base + ((float)level / Gain + GainRate + (float)level / 20f) * (float)level);
			}
			return (int)Math.Min((Max > 0) ? Max : int.MaxValue, (float)Base + ((float)level / Gain + GainRate) * (float)level);
		}
		if (FormulaType == StatFormula.Mana)
		{
			return job switch
			{
				MirClass.法师 => (int)Math.Min((Max > 0) ? Max : int.MaxValue, (float)Base + ((float)level / Gain + 2f) * 2.2f * (float)level + (float)level * GainRate), 
				MirClass.道士 => (int)Math.Min((Max > 0) ? Max : int.MaxValue, (float)Base + (float)level / Gain * 2.2f * (float)level + (float)level * GainRate), 
				_ => (int)Math.Min((Max > 0) ? Max : int.MaxValue, (float)Base + (float)level * Gain + (float)level * GainRate), 
			};
		}
		if (FormulaType == StatFormula.Weight)
		{
			return (int)Math.Min((Max > 0) ? Max : int.MaxValue, (float)Base + (float)level / Gain * (float)level);
		}
		return (int)Math.Min((Max > 0) ? Max : int.MaxValue, (float)Base + (float)level / Gain);
	}
}
