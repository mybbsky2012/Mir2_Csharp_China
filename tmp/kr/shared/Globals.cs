public static class Globals
{
	public const string ProductCodename = "Crystal";

	public const int MinAccountIDLength = 3;

	public const int MaxAccountIDLength = 15;

	public const int MinPasswordLength = 5;

	public const int MaxPasswordLength = 15;

	public const int MinCharacterNameLength = 3;

	public const int MaxCharacterNameLength = 15;

	public const int MaxCharacterCount = 4;

	public const int MaxChatLength = 80;

	public const int MaxGroup = 15;

	public const int MaxPets = 5;

	public const int MaxAttackRange = 9;

	public const int MaxDragonLevel = 13;

	public const int ClassWeaponCount = 100;

	public const int FlagIndexCount = 1999;

	public const int MaxConcurrentQuests = 20;

	public const int LogDelay = 10000;

	public const int DataRange = 16;

	public static float Commission = 0.05f;

	public const uint SearchDelay = 500u;

	public const uint ConsignmentLength = 7u;

	public const uint ConsignmentCost = 5000u;

	public const uint MinConsignment = 5000u;

	public const uint MaxConsignment = 50000000u;

	public const uint AuctionCost = 5000u;

	public const uint MinStartingBid = 0u;

	public const uint MaxStartingBid = 50000u;

	public static int[] FishingRodShapes = new int[2] { 49, 50 };

	public static Spell[] RangedSpells = new Spell[14]
	{
		Spell.FireBall,
		Spell.ThunderBolt,
		Spell.FireBang,
		Spell.FireWall,
		Spell.FrostCrunch,
		Spell.Vampirism,
		Spell.FlameDisruptor,
		Spell.IceStorm,
		Spell.MeteorStrike,
		Spell.Blizzard,
		Spell.SoulFireBall,
		Spell.StraightShot,
		Spell.ElementalShot,
		Spell.PoisonShot
	};
}
