public class RandomItemStat
{
	public byte MaxDuraChance;

	public byte MaxDuraStatChance;

	public byte MaxDuraMaxStat;

	public byte MaxAcChance;

	public byte MaxAcStatChance;

	public byte MaxAcMaxStat;

	public byte MaxMacChance;

	public byte MaxMacStatChance;

	public byte MaxMacMaxStat;

	public byte MaxDcChance;

	public byte MaxDcStatChance;

	public byte MaxDcMaxStat;

	public byte MaxMcChance;

	public byte MaxMcStatChance;

	public byte MaxMcMaxStat;

	public byte MaxScChance;

	public byte MaxScStatChance;

	public byte MaxScMaxStat;

	public byte AccuracyChance;

	public byte AccuracyStatChance;

	public byte AccuracyMaxStat;

	public byte AgilityChance;

	public byte AgilityStatChance;

	public byte AgilityMaxStat;

	public byte HpChance;

	public byte HpStatChance;

	public byte HpMaxStat;

	public byte MpChance;

	public byte MpStatChance;

	public byte MpMaxStat;

	public byte StrongChance;

	public byte StrongStatChance;

	public byte StrongMaxStat;

	public byte MagicResistChance;

	public byte MagicResistStatChance;

	public byte MagicResistMaxStat;

	public byte PoisonResistChance;

	public byte PoisonResistStatChance;

	public byte PoisonResistMaxStat;

	public byte HpRecovChance;

	public byte HpRecovStatChance;

	public byte HpRecovMaxStat;

	public byte MpRecovChance;

	public byte MpRecovStatChance;

	public byte MpRecovMaxStat;

	public byte PoisonRecovChance;

	public byte PoisonRecovStatChance;

	public byte PoisonRecovMaxStat;

	public byte CriticalRateChance;

	public byte CriticalRateStatChance;

	public byte CriticalRateMaxStat;

	public byte CriticalDamageChance;

	public byte CriticalDamageStatChance;

	public byte CriticalDamageMaxStat;

	public byte FreezeChance;

	public byte FreezeStatChance;

	public byte FreezeMaxStat;

	public byte PoisonAttackChance;

	public byte PoisonAttackStatChance;

	public byte PoisonAttackMaxStat;

	public byte AttackSpeedChance;

	public byte AttackSpeedStatChance;

	public byte AttackSpeedMaxStat;

	public byte LuckChance;

	public byte LuckStatChance;

	public byte LuckMaxStat;

	public byte CurseChance;

	public byte SlotChance;

	public byte SlotStatChance;

	public byte SlotMaxStat;

	public RandomItemStat(ItemType Type = ItemType.技能书)
	{
		switch (Type)
		{
		case ItemType.武器:
			SetWeapon();
			break;
		case ItemType.盔甲:
			SetArmour();
			break;
		case ItemType.头盔:
			SetHelmet();
			break;
		case ItemType.腰带:
		case ItemType.靴子:
			SetBeltBoots();
			break;
		case ItemType.项链:
			SetNecklace();
			break;
		case ItemType.手镯:
			SetBracelet();
			break;
		case ItemType.戒指:
			SetRing();
			break;
		case ItemType.坐骑:
			SetMount();
			break;
		}
	}

	public void SetWeapon()
	{
		MaxDuraChance = 2;
		MaxDuraStatChance = 13;
		MaxDuraMaxStat = 13;
		MaxDcChance = 15;
		MaxDcStatChance = 15;
		MaxDcMaxStat = 13;
		MaxMcChance = 20;
		MaxMcStatChance = 15;
		MaxMcMaxStat = 13;
		MaxScChance = 20;
		MaxScStatChance = 15;
		MaxScMaxStat = 13;
		AttackSpeedChance = 60;
		AttackSpeedStatChance = 30;
		AttackSpeedMaxStat = 3;
		StrongChance = 24;
		StrongStatChance = 20;
		StrongMaxStat = 2;
		AccuracyChance = 30;
		AccuracyStatChance = 20;
		AccuracyMaxStat = 2;
		SlotChance = 0;
		SlotStatChance = 0;
		SlotMaxStat = 4;
	}

