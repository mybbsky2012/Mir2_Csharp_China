using System;

[Flags]
public enum BindMode : short
{
	None = 0,
	DontDeathdrop = 1,
	DontDrop = 2,
	DontSell = 4,
	DontStore = 8,
	DontTrade = 0x10,
	DontRepair = 0x20,
	DontUpgrade = 0x40,
	DestroyOnDrop = 0x80,
	BreakOnDeath = 0x100,
	BindOnEquip = 0x200,
	NoSRepair = 0x400,
	NoWeddingRing = 0x800,
	UnableToRent = 0x1000,
	UnableToDisassemble = 0x2000,
	NoMail = 0x4000,
	NoHero = short.MinValue
}