	public void SetArmour()
	{
		MaxDuraChance = 2;
		MaxDuraStatChance = 10;
		MaxDuraMaxStat = 3;
		MaxAcChance = 30;
		MaxAcStatChance = 15;
		MaxAcMaxStat = 7;
		MaxMacChance = 30;
		MaxMacStatChance = 15;
		MaxMacMaxStat = 7;
		MaxDcChance = 40;
		MaxDcStatChance = 20;
		MaxDcMaxStat = 7;
		MaxMcChance = 40;
		MaxMcStatChance = 20;
		MaxMcMaxStat = 7;
		MaxScChance = 40;
		MaxScStatChance = 20;
		MaxScMaxStat = 7;
	}

	public void SetHelmet()
	{
		MaxDuraChance = 2;
		MaxDuraStatChance = 10;
		MaxDuraMaxStat = 3;
		MaxAcChance = 30;
		MaxAcStatChance = 15;
		MaxAcMaxStat = 7;
		MaxMacChance = 30;
		MaxMacStatChance = 15;
		MaxMacMaxStat = 7;
		MaxDcChance = 40;
		MaxDcStatChance = 20;
		MaxDcMaxStat = 7;
		MaxMcChance = 40;
		MaxMcStatChance = 20;
		MaxMcMaxStat = 7;
		MaxScChance = 40;
		MaxScStatChance = 20;
		MaxScMaxStat = 7;
	}

	public void SetBeltBoots()
	{
		MaxDuraChance = 2;
		MaxDuraStatChance = 10;
		MaxDuraMaxStat = 3;
		MaxAcChance = 30;
		MaxAcStatChance = 30;
		MaxAcMaxStat = 3;
		MaxMacChance = 30;
		MaxMacStatChance = 30;
		MaxMacMaxStat = 3;
		MaxDcChance = 30;
		MaxDcStatChance = 30;
		MaxDcMaxStat = 3;
		MaxMcChance = 30;
		MaxMcStatChance = 30;
		MaxMcMaxStat = 3;
		MaxScChance = 30;
		MaxScStatChance = 30;
		MaxScMaxStat = 3;
		AgilityChance = 60;
		AgilityStatChance = 30;
		AgilityMaxStat = 3;
	}

	public void SetNecklace()
	{
		MaxDuraChance = 2;
		MaxDuraStatChance = 10;
		MaxDuraMaxStat = 3;
		MaxDcChance = 15;
		MaxDcStatChance = 30;
		MaxDcMaxStat = 7;
		MaxMcChance = 15;
		MaxMcStatChance = 30;
		MaxMcMaxStat = 7;
		MaxScChance = 15;
		MaxScStatChance = 30;
		MaxScMaxStat = 7;
		AccuracyChance = 60;
		AccuracyStatChance = 30;
		AccuracyMaxStat = 7;
		AgilityChance = 60;
		AgilityStatChance = 30;
		AgilityMaxStat = 7;
	}

	public void SetBracelet()
	{
		MaxDuraChance = 2;
		MaxDuraStatChance = 10;
		MaxDuraMaxStat = 3;
		MaxAcChance = 20;
		MaxAcStatChance = 30;
		MaxAcMaxStat = 6;
		MaxMacChance = 20;
		MaxMacStatChance = 30;
		MaxMacMaxStat = 6;
		MaxDcChance = 30;
		MaxDcStatChance = 30;
		MaxDcMaxStat = 6;
		MaxMcChance = 30;
		MaxMcStatChance = 30;
		MaxMcMaxStat = 6;
		MaxScChance = 30;
		MaxScStatChance = 30;
		MaxScMaxStat = 6;
	}

	public void SetRing()
	{
		MaxDuraChance = 2;
		MaxDuraStatChance = 10;
		MaxDuraMaxStat = 3;
		MaxAcChance = 25;
		MaxAcStatChance = 20;
		MaxAcMaxStat = 6;
		MaxMacChance = 25;
		MaxMacStatChance = 20;
		MaxMacMaxStat = 6;
		MaxDcChance = 15;
		MaxDcStatChance = 30;
		MaxDcMaxStat = 6;
		MaxMcChance = 15;
		MaxMcStatChance = 30;
		MaxMcMaxStat = 6;
		MaxScChance = 15;
		MaxScStatChance = 30;
		MaxScMaxStat = 6;
	}

	public void SetMount()
	{
		SetRing();
	}
}
